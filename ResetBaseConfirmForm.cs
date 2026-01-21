#nullable enable

using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinSxSCleanupTool
{
    public sealed class ResetBaseConfirmForm : Form
    {
        private readonly Label _lblTitle;
        private readonly Label _lblDesc;
        private readonly CheckBox _chk;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        private readonly System.Windows.Forms.Timer _timer;
        private int _secondsLeft;

        public ResetBaseConfirmForm() : this(versionText: null) { }

        public ResetBaseConfirmForm(string? versionText)
        {
            Text = UiText.ResetBaseFinalTitle;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            _lblTitle = new Label
            {
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                MaximumSize = new Size(520, 0),
                Text = BuildTitle(versionText),
            };

            _lblDesc = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Text = UiText.ResetBaseFinalMessage,
            };

            _chk = new CheckBox
            {
                AutoSize = true,
                Text = UiText.ResetBaseConfirmCheck,
            };

            _btnOk = new Button
            {
                Text = UiText.ResetBaseExecuteButtonText,
                DialogResult = DialogResult.OK,
                Enabled = false,
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
            };

            _btnCancel = new Button
            {
                Text = UiText.CancelButtonText,
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
            Shown += (_, __) => _btnCancel.Focus(); // 안전: 취소에 포커스

            var buttonRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0),
            };
            buttonRow.Controls.Add(_btnCancel);
            buttonRow.Controls.Add(_btnOk);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Fill,
            };
            layout.Controls.Add(_lblTitle, 0, 0);
            layout.Controls.Add(_lblDesc, 0, 1);
            layout.Controls.Add(_chk, 0, 2);
            layout.Controls.Add(buttonRow, 0, 3);

            Controls.Add(layout);

            _secondsLeft = 3;
            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (_, __) =>
            {
                _secondsLeft--;
                UpdateOkState();

                if (_secondsLeft <= 0)
                {
                    _timer.Stop();
                    UpdateOkState();
                }
            };

            _chk.CheckedChanged += (_, __) =>
            {
                if (_chk.Checked)
                {
                    _secondsLeft = 3;
                    _timer.Stop();
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
                UpdateOkState();
            };

            FormClosed += (_, __) =>
            {
                _timer.Stop();
                _timer.Dispose();
            };

            UpdateOkState();
        }

        private static string BuildTitle(string? versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return "ResetBase는 되돌릴 수 없습니다.";
            return $"ResetBase는 되돌릴 수 없습니다.\r\n(버전: {versionText})";
        }

        private void UpdateOkState()
        {
            if (!_chk.Checked)
            {
                _btnOk.Enabled = false;
                _btnOk.Text = UiText.ResetBaseExecuteButtonText;
                return;
            }

            if (_timer.Enabled && _secondsLeft > 0)
            {
                _btnOk.Enabled = false;
                _btnOk.Text = $"{UiText.ResetBaseExecuteButtonText} ({_secondsLeft})";
                return;
            }

            _btnOk.Enabled = true;
            _btnOk.Text = UiText.ResetBaseExecuteButtonText;
        }
    }
}
