namespace ChroniclesLauncher;

public static class MemoryCardStorage
{
    private static byte[] ReadCard(string source)
    {
        var card = File.ReadAllBytes(source);
        if (card.Length != 128 * 1024 || card[0] != 'M' || card[1] != 'C')
            throw new InvalidDataException("Choose a raw 128 KB PlayStation memory card (.sav, .mcr or .mcd). Individual save files must first be exported as a memory card.");
        return card;
    }

    public static void AdoptMissingCards(string data, params string[] sources)
    {
        Directory.CreateDirectory(data);
        foreach (var name in new[] { "carda.sav", "cardb.sav" })
        {
            var destination = Path.Combine(data, name);
            if (File.Exists(destination)) continue;
            foreach (var folder in sources.Distinct())
            {
                var source = Path.Combine(folder, name);
                if (!File.Exists(source)) continue;
                // Legacy cards are copied only into empty slots; originals remain intact.
                byte[] card;
                try { card = ReadCard(source); }
                catch (InvalidDataException error)
                {
                    Console.Error.WriteLine($"[Storage] Skipped invalid legacy card '{source}': {error.Message}");
                    continue;
                }
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
                output.Write(card);
                break;
            }
        }
    }

    public static void Import(string source, string destination)
    {
        var card = ReadCard(source); // Validate before changing anything.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, card);
            if (File.Exists(destination))
                File.Copy(destination, destination + "." + Guid.NewGuid().ToString("N") + ".bak");
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
