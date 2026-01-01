using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinSxSCleanupTool
{
    public class MainForm : Form
    {
        // ===== UI (null 경고 제거) =====
        private Button btnAnalyze = null!;
        private Button btnCleanup = null!;
        private Button btnResetBase = null!;
        private Button btnCancel = null!;

        private Label lblReclaim = null!;
        private Label lblStatus = null!;
        private Label lblActualSaved = null!;
        private CheckBox chkReAnalyze = null!;

        private ProgressBar progress = null!;
        private TextBox txtLog = null!;

        // ===== 상태 =====
        private volatile bool _isBusy = false;
        private CancellationTokenSource? _cts;

        private double _lastReclaimMB = 0.0;
        private string? _lastReclaimText;

        // 구성 요소 저장소 "실제 크기"(정리 전/후 비교용)
        private double _lastActualStoreMB = 0.0;

        // ===== Settings(JSON) =====
        private sealed class AppSettings
        {
            public bool ReAnalyzeAfterCleanup { get; set; } = true;

            public int FormX { get; set; } = -1;
            public int FormY { get; set; } = -1;
            public int FormW { get; set; } = 920;
            public int FormH { get; set; } = 620;
            public bool Maximized { get; set; } = false;
        }

        private AppSettings _settings = new AppSettings();

        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WinSxSCleanupTool",
                "settings.json"
            );

        public MainForm()
        {
            // (보험) 한국어 DISM 출력 OEM 인코딩 지원
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeComponent();

            // 폼 아이콘(좌상단/Alt+Tab) - broom.ico가 출력 폴더에 있어야 함
            TrySetFormIcon();

            LoadSettings();
            ApplySettingsToUI();

            FormClosing += (_, __) =>
            {
                CaptureUIToSettings();
                SaveSettings();
            };

            if (!IsAdministrator())
            {
                MessageBox.Show(
                    "관리자 권한이 필요합니다.\n프로그램을 관리자 권한으로 실행하세요.",
                    "권한 필요",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        // =====================================================
        // UI 구성
        // =====================================================
        private void InitializeComponent()
        {
            Text = "WinSxS Cleanup Tool (C#)";
            Width = 920;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;

            btnAnalyze = new Button { Text = "분석", Left = 12, Top = 12, Width = 120, Height = 34 };
            btnCleanup = new Button { Text = "정리(StartComponentCleanup)", Left = 140, Top = 12, Width = 220, Height = 34 };
            btnResetBase = new Button { Text = "ResetBase (되돌릴 수 없음)", Left = 368, Top = 12, Width = 220, Height = 34 };
            btnCancel = new Button { Text = "취소", Left = 596, Top = 12, Width = 120, Height = 34, Enabled = false };

            lblReclaim = new Label { Text = "예상 절감 용량: -", Left = 12, Top = 56, Width = 860, Height = 24 };
            lblStatus = new Label { Text = "상태: 대기", Left = 12, Top = 82, Width = 860, Height = 24 };

            chkReAnalyze = new CheckBox
            {
                Text = "정리 후 재분석 (실제 절감량 계산)",
                Left = 12,
                Top = 106,
                Width = 420,
                Checked = true
            };

            lblActualSaved = new Label
            {
                Text = "실제 절감량: - (정리 전 - → 정리 후 -)",
                Left = 12,
                Top = 130,
                Width = 860,
                Height = 24
            };

            progress = new ProgressBar
            {
                Left = 12,
                Top = 158,
                Width = 860,
                Height = 18,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            txtLog = new TextBox
            {
                Left = 12,
                Top = 184,
                Width = 860,
                Height = 386,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 10)
            };

            Controls.AddRange(new Control[]
            {
                btnAnalyze, btnCleanup, btnResetBase, btnCancel,
                lblReclaim, lblStatus,
                chkReAnalyze, lblActualSaved,
                progress, txtLog
            });

            btnAnalyze.Click += async (_, __) => await AnalyzeAsync();
            btnCleanup.Click += async (_, __) => await CleanupAsync(false);
            btnResetBase.Click += async (_, __) => await CleanupAsync(true);
            btnCancel.Click += (_, __) => _cts?.Cancel();

            chkReAnalyze.CheckedChanged += (_, __) =>
            {
                lblActualSaved.Enabled = chkReAnalyze.Checked;
                CaptureUIToSettings();
                SaveSettings();
            };
        }

        private void TrySetFormIcon()
        {
            try
            {
                // ✅ EmbeddedResource로 내장된 broom.ico를 로드
                var asm = typeof(MainForm).Assembly;

                // 프로젝트 기본 네임스페이스가 WinSxSCleanupTool 이고,
                // 파일명이 broom.ico 라면 보통 리소스 이름은 "WinSxSCleanupTool.broom.ico"
                const string resName = "WinSxSCleanupTool.broom.ico";

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    // 혹시 네임스페이스/경로가 달라서 못 찾는 경우 대비(로그)
                    Log($"⚠ 아이콘 리소스를 찾지 못했습니다: {resName}");
                    // 디버그용: 리소스 목록 출력(원하면)
                    // foreach (var n in asm.GetManifestResourceNames()) Log(" - " + n);
                    return;
                }

                Icon = new Icon(stream);
            }
            catch
            {
                // 실패해도 앱은 정상 동작
            }
        }


        // =====================================================
        // Settings(JSON)
        // =====================================================
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) _settings = loaded;
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        private void ApplySettingsToUI()
        {
            // 체크박스
            chkReAnalyze.Checked = _settings.ReAnalyzeAfterCleanup;
            lblActualSaved.Enabled = chkReAnalyze.Checked;

            // 창 상태/위치
            if (_settings.Maximized)
            {
                WindowState = FormWindowState.Maximized;
                return;
            }

            if (_settings.FormW > 0 && _settings.FormH > 0)
            {
                Width = _settings.FormW;
                Height = _settings.FormH;
            }

            if (_settings.FormX >= 0 && _settings.FormY >= 0)
            {
                StartPosition = FormStartPosition.Manual;
                Left = _settings.FormX;
                Top = _settings.FormY;
            }
        }

        private void CaptureUIToSettings()
        {
            _settings.ReAnalyzeAfterCleanup = chkReAnalyze.Checked;
            _settings.Maximized = (WindowState == FormWindowState.Maximized);

            var b = (WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds;
            _settings.FormX = b.Left;
            _settings.FormY = b.Top;
            _settings.FormW = b.Width;
            _settings.FormH = b.Height;
        }

        // =====================================================
        // UI 헬퍼
        // =====================================================
        private void Log(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), text); return; }
            txtLog.AppendText(text + Environment.NewLine);
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(SetStatus), text); return; }
            lblStatus.Text = "상태: " + text;
        }

        private void SetReclaim(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(SetReclaim), text); return; }
            lblReclaim.Text = "예상 절감 용량: " + text;
        }

        private void SetBusy(bool busy, string mode)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), busy, mode); return; }

            _isBusy = busy;
            btnAnalyze.Enabled = !busy;
            btnCleanup.Enabled = !busy;
            btnResetBase.Enabled = !busy;
            btnCancel.Enabled = busy;

            if (busy)
            {
                progress.Value = Math.Min(10, progress.Maximum);
                SetStatus($"{mode} 중...");
            }
            else
            {
                SetStatus("대기");
            }
        }

        private void SetActualSaved(double savedMB, double beforeMB, double afterMB)
        {
            string text =
                (beforeMB > 0 && afterMB > 0)
                ? $"실제 절감량: {FormatMB(savedMB)} (정리 전 {FormatMB(beforeMB)} → 정리 후 {FormatMB(afterMB)})"
                : "실제 절감량: - (정리 전 - → 정리 후 -)";

            if (InvokeRequired) BeginInvoke(new Action(() => lblActualSaved.Text = text));
            else lblActualSaved.Text = text;
        }

        private void UpdateProgressFromLine(string line)
        {
            var m = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
            if (!m.Success) return;

            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct))
                return;

            int v = (int)Math.Clamp(Math.Round(pct), 0, 100);

            if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = v));
            else progress.Value = v;
        }

        // =====================================================
        // ResetBase 안전장치(카운트다운)
        // =====================================================
        private bool ConfirmResetBaseWithCountdown(int seconds = 5)
        {
            using var dialog = new Form
            {
                Text = "경고 - ResetBase",
                Width = 460,
                Height = 220,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            };

            var lbl = new Label
            {
                Left = 12,
                Top = 12,
                Width = 420,
                Height = 90,
                Text =
                    "ResetBase는 되돌릴 수 없습니다.\n" +
                    "실행하면 기존 컴포넌트 버전으로 되돌릴 수 없게 됩니다.\n\n" +
                    "아래 버튼이 활성화될 때까지 기다린 후 진행하세요."
            };

            var btnOk = new Button
            {
                Text = $"확인 ({seconds})",
                Left = 230,
                Top = 120,
                Width = 100,
                Height = 32,
                Enabled = false,
                DialogResult = DialogResult.OK
            };

            var btnCancel = new Button
            {
                Text = "취소",
                Left = 340,
                Top = 120,
                Width = 80,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            dialog.Controls.Add(lbl);
            dialog.Controls.Add(btnOk);
            dialog.Controls.Add(btnCancel);

            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (_, __) =>
            {
                seconds--;
                if (seconds <= 0)
                {
                    timer.Stop();
                    btnOk.Text = "확인";
                    btnOk.Enabled = true;
                }
                else
                {
                    btnOk.Text = $"확인 ({seconds})";
                }
            };

            dialog.Shown += (_, __) => timer.Start();

            return dialog.ShowDialog(this) == DialogResult.OK;
        }

        // =====================================================
        // 분석
        // =====================================================
        private async Task AnalyzeAsync()
        {
            if (_isBusy) return;

            _lastReclaimMB = 0;
            _lastReclaimText = null;
            SetReclaim("계산 중...");

            Log("▶ WinSxS 분석 시작...");
            SetBusy(true, "분석");

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var lines = new List<string>();

            try
            {
                int exitCode = await RunDismAsync(
                    "/Online /Cleanup-Image /AnalyzeComponentStore",
                    (line, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        lines.Add(line);
                        UpdateProgressFromLine(line);
                        Log(line);
                    },
                    token
                );

                // 예상 절감 용량(환경에 따라 reclaimable로 치는 항목이 다를 수 있음)
                string? reclaimText = ParseReclaimableFromLines(lines);
                if (!string.IsNullOrWhiteSpace(reclaimText))
                {
                    double mb = ConvertSizeToMB(reclaimText);
                    _lastReclaimMB = mb;
                    _lastReclaimText = reclaimText;
                    SetReclaim(FormatMB(mb));
                    Log($"✔ 분석 완료: 예상 절감 용량 {FormatMB(mb)}");
                }
                else
                {
                    SetReclaim("(파싱 실패)");
                    Log("⚠ 분석은 완료됐지만, 예상 절감 용량 정보를 찾지 못했습니다.");
                }

                // 실제 크기(정리 전/후 비교용)
                string? actualText = ParseActualStoreSizeFromLines(lines);
                if (!string.IsNullOrWhiteSpace(actualText))
                {
                    _lastActualStoreMB = ConvertSizeToMB(actualText);
                    Log($"ℹ 구성 요소 저장소 실제 크기: {FormatMB(_lastActualStoreMB)}");
                }

                Log($"(ExitCode: {exitCode})");

                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 100));
                else progress.Value = 100;

                SetStatus("완료");
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업이 취소되었습니다.");
                SetStatus("취소됨");
                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 0));
                else progress.Value = 0;
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                MessageBox.Show(ex.ToString(), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("오류");
                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 0));
                else progress.Value = 0;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                SetBusy(false, "대기");
            }
        }

        // =====================================================
        // 정리 / ResetBase
        // =====================================================
        private async Task CleanupAsync(bool resetBase)
        {
            if (_isBusy) return;

            if (resetBase)
            {
                if (!ConfirmResetBaseWithCountdown(5))
                    return;
            }

            double beforeMB = _lastActualStoreMB;

            // 시작 시 라벨 초기화
            SetActualSaved(0, 0, 0);

            Log(resetBase ? "▶ ResetBase 시작..." : "▶ 정리 시작...");
            SetBusy(true, "정리");

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                string args = resetBase
                    ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase"
                    : "/Online /Cleanup-Image /StartComponentCleanup";

                int exitCode = await RunDismAsync(
                    args,
                    (line, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        UpdateProgressFromLine(line);
                        Log(line);
                    },
                    token
                );

                Log($"✔ 정리 완료 (ExitCode: {exitCode})");

                if (chkReAnalyze.Checked)
                {
                    SetStatus("정리 후 재분석 중...");
                    Log("▶ 정리 후 재분석 시작... (실제 절감량 계산)");

                    var analyzeLines = new List<string>();
                    int analyzeExit = await RunDismAsync(
                        "/Online /Cleanup-Image /AnalyzeComponentStore",
                        (line, _) =>
                        {
                            if (string.IsNullOrWhiteSpace(line)) return;
                            analyzeLines.Add(line);
                            UpdateProgressFromLine(line);
                            Log(line);
                        },
                        token
                    );

                    string? afterActualText = ParseActualStoreSizeFromLines(analyzeLines);
                    double afterMB = 0.0;

                    if (!string.IsNullOrWhiteSpace(afterActualText))
                    {
                        afterMB = ConvertSizeToMB(afterActualText);
                        _lastActualStoreMB = afterMB;
                        Log($"ℹ 구성 요소 저장소 실제 크기(정리 후): {FormatMB(afterMB)}");
                    }

                    if (beforeMB > 0 && afterMB > 0)
                    {
                        double savedMB = Math.Max(0, beforeMB - afterMB);
                        Log($"✅ 실제 절감량: {FormatMB(savedMB)} (정리 전 {FormatMB(beforeMB)} → 정리 후 {FormatMB(afterMB)})");
                        SetActualSaved(savedMB, beforeMB, afterMB);
                    }
                    else
                    {
                        Log("⚠ 실제 절감량 계산 실패: 정리 전/후 '실제 크기' 정보가 충분하지 않습니다. (먼저 분석을 한 번 실행해 주세요)");
                        SetActualSaved(0, 0, 0);
                    }

                    Log($"(Re-Analyze ExitCode: {analyzeExit})");
                }
                else
                {
                    Log("ℹ 정리 후 재분석이 비활성화되어 실제 절감량 계산을 건너뜁니다.");
                    SetActualSaved(0, 0, 0);
                }

                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 100));
                else progress.Value = 100;

                SetStatus("완료");
            }
            catch (OperationCanceledException)
            {
                Log("⛔ 작업 취소됨");
                SetStatus("취소됨");
                SetActualSaved(0, 0, 0);

                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 0));
                else progress.Value = 0;
            }
            catch (Exception ex)
            {
                Log("❌ 오류: " + ex);
                SetStatus("오류");
                SetActualSaved(0, 0, 0);

                if (InvokeRequired) BeginInvoke(new Action(() => progress.Value = 0));
                else progress.Value = 0;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                SetBusy(false, "대기");
            }
        }

        // =====================================================
        // DISM 실행 (한국어 출력 OEM 인코딩)
        // =====================================================
        private static Task<int> RunDismAsync(string arguments, Action<string, bool> onLine, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            var oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = oem,
                StandardErrorEncoding = oem
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            DataReceivedEventHandler outHandler = (_, e) =>
            {
                try { if (e.Data != null) onLine(e.Data, false); } catch { }
            };

            DataReceivedEventHandler errHandler = (_, e) =>
            {
                try { if (e.Data != null) onLine(e.Data, true); } catch { }
            };

            proc.OutputDataReceived += outHandler;
            proc.ErrorDataReceived += errHandler;

            proc.Exited += (_, __) =>
            {
                try { tcs.TrySetResult(proc.ExitCode); }
                finally
                {
                    proc.OutputDataReceived -= outHandler;
                    proc.ErrorDataReceived -= errHandler;
                    proc.Dispose();
                }
            };

            if (!proc.Start())
                throw new InvalidOperationException("dism.exe를 시작하지 못했습니다.");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // 취소 처리
            token.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch { }
            });

            return tcs.Task;
        }

        // =====================================================
        // 파싱
        // =====================================================
        private static string? ParseReclaimableFromLines(IEnumerable<string> lines)
        {
            // 네 출력 환경에서 "백업 및 기능 사용 안 함"이 사실상 reclaimable로 쓰였음
            string[] patterns =
            {
                @"백업\s*및\s*기능\s*사용\s*안\s*함\s*:\s*(.+)$",
                @"정리\s*가능.*?:\s*(.+)$",
                @"회수\s*가능.*?:\s*(.+)$",
                @"Reclaimable\s*(Packages)?\s*:\s*(.+)$",
                @"Backups?\s*and\s*Disabled\s*Features\s*:\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pat in patterns)
                {
                    var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var value = m.Groups[m.Groups.Count - 1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
            return null;
        }

        private static string? ParseActualStoreSizeFromLines(IEnumerable<string> lines)
        {
            string[] patterns =
            {
                @"구성\s*요소\s*저장소의\s*실제\s*크기\s*:\s*(.+)$",
                @"구성\s*요소\s*저장소\s*실제\s*크기\s*:\s*(.+)$",
                @"Component\s*Store\s*Actual\s*Size\s*:\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pat in patterns)
                {
                    var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var value = m.Groups[m.Groups.Count - 1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
            return null;
        }

        // =====================================================
        // 크기 변환
        // =====================================================
        private static double ConvertSizeToMB(string text)
        {
            var s = text.Replace(",", "").Trim();

            var m = Regex.Match(s, @"([0-9]+(\.[0-9]+)?)\s*(KB|MB|GB|TB|B|bytes?)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;

            double value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string unit = m.Groups[3].Value.ToUpperInvariant();

            return unit switch
            {
                "B" or "BYTE" or "BYTES" => value / 1024.0 / 1024.0,
                "KB" => value / 1024.0,
                "MB" => value,
                "GB" => value * 1024.0,
                "TB" => value * 1024.0 * 1024.0,
                _ => 0
            };
        }

        private static string FormatMB(double mb)
            => mb >= 1024.0 ? $"{mb / 1024.0:0.00} GB" : $"{mb:0.0} MB";

        // =====================================================
        // 권한 체크
        // =====================================================
        private static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
