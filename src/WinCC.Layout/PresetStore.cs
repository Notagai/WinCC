namespace WinCC.Layout;

public sealed class PresetStore(string directory)
{
    private string PathFor(int slot)
    {
        if (slot is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(directory, $"preset-{slot}.json");
    }
    public bool Exists(int slot) => File.Exists(PathFor(slot));
    public void Save(int slot, LayoutDocument document) => new LayoutStore(PathFor(slot)).Save(document);
    public LayoutDocument Load(int slot)
    {
        string path = PathFor(slot);
        if (!File.Exists(path)) throw new FileNotFoundException("Preset is empty.");
        var loaded = new LayoutStore(path).Load();
        if (loaded.ReadOnly || loaded.Warning is not null) throw new IOException("Preset is damaged or unsupported. Original preserved.");
        return loaded.Document;
    }
}
