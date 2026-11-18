using System.Text;
using TheDotSide.Carbonite.Engine;
using TheDotSide.Carbonite.Tests.Support;

namespace TheDotSide.Carbonite.Tests.Engine;

public class SnapshotFileTests
{
    [Test]
    public async Task ReadNormalized_returns_null_for_a_missing_file()
    {
        using var temp = new TempDirectory();

        await Assert.That(SnapshotFile.ReadNormalized(temp.Combine("missing.txt"))).IsNull();
    }

    [Test]
    public async Task ReadNormalized_ignores_a_bom_and_windows_line_endings()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("windows.verified.txt");
        await File.WriteAllBytesAsync(
            path,
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("é\r\nb\r\n")]
        );

        await Assert.That(SnapshotFile.ReadNormalized(path)).IsEqualTo("é\nb");
    }

    [Test]
    public async Task ReadNormalized_waits_for_a_file_that_is_still_being_created()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("creating.verified.txt");
        await File.WriteAllTextAsync(path, "");
        var writer = Task.Run(async () =>
        {
            await Task.Delay(100);
            await File.WriteAllTextAsync(path, "done\n");
        });

        var content = SnapshotFile.ReadNormalized(path);

        await writer;
        await Assert.That(content).IsEqualTo("done");
    }

    [Test]
    public async Task WriteAtomic_writes_utf8_without_bom_with_one_final_line_feed()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("__snapshots__", "nested", "value.verified.txt");

        SnapshotFile.WriteAtomic(path, "café\nb");

        var bytes = await File.ReadAllBytesAsync(path);
        await Assert.That(bytes).IsEquivalentTo(Encoding.UTF8.GetBytes("café\nb\n"));
    }

    [Test]
    public async Task WriteAtomic_replaces_an_existing_file_and_leaves_no_temporary_file()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("value.verified.txt");
        SnapshotFile.WriteAtomic(path, "old");

        SnapshotFile.WriteAtomic(path, "new");

        await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("new\n");
        await Assert.That(Directory.GetFiles(temp.Path)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TryCreateNew_keeps_the_existing_file()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("value.verified.txt");
        await Assert.That(SnapshotFile.TryCreateNew(path, "first")).IsTrue();

        var created = SnapshotFile.TryCreateNew(path, "second");

        await Assert.That(created).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("first\n");
        await Assert.That(Directory.GetFiles(temp.Path)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TryCreateNew_from_many_threads_creates_exactly_one_file()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("race.verified.txt");

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 16)
                .Select(i => Task.Run(() => SnapshotFile.TryCreateNew(path, $"value {i}")))
        );

        await Assert.That(results.Count(created => created)).IsEqualTo(1);
        await Assert.That(Directory.GetFiles(temp.Path)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DeleteIfExists_accepts_a_missing_file()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("value.received.txt");
        SnapshotFile.DeleteIfExists(path);
        await File.WriteAllTextAsync(path, "x");

        SnapshotFile.DeleteIfExists(path);

        await Assert.That(File.Exists(path)).IsFalse();
    }
}
