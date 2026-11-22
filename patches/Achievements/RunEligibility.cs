namespace Chronicles.AchievementSystem;

public sealed class RunEligibility
{
    private int _eligible;
    public bool IsEligible => Volatile.Read(ref _eligible) == 1;
    public void BeginRun(bool cheatsEnabled) => Interlocked.Exchange(ref _eligible, cheatsEnabled ? 0 : 1);
    public void CheatActivated() => Interlocked.Exchange(ref _eligible, 0);
    public void Reset() => Interlocked.Exchange(ref _eligible, 0);
}
