// MainForm.cs (최종 통합본, 경고 0개 목표)
// - 상단 요약 카드(예상/정리 전/정리 후/실제 절감량)
// - 관리자 권한 표시(ADMIN 배지 + 비관리자 시 정리/ResetBase 비활성)
// - ResetBase 2단 확인(카운트다운으로 확인 버튼 활성)
// - 로그 저장 버튼(UTF-8)
// - 진행률 % 연동 + Fallback 타이머(진행률이 안 올라갈 때 부드럽게 전진)
// - 정리 후 재분석 체크박스(실제 절감량 계산)
// - GitHub 링크
// - 설정 저장(JSON): 창 위치/크기 + 체크박스
// - 콘솔 출력 인코딩(Windows OEM 코드페이지)로 글자 깨짐 방지
// - 아이콘: EXE에 내장된 아이콘을 폼에 그대로 적용(ExtractAssociatedIcon)

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
        private Label lblAdminBadge = null!;

        private GroupBox grpSummary = null!;
        private Label lblExpected = null!;
        private Label lblBefore = null!;
        private Label lblAfter = null!;
        private Label lblSaved = null!;

        private Label lblStatus = null!;
        private ProgressBar progress = null!;
        private TextBox txtLog = null!;

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

        // Fallback timer (명시적으로 WinForms Timer 사용)
        private System.Windows.Forms.Timer _fallbackTimer = null!;

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
                Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? Icon;
            }
            catch
            {
                // ignore
            }

            UpdateAdminUi();
            UpdateSummaryLabels();
            SetStatus("대기");

            FormClosing += (_, __) => SaveSettingsSafe();
        }

        // =========================
        // Initialize UI
        // =========================

        private void InitializeComponent()
        {
            Text = BuildTitle();
            Width = 980;
            Height = 680;
            StartPosition = FormStartPosition.CenterScreen;

            btnAnalyze = new Button { Text = "분석", Left = 12, Top = 12, Width = 110, Height = 34 };
            btnCleanup = new Button { Text = "정리", Left = 128, Top = 12, Width = 110, Height = 34 };
            btnResetBase = new Button { Text = "ResetBase", Left = 244, Top = 12, Width = 120, Height = 34 };
            btnCancel = new Button { Text = "취소", Left = 370, Top = 12, Width = 110, Height = 34, Enabled = false };

            lblAdminBadge = new Label
            {
                Text = "ADMIN",
                Left = 820,
                Top = 12,
                Width = 140,
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 140, 30),
                ForeColor = Color.White
            };

            btnSaveLog = new Button { Text = "로그 저장", Left = 820, Top = 52, Width = 140, Height = 30 };

            linkGitHub = new LinkLabel
            {
                Text = "GitHub",
                Left = 900,
                Top = 88,
                Width = 60,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight
            };

            var linkAbout = new LinkLabel
            {
                Text = "About",
                Left = linkGitHub.Left - 60,
                Top = linkGitHub.Top,
                Width = 55,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight
            };
            linkAbout.LinkClicked += (_, __) => ShowAbout();

            Controls.Add(linkAbout);

            // GitHub
            linkGitHub.TextAlign = ContentAlignment.MiddleRight;

            // About (GitHub 왼쪽에 딱 붙이기)
            linkAbout.Left = linkGitHub.Left - linkAbout.Width - 8;
            linkAbout.TextAlign = ContentAlignment.MiddleRight;

            chkReAnalyze = new CheckBox
            {
                Text = "정리 후 재분석 (실제 절감량 계산)",
                Left = 12,
                Top = 56,
                Width = 360,
                Height = 24,
                Checked = _settings.ReAnalyzeAfterCleanup
            };

            grpSummary = new GroupBox
            {
                Text = "요약",
                Left = 12,
                Top = 84,
                Width = 948,
                Height = 90
            };

            lblExpected = new Label { Left = 12, Top = 24, Width = 450, Height = 22 };
            lblBefore = new Label { Left = 12, Top = 48, Width = 450, Height = 22 };
            lblAfter = new Label { Left = 480, Top = 48, Width = 450, Height = 22 };
            lblSaved = new Label { Left = 480, Top = 24, Width = 450, Height = 22 };

            grpSummary.Controls.AddRange(new Control[] { lblExpected, lblSaved, lblBefore, lblAfter });

            lblStatus = new Label { Text = "상태: -", Left = 12, Top = 178, Width = 700, Height = 24 };

            progress = new ProgressBar
            {
                Left = 12,
                Top = 206,
                Width = 948,
                Height = 18,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            txtLog = new TextBox
            {
                Left = 12,
                Top = 232,
                Width = 948,
                Height = 400,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 10),
                WordWrap = false
            };

            Controls.AddRange(new Control[]
            {
                btnAnalyze, btnCleanup, btnResetBase, btnCancel,
                lblAdminBadge, btnSaveLog, linkGitHub,
                chkReAnalyze, grpSummary, lblStatus, progress, txtLog
            });

            btnAnalyze.Click += async (_, __) => await AnalyzeAsync();
            btnCleanup.Click += async (_, __) => await CleanupAsync(resetBase: false);
            btnResetBase.Click += async (_, __) => await CleanupAsync(resetBase: true);

            btnCancel.Click += (_, __) => _cts?.Cancel();
            btnSaveLog.Click += (_, __) => SaveLog();
            linkGitHub.LinkClicked += (_, __) => OpenUrl(GitHubUrl);

            chkReAnalyze.CheckedChanged += (_, __) =>
            {
                _settings.ReAnalyzeAfterCleanup = chkReAnalyze.Checked;
                SaveSettingsSafe();
            };

            // Fallback timer
            _fallbackTimer = new System.Windows.Forms.Timer();
            _fallbackTimer.Interval = 250;
            _fallbackTimer.Tick += (_, __) => ProgressFallbackTick();
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
            SetStatus("분석");
            SetBusy(true);

            ResetProgressForRun();

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            var lines = new List<string>();

            try
            {
                Log("▶ WinSxS 분석 시작...");
                int exitCode = await RunDismAsync(
                    "/Online /Cleanup-Image /AnalyzeComponentStore",
                    (line, isErr) =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        lines.Add(line);
                        UpdateProgressFromLine(line);
                        Log(line);
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

                UpdateSummaryLabels();

                if (_lastUpperBoundMB > 0)
                {
                    Log($"✔ 분석 완료: 정리 가능 상한 {FormatMB(_lastUpperBoundMB)}");
                }
                else
                {
                    Log("✅ 분석은 완료되었습니다. 다만 Windows(DISM)가 '정리 가능 상한' 정보를 제공하지 않아 예상치를 표시할 수 없습니다. (정리 후 재분석으로 실제 절감량 확인 가능)");
                }

                if (_lastActualBeforeMB > 0)
                {
                    Log($"ℹ 구성 요소 저장소 실제 크기(정리 전): {FormatMB(_lastActualBeforeMB)}");
                }

                Log($"(ExitCode: {exitCode})");
                SetStatus("완료");
                SetProgressSafe(100);
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업이 취소되었습니다.");
                SetStatus("취소됨");
                SetProgressSafe(0);
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                SetStatus("오류");
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
                MessageBox.Show("정리/ResetBase는 관리자 권한이 필요합니다.\nVisual Studio(또는 exe)를 관리자 권한으로 실행해주세요.",
                    "권한 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (resetBase)
            {
                bool ok = await ShowResetBaseTwoStepConfirmAsync();
                if (!ok) return;
            }

            // 정리 전 값 (분석을 했으면 있음)
            double beforeMB = _lastActualBeforeMB;

            Log(resetBase ? "▶ ResetBase 시작..." : "▶ 정리 시작...");
            SetStatus(resetBase ? "ResetBase" : "정리");
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
                        Log(line);
                    },
                    token);

                Log($"✔ {(resetBase ? "ResetBase" : "정리")} 완료 (ExitCode: {exitCode})");

                // 정리 후 재분석(옵션)
                if (chkReAnalyze.Checked)
                {
                    SetStatus("정리 후 재분석");
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
                            Log(line);
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
                        Log("⚠ 실제 절감량 계산 실패: 정리 전/후 '실제 크기' 정보가 충분하지 않습니다. (먼저 분석을 1회 실행해 주세요)");
                    }

                    Log($"(Re-Analyze ExitCode: {analyzeExit})");
                }

                UpdateSummaryLabels();
                SetStatus("완료");
                SetProgressSafe(100);
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업 취소됨");
                SetStatus("취소됨");
                SetProgressSafe(0);
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                SetStatus("오류");
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

        // ResetBase 2단 확인 (카운트다운 + 체크박스)
        private Task<bool> ShowResetBaseTwoStepConfirmAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            var dlg = new Form
            {
                Text = "경고 - ResetBase",
                StartPosition = FormStartPosition.CenterParent,
                Width = 540,
                Height = 300,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label
            {
                Left = 16,
                Top = 16,
                Width = 500,
                Height = 90,
                Text =
                    "ResetBase는 되돌릴 수 없습니다.\n" +
                    "실행하면 기존 컴포넌트 버전으로 되돌릴 수 없게 됩니다.\n\n" +
                    "아래 체크 후, '확인' 버튼이 활성화될 때까지 기다린 뒤 진행하세요."
            };

            var chk = new CheckBox
            {
                Left = 16,
                Top = 110,
                Width = 500,
                Height = 24,
                Text = "위 내용을 이해했으며 ResetBase를 실행하겠습니다."
            };

            var lblCountdown = new Label
            {
                Left = 16,
                Top = 140,
                Width = 240,
                Height = 24,
                Text = "대기: 5초"
            };

            var btnOk = new Button { Text = "확인", Left = 310, Top = 170, Width = 100, Height = 34, Enabled = false };
            var btnCancel = new Button { Text = "취소", Left = 420, Top = 170, Width = 100, Height = 34 };

            dlg.Controls.AddRange(new Control[] { lbl, chk, lblCountdown, btnOk, btnCancel });

            bool countdownDone = false;
            void UpdateOk() => btnOk.Enabled = countdownDone && chk.Checked;

            chk.CheckedChanged += (_, __) => UpdateOk();

            btnCancel.Click += (_, __) =>
            {
                dlg.Close();
                tcs.TrySetResult(false);
            };

            btnOk.Click += (_, __) =>
            {
                dlg.Close();
                tcs.TrySetResult(true);
            };

            int remain = 5;
            var timer = new System.Windows.Forms.Timer { Interval = 1000, Enabled = true };

            timer.Tick += (_, __) =>
            {
                remain--;
                if (remain <= 0)
                {
                    timer.Stop();
                    timer.Dispose();
                    lblCountdown.Text = "진행 가능";
                    countdownDone = true;
                    UpdateOk();
                }
                else
                {
                    lblCountdown.Text = $"대기: {remain}초";
                }
            };

            dlg.FormClosed += (_, __) =>
            {
                if (timer.Enabled)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            dlg.ShowDialog(this);
            return tcs.Task;
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
            // 1) "33.0%" 같은 숫자 퍼센트 파싱
            // 2) 가끔 "100.0%" 여러번 나올 수 있음
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

            // 실제 %가 한번이라도 있었으면, 95%까지만 천천히(“멈춘 듯” 보이는 것 방지)
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
                BeginInvoke(new Action(() =>
                {
                    progress.Value = value;
                }));
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

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyBusyState()));
            }
            else
            {
                ApplyBusyState();
            }
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
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => lblStatus.Text = $"상태: {text}"));
            }
            else
            {
                lblStatus.Text = $"상태: {text}";
            }
        }

        // =========================
        // Log
        // =========================
        private void Log(string msg)
        {
            string line = msg;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendLogLine(line)));
            }
            else
            {
                AppendLogLine(line);
            }
        }

        private void AppendLogLine(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
        }

        private void SaveLog()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "로그 저장",
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                FileName = $"WinSxS_Cleanup_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.WriteAllText(sfd.FileName, txtLog.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                MessageBox.Show("저장 완료!", "로그 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            var enc = GetConsoleOemEncoding();

            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = enc,
                StandardErrorEncoding = enc,
            };

            using var p = new Process { StartInfo = psi };

            p.Start();

            // EndOfStream 경고 회피: ReadLineAsync null이면 종료
            Task readStdOut = Task.Run(async () =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    string? line = await p.StandardOutput.ReadLineAsync();
                    if (line is null) break;
                    onLine(line, false);
                }
            }, token);

            Task readStdErr = Task.Run(async () =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    string? line = await p.StandardError.ReadLineAsync();
                    if (line is null) break;
                    onLine(line, true);
                }
            }, token);

            await Task.WhenAll(readStdOut, readStdErr, p.WaitForExitAsync(token));
            return p.ExitCode;
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
            // - "백업 및 사용 안 함 : 4.51 GB"
            // - "Backup and Disabled Features : 4.51 GB"
            // - 기타 "Reclaimable" 계열
            foreach (string line in lines)
            {
                string s = line.Trim();

                if (s.Contains("백업 및 사용 안 함", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Backup and Disabled", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Reclaimable", StringComparison.OrdinalIgnoreCase))
                {
                    // 콜론 뒤 값 추출
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
            // - "구성 요소 저장소의 실제 크기 : 12.03 GB"
            // - "Actual size of component store : 12.03 GB"
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

            // 숫자 + 단위
            var m = Regex.Match(s, @"(?<num>[\d\.,]+)\s*(?<unit>TB|GB|MB|KB|B|bytes?)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;

            string numStr = m.Groups["num"].Value.Replace(",", "");
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return 0;

            string unit = m.Groups["unit"].Value.ToUpperInvariant();

            // DISM은 일반적으로 1024 기반 표기
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
        // Summary UI
        // =========================
        private void UpdateSummaryLabels()
        {
            // _lastUpperBoundMB: DISM이 제공하는 "백업 및 기능 사용 안 함(=정리 가능 상한)" 값
            // 실제 절감량은 정리 전/후 Actual Size 비교로 계산됩니다.
            string before = _lastActualBeforeMB > 0 ? FormatMB(_lastActualBeforeMB) : "-";
            string after = _lastActualAfterMB > 0 ? FormatMB(_lastActualAfterMB) : "-";

            // 예상(상한 + 범위)
            string expected = "-";
            if (_lastUpperBoundMB > 0)
            {
                double low = _lastUpperBoundMB * 0.20; // 보수적 범위(경험치)
                double high = _lastUpperBoundMB * 0.40;

                expected = $"{FormatMB(_lastUpperBoundMB)} (예상 {FormatMB(low)} ~ {FormatMB(high)})";
            }

            // 실제 절감량
            string saved = "-";
            if (_lastActualBeforeMB > 0 && _lastActualAfterMB > 0)
            {
                double s = Math.Max(0, _lastActualBeforeMB - _lastActualAfterMB);
                saved = $"{FormatMB(s)} (정리 전 {before} → 정리 후 {after})";
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    lblExpected.Text = $"정리 가능 상한: {expected}";
                    lblBefore.Text = $"정리 전: {before}";
                    lblAfter.Text = $"정리 후: {after}";
                    lblSaved.Text = $"실제 절감량: {saved}";
                    Text = BuildTitle();
                }));
            }
            else
            {
                lblExpected.Text = $"정리 가능 상한: {expected}";
                lblBefore.Text = $"정리 전: {before}";
                lblAfter.Text = $"정리 후: {after}";
                lblSaved.Text = $"실제 절감량: {saved}";
                Text = BuildTitle();
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

            btnResetBase.Text = "ResetBase (위험)";
            btnResetBase.UseVisualStyleBackColor = false;

            btnResetBase.BackColor = Color.FromArgb(180, 50, 50);
            btnResetBase.ForeColor = Color.White;

            btnResetBase.FlatStyle = FlatStyle.Flat;
            btnResetBase.FlatAppearance.BorderColor = Color.FromArgb(140, 30, 30);
            btnResetBase.FlatAppearance.BorderSize = 1;
        }
    }

}
