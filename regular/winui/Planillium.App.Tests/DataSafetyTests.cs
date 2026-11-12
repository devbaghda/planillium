using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>2026-11-12 incident (a test run wrote into the real data and its backup burst pruned the real backups).</summary>
public class DataSafetyTests
{
    [Fact]
    public void A_test_run_never_resolves_the_real_data_folder()
    {
        Assert.True(AppPaths.IsScratch);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(AppPaths.Root), StringComparison.OrdinalIgnoreCase);
    }

    private static string B(string stamp) => @"C:\x\progress-" + stamp + ".db";

    [Fact]
    public void A_burst_of_backups_never_evicts_the_earliest_backup_of_older_days()
    {
        var files = new List<string>();
        for (var i = 60; i >= 1; i--) files.Add(B($"2026-11-12_003008-{i:000}"));   // a burst today
        files.Add(B("2026-11-11_201500-100")); files.Add(B("2026-11-11_090000-100"));
        files.Add(B("2026-11-10_120000-100"));
        var newestFirst = files.OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
        var prune = DataChangeDoor.BackupsToPrune(newestFirst);
        Assert.DoesNotContain(B("2026-11-11_090000-100"), prune);   // earliest of 11th kept
        Assert.DoesNotContain(B("2026-11-10_120000-100"), prune);
        Assert.Contains(B("2026-11-11_201500-100"), prune);          // later duplicate of that day goes
        Assert.DoesNotContain(B("2026-11-12_003008-001"), prune);    // earliest of today kept
        Assert.Equal(63 - 8, prune.Count);   // kept: newest 5 + earliest of today + earliest of the 11th + the 10th
    }

    [Fact]
    public void Old_days_beyond_the_window_are_pruned()
    {
        var files = new List<string>();
        for (var d = 1; d <= 20; d++) files.Add(B($"2026-10-{d:00}_100000-100"));
        var prune = DataChangeDoor.BackupsToPrune(files.OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).ToList());
        Assert.Equal(20 - DataChangeDoor.DailyBackupDays, prune.Count);   // newest 5 are inside the 14 days
    }
}
