using TheDotSide.Carbonite.Engine;
using TheDotSide.Carbonite.Tests.Support;

namespace TheDotSide.Carbonite.Tests;

/// <summary>The public API, with explicit source files so that the results do not depend on CI.</summary>
public class SnapshotTests
{
    [Test]
    public async Task Match_passes_for_the_verified_value_and_throws_with_paths_for_another()
    {
        // With SNAPSHOT_UPDATE=1 the mismatch below would be accepted instead of reported.
        Skip.When(
            SnapshotEnvironment.FromProcess().UpdateRequested,
            "Needs a real mismatch; SNAPSHOT_UPDATE=1 would accept it."
        );

        using var temp = new TempDirectory();
        var source = temp.Combine("PanierTests.cs");
        await File.WriteAllTextAsync(source, "// tests");
        // Two snapshots, as committed by an earlier run: checking two values against one
        // snapshot in the same run is a conflict.
        var same = temp.Combine("__snapshots__", "PanierTests.Same.verified.json");
        var verified = temp.Combine("__snapshots__", "PanierTests.Total.verified.json");
        Directory.CreateDirectory(Path.GetDirectoryName(verified)!);
        await File.WriteAllTextAsync(same, "{\n  \"Total\": 1\n}\n");
        await File.WriteAllTextAsync(verified, "{\n  \"Total\": 1\n}\n");

        Snapshot.Match(new { Total = 1 }, sourceFilePath: source, memberName: "Same");
        var exception = Assert.Throws<SnapshotMismatchException>(() =>
            Snapshot.Match(new { Total = 2 }, sourceFilePath: source, memberName: "Total")
        );

        await Assert.That(exception.VerifiedFilePath).IsEqualTo(verified);
        await Assert
            .That(exception.ReceivedFilePath)
            .IsEqualTo(temp.Combine("__snapshots__", "PanierTests.Total.received.json"));
        await Assert.That(exception.Message).Contains("+  \"Total\": 2");
    }

    [Test]
    public async Task Match_with_options_applies_them()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("PanierTests.cs");
        await File.WriteAllTextAsync(source, "// tests");
        var verified = temp.Combine("__snapshots__", "PanierTests.Options.verified.json");
        Directory.CreateDirectory(Path.GetDirectoryName(verified)!);
        await File.WriteAllTextAsync(verified, "{\n  \"Kept\": 1\n}\n");

        Snapshot.Match(
            new { Kept = 1, Hidden = 2 },
            options => options.IgnoreMember("Hidden"),
            sourceFilePath: source,
            memberName: "Options"
        );

        await Assert.That(File.Exists(verified)).IsTrue();
    }

    [Test]
    public async Task Match_explains_a_forgotten_await()
    {
        var exception = Assert.Throws<SnapshotException>(() => Snapshot.Match(LoadAsync()));

        await Assert.That(exception.Message).Contains("Did you forget 'await'?");
        await Assert.That(exception).IsNotTypeOf<SnapshotMismatchException>();
    }

    private static Task<int> LoadAsync() => Task.FromResult(42);
}
