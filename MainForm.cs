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
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinSxS_Cleanup_Tool.json");

        // =========================
        // UI Controls
        // =========================
        private readonly Label lblTitle = new();
        private readonly Label lblAdminBadge = new();
        private readonly Button btnAnalyze = new();
        private readonly Button btnCleanup = new();
        private readonly Button btnResetBase = new();
        private readonly Button btnCancel = new();
        private readonly Button btnSaveLog = new();
        private readonly Button btnGitHub = new();
        private readonly CheckBox chkReAnalyze = new();
        private readonly CheckBox chkDarkMode = new();
        private readonly ProgressBar progress = new();
        private readonly Label lblProgress = new();
        private readonly Label lblStatus = new();
        private readonly TextBox txtLog = new();

        // Summary cards
        private readonly Label lblExpected = new();
        private readonly Label lblActualBefore = new();
        private readonly Label lblActualAfter = new();
        private readonly Label lblActualFreed = new();

        // =========================
        // State
        // =========================
        private CancellationTokenSource? _cts;
        private bool _isBusy;

        // For actual sizes
        private double _lastExpectedMB;
        private double _lastActualBeforeMB;
        private double _lastActualAfterMB;

        // Full log buffer
        private readonly StringBuilder _fullLog = new();

        // progress fallback (for UX)
        private readonly System.Windows.Forms.Timer _progressFallbackTimer = new();
        private int _progressFallbackTarget = 0;

        // =========================
        // Ctor
        // =========================
        public MainForm()
        {
            Text = AppTitle;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 600);

            ApplyExeIcon();

            BuildUi();
            LoadConfig();
            ApplyDarkMode(chkDarkMode.Checked);

            Shown += async (_, __) =>
            {
                // 첫 표시 후 관리자/초기 상태 적용
                UpdateAdminState();
                SetStatus("대기");
                await Task.CompletedTask;
            };

            FormClosing += (_, __) =>
            {
                SaveConfig();
            };
        }

        // =========================
        // UI Construction
        // =========================
        private void BuildUi()
        {
            // Title
            lblTitle.Text = "🧹 WinSxS Cleanup Tool";
            lblTitle.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(18, 16);
            Controls.Add(lblTitle);

            // Admin badge
            lblAdminBadge.Text = "ADMIN";
            lblAdminBadge.AutoSize = false;
            lblAdminBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblAdminBadge.Size = new Size(74, 22);
            lblAdminBadge.Location = new Point(18, 56);
            lblAdminBadge.BackColor = Color.FromArgb(90, 90, 90);
            lblAdminBadge.ForeColor = Color.White;
            lblAdminBadge.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            lblAdminBadge.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(lblAdminBadge);

            // Buttons
            btnAnalyze.Text = "🔎 분석";
            btnCleanup.Text = "🧹 정리";
            btnResetBase.Text = "⚠ ResetBase";
            btnCancel.Text = "⛔ 취소";
            btnSaveLog.Text = "💾 로그 저장";
            btnGitHub.Text = "🌐 GitHub";

            int btnY = 90;
            int btnX = 18;
            int btnW = 140;
            int btnH = 34;
            int btnGap = 10;

            btnAnalyze.SetBounds(btnX, btnY, btnW, btnH);
            btnCleanup.SetBounds(btnX + (btnW + btnGap) * 1, btnY, btnW, btnH);
            btnResetBase.SetBounds(btnX + (btnW + btnGap) * 2, btnY, btnW, btnH);
            btnCancel.SetBounds(btnX + (btnW + btnGap) * 3, btnY, btnW, btnH);
            btnSaveLog.SetBounds(btnX + (btnW + btnGap) * 4, btnY, btnW, btnH);
            btnGitHub.SetBounds(btnX + (btnW + btnGap) * 5, btnY, btnW, btnH);

            Controls.Add(btnAnalyze);
            Controls.Add(btnCleanup);
            Controls.Add(btnResetBase);
            Controls.Add(btnCancel);
            Controls.Add(btnSaveLog);
            Controls.Add(btnGitHub);

            btnCancel.Enabled = false;

            btnAnalyze.Click += async (_, __) => await AnalyzeAsync();
            btnCleanup.Click += async (_, __) => await CleanupAsync(resetBase: false);
            btnResetBase.Click += async (_, __) => await CleanupAsync(resetBase: true);
            btnCancel.Click += (_, __) => CancelRun();
            btnSaveLog.Click += (_, __) => SaveLogToFile();
            btnGitHub.Click += (_, __) => OpenUrl(GitHubUrl);

            // Checkboxes
            chkReAnalyze.Text = "정리 후 재분석(실제 절감량 계산)";
            chkReAnalyze.AutoSize = true;
            chkReAnalyze.Location = new Point(18, 135);
            Controls.Add(chkReAnalyze);

            chkDarkMode.Text = "다크 모드";
            chkDarkMode.AutoSize = true;
            chkDarkMode.Location = new Point(18, 160);
            chkDarkMode.CheckedChanged += (_, __) =>
            {
                ApplyDarkMode(chkDarkMode.Checked);
                SaveConfig();
            };
            Controls.Add(chkDarkMode);

            // Summary cards
            int cardY = 200;
            int cardH = 80;
            int cardW = (ClientSize.Width - 18 * 2 - 10 * 3) / 4;
            int cardX = 18;

            var cards = new[] { lblExpected, lblActualBefore, lblActualAfter, lblActualFreed };
            string[] cardTitles = { "예상 절감(추정)", "정리 전(실제)", "정리 후(실제)", "실제 절감량" };

            for (int i = 0; i < cards.Length; i++)
            {
                var lbl = cards[i];
                lbl.BorderStyle = BorderStyle.FixedSingle;
                lbl.TextAlign = ContentAlignment.MiddleLeft;
                lbl.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
                lbl.Padding = new Padding(10, 10, 10, 10);

                lbl.SetBounds(cardX + i * (cardW + 10), cardY, cardW, cardH);
                lbl.Text = $"{cardTitles[i]}\n-";
                Controls.Add(lbl);
            }

            Resize += (_, __) =>
            {
                int newCardW = (ClientSize.Width - 18 * 2 - 10 * 3) / 4;
                for (int i = 0; i < cards.Length; i++)
                {
                    cards[i].SetBounds(cardX + i * (newCardW + 10), cardY, newCardW, cardH);
                }

                // log + progress area resize
                var logTop = cardY + cardH + 10;
                txtLog.SetBounds(18, logTop + 70, ClientSize.Width - 36, ClientSize.Height - (logTop + 70) - 20);
                progress.SetBounds(18, logTop, ClientSize.Width - 160, 20);
                lblProgress.SetBounds(progress.Right + 10, logTop, 120, 20);
                lblStatus.SetBounds(18, logTop + 30, ClientSize.Width - 36, 26);
            };

            // Progress
            int logAreaTop = cardY + cardH + 10;

            progress.Minimum = 0;
            progress.Maximum = 100;
            progress.Value = 0;
            progress.SetBounds(18, logAreaTop, ClientSize.Width - 160, 20);
            Controls.Add(progress);

            lblProgress.Text = "0%";
            lblProgress.TextAlign = ContentAlignment.MiddleLeft;
            lblProgress.SetBounds(progress.Right + 10, logAreaTop, 120, 20);
            Controls.Add(lblProgress);

            lblStatus.Text = "상태: 대기";
            lblStatus.AutoEllipsis = true;
            lblStatus.SetBounds(18, logAreaTop + 30, ClientSize.Width - 36, 26);
            Controls.Add(lblStatus);

            // Log box
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.SetBounds(18, logAreaTop + 70, ClientSize.Width - 36, ClientSize.Height - (logAreaTop + 70) - 20);
            Controls.Add(txtLog);

            // Progress fallback timer
            _progressFallbackTimer.Interval = 250;
            _progressFallbackTimer.Tick += (_, __) =>
            {
                if (!_isBusy) { _progressFallbackTimer.Stop(); return; }
                if (progress.Value < _progressFallbackTarget)
                {
                    progress.Value = Math.Min(_progressFallbackTarget, progress.Value + 1);
                    lblProgress.Text = $"{progress.Value}%";
                }
            };
        }

        // =========================
        // Admin / DarkMode
        // =========================
        private void UpdateAdminState()
        {
            bool isAdmin = IsAdministrator();

            lblAdminBadge.Text = isAdmin ? "ADMIN" : "USER";
            lblAdminBadge.BackColor = isAdmin ? Color.FromArgb(20, 130, 70) : Color.FromArgb(90, 90, 90);
            lblAdminBadge.ForeColor = Color.White;

            if (!isAdmin)
            {
                // admin badge highlight a bit
                lblAdminBadge.BackColor = Color.FromArgb(170, 40, 40);
                lblAdminBadge.ForeColor = Color.White;
            }

            // 안전장치: 비관리자면 정리/ResetBase 비활성
            btnCleanup.Enabled = isAdmin && !_isBusy;
            btnResetBase.Enabled = isAdmin && !_isBusy;
        }

        private void ApplyDarkMode(bool enabled)
        {
            Color back = enabled ? Color.FromArgb(25, 25, 28) : SystemColors.Control;
            Color fore = enabled ? Color.Gainsboro : SystemColors.ControlText;

            BackColor = back;
            ForeColor = fore;

            foreach (Control c in Controls)
            {
                ApplyThemeRecursive(c, back, fore, enabled);
            }
        }

        private static void ApplyThemeRecursive(Control c, Color back, Color fore, bool dark)
        {
            if (c is TextBox tb)
            {
                tb.BackColor = dark ? Color.FromArgb(18, 18, 20) : Color.White;
                tb.ForeColor = dark ? Color.Gainsboro : Color.Black;
                return;
            }

            if (c is ProgressBar)
            {
                return; // keep OS style
            }

            c.BackColor = back;
            c.ForeColor = fore;

            foreach (Control child in c.Controls)
                ApplyThemeRecursive(child, back, fore, dark);
        }

        // =========================
        // Analyze
        // =========================
        private async Task AnalyzeAsync()
        {
            if (_isBusy) return;

            if (!IsAdministrator())
            {
                MessageBox.Show(
                    UiText.AdminRequiredMessage,
                    UiText.AdminRequiredTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            SetBusy(true);
            ResetProgressForRun();
            SetStatus("분석 중");
            Log("▶ WinSxS 구성 요소 저장소 분석을 시작합니다...");

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            try
            {
                var lines = new List<string>();

                int exitCode = await RunDismAsync(
                    "/Online /Cleanup-Image /AnalyzeComponentStore",
                    (line, isErr) =>
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lock (lines)
                                lines.Add(line);
                        }
                        AddDismLine(line);
                    },
                    token);

                Log($"✔ 분석 완료 (ExitCode: {exitCode})");
                ParseAnalyzeResult(lines);
                UpdateSummaryLabels();

                SetStatus("분석 완료");
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

        // =========================
        // Cleanup / ResetBase
        // =========================
        private async Task CleanupAsync(bool resetBase)
        {
            if (_isBusy) return;

            if (!IsAdministrator())
            {
                MessageBox.Show(
                    UiText.AdminRequiredMessage,
                    UiText.AdminRequiredTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (resetBase)
            {
                bool ok = await ShowResetBaseTwoStepConfirmAsync();
                if (!ok)
                {
                    Log("⛔ ResetBase 실행이 취소되었습니다.");
                    return;
                }
            }

            // 정리 전 실제 크기(분석을 안 했더라도 가능하면 갱신)
            double beforeMB = _lastActualBeforeMB;

            Log(resetBase ? UiText.ResetBaseStartLog : UiText.CleanupStartLog);

            SetStatus(resetBase ? UiText.ResetBaseInProgressStatus : UiText.CleanupInProgressStatus);

            SetBusy(true);

            ResetProgressForRun();

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            try
            {
                string args = resetBase
                    ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase"
                    : "/Online /Cleanup-Image /StartComponentCleanup";

                // Progress target smooth
                _progressFallbackTarget = 30;
                _progressFallbackTimer.Start();

                int exitCode = await RunDismAsync(
                    args,
                    (line, isErr) =>
                    {
                        AddDismLine(line);

                        // Attempt to update progress by parsing percent
                        if (TryParsePercent(line, out int pct))
                        {
                            SetProgressSafe(pct);
                            _progressFallbackTarget = Math.Max(_progressFallbackTarget, pct);
                        }
                    },
                    token);

                Log($"✔ {(resetBase ? "ResetBase" : "정리")} 완료 (ExitCode: {exitCode})");

                // 정리 후 재분석(옵션)
                if (chkReAnalyze.Checked)
                {
                    SetStatus("정리 후 재분석");
                    Log("▶ 정리 후 재분석을 시작합니다...");

                    var lines = new List<string>();

                    _progressFallbackTarget = 60;

                    int analyzeExit = await RunDismAsync(
                        "/Online /Cleanup-Image /AnalyzeComponentStore",
                        (line, isErr) =>
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                lock (lines)
                                    lines.Add(line);
                            }
                            AddDismLine(line);

                            if (TryParsePercent(line, out int pct))
                            {
                                // map percent into 60~95 region
                                int mapped = 60 + (int)(pct * 0.35);
                                SetProgressSafe(mapped);
                                _progressFallbackTarget = Math.Max(_progressFallbackTarget, mapped);
                            }
                        },
                        token);

                    Log($"✔ 재분석 완료 (ExitCode: {analyzeExit})");

                    ParseAnalyzeResult(lines);

                    // 실 절감량 계산
                    if (beforeMB > 0 && _lastActualAfterMB > 0)
                    {
                        _lastActualFreed = beforeMB - _lastActualAfterMB;
                        if (_lastActualFreed < 0) _lastActualFreed = 0;
                    }

                    UpdateSummaryLabels();
                }

                // 🔽 요약은 항상 맨 마지막
                LogSummaryBlock("정리 결과 요약");
                SetStatus("완료 (결과 요약을 확인하세요)");
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

        private double _lastActualFreed;

        // =========================
        // ResetBase Two-Step Confirm
        // =========================
        private Task<bool> ShowResetBaseTwoStepConfirmAsync()
        {
            // 1) First warning dialog
            var first = MessageBox.Show(
                UiText.ResetBaseWarnMessage,
                UiText.ResetBaseWarnTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (first != DialogResult.OK)
                return Task.FromResult(false);

            // 2) Second confirm with countdown form
            using var f = new ResetBaseConfirmForm();
            var result = f.ShowDialog(this);

            return Task.FromResult(result == DialogResult.OK);
        }


        // =========================
        // Cancel / Busy
        // =========================
        private void CancelRun()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignore
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            btnAnalyze.Enabled = !busy;
            btnCancel.Enabled = busy;

            UpdateAdminState(); // respects isAdmin + busy

            btnSaveLog.Enabled = !busy;
            btnGitHub.Enabled = !busy;
        }

        // =========================
        // Summary labels
        // =========================
        private void UpdateSummaryLabels()
        {
            lblExpected.Text = $"예상 절감(추정)\n{FormatMB(_lastExpectedMB)}";
            lblActualBefore.Text = $"정리 전(실제)\n{FormatMB(_lastActualBeforeMB)}";
            lblActualAfter.Text = $"정리 후(실제)\n{FormatMB(_lastActualAfterMB)}";
            lblActualFreed.Text = $"실제 절감량\n{FormatMB(_lastActualFreed)}";
        }

        // =========================
        // Status / Progress / Log
        // =========================
        private void SetStatus(string status)
        {
            lblStatus.Text = $"상태: {status}";
        }

        private void SetProgressSafe(int value)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int>(SetProgressSafe), value);
                return;
            }

            value = Math.Max(0, Math.Min(100, value));
            progress.Value = value;
            lblProgress.Text = $"{value}%";
        }

        private void ResetProgressForRun()
        {
            SetProgressSafe(0);
            _progressFallbackTarget = 0;
        }

        private void AppendLogLine(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLogLine), line);
                return;
            }

            txtLog.AppendText(line + Environment.NewLine);
            TrimUiLogIfTooLong();
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _fullLog.AppendLine(line);
            AppendLogLine(line);
        }

        private void LogSummaryBlock(string title)
        {
            Log("");
            Log($"========== {title} ==========");
            Log($"예상 절감(추정): {FormatMB(_lastExpectedMB)}");
            if (_lastActualBeforeMB > 0)
                Log($"정리 전(실제): {FormatMB(_lastActualBeforeMB)}");
            if (_lastActualAfterMB > 0)
                Log($"정리 후(실제): {FormatMB(_lastActualAfterMB)}");
            if (_lastActualFreed > 0)
                Log($"실제 절감량: {FormatMB(_lastActualFreed)}");
            Log("================================");
            Log("");
        }

        // =========================
        // Save Log
        // =========================
        private void SaveLogToFile()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "로그 저장",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"winsxs_cleanup_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.WriteAllText(sfd.FileName, _fullLog.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                MessageBox.Show(
                    "로그 파일이 성공적으로 저장되었습니다.\n\n" +
                    "문제 발생 시, 이 로그 파일을 함께 전달해 주세요.",
                    "로그 저장 완료",
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
                StandardErrorEncoding = enc
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            p.Exited += (_, __) =>
            {
                try { tcs.TrySetResult(p.ExitCode); }
                catch { /* ignore */ }
            };

            p.Start();

            // Read output / error
            Task readOut = Task.Run(async () =>
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    string? line = await p.StandardOutput.ReadLineAsync();
                    if (line is null) break;
                    onLine(line, false);
                }
            }, token);

            Task readErr = Task.Run(async () =>
            {
                while (!p.StandardError.EndOfStream)
                {
                    string? line = await p.StandardError.ReadLineAsync();
                    if (line is null) break;
                    onLine(line, true);
                }
            }, token);

            using (token.Register(() =>
            {
                try
                {
                    if (!p.HasExited) p.Kill(entireProcessTree: true);
                }
                catch { /* ignore */ }
            }))
            {
                await Task.WhenAll(readOut, readErr);
                return await tcs.Task;
            }
        }

        // =========================
        // Parse Analyze Result
        // =========================
        private void ParseAnalyzeResult(List<string> lines)
        {
            // Expected cleanup size:
            // - "Windows 탐색기 보고서: ... 정리 가능한 패키지 크기 : 1.23 GB"
            // - "Recommended Cleanup : 1.23 GB"
            // We'll parse multiple patterns robustly.
            double expectedMB = ParseRecommendedCleanupFromLines(lines);
            _lastExpectedMB = expectedMB;

            double beforeMB = ParseActualStoreSizeFromLines(lines) ?? 0;
            if (beforeMB > 0)
            {
                _lastActualBeforeMB = beforeMB;
            }

            // After size may also appear depending on output; in analyze it's current actual size.
            // We'll store it as 'after' only when reanalyze after cleanup.
            // In first analyze, treat as before.
            if (chkReAnalyze.Checked == false)
            {
                _lastActualAfterMB = 0;
                _lastActualFreed = 0;
            }
            else
            {
                // When reanalyzing, we interpret as "after"
                _lastActualAfterMB = beforeMB;
            }
        }

        private static double ParseRecommendedCleanupFromLines(List<string> lines)
        {
            // Common lines:
            // - "권장 구성 요소 저장소 정리 : 1.23 GB"
            // - "Recommended Cleanup : 1.23 GB"
            // - "정리 가능한 패키지 크기 : 1.23 GB"
            foreach (string line in lines)
            {
                string s = line.Trim();

                // Korean patterns
                if (TryParseSizeFromLine(s, "권장", out double mb1)) return mb1;
                if (TryParseSizeFromLine(s, "Recommended Cleanup", out double mb2)) return mb2;
                if (TryParseSizeFromLine(s, "정리 가능한", out double mb3)) return mb3;
                if (TryParseSizeFromLine(s, "정리 가능", out double mb4)) return mb4;
            }
            return 0;
        }

        private static double? ParseActualStoreSizeFromLines(List<string> lines)
        {
            // - "구성 요소 저장소의 실제 크기 : 12.03 GB"
            // - "Actual size of component store : 12.03 GB"
            foreach (string line in lines)
            {
                string s = line.Trim();

                if (TryParseSizeFromLine(s, "실제", out double mb1)) return mb1;
                if (TryParseSizeFromLine(s, "Actual size of component store", out double mb2)) return mb2;
            }

            return null;
        }

        private static bool TryParseSizeFromLine(string line, string contains, out double mb)
        {
            mb = 0;
            if (!line.Contains(contains, StringComparison.OrdinalIgnoreCase)) return false;

            // find number + unit (KB/MB/GB)
            var m = Regex.Match(line, @"([0-9]+(?:\.[0-9]+)?)\s*(KB|MB|GB)", RegexOptions.IgnoreCase);
            if (!m.Success) return false;

            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return false;

            string unit = m.Groups[2].Value.ToUpperInvariant();
            mb = unit switch
            {
                "KB" => val / 1024.0,
                "MB" => val,
                "GB" => val * 1024.0,
                _ => 0
            };
            return mb > 0;
        }

        private static bool TryParsePercent(string line, out int percent)
        {
            percent = 0;
            var m = Regex.Match(line, @"(\d+)\s*%", RegexOptions.CultureInvariant);
            if (!m.Success) return false;

            if (!int.TryParse(m.Groups[1].Value, out int p)) return false;
            percent = Math.Max(0, Math.Min(100, p));
            return true;
        }

        private static string FormatMB(double mb)
        {
            if (mb <= 0) return "-";

            if (mb >= 1024.0)
                return $"{mb / 1024.0:0.00} GB";
            return $"{mb:0.00} MB";
        }

        // =========================
        // Config Save/Load
        // =========================
        private sealed class AppConfig
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int W { get; set; }
            public int H { get; set; }
            public bool ReAnalyze { get; set; }
            public bool DarkMode { get; set; }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg is null) return;

                if (cfg.W > 200 && cfg.H > 200)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(cfg.X, cfg.Y);
                    Size = new Size(cfg.W, cfg.H);
                }

                chkReAnalyze.Checked = cfg.ReAnalyze;
                chkDarkMode.Checked = cfg.DarkMode;
            }
            catch
            {
                // ignore
            }
        }

        private void SaveConfig()
        {
            try
            {
                var cfg = new AppConfig
                {
                    X = Location.X,
                    Y = Location.Y,
                    W = Size.Width,
                    H = Size.Height,
                    ReAnalyze = chkReAnalyze.Checked,
                    DarkMode = chkDarkMode.Checked
                };
                string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
                // ignore
            }
        }

        // =========================
        // Helpers
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

        private static Encoding GetConsoleOemEncoding()
        {
            try
            {
                // CP_OEMCP
                return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch
            {
                return Encoding.UTF8;
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void ApplyExeIcon()
        {
            try
            {
                string exe = Application.ExecutablePath;
                Icon? icon = Icon.ExtractAssociatedIcon(exe);
                if (icon is not null)
                    Icon = icon;
            }
            catch
            {
                // ignore
            }
        }

        // DISM 라인 UI 표시 필터
        private static bool ShouldShowDismLineInUi(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            string s = line.Trim();

            // 너무 장황한 헤더/라이선스 등은 숨김
            if (s.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("GPL", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("이 프로그램은", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("법률이", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("배포", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.StartsWith("이미지 버전", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        // DISM 라인 기록: 전체 로그에는 저장, UI에는 필터링해서 표시
        private void AddDismLine(string line)
        {
            _fullLog.AppendLine(line);

            if (ShouldShowDismLineInUi(line))
                AppendLogLine(line); // 기존 UI 출력 함수 재사용
        }

        // (선택) UI 로그 자체도 너무 길어지면 앞부분을 잘라내기
        private void TrimUiLogIfTooLong()
        {
            const int MaxChars = 60_000; // 대충 6만자 선에서 잘라내기
            if (txtLog.TextLength <= MaxChars) return;

            txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - MaxChars);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

    }

}
