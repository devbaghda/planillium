using MentorOverseer.App.Services;

namespace MentorOverseer.App.Tests;

/// <summary>
/// Points AppPaths.Root at a throwaway temp directory for the whole test
/// run, via the MENTOR_ROOT env var hook AppPaths already supports for
/// exactly this purpose. AppPaths.Root and Database/ScoreService's schema
/// checks are all cached in static fields for the life of the process (a
/// deliberate perf choice for the real app — see their doc comments) —
/// which means every test in this assembly shares one SQLite file, not a
/// fresh one each. Tests must use a unique plan id (Guid) so their
/// task_overrides/task_completions rows can never collide with another
/// test's, rather than relying on a clean database per test.
/// </summary>
public sealed class TestRootFixture : IDisposable
{
    public string Root { get; }

    public TestRootFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MentorOverseerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("MENTOR_ROOT", Root);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}

[CollectionDefinition("TestRoot")]
public sealed class TestRootCollection : ICollectionFixture<TestRootFixture>;
