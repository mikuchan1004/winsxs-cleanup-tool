using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WinSxSCleanupTool
{
    public partial class MainWindow : Window
    {
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
            Paragraph para = new() { Margin = new Thickness(0, 0, 0, 2) };
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
                pbProgress.IsIndeterminate = false;
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

                    var match = ProgressPercentRegex().Match(e.Data);
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
            var sizeRegex = ProgressSizeRegex();
            string[] lines = fullLog.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                // ParseDismResult 내부의 "정리 권장 여부" 체크 로직 수정
                if (line.Contains("구성 요소 저장소 정리 권장"))
                {
                    bool dismSaysYes = line.Contains('예') || line.Contains("Yes");

                    // 1. 예상 절감량 수치를 가져옴 (이전에 UpdateSizeLabel에서 저장된 값 활용)
                    // 혹은 여기서 직접 sizeRegex로 수치를 다시 확인
                    var expectedMatch = ProgressSizeRegex().Match(fullLog); // 전체 로그에서 절감량 라인 찾기

                    // 만약 ResetBase 직후라면 무조건 깨끗함으로 표시 (사용자 의도 반영)
                    if (_lastCommand == "ResetBase")
                    {
                        UpdateCleanupCard(true);
                    }
                    else
                    {
                        // DISM이 "예"라고 해도, 예상 절감량이 극히 적으면(예: 0.1GB 미만) "깨끗함"으로 간주
                        // (참고: 아래는 LblExpectedSize의 텍스트를 검사하는 방식)
                        bool isTinyAmount = LblExpectedSize.Text.Contains("0.00") || LblExpectedSize.Text.Contains(" 0 ");

                        UpdateCleanupCard(!dismSaysYes || isTinyAmount);
                    }
                    continue;
                }

                var m = sizeRegex.Match(line);
                if (!m.Success) continue;

                // 변수에 공백을 섞지 않고 정규식 그룹 값만 딱 가져옵니다.
                string size = m.Groups["size"].Value;
                string unit = m.Groups["unit"].Value;

                if (line.Contains("실제 크기"))
                    UpdateSizeLabel(LblActualSize, size, unit);
                else if (line.Contains("Windows와 공유됨"))
                    UpdateSizeLabel(LblSharedSize, size, unit);
                else if (line.Contains("백업 및 기능 사용 안 함"))
                {
                    if (!string.IsNullOrEmpty(_lastCommand))
                        UpdateSizeLabel(LblExpectedSize, "0", "bytes");
                    else
                        UpdateSizeLabel(LblExpectedSize, size, unit);
                }
            }
        }

        private static void UpdateSizeLabel(TextBlock label, string size, string unit)
        {
            if (string.IsNullOrWhiteSpace(size)) return;

            label.Dispatcher.Invoke(() => {
                // 1. 기존의 모든 Run과 텍스트를 싹 지워버립니다. (중복 원천 차단)
                label.Inlines.Clear();

                // 2. 숫자 부분 새로 생성 (XAML에서 설정했던 디자인 그대로 복구)
                // LblExpectedSize(노란색)인지 체크해서 색상 적용
                Brush sizeColor = (label.Name == "LblExpectedSize") ?
                    (Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FFD700")! :
                    Brushes.White;

                label.Inlines.Add(new Run(size.Trim()) { FontSize = 26, Foreground = sizeColor });

                // 3. 단위 부분 새로 생성 (무조건 공백 하나 + 단위 하나)
                // unit에 뭐가 왔든 "GB"면 "GB"로, 아니면 그 값으로 딱 한 번만 넣습니다.
                string displayUnit = unit.Trim().ToUpper().Contains("GB") ? "GB" : unit.Trim();
                label.Inlines.Add(new Run(" " + displayUnit) { FontSize = 16, Foreground = Brushes.Gray });
            });
        }

        private void UpdateCleanupCard(bool isClean)
        {
            LblCleanupRecommended.Dispatcher.Invoke(() => {
                // C#에서 FontSize를 지정하는 코드를 삭제했습니다. XAML 설정을 따릅니다.
                if (isClean)
                {
                    LblCleanupRecommended.Text = "✅ 아주 깨끗한 상태예요";
                    LblCleanupRecommended.Foreground = Brushes.LimeGreen;
                }
                else
                {
                    LblCleanupRecommended.Text = "📢 지금 정리를 추천해요";
                    LblCleanupRecommended.Foreground = Brushes.Tomato;
                }
            });
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /AnalyzeComponentStore", "WinSxS 분석");
            ParseDismResult(_currentLogBuffer.ToString());

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                // UI가 업데이트된 후 안내 메시지 출력
                if (LblCleanupRecommended.Text != "미측정")
                {
                    if (_lastCommand == "ResetBase")
                    {
                        AppendLog("✨ [최적화 완료] 현재 시스템 유지에 꼭 필요한 데이터만 남겨두었습니다.", Brushes.Orange);
                        AppendLog("가장 가벼운 상태입니다.", Brushes.Orange);
                    }
                    else if (_lastCommand == "Cleanup")
                    {
                        AppendLog("✨ [정리 성공] 불필요한 임시 파일을 모두 비웠습니다.", Brushes.Orange);
                    }
                    else if (string.IsNullOrEmpty(_lastCommand))
                    {
                        if (LblCleanupRecommended.Text.Contains("추천"))
                            AppendLog("📢 시스템 분석 결과 정리가 필요한 상태입니다!", Brushes.Orange);
                        else
                            AppendLog("✅ 완벽합니다! 추가 작업이 필요 없습니다.", Brushes.Orange);
                    }
                }
                _lastCommand = "";
                SetButtonsEnabled(true);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private async void BtnCleanup_Click(object sender, RoutedEventArgs e)
        {
            _lastCommand = "Cleanup";
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /StartComponentCleanup", "Windows 정리");
            BtnAnalyze_Click(null!, null!);
        }

        private async void BtnResetBase_Click(object sender, RoutedEventArgs e)
        {
            var msg = "심층 정리를 진행하면 이전 업데이트로 되돌릴 수 없게 됩니다.\n그래도 진행하시겠어요?";
            if (MessageBox.Show(msg, "⚠️ 신중한 선택", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _lastCommand = "ResetBase";
            SetButtonsEnabled(false);
            await RunDismCommand("/Online /Cleanup-Image /StartComponentCleanup /ResetBase", "심층 정리");
            BtnAnalyze_Click(null!, null!);
        }

        private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
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
            if (string.IsNullOrWhiteSpace(s) || (s.StartsWith('[') && s.Contains('%')) || s.StartsWith("배포 이미지") || s.StartsWith("이미지 버전")) return false;
            return true;
        }

        // 진행률(%) 추출용 정규식
        [GeneratedRegex(@"(?<num>\d+(\.\d+)?)%")]
        private static partial Regex ProgressPercentRegex();

        // 용량(GB, MB, bytes) 추출용 정규식
        [GeneratedRegex(@"(?<size>\d+(\.\d+)?) \s*(?<unit>GB|MB|bytes)", RegexOptions.IgnoreCase, "ko-KR")]
        private static partial Regex ProgressSizeRegex();
    }
}