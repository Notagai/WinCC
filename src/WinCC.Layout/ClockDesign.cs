using System.Globalization;

namespace WinCC.Layout;

public sealed record ClockDesign
{
    public string Font { get; init; } = "Lato";
    public int Weight { get; init; } = 600;
    public bool Italic { get; init; }
    public double SizePercent { get; init; } = 100;
    public double StretchX { get; init; } = 1;
    public double StretchY { get; init; } = 1;
    public double Tracking { get; init; }
    public double Stroke { get; init; }
    public double Refraction { get; init; } = 1;
    public string Tint { get; init; } = "#BFD5FF";
    public double TintAmount { get; init; }
    public bool ShowDate { get; init; } = true;
    public string TimeFormat { get; init; } = "";
    public string DateFormat { get; init; } = "dddd, MMMM d";
    public string DateFont { get; init; } = "Lato";
    public double DateSize { get; init; } = 13;
    public double DateLift { get; init; }
    public string DateColor { get; init; } = "#FFFFFF";
    public void Validate()
    {
        static bool Range(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
        static bool FontName(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 100 && !s.Any(char.IsControl) && s.IndexOfAny(['/', '\\', ':']) < 0;
        static bool Color(string s) => s is not null && s.Length == 7 && s[0] == '#' && int.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        if (!FontName(Font) || !FontName(DateFont) || Weight is < 100 or > 900 ||
            !Range(SizePercent,25,200) || !Range(StretchX,.4,2.5) || !Range(StretchY,.4,2.5) ||
            !Range(Tracking,-4,20) || !Range(Stroke,0,8) || !Range(Refraction,0,2) || !Range(TintAmount,0,.35) ||
            !Range(DateSize,8,40) || !Range(DateLift,0,25) || !Color(Tint) || !Color(DateColor) ||
            TimeFormat is null || DateFormat is null || TimeFormat.Length > 64 || DateFormat.Length > 64)
            throw new InvalidDataException("Invalid clock design. Check the field ranges and #RRGGBB colors.");
        try { if (TimeFormat.Length > 0) _ = DateTime.Now.ToString(TimeFormat); _ = DateTime.Now.ToString(DateFormat); }
        catch (FormatException) { throw new InvalidDataException("Invalid time or date format."); }
    }
}
