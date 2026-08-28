using System.Drawing.Drawing2D;

namespace GpuSentinel.UI;

internal sealed class MeterRow : Control
{
    private string _caption = string.Empty;
    private string _valueText = string.Empty;
    private double _percent;
    private Color _accent = Color.FromArgb(69, 196, 126);

    public MeterRow()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        Height = 31;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        ForeColor = Color.FromArgb(224, 231, 239);
        BackColor = Color.Transparent;
    }

    public void SetReading(string caption, string valueText, double percent, Color accent)
    {
        _caption = caption;
        _valueText = valueText;
        _percent = Math.Clamp(percent, 0, 100);
        _accent = accent;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var captionBrush = new SolidBrush(Color.FromArgb(156, 168, 184));
        using var valueBrush = new SolidBrush(ForeColor);
        using var trackBrush = new SolidBrush(Color.FromArgb(47, 55, 69));
        using var fillBrush = new SolidBrush(_accent);

        e.Graphics.DrawString(_caption, Font, captionBrush, 0, 0);
        var valueSize = e.Graphics.MeasureString(_valueText, Font);
        e.Graphics.DrawString(_valueText, Font, valueBrush, Width - valueSize.Width, 0);

        var bar = new RectangleF(0, 22, Width, 5);
        using var trackPath = RoundedRectangle(bar, 2.5F);
        e.Graphics.FillPath(trackBrush, trackPath);
        if (_percent > 0)
        {
            var fill = new RectangleF(bar.X, bar.Y, Math.Max(4, bar.Width * (float)(_percent / 100)), bar.Height);
            using var fillPath = RoundedRectangle(fill, 2.5F);
            e.Graphics.FillPath(fillBrush, fillPath);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
