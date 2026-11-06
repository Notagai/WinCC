using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCC.Host;

/// <summary>Measures content in design units and scales the complete composition to its actual bounds.
/// Extra space on the longer axis is available for layout rather than stretching glyphs.</summary>
internal sealed class WidgetViewport : Decorator
{
    private Size reference;
    public WidgetViewport(double width, double height)
    {
        reference = new(width, height);
        ClipToBounds = true;
    }
    public void SetReference(double width, double height)
    {
        reference = new(width, height);
        InvalidateMeasure();
    }
    private (Size Logical, double Scale) Resolve(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : reference.Width;
        double height = double.IsFinite(available.Height) ? available.Height : reference.Height;
        double scale = Math.Max(.001, Math.Min(width / reference.Width, height / reference.Height));
        return (new Size(width / scale, height / scale), scale);
    }
    protected override Size MeasureOverride(Size constraint)
    {
        var sizing = Resolve(constraint);
        Child?.Measure(sizing.Logical);
        return new Size(sizing.Logical.Width * sizing.Scale, sizing.Logical.Height * sizing.Scale);
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var sizing = Resolve(arrangeSize);
        if (Child is not null)
        {
            Child.RenderTransform = new ScaleTransform(sizing.Scale, sizing.Scale);
            Child.Arrange(new Rect(sizing.Logical));
        }
        return arrangeSize;
    }
}
