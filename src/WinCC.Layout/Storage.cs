using System.Text.Json;

namespace WinCC.Layout;

public interface ILayoutStore { void Save(LayoutDocument document); }
public sealed record LoadResult(LayoutDocument Document, string? Warning, bool ReadOnly);

public sealed class LayoutStore(string path) : ILayoutStore
{
    private bool readOnly;
    public LoadResult Load()
    {
        if (!File.Exists(path)) return new(LayoutDocument.Empty(), null, false);
        try { return new(Read(path), null, false); }
        catch (UnsupportedSchemaException)
        {
            readOnly = true;
            return new(LayoutDocument.Empty(), "Newer layout schema: original preserved. Layout saving is disabled.", true);
        }
        catch (Exception e) when (Recoverable(e))
        {
            // Preserve the damaged original. Never silently overwrite it, even after recovery.
            readOnly = true;
            try { return new(Read(path + ".bak"), "Recovered backup. Original preserved; layout saving is disabled.", true); }
            catch (Exception backup) when (Recoverable(backup))
            { return new(LayoutDocument.Empty(), "Layout could not be loaded. Original preserved; layout saving is disabled.", true); }
        }
    }
    public void Save(LayoutDocument document)
    {
        if (readOnly) throw new IOException("Original layout is protected. Close WinCC and archive it before recovery.");
        // Check the on-disk schema again in case another program changed the file after load.
        if (File.Exists(path)) _ = Read(path);
        AtomicFile.Write(path, LayoutJson.Serialize(document), backup: true);
    }
    private static LayoutDocument Read(string file)
    {
        using var stream = File.OpenRead(file);
        if (stream.Length > LayoutJson.MaxBytes) throw new InvalidDataException("Layout is too large.");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        return LayoutJson.Parse(buffer.ToArray());
    }
    private static bool Recoverable(Exception e) => e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
}

public static class AtomicFile
{
    public static void Write(string path, byte[] bytes, bool backup = false)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, ".wincc-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            if (File.Exists(path)) File.Replace(temporary, path, backup ? path + ".bak" : null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

/// <summary>Widget content is independent of the saved layout transaction.</summary>
public sealed class NotesStore(string directory)
{
    public const int MaxCharacters = 100000;
    private string FileFor(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid note ID.");
        return Path.Combine(directory, id + ".txt");
    }
    public string Load(string id)
    {
        var path = FileFor(id);
        if (!File.Exists(path)) return "";
        if (new FileInfo(path).Length > MaxCharacters * 4) throw new IOException("Note is too large.");
        return File.ReadAllText(path);
    }
    public void Save(string id, string text)
    {
        if (text.Length > MaxCharacters) throw new InvalidDataException("Note is too large.");
        AtomicFile.Write(FileFor(id), System.Text.Encoding.UTF8.GetBytes(text), backup: true);
    }
}
