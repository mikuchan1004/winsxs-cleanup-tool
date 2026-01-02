using System;
using System.Windows.Forms;

namespace WinSxSCleanupTool
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // ApplicationConfiguration.Initialize(); ❌ 제거
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
