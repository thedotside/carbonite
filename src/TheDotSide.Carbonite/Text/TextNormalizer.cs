namespace TheDotSide.Carbonite.Text;

/// <summary>Puts snapshot text in the single form that is stored and compared.</summary>
internal static class TextNormalizer
{
    private const char ByteOrderMark = '\uFEFF';

    /// <summary>
    /// Removes a leading byte order mark, converts CRLF line endings to LF and trims trailing
    /// line feeds, so that Git settings, editors and operating systems cannot change a result.
    /// </summary>
    internal static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var withoutBom = text.Length > 0 && text[0] == ByteOrderMark ? text[1..] : text;
        return withoutBom.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }
}
