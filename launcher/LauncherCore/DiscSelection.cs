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
            return new(null, "Selecciona una imagen CUE o CHD.");

        try
        {
            var path = Path.GetFullPath(rawPath.Trim().Trim('"'));
            if (!File.Exists(path)) return new(null, "No se encontró la imagen seleccionada.");
            var extension = Path.GetExtension(path);
            if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase))
            {
                var files = File.ReadLines(path).Select(line => CueFile.Match(line)).Where(match => match.Success).ToArray();
                if (files.Length == 0 || !File.ReadLines(path).Any(line => line.TrimStart().StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase)))
                    return new(null, "El archivo CUE no contiene pistas válidas.");
                foreach (var match in files)
                {
                    var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, name)))
                        return new(null, $"Falta el BIN referido por el CUE: {name}");
                }
            }
            else if (extension.Equals(".chd", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                Span<byte> magic = stackalloc byte[8];
                if (stream.Read(magic) != 8 || !magic.SequenceEqual("MComprHD"u8))
                    return new(null, "El archivo CHD no tiene una cabecera válida.");
            }
            else return new(null, "Selecciona un archivo CUE o CHD.");
            return new(path, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(null, $"No se pudo abrir la imagen: {error.Message}");
        }
    }
}
