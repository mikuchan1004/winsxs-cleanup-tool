#nullable enable

using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace WinSxSCleanupTool
{
    public sealed class AboutForm : Form
    {
        public AboutForm(Form owner)
        {
            // =========================
            // Form basic settings
            // =========================
            Text = $"About {Application.ProductName}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            MinimumSize = new Size(420, 220);
            ClientSize = new Size(460, 240);

            // =========================
            // Icon (single-file safe + CS8600 safe)
            // =========================
            Icon? resolvedIcon = null;

            // 1) owner 아이콘(있으면 최우선)
            try { resolvedIcon = owner.Icon; } catch { /* ignore */ }

            // 2) EXE에서 아이콘 추출 (null 가능)
            if (resolvedIcon is null)
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrWhiteSpace(exePath))
                        resolvedIcon = Icon.ExtractAssociatedIcon(exePath); // Icon? 반환
                }
                catch { /* ignore */ }
            }

            // 3) 최종 fallback (절대 null 아님)
            Icon = resolvedIcon ?? SystemIcons.Application;

            // PictureBox에도 "확정된 Icon" 사용 (null 역참조 방지)
            var iconBitmap = Icon.ToBitmap();

            // =========================
            // App info
            // =========================
            string appName = Application.ProductName ?? "Application";
            string productVersion = GetInformationalVersion();
            string fileVersion = GetFileVersion();

            // =========================
            // Controls
            // =========================
            var finalIcon = resolvedIcon ?? SystemIcons.Application;

            var iconBox = new PictureBox
            {
                Image = finalIcon.ToBitmap(),
                Size = new Size(48, 48),
                Location = new Point(16, 16),
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            var title = new Label
            {
                Text = appName,
                Font = new Font(Font.FontFamily, 15, FontStyle.Bold),
                Location = new Point(80, 16),
                Size = new Size(360, 30)
            };

            var ver1 = new Label
            {
                Text = $"Product Version: {productVersion}",
                Location = new Point(80, 52),
                Size = new Size(360, 18)
            };

            var ver2 = new Label
            {
                Text = $"File Version: {fileVersion}",
                Location = new Point(80, 72),
                Size = new Size(360, 18)
            };

            var desc = new Label
            {
                Text =
                    "WinSxS(Component Store) 정리 도구\r\n" +
                    "DISM 기반 Windows 공식 명령만 사용합니다.",
                Location = new Point(16, 110),
                Size = new Size(420, 36)
            };

            var network = new Label
            {
                Text = "※ 본 프로그램은 네트워크 통신을 전혀 사용하지 않습니다.",
                Location = new Point(16, 155),
                Size = new Size(420, 18),
                ForeColor = SystemColors.GrayText,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Regular)
            };

            var linkGitHub = new LinkLabel
            {
                Text = "GitHub Repository",
                Location = new Point(16, 180),
                AutoSize = true
            };
            linkGitHub.LinkClicked += (_, __) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(
                        "https://github.com/mikuchan1004/winsxs-cleanup-tool")
                    { UseShellExecute = true });
                }
                catch { }
            };

            var admin = new Label
            {
                Text = IsAdministrator() ? "권한: 관리자" : "권한: 일반 사용자",
                Location = new Point(16, 205),
                Size = new Size(420, 18)
            };

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 26),
                Location = new Point(ClientSize.Width - 96, ClientSize.Height - 40),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            AcceptButton = ok;
            CancelButton = ok;

            // =========================
            // Add controls (Z-order safe)
            // =========================
            Controls.AddRange(new Control[]
            {
                iconBox,
                title,
                ver1,
                ver2,
                desc,
                network,
                linkGitHub,
                admin,
                ok
            });
        }

        // =========================
        // Utils
        // =========================
        private static string GetFileVersion()
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe))
                    return "unknown";

                var info = FileVersionInfo.GetVersionInfo(exe);
                return info.FileVersion ?? "unknown";
            }
            catch
            {
                return "unknown";
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
                    int plus = v.IndexOf('+');
                    return plus >= 0 ? v[..plus] : v;
                }
            }
            catch { }

            return "unknown";
        }

        private static bool IsAdministrator()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
