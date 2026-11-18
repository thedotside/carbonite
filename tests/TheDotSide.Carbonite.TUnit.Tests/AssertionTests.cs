using System.Globalization;
using TheDotSide.Carbonite.Engine;
using TheDotSide.Carbonite.Naming;
using TUnit.Assertions.Exceptions;

namespace TheDotSide.Carbonite.TUnit.Tests;

public class AssertionTests
{
    [Test]
    public async Task Matches_with_the_tunit_assertion()
    {
        await Assert.That(new { Total = 42 }).MatchesSnapshot();
    }

    [Test]
    public async Task Takes_a_name_and_options()
    {
        await Assert
            .That(new { Id = 7, Name = "Ada" })
            .MatchesSnapshot("without-id", options => options.IgnoreMember("Id"));
    }

    [Test]
    public async Task Awaits_a_task_given_to_assert_that()
    {
        await Assert.That(LoadAsync()).MatchesSnapshot();
    }

    [Test]
    public async Task Assert_multiple_collects_a_snapshot_mismatch()
    {
        // With SNAPSHOT_UPDATE=1 the mismatch below would be accepted instead of reported.
        Skip.When(
            SnapshotEnvironment.FromProcess().UpdateRequested,
            "Needs a real mismatch; SNAPSHOT_UPDATE=1 would accept it."
        );

        // A verified file with another value, written by the test so that nothing committed is
        // wrong on purpose. Its name carries the framework: the net10.0 and net11.0 runs execute
        // at the same time and must not delete each other's file. On CI the compiler rewrites
        // source paths to /_/...: resolve the folder like Carbonite does.
        var name = string.Create(CultureInfo.InvariantCulture, $"net{Environment.Version.Major}");
        var context = new TUnitTestContextProvider().GetCurrent()!;
        var source = SourcePathResolver.Default.Resolve(
            context.SourceFilePath,
            context.TestAssembly
        );
        var stem = Path.Combine(
            Path.GetDirectoryName(source)!,
            "__snapshots__",
            $"AssertionTests.Assert_multiple_collects_a_snapshot_mismatch.{name}"
        );
        var verified = stem + ".verified.json";
        var received = stem + ".received.json";
        await File.WriteAllTextAsync(verified, "{\n  \"Total\": 1\n}\n");
        try
        {
            var failure = await Assert.ThrowsAsync<AssertionException>(async () =>
            {
                using (Assert.Multiple())
                {
                    await Assert.That(new { Total = 2 }).MatchesSnapshot(name);
                    await Assert.That(new[] { 1, 1 }.Sum()).IsEqualTo(3);
                }
            });

            await Assert.That(failure!.Message).Contains("Snapshot mismatch");
            await Assert.That(failure.Message).Contains("+  \"Total\": 2");
            await Assert.That(failure.Message).Contains("Expected to be 3");
        }
        finally
        {
            File.Delete(verified);
            File.Delete(received);
        }
    }

    private static async Task<int[]> LoadAsync()
    {
        await Task.Yield();
        return [1, 2, 3];
    }
}
