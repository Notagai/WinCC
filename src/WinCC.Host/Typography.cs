using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace WinCC.Host;

internal static class Typography
{
    private sealed record OriginalSize(double Value);
    private static readonly ConditionalWeakTable<DependencyObject, OriginalSize> Originals = new();
    public static void Apply(DependencyObject root, double scale)
    {
        var property = root is TextBlock ? TextBlock.FontSizeProperty :
            root is Control ? Control.FontSizeProperty : null;
        if (property is not null && root.ReadLocalValue(property) is double value)
        {
            var original = Originals.GetValue(root, _ => new OriginalSize(value));
            root.SetValue(property, original.Value * scale);
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Apply(child, scale);
    }
}
