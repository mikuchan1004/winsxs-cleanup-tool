using System;
using System.Drawing;
using System.Windows.Forms;

public class ResetBaseConfirmForm : Form
{
    private CheckBox chk;
    private Button btnOk;
    private Button btnCancel;

    public ResetBaseConfirmForm(string versionText)
    {
        Text = "ResetBase 확인";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Text = $"ResetBase는 되돌릴 수 없습니다.\r\n(버전: {versionText})",
            MaximumSize = new Size(460, 0),
        };

        var desc = new Label
        {
            AutoSize = true,
            Text =
                "• 정리 후, 업데이트/패치 되돌리기가 어려워질 수 있습니다.\r\n" +
                "• 문제가 생겨도 복구가 힘들 수 있습니다.\r\n\r\n" +
                "그래도 실행하려면 아래를 체크하고 진행하세요.",
            MaximumSize = new Size(460, 0),
        };

        chk = new CheckBox
        {
            AutoSize = true,
            Text = "위 내용을 이해했으며, ResetBase를 실행하겠습니다.",
        };

        btnOk = new Button
        {
            Text = "ResetBase 실행",
            DialogResult = DialogResult.OK,
            Enabled = false,
            BackColor = Color.FromArgb(180, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
        };

        btnCancel = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
        };

        chk.CheckedChanged += (_, __) => btnOk.Enabled = chk.Checked;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnOk);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
        };
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(desc, 0, 1);
        layout.Controls.Add(chk, 0, 2);
        layout.Controls.Add(buttons, 0, 3);

        Controls.Add(layout);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
