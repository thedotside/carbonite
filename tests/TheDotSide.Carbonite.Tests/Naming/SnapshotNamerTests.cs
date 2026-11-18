using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.Tests.Naming;

public class SnapshotNamerTests
{
    private static readonly string SourceFile = Path.Combine(
        Path.GetTempPath(),
        "repo",
        "tests",
        "FactureTests.cs"
    );

    private static string Stem(string name) =>
        Path.Combine(Path.GetDirectoryName(SourceFile)!, "__snapshots__", name);

    [Test]
    [Arguments("Calcule_la_facture_été", "Calcule_la_facture_ete")]
    [Arguments("a/b:c*d", "a_b_c_d")]
    [Arguments("  spaced  name ", "spaced_name")]
    [Arguments("trailing.", "trailing")]
    [Arguments(".hidden", "hidden")]
    [Arguments("Ça marche !", "Ca_marche")]
    [Arguments("v1.2-beta", "v1.2-beta")]
    [Arguments("emoji 😀 here", "emoji_here")]
    public async Task Sanitize_keeps_shell_safe_ascii(string segment, string expected)
    {
        await Assert.That(SnapshotNamer.Sanitize(segment)).IsEqualTo(expected);
    }

    [Test]
    public async Task GetStemPath_combines_file_test_case_name_and_occurrence()
    {
        await Assert
            .That(SnapshotNamer.GetStemPath(SourceFile, "Calcule", null, null, 1))
            .IsEqualTo(Stem("FactureTests.Calcule"));
        await Assert
            .That(SnapshotNamer.GetStemPath(SourceFile, "Calcule", "1_a", null, 1))
            .IsEqualTo(Stem("FactureTests.Calcule_1_a"));
        await Assert
            .That(SnapshotNamer.GetStemPath(SourceFile, "Calcule", null, "après paiement", 1))
            .IsEqualTo(Stem("FactureTests.Calcule.apres_paiement"));
        await Assert
            .That(SnapshotNamer.GetStemPath(SourceFile, "Calcule", null, null, 2))
            .IsEqualTo(Stem("FactureTests.Calcule.2"));
        await Assert
            .That(SnapshotNamer.GetStemPath(SourceFile, "Calcule", "x", "etape", 3))
            .IsEqualTo(Stem("FactureTests.Calcule_x.etape.3"));
    }

    [Test]
    public async Task GetStemPath_shortens_long_names_and_keeps_them_unique()
    {
        var first = Path.GetFileName(
            SnapshotNamer.GetStemPath(SourceFile, "Test", new string('a', 200), null, 1)
        );
        var second = Path.GetFileName(
            SnapshotNamer.GetStemPath(SourceFile, "Test", new string('a', 199) + "b", null, 1)
        );

        await Assert.That(first.Length).IsEqualTo(SnapshotNamer.MaxStemLength);
        await Assert.That(first).Matches("^FactureTests\\.Test_a+_[0-9a-f]{8}$");
        await Assert.That(second).IsNotEqualTo(first);
    }

    [Test]
    public async Task GetStemPath_rejects_a_name_without_letters_or_digits()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SnapshotNamer.GetStemPath(SourceFile, "Test", null, "???", 1)
        );

        await Assert.That(exception.Message).Contains("???");
    }
}
