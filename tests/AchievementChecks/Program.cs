using Chronicles.AchievementSystem;
using System.Text.Json;

var checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
var eligibility = new RunEligibility();
Check(!eligibility.IsEligible, "startup is not a run");
eligibility.BeginRun(false);
Check(eligibility.IsEligible, "clean run");
eligibility.CheatActivated();
Check(!eligibility.IsEligible, "cheat invalidates immediately");
Check(!eligibility.IsEligible, "disabling a cheat cannot restore eligibility");
eligibility.Reset();
Check(!eligibility.IsEligible, "reset awaits a new run");
eligibility.BeginRun(true);
Check(!eligibility.IsEligible, "new run with active cheats");
eligibility.BeginRun(false);
Check(eligibility.IsEligible, "new clean run restores eligibility");
var root = Path.Combine(Path.GetTempPath(), "chronicles-achievements-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    var stamp = DateTimeOffset.Parse("2026-10-22T12:00:00Z");
    using (var store = new AchievementStore(root))
    {
        Check(store.Snapshot.Count == 0 && store.Error == null, "missing save is empty");
        Check(store.Unlock("stage-1", stamp), "first unlock");
        Check(!store.Unlock("stage-1", stamp.AddDays(1)), "duplicate unlock");
        Check(!store.Unlock("", stamp), "invalid ID");
        await store.FlushAsync();
        Check(store.Error == null, "save succeeded");
        Check(store.Snapshot.Single().UnlockedAt == stamp, "original timestamp retained");
    }
    using (var store = new AchievementStore(root))
    {
        Check(store.Snapshot.Single().Id == "stage-1", "round trip");
        store.Unlock("stage-2", stamp);
        await store.FlushAsync();
    }
    var path = Path.Combine(root, "achievements.json");
    File.WriteAllText(path, "{truncated");
    using (var store = new AchievementStore(root))
    {
        Check(store.Snapshot.Any(x => x.Id == "stage-1"), "recover backup");
        Check(store.Error != null, "recovery is visible");
        Check(!store.Unlock("stage-3", stamp), "corrupt primary is write protected");
        await store.FlushAsync();
        Check(File.ReadAllText(path) == "{truncated", "corrupt original preserved");
    }
    File.WriteAllText(path, "{\"Version\":99,\"Unlocks\":[]}");
    using (var store = new AchievementStore(root))
    {
        Check(store.Error != null, "newer version reported");
        Check(!store.Unlock("stage-3", stamp), "newer version write protected");
    }
    File.Delete(path);
    File.Delete(path + ".bak");
    using (var store = new AchievementStore(root))
        for (int i = 0; i < 100; i++) store.Unlock("item-" + i, stamp);
    using (var store = new AchievementStore(root))
        Check(store.Snapshot.Count == 100, "dispose drains pending writes");
    using (var first = new AchievementStore(root))
    using (var second = new AchievementStore(root))
    {
        Check(second.Error != null, "concurrent store rejected");
        Check(!second.Unlock("conflict", stamp), "second writer cannot lose progress");
    }
    using (var store = new AchievementStore(root))
    {
        File.Delete(path + ".bak");
        Directory.CreateDirectory(path + ".bak");
        store.Unlock("retry", stamp);
        await store.FlushAsync();
        Check(store.Error != null, "write failure reported");
        Directory.Delete(path + ".bak");
        await store.RetrySaveAsync();
        Check(store.Error == null, "failed write can be retried");
    }
    using (var store = new AchievementStore(root))
        Check(store.Snapshot.Any(x => x.Id == "retry"), "retry persists pending unlock");
    var serviceDir = Path.Combine(root, "service");
    var definitions = new[] { new AchievementDefinition("test-clear", "Clear", "Complete a test objective.", 5) };
    using (var service = new AchievementService(serviceDir, definitions))
    {
        Check(!service.CompleteVerifiedObjective("test-clear", stamp), "no awards before a run");
        service.BeginRun(isDemo: true);
        Check(!service.CompleteVerifiedObjective("test-clear", stamp), "demo awards excluded");
        service.SetCheatsEnabled(true);
        service.BeginRun(isDemo: false);
        service.SetCheatsEnabled(false);
        Check(!service.CompleteVerifiedObjective("test-clear", stamp), "cheated run stays invalid");
        service.BeginRun(isDemo: false);
        service.SetCheatsEnabled(true);
        service.SetCheatsEnabled(false);
        Check(!service.CompleteVerifiedObjective("test-clear", stamp), "brief cheat toggle invalidates run");
        service.BeginRun(isDemo: false);
        Check(!service.CompleteVerifiedObjective("unknown", stamp), "unknown objective excluded");
        Check(service.CompleteVerifiedObjective("test-clear", stamp), "verified objective accepted");
        Check(!service.CompleteVerifiedObjective("test-clear", stamp), "duplicate event ignored");
        Check(service.TryDequeueNotification(out var notice) && notice!.Id == "test-clear", "unlock notice queued");
        Check(!service.TryDequeueNotification(out _), "notice emitted once");
        service.ResetSession();
        Check(service.Snapshot.EarnedPoints == 5 && service.Snapshot.EarnedCount == 1, "reset preserves progress");
        Check(!service.Snapshot.IsEligible, "reset requires a new run");
    }
    using (var service = new AchievementService(serviceDir, definitions))
    {
        Check(service.Snapshot.EarnedCount == 1, "service persists progress");
        Check(!service.TryDequeueNotification(out _), "loading progress does not notify");
    }
    var blocked = Path.Combine(root, "blocked");
    File.WriteAllText(blocked, "file");
    using (var store = new AchievementStore(blocked))
        Check(store.Error != null && !store.Unlock("stage-1", stamp), "IO failure is nonfatal");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"Achievement checks: {checks} passed.");
