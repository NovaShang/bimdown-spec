using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace BimDown.RevitAddin;

public class BimDownApp : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        var tabName = L.TabName;
        application.CreateRibbonTab(tabName);

        var panel = application.CreateRibbonPanel(tabName, L.PanelName);
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        var exportButton = new PushButtonData(
            "BimDownExport", L.ExportButton, assemblyPath,
            typeof(ExportCommand).FullName)
        {
            ToolTip = L.ExportTooltip,
            LargeImage = CreateIcon("E", 32),
            Image = CreateIcon("E", 16),
        };

        var importButton = new PushButtonData(
            "BimDownImport", L.ImportButton, assemblyPath,
            typeof(ImportCommand).FullName)
        {
            ToolTip = L.ImportTooltip,
            LargeImage = CreateIcon("I", 32),
            Image = CreateIcon("I", 16),
        };

        panel.AddItem(exportButton);
        panel.AddItem(importButton);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    static BitmapSource CreateIcon(string letter, int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using var bgBrush = new SolidBrush(Color.FromArgb(0, 120, 212));
            var r = size / 8;
            var path = RoundedRect(new Rectangle(0, 0, size, size), r);
            g.FillPath(bgBrush, path);

            using var font = new Font("Segoe UI", size * 0.45f, FontStyle.Bold);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(letter, font, Brushes.White, new RectangleF(0, 0, size, size), sf);
        }

        var hBitmap = bmp.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
}
