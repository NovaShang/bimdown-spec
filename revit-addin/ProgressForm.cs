namespace BimDown.RevitAddin;

sealed class ProgressForm : BaseForm
{
    readonly Label _stepLabel;
    readonly ProgressBar _progressBar;
    readonly Label _detailLabel;

    public ProgressForm(string title)
    {
        Text = title;
        Size = new Size(420, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        _stepLabel = new Label
        {
            Text = L.ProgressInitializing,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 15),
            Size = new Size(370, 25),
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 45),
            Size = new Size(370, 25),
            Style = ProgressBarStyle.Continuous,
        };

        _detailLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray,
            Location = new Point(20, 78),
            Size = new Size(370, 20),
        };

        Controls.AddRange([_stepLabel, _progressBar, _detailLabel]);
    }

    public void SetProgress(string step, int current, int total, string? detail = null)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetProgress(step, current, total, detail));
            return;
        }

        _stepLabel.Text = step;
        _progressBar.Maximum = total;
        _progressBar.Value = Math.Min(current, total);
        _detailLabel.Text = detail ?? $"{current} / {total}";
        Application.DoEvents();
    }
}
