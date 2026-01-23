// MainForm.cs (UI 수정 + 색상 패치 통합본)
// - 버튼 정렬(취소 분리 / ResetBase 간격)
// - 우측 패널(ADMIN/로그저장/링크) 정렬 개선
// - 요약 카드 값 색상(측정/미측정/절감량) 반영
// - ResetBase 확인 흐름: 1차 경고(MessageBox) + 2차(ResetBaseConfirmForm)
// - 로그 자동 스크롤, 상태 강조, 진행률 Fallback 유지

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinSxSCleanupTool
{
    public sealed class MainForm : Form
    {
        // =========================
        // App / Links
        // =========================
        private const string AppTitle = "WinSxS Cleanup Tool";
        private const string GitHubUrl = "https://github.com/mikuchan1004/winsxs-cleanup-tool";

        // =========================
        // Settings
        // =========================
        private static readonly string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinSxS_Cleanup_Tool.json");

        private sealed class AppSettings
        {
            public bool ReAnalyzeAfterCleanup { get; set; } = true;
            public int? WindowX { get; set; }
            public int? WindowY { get; set; }
            public int? WindowW { get; set; }
            public int? WindowH { get; set; }
        }

        private AppSettings _settings = new();

        // =========================
        // UI Controls
        // =========================
        private Button btnAnalyze = null!;
        private Button btnCleanup = null!;
        private Button btnResetBase = null!;
        private Button btnCancel = null!;
        private Button btnSaveLog = null!;

        private CheckBox chkReAnalyze = null!;
        private LinkLabel linkGitHub = null!;
        private LinkLabel linkAbout = null!;
        private Label lblAdminBadge = null!;

        private GroupBox grpSummary = null!;
        private Label valExpected = null!;
        private Label valSaved = null!;
        private Label valBefore = null!;
        private Label valAfter = null!;

        private Label lblStatus = null!;
        private ProgressBar progress = null!;
        private TextBox txtLog = null!;
        private ToolTip _tt = null!;

        // =========================
        // State
        // =========================
        private bool _isBusy;
        private CancellationTokenSource? _cts;

        private double _lastUpperBoundMB;
        private double _lastActualBeforeMB;
        private double _lastActualAfterMB;

        private DateTime _lastProgressUpdateUtc = DateTime.MinValue;
        private int _lastProgressValue;
        private bool _progressHadRealPercent;

        // Fallback timer
        private System.Windows.Forms.Timer _fallbackTimer = null!;

        private readonly StringBuilder _fullLog = new StringBuilder(256 * 1024);

        // =========================
        // Ctor
        // =========================
        public MainForm()
        {
            LoadSettingsSafe();

            InitializeComponent();
            ApplyDangerButtonStyle();
            ApplySettingsToWindow();

            // 폼 아이콘: EXE에 박힌 아이콘을 그대로 사용 (가장 안정적)
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
            }
            catch
            {
                // ignore
            }

            UpdateAdminUi();
            UpdateSummaryCards();
            SetStatus(UiText.AppReadyStatus);

            FormClosing += (_, __) => SaveSettingsSafe();
        }

        // =========================
        // Initialize UI (Layout-first)
        // =========================
        private void InitializeComponent()
        {
            Text = BuildTitle();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 560);

            Font = new Font("Segoe UI", 9F);
            Padding = new Padding(12);

            _tt = new ToolTip
            {
                AutoPopDelay = 9000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };

            // Root layout
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // toolbar
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // option row
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // summary
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // status+progress
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log

            // Toolbar: left actions + right panel
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Left area: actions row + cancel row
            var leftArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true
            };
            leftArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var cancelRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 6, 0, 0),
                Padding = new Padding(0)
            };

            btnAnalyze = MakeButton("분석", AnalyzeAsync);
            btnCleanup = MakeButton("정리", () => CleanupAsync(resetBase: false));
            btnResetBase = MakeButton("ResetBase", () => CleanupAsync(resetBase: true));
            btnCancel = MakeButton("취소", () => _cts?.Cancel());
            btnCancel.Enabled = false;

            // ✅ ResetBase 버튼은 의도적으로 간격을 둬서(사고 방지 UX)
            btnResetBase.Margin = new Padding(16, 0, 8, 0);

            actionRow.Controls.AddRange(new Control[] { btnAnalyze, btnCleanup, btnResetBase });
            cancelRow.Controls.Add(btnCancel);

            leftArea.Controls.Add(actionRow, 0, 0);
            leftArea.Controls.Add(cancelRow, 0, 1);

            // Right panel: ADMIN + Log + Links (정렬 고정)
            var rightPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            lblAdminBadge = new Label
            {
                Text = "ADMIN",
                AutoSize = false,
                Width = 140,
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 140, 30),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, 8)
            };

            btnSaveLog = new Button
            {
                Text = "로그 저장",
                Width = 140,
                Height = 30,
                Margin = new Padding(0, 0, 0, 8)
            };
            btnSaveLog.Click += (_, __) => SaveLog();

            var linkRow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            linkAbout = new LinkLabel { Text = "About", AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            linkGitHub = new LinkLabel { Text = "GitHub", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };

            linkAbout.LinkClicked += (_, __) => ShowAbout();
            linkGitHub.LinkClicked += (_, __) => OpenUrl(GitHubUrl);

            linkRow.Controls.Add(linkAbout);
            linkRow.Controls.Add(linkGitHub);

            rightPanel.Controls.Add(lblAdminBadge);
            rightPanel.Controls.Add(btnSaveLog);
            rightPanel.Controls.Add(linkRow);

            toolbar.Controls.Add(leftArea, 0, 0);
            toolbar.Controls.Add(rightPanel, 1, 0);

            // Option row
            var optRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 8, 0, 0)
            };

            chkReAnalyze = new CheckBox
            {
                Text = "정리 후 재분석 (실제 절감량 계산)",
                AutoSize = true,
                Checked = _settings.ReAnalyzeAfterCleanup
            };
            chkReAnalyze.CheckedChanged += (_, __) =>
            {
                _settings.ReAnalyzeAfterCleanup = chkReAnalyze.Checked;
                SaveSettingsSafe();
            };

            optRow.Controls.Add(chkReAnalyze);

            // Summary (2x2 cards)
            grpSummary = new GroupBox
            {
                Text = "요약",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 8, 0, 0)
            };

            var summaryGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true
            };
            summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            summaryGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var c00 = MakeSummaryCard("예상 절감량(상한)", out valExpected);
            var c01 = MakeSummaryCard("실제 절감량", out valSaved);
            var c10 = MakeSummaryCard("정리 전 WinSxS 크기", out valBefore);
            var c11 = MakeSummaryCard("정리 후 WinSxS 크기", out valAfter);

            // ✅ 카드 간격: 오른쪽 컬럼은 우측 마진 0
            c00.Margin = new Padding(0, 0, 10, 10);
            c01.Margin = new Padding(0, 0, 0, 10);
            c10.Margin = new Padding(0, 0, 10, 0);
            c11.Margin = new Padding(0, 0, 0, 0);

            summaryGrid.Controls.Add(c00, 0, 0);
            summaryGrid.Controls.Add(c01, 1, 0);
            summaryGrid.Controls.Add(c10, 0, 1);
            summaryGrid.Controls.Add(c11, 1, 1);

            grpSummary.Controls.Add(summaryGrid);

            // Status + progress
            var statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblStatus = new Label
            {
                Text = "상태: -",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            };

            progress = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 18,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            statusPanel.Controls.Add(lblStatus, 0, 0);
            statusPanel.Controls.Add(progress, 0, 1);

            // Log
            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 10),
                WordWrap = false
            };

            // Root add
            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(optRow, 0, 1);
            root.Controls.Add(grpSummary, 0, 2);
            root.Controls.Add(statusPanel, 0, 3);
            root.Controls.Add(txtLog, 0, 4);

            Controls.Add(root);

            // Tooltips
            _tt.SetToolTip(btnAnalyze, "WinSxS 분석(권장): 예상 절감량/정리 전후 비교 정보를 가져옵니다.");
            _tt.SetToolTip(btnCleanup, "Windows 구성 요소 정리(안전): 일반 정리 작업입니다.");
            _tt.SetToolTip(btnResetBase, "ResetBase(위험): 업데이트 제거/롤백이 불가해질 수 있습니다.");
            _tt.SetToolTip(btnCancel, "현재 실행 중인 작업을 취소합니다.");
            _tt.SetToolTip(btnSaveLog, "현재 로그를 텍스트 파일로 저장합니다.");
            _tt.SetToolTip(chkReAnalyze, "정리 후 다시 분석하여 실제 절감량을 계산합니다.");

            // Fallback timer
            _fallbackTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _fallbackTimer.Tick += (_, __) => ProgressFallbackTick();

            UpdateAdminUi();
            UpdateSummaryCards();
            SetStatus(UiText.AppReadyStatus);
        }

        // ✅ 비동기 버튼
        private Button MakeButton(string text, Func<Task> onClickAsync)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 8, 0)
            };

            b.Click += async (_, __) => await onClickAsync();
            return b;
        }

        // ✅ 동기 버튼
        private Button MakeButton(string text, Action onClick)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 8, 0)
            };

            b.Click += (_, __) => onClick();
            return b;
        }

        private Control MakeSummaryCard(string title, out Label valueLabel)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10),
                MinimumSize = new Size(240, 62)
            };

            var t = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Dock = DockStyle.Top
            };

            valueLabel = new Label
            {
                Text = "-",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 6, 0, 0)
            };

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(t);

            return panel;
        }

        private string BuildTitle()
        {
            string ver = GetInformationalVersion();
            return $"{AppTitle}  v{ver}";
        }

        // =========================
        // Admin
        // =========================
        private static bool IsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAdminUi()
        {
            bool isAdmin = IsAdministrator();

            if (isAdmin)
            {
                lblAdminBadge.Text = "ADMIN";
                lblAdminBadge.BackColor = Color.FromArgb(30, 140, 30);
                lblAdminBadge.ForeColor = Color.White;
            }
            else
            {
                lblAdminBadge.Text = "NOT ADMIN";
                lblAdminBadge.BackColor = Color.FromArgb(170, 40, 40);
                lblAdminBadge.ForeColor = Color.White;
            }

            // 안전장치: 비관리자면 정리/ResetBase 비활성
            btnCleanup.Enabled = isAdmin && !_isBusy;
            btnResetBase.Enabled = isAdmin && !_isBusy;
        }

        // =========================
        // Analyze
        // =========================
        private async Task AnalyzeAsync()
        {
            if (_isBusy) return;

            _lastUpperBoundMB = 0;
            SetStatus(UiText.AnalyzeRunningStatus);
            SetBusy(true);

            ResetProgressForRun();

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            var lines = new List<string>();

            try
            {
                Log("▶ WinSxS 구성 요소 저장소 분석을 시작합니다...");
                int exitCode = await RunDismAsync(
                    "/Online /Cleanup-Image /AnalyzeComponentStore",
                    (line, isErr) =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        lines.Add(line);
                        UpdateProgressFromLine(line);
                        AddDismLine(line);
                    },
                    token);

                // Parse expected reclaimable
                string? expectedText = ParseReclaimableFromLines(lines);
                if (!string.IsNullOrWhiteSpace(expectedText))
                {
                    _lastUpperBoundMB = ConvertSizeToMB(expectedText);
                }

                // Parse actual store size
                string? actualText = ParseActualStoreSizeFromLines(lines);
                if (!string.IsNullOrWhiteSpace(actualText))
                {
                    _lastActualBeforeMB = ConvertSizeToMB(actualText);
                }

                UpdateSummaryCards();
                LogSummaryBlock("분석 결과 요약");

                if (_lastUpperBoundMB > 0)
                {
                    Log($"✔ 분석 완료: 정리 가능 상한 {FormatMB(_lastUpperBoundMB)}");
                }
                else
                {
                    Log("✅ 분석 완료: 추가 정리 가능 항목이 없거나, Windows가 정리 가능 정보를 제공하지 않았습니다.");
                }

                if (_lastActualBeforeMB > 0)
                {
                    Log($"ℹ 구성 요소 저장소 실제 크기(정리 전): {FormatMB(_lastActualBeforeMB)}");
                }

                Log($"(ExitCode: {exitCode})");
                SetStatus(UiText.CompletedStatus);
                SetProgressSafe(100);
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업이 취소되었습니다.");
                SetStatus(UiText.CanceledStatus);
                SetProgressSafe(0);
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                SetStatus(UiText.ErrorStatus);
                SetProgressSafe(0);
                MessageBox.Show(ex.ToString(), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        // =========================
        // Cleanup / ResetBase
        // =========================
        private async Task CleanupAsync(bool resetBase)
        {
            if (_isBusy) return;

            UpdateAdminUi();
            if (!IsAdministrator())
            {
                MessageBox.Show(
                    UiText.AdminRequiredMessage,
                    UiText.AdminRequiredTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // UX: 분석 없이 정리 시 안내(ResetBase 제외)
            if (!resetBase && _lastActualBeforeMB <= 0 && _lastUpperBoundMB <= 0)
            {
                var r = MessageBox.Show(
                    UiText.CleanupWithoutAnalyzeMessage,
                    UiText.CleanupWithoutAnalyzeTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (r != DialogResult.Yes) return;
            }

            if (resetBase)
            {
                bool ok = ShowResetBaseConfirmFlow();
                if (!ok) return;
            }

            // 정리 전 값 (분석을 했으면 있음)
            double beforeMB = _lastActualBeforeMB;

            Log(resetBase ? UiText.ResetBaseStartLog : UiText.CleanupStartLog);

            SetStatus(resetBase
                ? UiText.ResetBaseRunningStatus
                : UiText.CleanupRunningStatus);

            SetBusy(true);
            ResetProgressForRun();

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            try
            {
                string args = resetBase
                    ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase"
                    : "/Online /Cleanup-Image /StartComponentCleanup";

                int exitCode = await RunDismAsync(
                    args,
                    (line, isErr) =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        UpdateProgressFromLine(line);
                        AddDismLine(line);
                    },
                    token);

                Log($"✔ {(resetBase ? "ResetBase" : "정리")} 완료 (ExitCode: {exitCode})");

                // 정리 후 재분석(옵션)
                if (chkReAnalyze.Checked)
                {
                    SetStatus("정리 후 재분석 중입니다. 잠시만 기다려 주세요...");
                    Log("▶ 정리 후 재분석 시작... (실제 절감량 계산)");

                    var analyzeLines = new List<string>();
                    ResetProgressForRun();

                    int analyzeExit = await RunDismAsync(
                        "/Online /Cleanup-Image /AnalyzeComponentStore",
                        (line, isErr) =>
                        {
                            if (string.IsNullOrWhiteSpace(line)) return;
                            analyzeLines.Add(line);
                            UpdateProgressFromLine(line);
                            AddDismLine(line);
                        },
                        token);

                    string? afterActualText = ParseActualStoreSizeFromLines(analyzeLines);
                    if (!string.IsNullOrWhiteSpace(afterActualText))
                    {
                        _lastActualAfterMB = ConvertSizeToMB(afterActualText);
                    }

                    // 실제 절감량 계산/표시
                    if (beforeMB > 0 && _lastActualAfterMB > 0)
                    {
                        double savedMB = Math.Max(0, beforeMB - _lastActualAfterMB);
                        Log($"✅ 실제 절감량: {FormatMB(savedMB)} (정리 전 {FormatMB(beforeMB)} → 정리 후 {FormatMB(_lastActualAfterMB)})");
                    }
                    else
                    {
                        Log("ℹ 정리는 정상적으로 완료되었습니다. 다만 비교를 위한 사전 정보가 없어 실제 절감량은 계산되지 않았습니다.");
                    }

                    UpdateSummaryCards();
                    Log($"(Re-Analyze ExitCode: {analyzeExit})");
                    LogSummaryBlock("정리 결과 요약");
                }

                SetStatus(UiText.CompletedStatus);
                SetProgressSafe(100);
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업 취소됨");
                SetStatus(UiText.CanceledStatus);
                SetProgressSafe(0);
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                SetStatus(UiText.ErrorStatus);
                SetProgressSafe(0);
                MessageBox.Show(ex.ToString(), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        // ResetBase UX 흐름 통합:
        // 1) 1차 경고(MessageBox)
        // 2) 최종 확인(ResetBaseConfirmForm: 체크 + 카운트다운)
        private bool ShowResetBaseConfirmFlow()
        {
            var r = MessageBox.Show(
                UiText.ResetBaseWarnMessage,
                UiText.ResetBaseWarnTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (r != DialogResult.Yes)
                return false;

            using var dlg = new ResetBaseConfirmForm(GetInformationalVersion());
            return dlg.ShowDialog(this) == DialogResult.OK;
        }

        // =========================
        // Progress
        // =========================
        private void ResetProgressForRun()
        {
            _lastProgressUpdateUtc = DateTime.UtcNow;
            _lastProgressValue = 0;
            _progressHadRealPercent = false;
            SetProgressSafe(0);
        }

        private void UpdateProgressFromLine(string line)
        {
            // "33.0%" 같은 숫자 퍼센트 파싱
            var m = Regex.Match(line, @"(?<!\d)(\d{1,3}(?:\.\d+)?)\s*%");
            if (m.Success)
            {
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    int p = (int)Math.Round(Math.Clamp(v, 0, 100));
                    _progressHadRealPercent = true;
                    _lastProgressUpdateUtc = DateTime.UtcNow;
                    _lastProgressValue = Math.Max(_lastProgressValue, p);
                    SetProgressSafe(_lastProgressValue);
                }
            }
        }

        private void ProgressFallbackTick()
        {
            if (!_isBusy) return;

            // 실제 % 업데이트가 너무 오래 없으면(2초) 부드럽게 전진
            var since = DateTime.UtcNow - _lastProgressUpdateUtc;
            if (since.TotalSeconds < 2.0) return;

            // 실제 %가 한번이라도 있었으면 95%까지만 천천히
            int cap = _progressHadRealPercent ? 95 : 90;

            int next = _lastProgressValue + 1;
            if (next > cap) next = cap;

            if (next != _lastProgressValue)
            {
                _lastProgressValue = next;
                SetProgressSafe(_lastProgressValue);
            }
        }

        private void SetProgressSafe(int value)
        {
            value = Math.Clamp(value, 0, 100);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => progress.Value = value));
            }
            else
            {
                progress.Value = value;
            }
        }

        // =========================
        // Busy / Status
        // =========================
        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            if (InvokeRequired) BeginInvoke(new Action(ApplyBusyState));
            else ApplyBusyState();
        }

        private void ApplyBusyState()
        {
            btnAnalyze.Enabled = !_isBusy;
            btnCancel.Enabled = _isBusy;

            // admin 여부 반영
            UpdateAdminUi();

            // fallback 타이머 on/off
            if (_isBusy) _fallbackTimer.Start();
            else _fallbackTimer.Stop();
        }

        private void SetStatus(string text)
        {
            void Apply()
            {
                lblStatus.Text = $"상태: {text}";
                lblStatus.ForeColor = _isBusy ? Color.DarkBlue : SystemColors.ControlText;
            }

            if (InvokeRequired) BeginInvoke(new Action(Apply));
            else Apply();
        }

        // =========================
        // Log
        // =========================
        private void Log(string msg)
        {
            _fullLog.AppendLine(msg);

            if (InvokeRequired)
                BeginInvoke(new Action(() => AppendLogLine(msg)));
            else
                AppendLogLine(msg);
        }

        private void AppendLogLine(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            TrimUiLogIfTooLong();

            // ✅ 자동 스크롤
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void LogSummaryBlock(string title)
        {
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("==================================================");
            sb.AppendLine($" {title}");
            sb.AppendLine("--------------------------------------------------");

            sb.AppendLine(" • 정리 전 WinSxS 크기 : " +
                (_lastActualBeforeMB > 0 ? FormatMB(_lastActualBeforeMB) : "분석 필요"));

            sb.AppendLine(" • 정리 후 WinSxS 크기 : " +
                (_lastActualAfterMB > 0 ? FormatMB(_lastActualAfterMB) : "미측정"));

            if (_lastActualBeforeMB > 0 && _lastActualAfterMB > 0)
            {
                var saved = _lastActualBeforeMB - _lastActualAfterMB;
                sb.AppendLine(" • 실제 절감량         : " +
                    (saved > 0 ? FormatMB(saved) : "없음"));
            }
            else
            {
                sb.AppendLine(" • 실제 절감량         : 아직 계산되지 않음");
            }

            sb.AppendLine(" • 작업 결과           : 정상 완료");
            sb.AppendLine("==================================================");
            sb.AppendLine();

            Log(sb.ToString());
        }

        private void SaveLog()
        {
            using var sfd = new SaveFileDialog
            {
                Title = UiText.SaveLogTitle,
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                FileName = $"WinSxS_Cleanup_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.WriteAllText(sfd.FileName, _fullLog.ToString(), new UTF8Encoding(false));
                MessageBox.Show(
                    UiText.SaveLogDoneMessage,
                    UiText.SaveLogDoneTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =========================
        // DISM Runner (Encoding-safe)
        // =========================
        private async Task<int> RunDismAsync(
            string arguments,
            Action<string, bool> onLine,   // bool: isError
            CancellationToken token)
        {
            // DISM 출력 인코딩 자동 판별
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Start();

            async Task PumpStreamAsync(Stream stream, bool isErr)
            {
                const int sniffMax = 8192;
                var initial = new List<byte>(sniffMax);
                var buf = new byte[4096];

                int read;
                while (initial.Count < sniffMax &&
                       (read = await stream.ReadAsync(buf, 0, Math.Min(buf.Length, sniffMax - initial.Count), token)) > 0)
                {
                    initial.AddRange(buf.AsSpan(0, read).ToArray());
                    if (initial.Count >= 512) break;
                }

                Encoding enc = GuessEncoding(initial);
                var decoder = enc.GetDecoder();
                var pending = new StringBuilder();

                void EmitLinesFromPending()
                {
                    int idx;
                    while ((idx = pending.ToString().IndexOf('\n')) >= 0)
                    {
                        string line = pending.ToString(0, idx);
                        if (line.EndsWith("\r", StringComparison.Ordinal)) line = line[..^1];
                        pending.Remove(0, idx + 1);
                        onLine(line, isErr);
                    }
                }

                void FeedBytes(ReadOnlySpan<byte> bytes)
                {
                    if (bytes.Length == 0) return;

                    int charCount = decoder.GetCharCount(bytes, flush: false);
                    if (charCount == 0) return;

                    var chars = new char[charCount];
                    int written = decoder.GetChars(bytes, chars, flush: false);
                    if (written > 0)
                    {
                        pending.Append(chars, 0, written);
                        EmitLinesFromPending();
                    }
                }

                FeedBytes(initial.ToArray());

                while ((read = await stream.ReadAsync(buf, 0, buf.Length, token)) > 0)
                {
                    FeedBytes(buf.AsSpan(0, read));
                }

                int flushCount = decoder.GetCharCount(Array.Empty<byte>(), flush: true);
                if (flushCount > 0)
                {
                    var flushChars = new char[flushCount];
                    int fw = decoder.GetChars(Array.Empty<byte>(), flushChars, flush: true);
                    if (fw > 0)
                    {
                        pending.Append(flushChars, 0, fw);
                    }
                }

                if (pending.Length > 0)
                {
                    string rest = pending.ToString();
                    if (rest.EndsWith("\r", StringComparison.Ordinal)) rest = rest[..^1];
                    onLine(rest, isErr);
                    pending.Clear();
                }

                Encoding GuessEncoding(IReadOnlyList<byte> data)
                {
                    if (data.Count >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Encoding.UTF8;
                    if (data.Count >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode;              // UTF-16LE
                    if (data.Count >= 2 && data[0] == 0xFE && data[1] == 0xFF) return Encoding.BigEndianUnicode;      // UTF-16BE

                    // UTF-16 패턴 감지(널바이트 분포)
                    int n = Math.Min(data.Count, 2048);
                    int zeroEven = 0, zeroOdd = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (data[i] == 0)
                        {
                            if ((i & 1) == 0) zeroEven++;
                            else zeroOdd++;
                        }
                    }
                    if (n >= 64)
                    {
                        double ze = (double)zeroEven / n;
                        double zo = (double)zeroOdd / n;
                        if (ze > 0.12 && zo < 0.02) return Encoding.BigEndianUnicode;
                        if (zo > 0.12 && ze < 0.02) return Encoding.Unicode;
                        if (ze > 0.08 && zo > 0.08) return Encoding.Unicode;
                    }

                    if (LooksLikeUtf8(data)) return Encoding.UTF8;

                    // 기본: 콘솔 OEM(대개 CP949)
                    try { return GetConsoleOemEncoding(); }
                    catch { return Encoding.Default; }
                }

                static bool LooksLikeUtf8(IReadOnlyList<byte> data)
                {
                    int i = 0;
                    int n = data.Count;
                    while (i < n)
                    {
                        byte b = data[i];
                        if (b <= 0x7F) { i++; continue; }

                        int need =
                            (b & 0xE0) == 0xC0 ? 1 :
                            (b & 0xF0) == 0xE0 ? 2 :
                            (b & 0xF8) == 0xF0 ? 3 : -1;

                        if (need < 0) return false;
                        if (i + need >= n) break;

                        for (int k = 1; k <= need; k++)
                        {
                            byte c = data[i + k];
                            if ((c & 0xC0) != 0x80) return false;
                        }
                        i += need + 1;
                    }
                    return true;
                }
            }

            Task tOut = PumpStreamAsync(p.StandardOutput.BaseStream, isErr: false);
            Task tErr = PumpStreamAsync(p.StandardError.BaseStream, isErr: true);

            using (token.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            }))
            {
                await Task.WhenAll(tOut, tErr);
                await p.WaitForExitAsync(token);
                return p.ExitCode;
            }
        }

        private static Encoding GetConsoleOemEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            int oemCp = GetOEMCP();
            return Encoding.GetEncoding(oemCp);
        }

        [DllImport("kernel32.dll")]
        private static extern int GetOEMCP();

        // =========================
        // Parsing
        // =========================
        private static string? ParseReclaimableFromLines(List<string> lines)
        {
            // 한국어/영어 혼합 대응
            foreach (string line in lines)
            {
                string s = line.Trim();

                if (s.Contains("백업 및 사용 안 함", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Backup and Disabled", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Reclaimable", StringComparison.OrdinalIgnoreCase))
                {
                    int idx = s.IndexOf(':');
                    if (idx >= 0 && idx + 1 < s.Length)
                    {
                        string value = s[(idx + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
            return null;
        }

        private static string? ParseActualStoreSizeFromLines(List<string> lines)
        {
            foreach (string line in lines)
            {
                string s = line.Trim();

                if (s.Contains("실제 크기", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Actual size", StringComparison.OrdinalIgnoreCase))
                {
                    int idx = s.IndexOf(':');
                    if (idx >= 0 && idx + 1 < s.Length)
                    {
                        string value = s[(idx + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
            return null;
        }

        private static double ConvertSizeToMB(string text)
        {
            // 예: "12.03 GB", "4.51 GB", "0 bytes"
            string s = text.Trim();

            if (s.Equals("0 bytes", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("0 byte", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var m = Regex.Match(s, @"(?<num>[\d\.,]+)\s*(?<unit>TB|GB|MB|KB|B|bytes?)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;

            string numStr = m.Groups["num"].Value.Replace(",", "");
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return 0;

            string unit = m.Groups["unit"].Value.ToUpperInvariant();

            return unit switch
            {
                "TB" => num * 1024 * 1024,
                "GB" => num * 1024,
                "MB" => num,
                "KB" => num / 1024,
                "B" => num / (1024 * 1024),
                "BYTE" => num / (1024 * 1024),
                "BYTES" => num / (1024 * 1024),
                _ => 0
            };
        }

        private static string FormatMB(double mb)
        {
            if (mb <= 0) return "0.0 MB";

            if (mb >= 1024 * 1024) return $"{mb / (1024 * 1024):0.00} TB";
            if (mb >= 1024) return $"{mb / 1024:0.00} GB";
            return $"{mb:0.0} MB";
        }

        // =========================
        // Summary UI (cards + color patch)
        // =========================
        private enum ValueTone
        {
            Muted,
            Normal,
            Good,
            Warn
        }

        private static void ApplyValueTone(Label label, ValueTone tone)
        {
            // 기본은 과하지 않게
            label.ForeColor = tone switch
            {
                ValueTone.Good => Color.FromArgb(30, 130, 30),
                ValueTone.Warn => Color.FromArgb(180, 120, 0),
                ValueTone.Muted => Color.DimGray,
                _ => SystemColors.ControlText
            };
        }

        private void UpdateSummaryCards()
        {
            // 정리 전
            if (_lastActualBeforeMB > 0)
            {
                valBefore.Text = FormatMB(_lastActualBeforeMB);
                ApplyValueTone(valBefore, ValueTone.Normal);
            }
            else
            {
                valBefore.Text = "분석 필요";
                ApplyValueTone(valBefore, ValueTone.Muted);
            }

            // 정리 후
            if (_lastActualAfterMB > 0)
            {
                valAfter.Text = FormatMB(_lastActualAfterMB);
                ApplyValueTone(valAfter, ValueTone.Normal);
            }
            else
            {
                valAfter.Text = "미측정";
                ApplyValueTone(valAfter, ValueTone.Muted);
            }

            // 예상 절감량(상한)
            if (_lastUpperBoundMB > 0)
            {
                valExpected.Text = FormatMB(_lastUpperBoundMB);
                ApplyValueTone(valExpected, ValueTone.Good);
            }
            else
            {
                valExpected.Text = "미측정";
                ApplyValueTone(valExpected, ValueTone.Muted);
            }

            // 실제 절감량
            if (_lastActualBeforeMB > 0 && _lastActualAfterMB > 0)
            {
                var saved = Math.Max(0, _lastActualBeforeMB - _lastActualAfterMB);
                if (saved > 0.01)
                {
                    valSaved.Text = FormatMB(saved);
                    ApplyValueTone(valSaved, ValueTone.Good);
                }
                else
                {
                    valSaved.Text = "없음";
                    ApplyValueTone(valSaved, ValueTone.Muted);
                }
            }
            else
            {
                valSaved.Text = "아직 계산되지 않음";
                ApplyValueTone(valSaved, ValueTone.Muted);
            }
        }

        // =========================
        // Settings JSON
        // =========================
        private void LoadSettingsSafe()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;

                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) _settings = loaded;
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private void SaveSettingsSafe()
        {
            try
            {
                _settings.ReAnalyzeAfterCleanup = chkReAnalyze?.Checked ?? _settings.ReAnalyzeAfterCleanup;

                // 창 위치/크기 저장(최소화 상태 등은 제외)
                if (WindowState == FormWindowState.Normal)
                {
                    _settings.WindowX = Left;
                    _settings.WindowY = Top;
                    _settings.WindowW = Width;
                    _settings.WindowH = Height;
                }

                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
            }
            catch
            {
                // ignore
            }
        }

        private void ApplySettingsToWindow()
        {
            try
            {
                if (_settings.WindowW is int w && _settings.WindowH is int h && w > 300 && h > 300)
                {
                    Width = w;
                    Height = h;
                }

                if (_settings.WindowX is int x && _settings.WindowY is int y)
                {
                    // 화면 밖 방지(대충)
                    if (x > -2000 && y > -2000)
                    {
                        Left = x;
                        Top = y;
                        StartPosition = FormStartPosition.Manual;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // =========================
        // About
        // =========================
        private void ShowAbout()
        {
            using var dlg = new AboutForm(this);
            dlg.ShowDialog(this);
        }

        // =========================
        // Utils
        // =========================
        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        }

        private static string GetInformationalVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (attr?.InformationalVersion is { Length: > 0 } v)
                {
                    // 1.0.5+abcdef → 1.0.5
                    int plus = v.IndexOf('+');
                    return plus >= 0 ? v[..plus] : v;
                }

                return asm.GetName().Version?.ToString() ?? "?.?.?";
            }
            catch
            {
                return "?.?.?";
            }
        }

        private void ApplyDangerButtonStyle()
        {
            if (btnResetBase == null) return;

            btnResetBase.Text = "ResetBase (되돌릴 수 없음)";
            btnResetBase.UseVisualStyleBackColor = false;

            btnResetBase.BackColor = Color.FromArgb(180, 50, 50);
            btnResetBase.ForeColor = Color.White;

            btnResetBase.FlatStyle = FlatStyle.Flat;
            btnResetBase.FlatAppearance.BorderColor = Color.FromArgb(140, 30, 30);
            btnResetBase.FlatAppearance.BorderSize = 1;
        }

        // DISM 출력 라인을 UI에 표시할지 결정 (진행률/잡다한 헤더 제거)
        private static bool ShouldShowDismLineInUi(string line)
        {
            var s = line.Trim();

            // 진행률 바/퍼센트 반복 줄 제거
            if (Regex.IsMatch(s, @"^\[[=\-\s]*\d{1,3}(\.\d+)?%[=\-\s]*\]$")) return false;
            if (s.Contains("%") && s.Contains("[") && s.Contains("]") && s.Contains('=')) return false;

            // "50.0%" 같은 단독 퍼센트 줄 제거
            if (Regex.IsMatch(s, @"^\d{1,3}(\.\d+)?%$")) return false;

            // DISM 헤더/군더더기
            if (s.StartsWith("배포 이미지 서비스", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("Deployment Image Servicing", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("이미지 버전", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        // DISM 라인 기록: 전체 로그에는 저장, UI에는 필터링해서 표시
        private void AddDismLine(string line)
        {
            _fullLog.AppendLine(line);

            if (ShouldShowDismLineInUi(line))
                AppendLogLine(line);
        }

        private void TrimUiLogIfTooLong()
        {
            const int MaxChars = 60_000;
            if (txtLog.TextLength <= MaxChars) return;

            txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - MaxChars);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }
    }
}
