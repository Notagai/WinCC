using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCC.Host;

/// <summary>App-drawn materials. No system backdrop APIs or stock widget surfaces.</summary>
internal sealed class LiquidGlass : Decorator
{
    public string Material { get; set; } = "center";
    public LiquidGlass() { SizeChanged += (_, _) => InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 2 || ActualHeight < 2) return;
        var rect = new Rect(1, 1, ActualWidth - 2, ActualHeight - 2);
        Color top, bottom;
        switch (Material)
        {
            case "calendar": top = Color.FromRgb(248, 246, 241); bottom = Color.FromRgb(235, 233, 228); break;
            case "notes": top = Color.FromRgb(255, 250, 230); bottom = Color.FromRgb(245, 237, 212); break;
            case "weather": top = Color.FromRgb(76, 140, 195); bottom = Color.FromRgb(41, 83, 138); break;
            case "spotify": top = Color.FromArgb(237, 38, 43, 48); bottom = Color.FromArgb(242, 22, 26, 32); break;
            case "ai-island": top = Color.FromArgb(242, 37, 39, 47); bottom = Color.FromArgb(245, 22, 24, 30); break;
            case "clock": top = Color.FromArgb(110, 32, 51, 68); bottom = Color.FromArgb(145, 22, 35, 48); break;
            default: top = Color.FromArgb(225, 40, 51, 65); bottom = Color.FromArgb(240, 25, 33, 46); break;
        }
        dc.DrawRoundedRectangle(new LinearGradientBrush(top, bottom, 90),
            new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), 1), rect, 22, 22);
        var sheen = new LinearGradientBrush(Color.FromArgb(28, 255, 255, 255), Colors.Transparent, 90);
        dc.DrawRoundedRectangle(sheen, null, rect, 22, 22);
    }
}
