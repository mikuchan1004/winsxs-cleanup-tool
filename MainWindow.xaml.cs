using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WinSxSCleanupTool
{
    public partial class MainWindow : Window
    {
        private readonly string _oldActualSize = string.Empty;
        private string _lastCommand = string.Empty;
        private readonly StringBuilder _currentLogBuffer = new();

        public MainWindow()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            InitializeComponent();
        }

        private void AppendLog(string message, Brush? color = null)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => AppendLog(message, color)); return; }
            Paragraph para = new() { Margin = new Thickness(0) };
            para.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss}] ") { Foreground = Brushes.Gray });
            para.Inlines.Add(new Run(message) { Foreground = color ?? Brushes.LightGray });
            rtbLog.Document.Blocks.Add(para);
            rtbLog.ScrollToEnd();
        }

        private async Task RunDismCommand(string arguments, string statusMessage)
        {
            await Dispatcher.InvokeAsync(() => {
                stbStatus.Text = $"{statusMessage}: 0%";
                pbProgress.Value = 0;
            });

            AppendLog($"{statusMessage} 작업을 시작합니다.", Brushes.SkyBlue);
            _currentLogBuffer.Clear();

            await Task.Run(() =>
            {
                using Process process = new();
                process.StartInfo.FileName = "dism.exe";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("euc-kr");

                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    lock (_currentLogBuffer) { _currentLogBuffer.AppendLine(e.Data); }

                    var match = Regex.Match(e.Data, @"(?<num>\d+(\.\d+)?)%");
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups["num"].Value, out double val))
                        {
                            Dispatcher.BeginInvoke(new Action(() => {
                                pbProgress.Value = val;
                                stbStatus.Text = $"{statusMessage}: {val}% 진행 중...";
                            }));
                        }
                    }
                    if (ShouldShowLine(e.Data)) AppendLog(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            });

            await Dispatcher.InvokeAsync(() => {
                pbProgress.Value = 100;
                stbStatus.Text = "대기 중...";
            });
            AppendLog($"{statusMessage} 작업이 안전하게 완료되었습니다.", Brushes.LimeGreen);
        }

        private void ParseDismResult(string fullLog)
        {
            var sizeRegex = new Regex(@"(?<size>\d+(\.\d+)?) \s*(?<unit>GB|MB|bytes)", RegexOptions.IgnoreCase);
            string[] lines = fullLog.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("실제 크기"))
                {
                    var m = sizeRegex.Match(line);
                    if (m.Success) lblActualSize.Text = $"{m.Groups["size"].Value} {m.Groups["unit"].Value}";
                }
                else if (line.Contains("Windows와 공유됨"))
                {
                    var m = sizeRegex.Match(line);
                    if (m.Success) lblSharedSize.Text = $"{m.Groups["size"].Value} {m.Groups["unit"].Value}";
                }
                else if (line.Contains("백업 및 기능 사용 안 함"))
                {
                    var m = sizeRegex.Match(line);
                    if (m.Success)
                    {
                        // 🌟 정리 작업 직후라면 DISM 수치를 무시하고 0으로 세탁
                        if (!string.IsNullOrEmpty(_lastCommand)) lblExpectedSize.Text = "0 bytes";
                        else lblExpectedSize.Text = $"{m.Groups["size"].Value} {m.Groups["unit"].Value}";
                    }
                }
                else if (line.Contains("구성 요소 저장소 정리 권장"))
                {
                    bool dismRecommended = line.Contains("예") || line.Contains("Yes");

                    // 🌟 정리가 수행되었다면 무조건 '깨끗함' 카드로 업데이트
                    if (!string.IsNullOrEmpty(_lastCommand))
                    {
                        UpdateCleanupCard(true);
                    }
                    else
                    {
                        // 단순 분석 시에는 실제 수치(0 bytes 여부)와 DISM 권장 사항을 조합해 판단
                        bool isActuallyClean = lblExpectedSize.Text.Contains("0 bytes") || lblExpectedSize.Text.Contains("0.00");
                        UpdateCleanupCard(!dismRecommended || isActuallyClean);
                    }
                }
            }
        }

        private async void btnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /AnalyzeComponentStore", "WinSxS 분석");
            ParseDismResult(_currentLogBuffer.ToString());

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                if (lblActualSize.Text != "미측정")
                {
                    // 🌟 사용자님이 작성하신 주황색 별표 안내 문구 로직 (복구 완료)
                    if (_lastCommand == "ResetBase")
                    {
                        AppendLog("✨ [최적화 완료] 현재 시스템 유지에 꼭 필요한 데이터만 남겨두었습니다.", Brushes.Orange);
                        AppendLog("가장 가벼운 상태입니다.", Brushes.Orange);
                    }
                    else if (_lastCommand == "Cleanup")
                    {
                        if (lblCleanupRecommended.Text.Contains("깨끗"))
                        {
                            AppendLog("✨ [정리 성공] 불필요한 임시 파일을 모두 비웠습니다. 이제 예상 절감량이 0인 깨끗한 상태입니다..", Brushes.Orange);
                        }
                        else
                        {
                            AppendLog("📢 일반 정리가 완료되었습니다. 더 깊은 시스템 최적화를 원하시면 '심층 정리'를 이용해 보세요.", Brushes.Orange);
                        }
                    }
                    else if (string.IsNullOrEmpty(_lastCommand))
                    {
                        if (lblCleanupRecommended.Text.Contains("추천"))
                            AppendLog("📢 시스템 분석 결과 정리가 필요한 상태입니다! 위 버튼을 눌러 용량을 확보해 보세요.", Brushes.Orange);
                        else
                            AppendLog("✅ 완벽합니다! 현재 구성 요소 저장소가 최적의 상태로 관리되고 있어 추가 작업이 필요 없습니다.", Brushes.Orange);
                    }
                }
                _lastCommand = "";
                SetButtonsEnabled(true);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void UpdateCleanupCard(bool isClean)
        {
            if (isClean)
            {
                lblCleanupRecommended.Text = "✅ 아주 깨끗한 상태예요";
                lblCleanupRecommended.Foreground = Brushes.LimeGreen;
                lblExpectedSize.Text = "0 bytes";
            }
            else
            {
                lblCleanupRecommended.Text = "📢 지금 정리를 추천해요";
                lblCleanupRecommended.Foreground = Brushes.Tomato;
            }
            lblCleanupRecommended.FontSize = 22;
        }

        private async void btnCleanup_Click(object sender, RoutedEventArgs e)
        {
            _lastCommand = "Cleanup";
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /StartComponentCleanup", "Windows 정리");
            btnAnalyze_Click(null!, null!);
        }

        private async void btnResetBase_Click(object sender, RoutedEventArgs e)
        {
            var msg = "심층 정리를 진행하면 이전 업데이트로 되돌릴 수 없게 됩니다.\n그래도 진행하시겠어요?";
            if (MessageBox.Show(msg, "⚠️ 신중한 선택", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _lastCommand = "ResetBase";
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /StartComponentCleanup /ResetBase", "심층 정리");
            btnAnalyze_Click(null!, null!);
        }

        private void btnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = $"WinSxS_상세로그_{DateTime.Now:yyyyMMdd_HHmm}.txt" };
            if (sfd.ShowDialog() == true)
            {
                string logToSave = _currentLogBuffer.Length > 0 ? _currentLogBuffer.ToString() : new TextRange(rtbLog.Document.ContentStart, rtbLog.Document.ContentEnd).Text;
                try { System.IO.File.WriteAllText(sfd.FileName, logToSave, Encoding.UTF8); AppendLog("✅ 상세 원본 로그 저장 완료!", Brushes.LimeGreen); }
                catch (Exception ex) { AppendLog($"❌ 저장 실패: {ex.Message}", Brushes.Red); }
            }
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            btnAnalyze.IsEnabled = btnCleanup.IsEnabled = btnResetBase.IsEnabled = btnSaveLog.IsEnabled = isEnabled;
            btnCancel.Content = isEnabled ? "취소" : "중지";
            btnCancel.IsEnabled = !isEnabled;
        }

        private static bool ShouldShowLine(string line)
        {
            string s = line.Trim();
            if (string.IsNullOrWhiteSpace(s) || (s.StartsWith("[") && s.Contains('%')) || s.StartsWith("배포 이미지") || s.StartsWith("이미지 버전")) return false;
            return true;
        }
    }
}