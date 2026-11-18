using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TheDotSide.Carbonite.Naming;

/// <summary>
/// Where a snapshot lives: <c>__snapshots__/&lt;File&gt;.&lt;Test&gt;[_&lt;case&gt;][.&lt;name&gt;][.&lt;n&gt;]</c>
/// next to the test file, with shell-safe ASCII names.
/// </summary>
internal static class SnapshotNamer
{
    internal const string DirectoryName = "__snapshots__";

    internal const int MaxStemLength = 120;

    private const int HashLength = 8;

    /// <summary>The snapshot path without its <c>.verified.json</c> or <c>.verified.txt</c> suffix.</summary>
    internal static string GetStemPath(
        string sourceFilePath,
        string testName,
        string? caseName,
        string? explicitName,
        int occurrence
    )
    {
        var stem = new StringBuilder()
            .Append(Sanitize(Path.GetFileNameWithoutExtension(sourceFilePath)))
            .Append('.')
            .Append(Sanitize(testName));
        if (!string.IsNullOrEmpty(caseName))
        {
            stem.Append('_').Append(Sanitize(caseName));
        }

        if (explicitName is not null)
        {
            var name = Sanitize(explicitName);
            if (name.Length == 0)
            {
                throw new ArgumentException(
                    $"The snapshot name '{explicitName}' has no letter or digit. Use a name such as \"after-payment\".",
                    nameof(explicitName)
                );
            }

            stem.Append('.').Append(name);
        }

        if (occurrence > 1)
        {
            stem.Append('.').Append(occurrence.ToString(CultureInfo.InvariantCulture));
        }

        var directory = Path.Combine(Path.GetDirectoryName(sourceFilePath) ?? "", DirectoryName);
        return Path.Combine(directory, Shorten(stem.ToString()));
    }

    /// <summary>
    /// Keeps <c>A-Z a-z 0-9 . _ -</c>, removes accents, turns everything else into one <c>_</c>,
    /// and trims separators at both ends.
    /// </summary>
    internal static string Sanitize(string segment)
    {
        var decomposed = segment.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var safe = char.IsAsciiLetterOrDigit(character) || character is '.' or '-';
            if (safe)
            {
                builder.Append(character);
            }
            else if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        // A leading '-' reads as an option in a shell, and Windows drops trailing dots.
        return builder.ToString().Trim('_', '.', '-');
    }

    // Long names keep their beginning and get a short hash of the full name, so they stay
    // unique and fit in Windows paths.
    private static string Shorten(string stem)
    {
        if (stem.Length <= MaxStemLength)
        {
            return stem;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stem)));
        var kept = stem[..(MaxStemLength - HashLength - 1)].TrimEnd('_', '.', '-');
        return kept + "_" + hash[..HashLength];
    }
}
