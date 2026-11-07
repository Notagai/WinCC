using System.Text.RegularExpressions;

namespace WinCC.Layout;

public static class GoogleFontMetadata
{
    public static string FileName(string metadata)
    {
        var blocks = Regex.Matches(metadata, @"fonts\s*\{([^}]+)\}").Select(m => m.Groups[1].Value).ToArray();
        string? normal = blocks.FirstOrDefault(b => Regex.IsMatch(b, @"weight:\s*400\b") && b.Contains("style: \"normal\""));
        string block = normal ?? blocks.FirstOrDefault() ?? throw new InvalidDataException("No font in upstream metadata.");
        var match = Regex.Match(block, @"filename:\s*""([^""]+\.ttf)""");
        if (!match.Success) throw new InvalidDataException("No supported TrueType font in upstream metadata.");
        string file = match.Groups[1].Value;
        if (file.Length > 180 || file.Contains("..") || file.Any(c => !char.IsAsciiLetterOrDigit(c) && !" -_[],.".Contains(c)))
            throw new InvalidDataException("Invalid upstream font filename.");
        return file;
    }
}
