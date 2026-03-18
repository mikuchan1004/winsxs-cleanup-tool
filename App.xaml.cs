using System;
using System.Security.Principal;
using System.Windows;

namespace WinSxSCleanupTool
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. 관리자 권한 체크
            if (!IsAdministrator())
            {
                // WinForms에서 쓰던 UiText 메시지를 그대로 활용하거나 직접 입력합니다.
                MessageBox.Show("이 프로그램은 시스템 파일을 수정하기 위해 '관리자 권한'이 반드시 필요합니다.\n\n관리자 권한으로 다시 실행해 주세요.",
                                "권한 오류", MessageBoxButton.OK, MessageBoxImage.Stop);

                // 프로그램 즉시 종료
                Shutdown();
                return;
            }

            // 2. 권한이 확인되면 정상적으로 시작
            base.OnStartup(e);
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}