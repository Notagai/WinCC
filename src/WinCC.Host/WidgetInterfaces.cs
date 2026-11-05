using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinCC.Host;

internal sealed class MonthView : StackPanel
{
    private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime selected = DateTime.Today;
    public MonthView() => Draw();
    private void Draw()
    {
        Children.Clear();
        var navigation = new DockPanel();
        var back = new Button { Content = "‹", Padding = new Thickness(12, 4, 12, 4), ToolTip = "Previous month" };
        var next = new Button { Content = "›", Padding = new Thickness(12, 4, 12, 4), ToolTip = "Next month" };
        back.Click += (_, _) => { month = month.AddMonths(-1); Draw(); };
        next.Click += (_, _) => { month = month.AddMonths(1); Draw(); };
        DockPanel.SetDock(back, Dock.Left); DockPanel.SetDock(next, Dock.Right);
        navigation.Children.Add(back); navigation.Children.Add(next);
        navigation.Children.Add(new TextBlock { Text = month.ToString("MMMM yyyy"), FontSize = 22, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center });
        Children.Add(navigation);
        var days = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7, Margin = new Thickness(0, 12, 0, 8) };
        var culture = CultureInfo.CurrentCulture.DateTimeFormat;
        int first = (int)culture.FirstDayOfWeek;
        for (int i = 0; i < 7; i++) days.Children.Add(new TextBlock { Text = culture.AbbreviatedDayNames[(first + i) % 7], TextAlignment = TextAlignment.Center, Foreground = Brushes.LightSteelBlue });
        int offset = ((int)month.DayOfWeek - first + 7) % 7;
        for (int i = 0; i < 42; i++)
        {
            var date = month.AddDays(i - offset);
            bool current = date.Month == month.Month;
            var button = new Button
            {
                Content = date.Day.ToString(), MinHeight = 32, Padding = new Thickness(2), Margin = new Thickness(2),
                Opacity = current ? 1 : .35,
                Background = date == selected ? new SolidColorBrush(Color.FromRgb(102, 79, 166)) : date == DateTime.Today ? new SolidColorBrush(Color.FromRgb(46, 74, 109)) : Brushes.Transparent,
                ToolTip = date.ToString("D")
            };
            button.Click += (_, _) => { selected = date; month = new(date.Year, date.Month, 1); Draw(); };
            days.Children.Add(button);
        }
        Children.Add(days);
        Children.Add(new TextBlock { Text = selected.ToString("dddd, MMMM d"), FontSize = 17, TextAlignment = TextAlignment.Center });
    }
}

/// <summary>App-drawn seek rail; no operating-system slider template.</summary>
internal sealed class SeekRail : FrameworkElement
{
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 1;
    private double value;
    public double Value { get => value; set { this.value = Math.Clamp(value, Minimum, Math.Max(Minimum, Maximum)); InvalidateVisual(); } }
    public SeekRail()
    {
        Height = 28; Focusable = true; Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) => { if (!IsEnabled) return; Focus(); CaptureMouse(); Seek(e.GetPosition(this).X); };
        MouseMove += (_, e) => { if (IsMouseCaptured) Seek(e.GetPosition(this).X); };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        KeyDown += (_, e) =>
        {
            double step = Math.Max(1, (Maximum - Minimum) / 100);
            if (e.Key == Key.Left) Value -= step;
            else if (e.Key == Key.Right) Value += step;
            else if (e.Key == Key.Home) Value = Minimum;
            else if (e.Key == Key.End) Value = Maximum;
            else return;
            e.Handled = true;
        };
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }
    private void Seek(double x) => Value = Minimum + Math.Clamp(x / Math.Max(1, ActualWidth), 0, 1) * (Maximum - Minimum);
    protected override void OnRender(DrawingContext dc)
    {
        double width = Math.Max(1, ActualWidth), fraction = (Value - Minimum) / Math.Max(.001, Maximum - Minimum);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null, new Rect(0, 11, width, 6), 3, 3);
        dc.DrawRoundedRectangle(IsEnabled ? Brushes.PaleGreen : Brushes.SlateGray, null, new Rect(0, 11, width * fraction, 6), 3, 3);
        dc.DrawEllipse(Brushes.White, null, new Point(Math.Clamp(width * fraction, 5, Math.Max(5, width - 5)), 14), IsKeyboardFocused ? 6 : 4, IsKeyboardFocused ? 6 : 4);
    }
}
