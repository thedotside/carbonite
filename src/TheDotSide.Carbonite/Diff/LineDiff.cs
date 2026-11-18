using System.Globalization;

namespace TheDotSide.Carbonite.Diff;

/// <summary>Line-based unified diff between a verified snapshot and the received value.</summary>
internal static class LineDiff
{
    // Above this many comparisons (16 MB of table), show the first difference instead.
    private const long MaxComparedCells = 4_000_000;

    /// <summary>
    /// Returns a unified diff (<c>--- verified</c> / <c>+++ received</c>) with
    /// <paramref name="contextLines"/> lines of context, cut after <paramref name="maxOutputLines"/>
    /// lines. Returns an empty string when both texts are equal.
    /// </summary>
    internal static string Unified(
        string expected,
        string actual,
        int contextLines = 3,
        int maxOutputLines = 80
    )
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return "";
        }

        var oldLines = expected.Split('\n');
        var newLines = actual.Split('\n');
        var output = new List<string> { "--- verified", "+++ received" };
        var edits = ComputeEdits(oldLines, newLines);
        if (edits is null)
        {
            AddFirstDifference(output, oldLines, newLines);
        }
        else
        {
            AddHunks(output, edits, contextLines);
        }

        return Truncate(output, maxOutputLines);
    }

    /// <summary>True when the two texts become equal once every whitespace character is removed.</summary>
    internal static bool DiffersOnlyByWhitespace(string expected, string actual) =>
        string.Equals(
            WithoutWhitespace(expected),
            WithoutWhitespace(actual),
            StringComparison.Ordinal
        );

    private static string WithoutWhitespace(string text) =>
        string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    private static List<Edit>? ComputeEdits(string[] oldLines, string[] newLines)
    {
        var prefix = 0;
        while (
            prefix < oldLines.Length
            && prefix < newLines.Length
            && Same(oldLines[prefix], newLines[prefix])
        )
        {
            prefix++;
        }

        var suffix = 0;
        while (
            suffix < oldLines.Length - prefix
            && suffix < newLines.Length - prefix
            && Same(oldLines[^(suffix + 1)], newLines[^(suffix + 1)])
        )
        {
            suffix++;
        }

        var oldCount = oldLines.Length - prefix - suffix;
        var newCount = newLines.Length - prefix - suffix;
        if ((long)oldCount * newCount > MaxComparedCells)
        {
            return null;
        }

        var edits = new List<Edit>(oldLines.Length + newLines.Length);
        for (var line = 0; line < prefix; line++)
        {
            edits.Add(new Edit(' ', oldLines[line], line + 1, line + 1));
        }

        AddMiddleEdits(edits, oldLines, newLines, prefix, oldCount, newCount);

        for (var line = suffix; line > 0; line--)
        {
            edits.Add(
                new Edit(
                    ' ',
                    oldLines[^line],
                    oldLines.Length - line + 1,
                    newLines.Length - line + 1
                )
            );
        }

        return edits;
    }

    // Longest common subsequence of the lines between the common prefix and suffix.
    private static void AddMiddleEdits(
        List<Edit> edits,
        string[] oldLines,
        string[] newLines,
        int prefix,
        int oldCount,
        int newCount
    )
    {
        var common = new int[oldCount + 1, newCount + 1];
        for (var x = oldCount - 1; x >= 0; x--)
        {
            for (var y = newCount - 1; y >= 0; y--)
            {
                common[x, y] = Same(oldLines[prefix + x], newLines[prefix + y])
                    ? common[x + 1, y + 1] + 1
                    : Math.Max(common[x + 1, y], common[x, y + 1]);
            }
        }

        int i = 0,
            j = 0;
        while (i < oldCount || j < newCount)
        {
            var oldLine = prefix + i + 1;
            var newLine = prefix + j + 1;
            if (i < oldCount && j < newCount && Same(oldLines[prefix + i], newLines[prefix + j]))
            {
                edits.Add(new Edit(' ', oldLines[prefix + i], oldLine, newLine));
                i++;
                j++;
            }
            else if (j >= newCount || (i < oldCount && common[i + 1, j] >= common[i, j + 1]))
            {
                // Removals come before additions inside a changed block, as in Git.
                edits.Add(new Edit('-', oldLines[prefix + i], oldLine, newLine));
                i++;
            }
            else
            {
                edits.Add(new Edit('+', newLines[prefix + j], oldLine, newLine));
                j++;
            }
        }
    }

    private static void AddHunks(List<string> output, List<Edit> edits, int contextLines)
    {
        var previousEnd = 0;
        var index = 0;
        while (index < edits.Count)
        {
            if (edits[index].Kind == ' ')
            {
                index++;
                continue;
            }

            var start = Math.Max(index - contextLines, previousEnd);
            var cursor = index;
            int end;
            while (true)
            {
                while (cursor < edits.Count && edits[cursor].Kind != ' ')
                {
                    cursor++;
                }

                var runStart = cursor;
                while (cursor < edits.Count && edits[cursor].Kind == ' ')
                {
                    cursor++;
                }

                var run = cursor - runStart;
                if (cursor >= edits.Count || run > 2 * contextLines)
                {
                    end = runStart + Math.Min(run, contextLines);
                    break;
                }
            }

            AddHunk(output, edits, start, end);
            previousEnd = end;
            index = end;
        }
    }

    private static void AddHunk(List<string> output, List<Edit> edits, int start, int end)
    {
        var hunk = edits.GetRange(start, end - start);
        var oldSide = hunk.Where(edit => edit.Kind != '+').ToList();
        var newSide = hunk.Where(edit => edit.Kind != '-').ToList();
        var oldStart = oldSide.Count > 0 ? oldSide[0].OldLine : hunk[0].OldLine - 1;
        var newStart = newSide.Count > 0 ? newSide[0].NewLine : hunk[0].NewLine - 1;

        output.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"@@ -{oldStart},{oldSide.Count} +{newStart},{newSide.Count} @@"
            )
        );
        output.AddRange(hunk.Select(edit => edit.Kind + edit.Text));
    }

    private static void AddFirstDifference(
        List<string> output,
        string[] oldLines,
        string[] newLines
    )
    {
        var line = 0;
        while (
            line < oldLines.Length && line < newLines.Length && Same(oldLines[line], newLines[line])
        )
        {
            line++;
        }

        output.Add(
            string.Create(CultureInfo.InvariantCulture, $"First difference at line {line + 1}:")
        );
        output.Add("- " + (line < oldLines.Length ? oldLines[line] : "(end of the snapshot)"));
        output.Add("+ " + (line < newLines.Length ? newLines[line] : "(end of the value)"));
        output.Add("(The texts differ too much for a line-by-line diff.)");
    }

    private static string Truncate(List<string> output, int maxOutputLines)
    {
        if (output.Count <= maxOutputLines)
        {
            return string.Join('\n', output);
        }

        var cut = output.Count - maxOutputLines;
        return string.Join('\n', output.Take(maxOutputLines))
            + "\n"
            + string.Create(CultureInfo.InvariantCulture, $"... {cut} more diff lines");
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private readonly record struct Edit(char Kind, string Text, int OldLine, int NewLine);
}
