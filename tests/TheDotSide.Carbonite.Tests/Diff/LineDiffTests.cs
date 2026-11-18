using TheDotSide.Carbonite.Diff;

namespace TheDotSide.Carbonite.Tests.Diff;

public class LineDiffTests
{
    private static string Lines(params string[] lines) => string.Join('\n', lines);

    [Test]
    public async Task Unified_is_empty_for_equal_texts()
    {
        await Assert.That(LineDiff.Unified("a\nb", "a\nb")).IsEmpty();
    }

    [Test]
    public async Task Unified_shows_a_changed_line_with_three_lines_of_context()
    {
        var expected = Lines(
            "{",
            "  \"A\": 1,",
            "  \"B\": 2,",
            "  \"C\": 3,",
            "  \"Total\": 120.5,",
            "  \"D\": 4,",
            "  \"E\": 5,",
            "  \"F\": 6,",
            "  \"G\": 7",
            "}"
        );
        var actual = expected.Replace("120.5", "121.5", StringComparison.Ordinal);

        var diff = LineDiff.Unified(expected, actual);

        await Assert
            .That(diff)
            .IsEqualTo(
                Lines(
                    "--- verified",
                    "+++ received",
                    "@@ -2,7 +2,7 @@",
                    "   \"A\": 1,",
                    "   \"B\": 2,",
                    "   \"C\": 3,",
                    "-  \"Total\": 120.5,",
                    "+  \"Total\": 121.5,",
                    "   \"D\": 4,",
                    "   \"E\": 5,",
                    "   \"F\": 6,"
                )
            );
    }

    [Test]
    public async Task Unified_shows_an_added_line_at_the_end()
    {
        var diff = LineDiff.Unified(Lines("a", "b"), Lines("a", "b", "c"));

        await Assert
            .That(diff)
            .IsEqualTo(Lines("--- verified", "+++ received", "@@ -1,2 +1,3 @@", " a", " b", "+c"));
    }

    [Test]
    public async Task Unified_shows_a_removed_first_line()
    {
        var diff = LineDiff.Unified(Lines("a", "b", "c"), Lines("b", "c"));

        await Assert
            .That(diff)
            .IsEqualTo(Lines("--- verified", "+++ received", "@@ -1,3 +1,2 @@", "-a", " b", " c"));
    }

    [Test]
    public async Task Unified_uses_separate_hunks_for_distant_changes()
    {
        var expected = Lines(Enumerable.Range(1, 20).Select(i => $"line {i}").ToArray());
        var actual = expected
            .Replace("line 2\n", "line two\n", StringComparison.Ordinal)
            .Replace("line 19\n", "line nineteen\n", StringComparison.Ordinal);

        var diff = LineDiff.Unified(expected, actual);

        await Assert.That(diff).Contains("@@ -1,5 +1,5 @@");
        await Assert.That(diff).Contains("@@ -16,5 +16,5 @@");
        await Assert
            .That(diff.Split('\n').Count(l => l.StartsWith("@@", StringComparison.Ordinal)))
            .IsEqualTo(2);
    }

    [Test]
    public async Task Unified_reports_an_empty_side()
    {
        var diff = LineDiff.Unified("", "a");

        await Assert
            .That(diff)
            .IsEqualTo(Lines("--- verified", "+++ received", "@@ -1,1 +1,1 @@", "-", "+a"));
    }

    [Test]
    public async Task Unified_truncates_long_diffs_and_says_how_many_lines_were_cut()
    {
        var expected = Lines(Enumerable.Range(1, 100).Select(i => $"old {i}").ToArray());
        var actual = Lines(Enumerable.Range(1, 100).Select(i => $"new {i}").ToArray());

        var diff = LineDiff.Unified(expected, actual, maxOutputLines: 10);
        var lines = diff.Split('\n');

        await Assert.That(lines).Count().IsEqualTo(11);
        await Assert.That(lines[^1]).IsEqualTo("... 193 more diff lines");
    }

    [Test]
    public async Task Unified_falls_back_to_the_first_difference_for_huge_rewrites()
    {
        var expected = Lines(Enumerable.Range(1, 3000).Select(i => $"old {i}").ToArray());
        var actual = Lines(Enumerable.Range(1, 3000).Select(i => $"new {i}").ToArray());

        var diff = LineDiff.Unified(expected, actual);

        await Assert.That(diff).Contains("First difference at line 1:");
        await Assert.That(diff).Contains("- old 1");
        await Assert.That(diff).Contains("+ new 1");
    }

    [Test]
    [Arguments("a b", "a  b", true)]
    [Arguments("a\tb", "a b", true)]
    [Arguments("ab", "a b\n", true)]
    [Arguments("a", "b", false)]
    public async Task DiffersOnlyByWhitespace_ignores_every_whitespace_character(
        string expected,
        string actual,
        bool result
    )
    {
        await Assert.That(LineDiff.DiffersOnlyByWhitespace(expected, actual)).IsEqualTo(result);
    }
}
