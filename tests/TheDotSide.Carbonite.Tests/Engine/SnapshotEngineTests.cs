using System.Text;
using TheDotSide.Carbonite.Engine;
using TheDotSide.Carbonite.Extensibility;
using TheDotSide.Carbonite.Naming;
using TheDotSide.Carbonite.Tests.Support;

namespace TheDotSide.Carbonite.Tests.Engine;

public class SnapshotEngineTests
{
    [Test]
    public async Task Creates_the_snapshot_on_the_first_local_run()
    {
        using var harness = new Harness();

        var outcome = harness.Check(new { Total = 1 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Created);
        await Assert.That(outcome.VerifiedPath).IsEqualTo(harness.Verified());
        await Assert
            .That(await File.ReadAllTextAsync(harness.Verified()))
            .IsEqualTo("{\n  \"Total\": 1\n}\n");
        await Assert.That(harness.Log).Count().IsEqualTo(1);
        await Assert.That(harness.Log[0]).Contains("created");
    }

    [Test]
    public async Task Matches_an_identical_value_and_removes_a_stale_received_file()
    {
        using var harness = new Harness();
        harness.Check(new { Total = 1 });
        await File.WriteAllTextAsync(harness.Received(), "old");

        var outcome = harness.Check(new { Total = 1 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Matched);
        await Assert.That(File.Exists(harness.Received())).IsFalse();
    }

    [Test]
    public async Task Fails_on_a_different_value_and_explains_how_to_accept_it()
    {
        using var harness = new Harness();
        harness.Seed("{\n  \"Total\": 1\n}");

        var outcome = harness.Check(new { Total = 2 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Mismatched);
        await Assert.That(outcome.ReceivedPath).IsEqualTo(harness.Received());
        await Assert
            .That(await File.ReadAllTextAsync(harness.Received()))
            .IsEqualTo("{\n  \"Total\": 2\n}\n");
        await Assert.That(await File.ReadAllTextAsync(harness.Verified())).Contains("1");
        var message = outcome.FailureMessage!;
        await Assert.That(message).Contains("FactureTests.Calcule");
        await Assert.That(message).Contains("--- verified");
        await Assert.That(message).Contains("-  \"Total\": 1");
        await Assert.That(message).Contains("+  \"Total\": 2");
        await Assert.That(message).Contains(harness.Verified());
        await Assert.That(message).Contains(harness.Received());
        await Assert.That(message).Contains("SNAPSHOT_UPDATE=1 dotnet test");
        await Assert.That(message).Contains("$env:SNAPSHOT_UPDATE=1; dotnet test");
    }

    [Test]
    public async Task Fails_when_the_snapshot_is_missing_on_ci()
    {
        using var harness = new Harness { Environment = new SnapshotEnvironment(true, false) };

        var outcome = harness.Check(new { Total = 1 });

        await Assert
            .That(outcome.Kind)
            .IsEqualTo(SnapshotOutcomeKind.MissingOnContinuousIntegration);
        await Assert.That(File.Exists(harness.Verified())).IsFalse();
        await Assert.That(File.Exists(harness.Received())).IsTrue();
        await Assert.That(outcome.FailureMessage!).Contains("commit");
    }

    [Test]
    public async Task Updates_the_snapshot_when_asked_on_a_developer_machine()
    {
        using var harness = new Harness();
        harness.Seed("{\n  \"Total\": 1\n}");
        await File.WriteAllTextAsync(harness.Received(), "old");
        harness.Environment = new SnapshotEnvironment(false, true);

        var outcome = harness.Check(new { Total = 2 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Updated);
        await Assert
            .That(await File.ReadAllTextAsync(harness.Verified()))
            .IsEqualTo("{\n  \"Total\": 2\n}\n");
        await Assert.That(File.Exists(harness.Received())).IsFalse();
        await Assert.That(harness.Log[^1]).Contains("updated");
    }

    [Test]
    public async Task Ignores_update_requests_on_ci()
    {
        using var harness = new Harness();
        harness.Seed("{\n  \"Total\": 1\n}");
        harness.Environment = new SnapshotEnvironment(true, true);

        var outcome = harness.Check(new { Total = 2 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Mismatched);
        await Assert.That(await File.ReadAllTextAsync(harness.Verified())).Contains("1");
        await Assert.That(outcome.FailureMessage!).Contains("ignored on continuous integration");
    }

    [Test]
    public async Task Accepts_a_snapshot_saved_with_a_bom_and_windows_line_endings()
    {
        using var harness = new Harness();
        Directory.CreateDirectory(Path.GetDirectoryName(harness.Verified())!);
        await File.WriteAllBytesAsync(
            harness.Verified(),
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("{\r\n  \"Total\": 1\r\n}")]
        );

        var outcome = harness.Check(new { Total = 1 });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Matched);
    }

    [Test]
    public async Task Points_out_two_values_checked_against_one_snapshot()
    {
        using var harness = new Harness();
        harness.Check(new { Case = "A" });

        var outcome = harness.Check(new { Case = "B" });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Conflict);
        await Assert.That(outcome.FailureMessage!).Contains("already checked earlier in this run");
        await Assert.That(outcome.FailureMessage!).Contains("-  \"Case\": \"A\"");
        await Assert.That(outcome.FailureMessage!).Contains("+  \"Case\": \"B\"");
        await Assert
            .That(outcome.FailureMessage!)
            .Contains("Snapshot.Match(value, $\"case-{id}\")");
    }

    [Test]
    public async Task A_second_value_fails_even_when_it_matches_the_file()
    {
        // Whatever the order of the cases, the run fails with the conflict explained.
        using var harness = new Harness();
        harness.Seed("{\n  \"Case\": \"A\"\n}");

        var first = harness.Check(new { Case = "B" });
        var second = harness.Check(new { Case = "A" });

        await Assert.That(first.Kind).IsEqualTo(SnapshotOutcomeKind.Mismatched);
        await Assert.That(second.Kind).IsEqualTo(SnapshotOutcomeKind.Conflict);
        await Assert.That(second.FailureMessage!).Contains("already checked earlier in this run");
    }

    [Test]
    public async Task Names_that_differ_only_by_letter_case_conflict_on_every_system()
    {
        // Windows and macOS see one file where Linux sees two: refuse it everywhere.
        using var harness = new Harness();
        harness.Check(new { Value = 1 }, name: "Total");

        var outcome = harness.Check(new { Value = 1 }, name: "total");

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Conflict);
        await Assert.That(outcome.FailureMessage!).Contains("differ only by letter case");
        await Assert.That(File.Exists(harness.Verified("FactureTests.Calcule.Total"))).IsTrue();
    }

    [Test]
    public async Task Refuses_to_update_a_snapshot_that_received_two_values()
    {
        using var harness = new Harness { Environment = new SnapshotEnvironment(false, true) };
        harness.Check(new { Case = "A" });

        var outcome = harness.Check(new { Case = "B" });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Conflict);
        await Assert.That(await File.ReadAllTextAsync(harness.Verified())).Contains("\"A\"");
    }

    [Test]
    public async Task Explains_a_snapshot_that_changed_format()
    {
        using var harness = new Harness();
        harness.Check("plain text");

        var outcome = harness.Check(new { Text = "plain text" });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.FormatChanged);
        await Assert.That(outcome.FailureMessage!).Contains(harness.Verified(extension: "txt"));
        await Assert.That(File.Exists(harness.Verified())).IsFalse();
    }

    [Test]
    public async Task Replaces_a_snapshot_of_another_format_when_asked()
    {
        using var harness = new Harness();
        harness.Check("plain text");
        harness.Environment = new SnapshotEnvironment(false, true);

        var outcome = harness.Check(new { Text = "plain text" });

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Updated);
        await Assert.That(File.Exists(harness.Verified())).IsTrue();
        await Assert.That(File.Exists(harness.Verified(extension: "txt"))).IsFalse();
    }

    [Test]
    public async Task Stores_strings_as_text_and_uses_explicit_names()
    {
        using var harness = new Harness();

        var outcome = harness.Check("hello", name: "après paiement");

        await Assert
            .That(outcome.VerifiedPath)
            .IsEqualTo(harness.Verified("FactureTests.Calcule.apres_paiement", "txt"));
        await Assert.That(await File.ReadAllTextAsync(outcome.VerifiedPath)).IsEqualTo("hello\n");
    }

    [Test]
    public async Task Uses_the_running_test_from_a_framework_adapter()
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        using var harness = new Harness(
            new FakeProvider(source => new SnapshotTestContext(
                source,
                "FromContext",
                [],
                [1, "a"],
                null,
                "FactureTests.FromContext(1, a)",
                key => occurrences[key] = occurrences.GetValueOrDefault(key) + 1
            ))
        );

        var first = harness.Check(new { Step = 1 });
        var second = harness.Check(new { Step = 2 });

        await Assert
            .That(first.VerifiedPath)
            .IsEqualTo(harness.Verified("FactureTests.FromContext_1_a"));
        await Assert
            .That(second.VerifiedPath)
            .IsEqualTo(harness.Verified("FactureTests.FromContext_1_a.2"));
        await Assert.That(second.Kind).IsEqualTo(SnapshotOutcomeKind.Created);
    }

    [Test]
    public async Task Parallel_checks_of_a_new_snapshot_create_one_file()
    {
        using var harness = new Harness();

        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => harness.Check(new { Total = 1 })))
        );

        await Assert
            .That(outcomes.Count(outcome => outcome.Kind == SnapshotOutcomeKind.Created))
            .IsEqualTo(1);
        await Assert
            .That(outcomes.Count(outcome => outcome.Kind == SnapshotOutcomeKind.Matched))
            .IsEqualTo(7);
    }

    [Test]
    public async Task Rejects_a_snapshot_file_that_is_not_utf8()
    {
        using var harness = new Harness();
        Directory.CreateDirectory(Path.GetDirectoryName(harness.Verified())!);
        await File.WriteAllBytesAsync(harness.Verified(), [0xFF, 0xFE, 0x41]);

        var exception = Assert.Throws<SnapshotException>(() => harness.Check(new { Total = 1 }));

        await Assert.That(exception.Message).Contains("UTF-8");
    }

    [Test]
    public async Task Notes_differences_that_are_only_whitespace()
    {
        using var harness = new Harness();
        harness.Seed("a b", extension: "txt");

        var outcome = harness.Check("a  b");

        await Assert.That(outcome.Kind).IsEqualTo(SnapshotOutcomeKind.Mismatched);
        await Assert.That(outcome.FailureMessage!).Contains("only differences are whitespace");
    }

    [Test]
    public async Task Applies_defaults_before_the_options_of_the_call()
    {
        using var harness = new Harness(defaults: () => new SnapshotOptions().IgnoreMember("Id"));

        var outcome = harness.Check(
            new
            {
                Id = 1,
                Name = "a",
                Secret = "s",
            },
            configure: o => o.ScrubMember("Secret")
        );

        await Assert
            .That(await File.ReadAllTextAsync(outcome.VerifiedPath))
            .IsEqualTo("{\n  \"Name\": \"a\",\n  \"Secret\": \"{Scrubbed}\"\n}\n");
    }

    private sealed class FakeProvider(Func<string, SnapshotTestContext> create)
        : ITestContextProvider
    {
        public string SourceFile { get; set; } = "";

        public SnapshotTestContext? GetCurrent() => create(SourceFile);
    }

    private sealed class Harness : IDisposable
    {
        private readonly TempDirectory _temp = new();

        public Harness(FakeProvider? provider = null, Func<SnapshotOptions>? defaults = null)
        {
            SourceFile = _temp.Combine("FactureTests.cs");
            File.WriteAllText(SourceFile, "// tests");
            provider?.SourceFile = SourceFile;
            Engine = new SnapshotEngine(
                () => Environment,
                new SnapshotRegistry(),
                provider,
                defaults ?? (() => new SnapshotOptions()),
                new SourcePathResolver(File.Exists, () => []),
                Log.Add
            );
        }

        public string SourceFile { get; }

        public SnapshotEngine Engine { get; }

        public SnapshotEnvironment Environment { get; set; } = new(false, false);

        public List<string> Log { get; } = [];

        public SnapshotOutcome Check(
            object? value,
            string? name = null,
            Action<SnapshotOptions>? configure = null
        ) => Engine.Check(value, name, configure, SourceFile, "Calcule");

        public string Verified(string stem = "FactureTests.Calcule", string extension = "json") =>
            _temp.Combine("__snapshots__", $"{stem}.verified.{extension}");

        public string Received(string stem = "FactureTests.Calcule", string extension = "json") =>
            _temp.Combine("__snapshots__", $"{stem}.received.{extension}");

        // A verified file committed by an earlier test run.
        public void Seed(
            string content,
            string stem = "FactureTests.Calcule",
            string extension = "json"
        )
        {
            var path = Verified(stem, extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content + "\n");
        }

        public void Dispose() => _temp.Dispose();
    }
}
