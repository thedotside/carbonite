using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Tests.Serialization;

public class JsonTextWriterTests
{
    [Test]
    public async Task Writes_nested_containers_with_two_space_indentation_and_lf()
    {
        var writer = new JsonTextWriter();
        writer.StartObject();
        writer.PropertyName("Name");
        writer.StringValue("Alice");
        writer.PropertyName("Tags");
        writer.StartArray();
        writer.StringValue("a");
        writer.RawValue("2");
        writer.EndArray();
        writer.PropertyName("Empty");
        writer.StartArray();
        writer.EndArray();
        writer.PropertyName("Nested");
        writer.StartObject();
        writer.EndObject();
        writer.EndObject();

        await Assert
            .That(writer.ToString())
            .IsEqualTo(
                "{\n  \"Name\": \"Alice\",\n  \"Tags\": [\n    \"a\",\n    2\n  ],\n  \"Empty\": [],\n  \"Nested\": {}\n}"
            );
    }

    [Test]
    public async Task Writes_a_single_top_level_literal()
    {
        var writer = new JsonTextWriter();
        writer.RawValue("null");

        await Assert.That(writer.ToString()).IsEqualTo("null");
    }

    [Test]
    public async Task Compact_mode_writes_one_line()
    {
        var writer = new JsonTextWriter(indented: false);
        writer.StartObject();
        writer.PropertyName("A");
        writer.RawValue("1");
        writer.PropertyName("B");
        writer.StartArray();
        writer.RawValue("true");
        writer.StringValue("x");
        writer.EndArray();
        writer.EndObject();

        await Assert.That(writer.ToString()).IsEqualTo("{\"A\":1,\"B\":[true,\"x\"]}");
    }

    [Test]
    [Arguments("plain", "\"plain\"")]
    [Arguments("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [Arguments("back\\slash", "\"back\\\\slash\"")]
    [Arguments("line\nbreak\ttab\rcr", "\"line\\nbreak\\ttab\\rcr\"")]
    [Arguments("\b\f", "\"\\b\\f\"")]
    [Arguments("\u0001\u007f\u0085", "\"\\u0001\\u007f\\u0085\"")]
    [Arguments("café 😀 <&>'", "\"café 😀 <&>'\"")]
    [Arguments("\u2028\u2029", "\"\\u2028\\u2029\"")]
    [Arguments("lone \ud800 high", "\"lone \\ud800 high\"")]
    [Arguments("lone \udc00 low", "\"lone \\udc00 low\"")]
    public async Task Quote_escapes_only_what_json_or_readability_requires(
        string value,
        string expected
    )
    {
        await Assert.That(JsonTextWriter.Quote(value)).IsEqualTo(expected);
    }
}
