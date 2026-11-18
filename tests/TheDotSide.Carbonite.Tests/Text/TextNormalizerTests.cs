using TheDotSide.Carbonite.Text;

namespace TheDotSide.Carbonite.Tests.Text;

public class TextNormalizerTests
{
    [Test]
    [Arguments("﻿a\r\nb\n\n", "a\nb")]
    [Arguments("a\r\nb\r\n", "a\nb")]
    [Arguments("a\nb", "a\nb")]
    [Arguments("", "")]
    [Arguments("\n\n", "")]
    public async Task Normalize_removes_bom_crlf_and_trailing_line_feeds(
        string input,
        string expected
    )
    {
        await Assert.That(TextNormalizer.Normalize(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task Normalize_keeps_a_lone_carriage_return_because_it_is_content()
    {
        await Assert.That(TextNormalizer.Normalize("a\rb")).IsEqualTo("a\rb");
    }

    [Test]
    public async Task Normalize_keeps_leading_and_inner_blank_lines()
    {
        await Assert.That(TextNormalizer.Normalize("\n\na\n\nb\n")).IsEqualTo("\n\na\n\nb");
    }
}
