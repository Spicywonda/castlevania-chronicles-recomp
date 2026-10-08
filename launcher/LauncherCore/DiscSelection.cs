using System.Text.RegularExpressions;

namespace ChroniclesLauncher;

public sealed record DiscValidationResult(string? Path, string? Error)
{
    public bool IsValid => Error is null;
}

public static class DiscSelection
{
    private static readonly Regex CueFile = new("^\\s*FILE\\s+(?:\"([^\"]+)\"|(\\S+))\\s+\\S+", RegexOptions.IgnoreCase);

    public static DiscValidationResult Validate(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return new(null, "Choose a CUE or CHD disc image.");

        try
        {
            var path = Path.GetFullPath(rawPath.Trim().Trim('"'));
            if (!File.Exists(path)) return new(null, "The selected disc image could not be found.");
            var extension = Path.GetExtension(path);
            if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase))
            {
                var files = File.ReadLines(path).Select(line => CueFile.Match(line)).Where(match => match.Success).ToArray();
                if (files.Length == 0 || !File.ReadLines(path).Any(line => line.TrimStart().StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase)))
                    return new(null, "The CUE file does not contain valid tracks.");
                foreach (var match in files)
                {
                    var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, name)))
                        return new(null, $"Missing BIN file referenced by the CUE: {name}");
                }
            }
            else if (extension.Equals(".chd", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                Span<byte> magic = stackalloc byte[8];
                if (stream.Read(magic) != 8 || !magic.SequenceEqual("MComprHD"u8))
                    return new(null, "The CHD file has an invalid header.");
            }
            else return new(null, "Choose a CUE or CHD file.");
            return new(path, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(null, $"Could not open the disc image: {error.Message}");
        }
    }
}
