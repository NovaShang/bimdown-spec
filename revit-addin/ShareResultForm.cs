using System.Diagnostics;

namespace BimDown.RevitAddin;

sealed class ShareResultForm : BaseForm
{
    public ShareResultForm(string url, string expiresAt)
    {
        Text = L.ShareResultTitle;
        Size = new Size(480, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var y = 20;

        var msgLabel = new Label
        {
            Text = L.ShareSuccess(url, expiresAt),
            Font = new Font("Segoe UI", 9),
            Location = new Point(20, y),
            Size = new Size(430, 90),
        };
        y += 100;

        var copyBtn = new Button
        {
            Text = L.ShareCopyLink,
            Font = new Font("Segoe UI", 11),
            Size = new Size(140, 40),
            Location = new Point(80, y),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        copyBtn.FlatAppearance.BorderSize = 0;
        copyBtn.Click += (_, _) => Clipboard.SetText(url);

        var openBtn = new Button
        {
            Text = L.ShareOpenBrowser,
            Font = new Font("Segoe UI", 11),
            Size = new Size(140, 40),
            Location = new Point(230, y),
            FlatStyle = FlatStyle.Flat,
        };
        openBtn.Click += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        var closeBtn = new Button
        {
            Text = L.ShareClose,
            Font = new Font("Segoe UI", 9),
            Size = new Size(80, 32),
            Location = new Point(380, y + 4),
            FlatStyle = FlatStyle.Flat,
        };
        closeBtn.Click += (_, _) => Close();

        Controls.AddRange([msgLabel, copyBtn, openBtn, closeBtn]);
    }
}
