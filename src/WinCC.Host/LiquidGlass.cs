using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinCC.Host;

/// <summary>App-owned vector glass material. No DWM, Acrylic, Mica or backdrop APIs.</summary>
internal sealed class LiquidGlass : Decorator
{
    private Point light = new(.28, .12);
    public LiquidGlass()
    {
        MouseMove += (_, e) =>
        {
            var p = e.GetPosition(this);
            light = new(p.X / Math.Max(1, ActualWidth), p.Y / Math.Max(1, ActualHeight));
            InvalidateVisual();
        };
        MouseLeave += (_, _) => { light = new(.28, .12); InvalidateVisual(); };
        SizeChanged += (_, _) => InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 2 || ActualHeight < 2) return;
        var rect = new Rect(1, 1, ActualWidth - 2, ActualHeight - 2);
        const double radius = 28;
        var shape = new RectangleGeometry(rect, radius, radius);
        dc.PushClip(shape);
        var body = new LinearGradientBrush();
        body.StartPoint = new(0, 0); body.EndPoint = new(1, 1);
        body.GradientStops.Add(new(Color.FromArgb(238, 55, 67, 88), 0));
        body.GradientStops.Add(new(Color.FromArgb(226, 23, 31, 48), .48));
        body.GradientStops.Add(new(Color.FromArgb(240, 39, 43, 63), 1));
        dc.DrawRectangle(body, null, rect);
        var glow = new RadialGradientBrush(Color.FromArgb(75, 224, 243, 255), Colors.Transparent)
        { Center = light, GradientOrigin = light, RadiusX = .8, RadiusY = .9 };
        dc.DrawRectangle(glow, null, rect);
        var lower = new RadialGradientBrush(Color.FromArgb(32, 159, 132, 255), Colors.Transparent)
        { Center = new(.9, 1.1), GradientOrigin = new(.9, 1.1), RadiusX = .9, RadiusY = .7 };
        dc.DrawRectangle(lower, null, rect);
        dc.Pop();
        var rim = new LinearGradientBrush();
        rim.StartPoint = new(0, 0); rim.EndPoint = new(.8, 1);
        rim.GradientStops.Add(new(Color.FromArgb(205, 243, 252, 255), 0));
        rim.GradientStops.Add(new(Color.FromArgb(35, 220, 235, 255), .35));
        rim.GradientStops.Add(new(Color.FromArgb(15, 220, 235, 255), .65));
        rim.GradientStops.Add(new(Color.FromArgb(110, 225, 222, 255), 1));
        dc.DrawRoundedRectangle(null, new Pen(rim, 1.25), rect, radius, radius);
        var inner = new Rect(3, 3, Math.Max(0, ActualWidth - 6), Math.Max(0, ActualHeight - 6));
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 1), inner, 26, 26);
    }
}
