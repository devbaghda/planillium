using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// Points AppPaths.Root at a throwaway temp directory for the whole test
/// run, via the MENTOR_ROOT env var hook AppPaths already supports for
/// exactly this purpose — now gated on PLANILLIUM_TESTS as well as DEBUG
/// (see AppPaths.Root and the Tests .csproj's DefineConstants), not DEBUG
/// alone: `dotnet test -c Release` used to compile that hook away entirely
/// and silently redirect every test in this assembly at the real
/// data/progress.db (2026-08-13, discovered via a spurious test failure
/// that turned out to be a collision with a real ledger row). AppPaths.Root
/// and Database/ScoreService's schema checks are all cached in static
/// fields for the life of the process (a deliberate perf choice for the
/// real app — see their doc comments) — which means every test in this
/// assembly shares one SQLite file, not a fresh one each. Tests must use a
/// unique plan id (Guid) so their task_overrides/task_completions rows can
/// never collide with another test's, rather than relying on a clean
/// database per test.
/// </summary>
internal static class TestRootBootstrap
{
    /// <summary>2026-11-12: runs when the test assembly loads, before ANY test. Test classes outside the "TestRoot"
    /// collection (and parallel ones) used to read AppPaths.Root before the fixture set MENTOR_ROOT, which cached
    /// the REAL data folder and let PlanRemainderTests write fake plans/rows into the user's live data.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlanilliumTests_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("MENTOR_ROOT", root);
    }
}

public sealed class TestRootFixture : IDisposable
{
    public string Root { get; }

    public TestRootFixture()
    {
        // The module initializer below already set MENTOR_ROOT before any test ran; reuse it.
        var env = Environment.GetEnvironmentVariable("MENTOR_ROOT");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) { Root = env; return; }
        Root = Path.Combine(Path.GetTempPath(), "PlanilliumTests_" + Guid.NewGuid());
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
