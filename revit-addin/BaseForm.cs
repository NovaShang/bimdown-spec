namespace BimDown.RevitAddin;

class BaseForm : Form
{
    protected BaseForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Force GDI+ text rendering (UseCompatibleTextRendering = false) on all child controls
        SetTextRendering(this);
    }

    static void SetTextRendering(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is ButtonBase btn)
                btn.UseCompatibleTextRendering = false;
            else if (c is Label lbl)
                lbl.UseCompatibleTextRendering = false;
            else if (c is LinkLabel ll)
                ll.UseCompatibleTextRendering = false;
            SetTextRendering(c);
        }
    }
}
