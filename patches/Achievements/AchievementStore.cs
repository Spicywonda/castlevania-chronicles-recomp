using System.Text.Json;

namespace Chronicles.AchievementSystem;

public sealed record AchievementUnlock(string Id, DateTimeOffset UnlockedAt);

public sealed class AchievementStore : IDisposable
{
    private sealed record Document(int Version, AchievementUnlock[] Unlocks);
    private readonly object _gate = new();
    private readonly Dictionary<string, AchievementUnlock> _unlocks = new(StringComparer.Ordinal);
    private readonly string _path;
    private FileStream? _lease;
    private Task _pending = Task.CompletedTask;
    private bool _readOnly, _disposed;
    private string? _error;

    public string? Error { get { lock (_gate) return _error; } }
    public IReadOnlyList<AchievementUnlock> Snapshot
    {
        get { lock (_gate) return Array.AsReadOnly(_unlocks.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray()); }
    }

    public AchievementStore(string directory)
    {
        _path = Path.Combine(directory, "achievements.json");
        try
        {
            Directory.CreateDirectory(directory);
            _lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(_path)) Load(_path);
            else if (File.Exists(_path + ".bak")) Load(_path + ".bak");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            _readOnly = true;
            _error = "Achievement progress could not be loaded. Existing files have been preserved.";
            Console.Error.WriteLine($"[Achievements] Load failed: {error.Message}");
            if (_lease != null && File.Exists(_path + ".bak"))
            {
                try { Load(_path + ".bak"); }
                catch (Exception backupError) when (IsStorageError(backupError))
                { Console.Error.WriteLine($"[Achievements] Backup load failed: {backupError.Message}"); }
            }
        }
    }

    private static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException
        or JsonException or NotSupportedException or InvalidDataException;

    private void Load(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 1024 * 1024) throw new InvalidDataException("Achievement save exceeds the size limit.");
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
        if (document is not { Version: 1, Unlocks: not null })
            throw new InvalidDataException("Unsupported achievement save format.");
        var loaded = new Dictionary<string, AchievementUnlock>(StringComparer.Ordinal);
        foreach (var item in document.Unlocks)
        {
            if (item == null || !ValidId(item.Id) || item.UnlockedAt == default || !loaded.TryAdd(item.Id, item))
                throw new InvalidDataException("Invalid achievement progress.");
        }
        foreach (var item in loaded) _unlocks.Add(item.Key, item.Value);
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;

    public bool Unlock(string id, DateTimeOffset unlockedAt)
    {
        lock (_gate)
        {
            if (_disposed || _readOnly || !ValidId(id) || unlockedAt == default || _unlocks.ContainsKey(id)) return false;
            _unlocks.Add(id, new(id, unlockedAt));
            QueueSave();
            return true;
        }
    }

    private void QueueSave()
    {
        var document = new Document(1, _unlocks.Values.ToArray());
        var previous = _pending;
        _pending = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            Save(document);
        });
    }

    public Task RetrySaveAsync()
    {
        lock (_gate)
        {
            if (!_disposed && !_readOnly) QueueSave();
            return _pending;
        }
    }

    private void Save(Document document)
    {
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document);
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Copy(_path, _path + ".bak", overwrite: true);
            File.Move(temp, _path, overwrite: true);
            lock (_gate) _error = null;
        }
        catch (Exception error) when (IsStorageError(error))
        {
            lock (_gate) _error = "Achievement progress could not be saved.";
            Console.Error.WriteLine($"[Achievements] Save failed: {error.Message}");
        }
        finally
        {
            try { File.Delete(temp); }
            catch (Exception error) when (IsStorageError(error))
            { Console.Error.WriteLine($"[Achievements] Temporary file cleanup failed: {error.Message}"); }
        }
    }

    public Task FlushAsync() { lock (_gate) return _pending; }

    public void Dispose()
    {
        Task pending;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            pending = _pending;
        }
        pending.GetAwaiter().GetResult();
        _lease?.Dispose();
    }
}
