using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinCC.Layout;

public sealed record RectDip(double X, double Y, double Width, double Height);
public sealed record Widget(string Id, string Type, string MonitorKey, RectDip RectDip,
    int ZIndex, Dictionary<string, string> Settings);
public sealed record Profile(string Id, string Name, List<Widget> Widgets);
public sealed record LayoutDocument(int SchemaVersion, string ActiveProfileId, List<Profile> Profiles)
{
    [JsonIgnore] public Profile Active => Profiles.Single(p => p.Id == ActiveProfileId);
    public static LayoutDocument Empty() => new(1, "default", [new("default", "Default", [])]);
    public LayoutDocument Copy() => LayoutJson.Parse(LayoutJson.Serialize(this));
}

public sealed record WidgetSpec(string Type, string Name, double MinWidth, double MinHeight,
    double Width, double Height, double MaxWidth = 1200, double MaxHeight = 1000);

public static class WidgetCatalog
{
    public static IReadOnlyList<WidgetSpec> All { get; } = Array.AsReadOnly(new WidgetSpec[]
    {
        new("clock", "Clock", 220, 150, 300, 180),
        new("calendar", "Calendar", 280, 320, 320, 370),
        new("notes", "Notes", 240, 200, 320, 300),
        new("system", "System", 240, 180, 300, 220),
        new("spotify", "Spotify", 260, 160, 320, 180),
        new("weather", "Weather", 240, 160, 300, 190),
        new("ai-island", "AI Island", 300, 100, 380, 130)
    });
    public static WidgetSpec Get(string type) => All.SingleOrDefault(s => s.Type == type)
        ?? throw new InvalidDataException("Unknown widget type.");
}

public static class LayoutJson
{
    public const int MaxBytes = 1024 * 1024;
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static byte[] Serialize(LayoutDocument document)
    {
        LayoutValidator.Validate(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Layout is too large.");
        return bytes;
    }
    public static LayoutDocument Parse(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Layout is too large.");
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int number)) throw new InvalidDataException("Missing schema version.");
        if (number != 1) throw new UnsupportedSchemaException();
        var document = JsonSerializer.Deserialize<LayoutDocument>(bytes, Options)
            ?? throw new InvalidDataException("Empty layout.");
        LayoutValidator.Validate(document);
        return document;
    }
}

public sealed class UnsupportedSchemaException() : IOException("Unsupported layout schema; original preserved.");

public static class LayoutValidator
{
    public static void Validate(LayoutDocument d)
    {
        if (d.SchemaVersion != 1) throw new UnsupportedSchemaException();
        if (!Id(d.ActiveProfileId) || d.Profiles is null || d.Profiles.Count is < 1 or > 20)
            throw new InvalidDataException("Invalid profiles.");
        var profiles = new HashSet<string>();
        foreach (var p in d.Profiles)
        {
            if (p is null || !Id(p.Id) || !profiles.Add(p.Id) || string.IsNullOrWhiteSpace(p.Name) ||
                p.Name.Length > 100 || p.Widgets is null || p.Widgets.Count > 100)
                throw new InvalidDataException("Invalid profile.");
            var ids = new HashSet<string>();
            foreach (var w in p.Widgets)
            {
                if (w is null || !Id(w.Id) || !ids.Add(w.Id) || !Id(w.MonitorKey) || w.RectDip is null ||
                    w.Settings is null || w.Settings.Count > 20 || w.ZIndex is < 0 or > 10000)
                    throw new InvalidDataException("Invalid widget identity or settings.");
                var s = WidgetCatalog.Get(w.Type);
                var r = w.RectDip;
                if (!new[] { r.X, r.Y, r.Width, r.Height }.All(double.IsFinite) ||
                    r.X is < 0 or > 100000 || r.Y is < 0 or > 100000 ||
                    r.Width < s.MinWidth || r.Height < s.MinHeight || r.Width > s.MaxWidth || r.Height > s.MaxHeight)
                    throw new InvalidDataException("Invalid widget geometry.");
                if (w.Settings.Any(kv => !Id(kv.Key) || kv.Value is null || kv.Value.Length > 4096))
                    throw new InvalidDataException("Invalid setting.");
                if (w.Settings.TryGetValue("hourFormat", out var hour) && hour is not ("system" or "12" or "24"))
                    throw new InvalidDataException("Invalid hour format.");
            }
        }
        if (!profiles.Contains(d.ActiveProfileId)) throw new InvalidDataException("Missing active profile.");
    }
    private static bool Id(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 512 && !s.Any(char.IsControl);
}

public sealed record MonitorArea(string Key, double LeftPx, double TopPx, double WidthDip, double HeightDip,
    double Scale, bool Primary);

public static class Geometry
{
    public static MonitorArea Resolve(string key, IReadOnlyList<MonitorArea> monitors) =>
        monitors.FirstOrDefault(m => m.Key == key) ?? monitors.FirstOrDefault(m => m.Primary)
        ?? monitors.FirstOrDefault() ?? throw new InvalidOperationException("No usable displays.");
    public static RectDip Clamp(RectDip r, WidgetSpec s, MonitorArea m)
    {
        // A display smaller than a type's minimum uses a clipped runtime projection only.
        double width = Math.Clamp(r.Width, Math.Min(s.MinWidth, m.WidthDip), Math.Min(s.MaxWidth, m.WidthDip));
        double height = Math.Clamp(r.Height, Math.Min(s.MinHeight, m.HeightDip), Math.Min(s.MaxHeight, m.HeightDip));
        return new(Math.Clamp(r.X, 0, Math.Max(0, m.WidthDip - width)),
            Math.Clamp(r.Y, 0, Math.Max(0, m.HeightDip - height)), width, height);
    }
    public static (double X, double Y) ToPhysical(RectDip r, MonitorArea m) =>
        (m.LeftPx + r.X * m.Scale, m.TopPx + r.Y * m.Scale);
}
