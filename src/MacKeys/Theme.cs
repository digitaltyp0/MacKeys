using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MacKeys;

static class Theme
{
    public static readonly Color Text = Color.FromArgb(28, 28, 32);
    public static readonly Color Muted = Color.FromArgb(98, 98, 108);
    public static readonly Color Faint = Color.FromArgb(158, 158, 168);
    public static readonly Color Border = Color.FromArgb(226, 226, 232);
    public static readonly Color HeaderBack = Color.FromArgb(246, 246, 248);
    public static readonly Color Selection = Color.FromArgb(222, 233, 252);
    public static readonly Color Error = Color.FromArgb(190, 40, 30);

    public static Font Semibold(float size) => new("Segoe UI Semibold", size);

    /// <summary>A ⌘ badge used for the tray and window icons; gray when remapping is paused.</summary>
    public static Icon CreateIcon(bool active)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var path = RoundedRect(new RectangleF(1, 1, size - 2, size - 2), 8);
            using var fill = new SolidBrush(active ? Color.FromArgb(30, 30, 36) : Color.FromArgb(140, 140, 148));
            g.FillPath(fill, path);
            using var font = new Font("Segoe UI Symbol", 21, FontStyle.Regular, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("⌘", font, Brushes.White, new RectangleF(0, 1, size, size), format);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
