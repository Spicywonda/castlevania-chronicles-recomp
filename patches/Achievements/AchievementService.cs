namespace Chronicles.AchievementSystem;

public sealed record AchievementDefinition(string Id, string Title, string Description, int Points);
public sealed record AchievementView(AchievementDefinition Definition, DateTimeOffset? UnlockedAt);
public sealed record AchievementSnapshot(IReadOnlyList<AchievementView> Entries, bool IsEligible, string? Error)
{
    public int EarnedCount => Entries.Count(x => x.UnlockedAt != null);
    public int EarnedPoints => Entries.Where(x => x.UnlockedAt != null).Sum(x => x.Definition.Points);
}

public sealed class AchievementService : IDisposable
{
    private readonly object _gate = new();
    private readonly AchievementStore _store;
    private readonly Dictionary<string, AchievementDefinition> _definitions;
    private readonly Queue<AchievementDefinition> _notifications = new();
    private readonly RunEligibility _eligibility = new();
    private bool _cheatsEnabled, _disposed;

    public AchievementService(string directory, IEnumerable<AchievementDefinition> definitions)
    {
        _definitions = new(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || definition.Id.Length > 128
                || string.IsNullOrWhiteSpace(definition.Title) || string.IsNullOrWhiteSpace(definition.Description)
                || definition.Points <= 0 || !_definitions.TryAdd(definition.Id, definition))
                throw new ArgumentException("Invalid or duplicate achievement definition.", nameof(definitions));
        }
        _store = new AchievementStore(directory);
    }

    public AchievementSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                var unlocked = _store.Snapshot.ToDictionary(x => x.Id, x => x.UnlockedAt, StringComparer.Ordinal);
                var entries = _definitions.Values.Select(x => new AchievementView(x,
                    unlocked.TryGetValue(x.Id, out var timestamp) ? timestamp : null)).ToArray();
                return new(Array.AsReadOnly(entries), !_disposed && _eligibility.IsEligible, _store.Error);
            }
        }
    }

    public void SetCheatsEnabled(bool enabled)
    {
        lock (_gate)
        {
            _cheatsEnabled = enabled;
            if (enabled) _eligibility.CheatActivated();
        }
    }

    public void BeginRun(bool isDemo)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _eligibility.BeginRun(_cheatsEnabled || isDemo);
        }
    }

    public void ResetSession()
    {
        lock (_gate)
        {
            _eligibility.Reset();
            _notifications.Clear();
        }
    }

    public bool CompleteVerifiedObjective(string id, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            if (_disposed || !_eligibility.IsEligible || !_definitions.TryGetValue(id, out var definition)
                || !_store.Unlock(id, timestamp)) return false;
            _notifications.Enqueue(definition);
            return true;
        }
    }

    public bool TryDequeueNotification(out AchievementDefinition? definition)
    {
        lock (_gate) return _notifications.TryDequeue(out definition);
    }

    public Task FlushAsync() => _store.FlushAsync();
    public Task RetrySaveAsync() => _store.RetrySaveAsync();

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _eligibility.Reset();
            _notifications.Clear();
        }
        _store.Dispose();
    }
}
