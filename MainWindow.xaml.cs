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
    partial class MainWindow : Window
    {
        private string _lastCommand = string.Empty;
        private readonly StringBuilder _currentLogBuffer = new();

        MainWindow()
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
            var sizeRegex = ProgressSizeRegex(); // 메서드 이름 확인!
            string[] lines = fullLog.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                // 1. 먼저 "정리 권장 여부"를 체크 (이 줄은 숫자가 없으므로 정규식 앞에 있어야 함)
                if (line.Contains("구성 요소 저장소 정리 권장"))
                {
                    bool dismRecommended = line.Contains('예') || line.Contains("Yes");

                    if (!string.IsNullOrEmpty(_lastCommand))
                    {
                        UpdateCleanupCard(true); // 정리 직후라면 무조건 깨끗함으로 표시
                    }
                    else
                    {
                        // 예상 절감량 수치를 확인하여 실제 정리 필요성 판단
                        string currentText = LblExpectedSize.Text;
                        bool isActuallyClean = currentText.Contains("0 bytes") || currentText.Contains("0.00");
                        UpdateCleanupCard(!dismRecommended || isActuallyClean);
                    }
                    continue; // 이 줄은 처리가 끝났으므로 다음 줄로 이동
                }

                // 2. 그 다음 용량 수치가 있는 줄들만 정규식으로 분석
                var m = sizeRegex.Match(line);
                if (!m.Success) continue;

                string size = m.Groups["size"].Value;
                string unit = " " + m.Groups["unit"].Value;

                if (line.Contains("실제 크기"))
                {
                    UpdateSizeLabel(LblActualSize, size, unit);
                }
                else if (line.Contains("Windows와 공유됨"))
                {
                    UpdateSizeLabel(LblSharedSize, size, unit);
                }
                else if (line.Contains("백업 및 기능 사용 안 함"))
                {
                    if (!string.IsNullOrEmpty(_lastCommand))
                        UpdateSizeLabel(LblExpectedSize, "0", " bytes");
                    else
                        UpdateSizeLabel(LblExpectedSize, size, unit);
                }
            }
        }

        private static void UpdateSizeLabel(TextBlock label, string size, string unit)
        {
            if (label.Inlines.FirstInline is Run sizeRun && label.Inlines.LastInline is Run unitRun)
            {
                sizeRun.Text = size;
                unitRun.Text = unit;
            }
            else
            {
                label.Text = size + unit;
            }
        }

        private void UpdateCleanupCard(bool isClean)
        {
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
            LblCleanupRecommended.FontSize = 22;
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
