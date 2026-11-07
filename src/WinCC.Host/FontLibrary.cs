using System.IO;
using System.Net.Http;
using System.Windows.Media;
using WinCC.Layout;

namespace WinCC.Host;

internal static class FontLibrary
{
    // Curated official Google Fonts families; metadata supplies the actual upstream filename.
    public static IReadOnlyDictionary<string, string> Google { get; } = new Dictionary<string,string>
    { ["Space Mono"] = "spacemono", ["Share Tech Mono"] = "sharetechmono", ["Rajdhani"] = "rajdhani", ["Bungee"] = "bungee" };
    private static string DirectoryFor(string family) => Path.Combine(Paths.Data, "fonts", Google[family]);
    public static FontFamily Resolve(string family)
    {
        if (family is "Lato" or "VT323") return new("/WinCC;component/Assets/Fonts/#" + family);
        if (Google.ContainsKey(family) && File.Exists(Path.Combine(DirectoryFor(family), "font.ttf")))
            return new FontFamily(new Uri(DirectoryFor(family) + Path.DirectorySeparatorChar), "./#" + family);
        return new FontFamily(family);
    }
    public static IReadOnlyList<string> Names => new[] { "Lato", "VT323" }.Concat(Google.Keys)
        .Concat(Fonts.SystemFontFamilies.Select(f => f.Source)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToArray();
    public static async Task Download(string family)
    {
        if (!Google.TryGetValue(family, out string? folder)) throw new InvalidOperationException("Select a Google Fonts family: Space Mono, Share Tech Mono, Rajdhani or Bungee.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        string root = "https://raw.githubusercontent.com/google/fonts/main/ofl/" + folder + "/";
        async Task<byte[]> Read(string url, int limit)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > limit) throw new IOException("Font download exceeds size limit.");
            using var stream = await response.Content.ReadAsStreamAsync(cancellation.Token); using var bytes = new MemoryStream();
            byte[] buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, cancellation.Token)) > 0)
            { if (bytes.Length + count > limit) throw new IOException("Font download exceeds size limit."); bytes.Write(buffer,0,count); }
            return bytes.ToArray();
        }
        string metadata = System.Text.Encoding.UTF8.GetString(await Read(root + "METADATA.pb", 256000));
        string file = GoogleFontMetadata.FileName(metadata);
        byte[] font = await Read(root + Uri.EscapeDataString(file), 8 * 1024 * 1024);
        byte[] license = await Read(root + "OFL.txt", 128000);
        if (font.Length < 12 || !(font[0] == 0 && font[1] == 1 && font[2] == 0 && font[3] == 0)) throw new IOException("Invalid TrueType font.");
        string directory = DirectoryFor(family);
        AtomicFile.Write(Path.Combine(directory, "OFL.txt"), license);
        AtomicFile.Write(Path.Combine(directory, "font.ttf"), font);
    }
}
