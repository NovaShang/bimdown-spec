namespace BimDown.RevitAddin;

sealed class ShareProgressForm : BaseForm
{
    readonly Label _statusLabel;

    public ShareProgressForm()
    {
        Text = L.ShareProgressTitle;
        Size = new Size(360, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        _statusLabel = new Label
        {
            Text = L.ShareExporting,
            Font = new Font("Segoe UI", 11),
            Location = new Point(20, 35),
            Size = new Size(310, 50),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Controls.Add(_statusLabel);
    }

    public void SetStatus(string text)
    {
        if (InvokeRequired)
            Invoke(() => _statusLabel.Text = text);
        else
            _statusLabel.Text = text;
    }
}
