using System.Globalization;
using System.Text;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>
/// Minimal JSON writer with a fixed layout: two-space indentation, LF line endings,
/// <c>"Name": value</c>, empty containers on one line, and readable non-ASCII text.
/// </summary>
internal sealed class JsonTextWriter
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    private readonly StringBuilder _builder = new();
    private readonly Stack<Container> _containers = new();
    private readonly bool _indented;
    private bool _afterPropertyName;

    internal JsonTextWriter(bool indented = true) => _indented = indented;

    internal void StartObject() => Open('{');

    internal void EndObject() => Close('}');

    internal void StartArray() => Open('[');

    internal void EndArray() => Close(']');

    internal void PropertyName(string name)
    {
        BeforeItem(_containers.Peek());
        _builder.Append(Quote(name)).Append(_indented ? ": " : ":");
        _afterPropertyName = true;
    }

    internal void StringValue(string value)
    {
        BeforeValue();
        _builder.Append(Quote(value));
    }

    /// <summary>Writes a literal as is: a number, <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    internal void RawValue(string literal)
    {
        BeforeValue();
        _builder.Append(literal);
    }

    /// <summary>Escapes a string for JSON, keeping every printable character readable.</summary>
    internal static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (
                        char.IsHighSurrogate(character)
                        && index + 1 < value.Length
                        && char.IsLowSurrogate(value[index + 1])
                    )
                    {
                        builder.Append(character).Append(value[++index]);
                    }
                    else if (
                        char.IsSurrogate(character)
                        || char.IsControl(character)
                        || character is LineSeparator or ParagraphSeparator
                    )
                    {
                        // Invisible or invalid on their own: escaped so that the file stays
                        // valid UTF-8 and every difference stays visible in a diff.
                        builder
                            .Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    public override string ToString() => _builder.ToString();

    private void Open(char opening)
    {
        BeforeValue();
        _builder.Append(opening);
        _containers.Push(new Container());
    }

    private void Close(char closing)
    {
        var container = _containers.Pop();
        if (container.Count > 0)
        {
            NewLine();
        }

        _builder.Append(closing);
    }

    private void BeforeValue()
    {
        if (_afterPropertyName)
        {
            _afterPropertyName = false;
        }
        else if (_containers.Count > 0)
        {
            BeforeItem(_containers.Peek());
        }
    }

    private void BeforeItem(Container container)
    {
        if (container.Count > 0)
        {
            _builder.Append(',');
        }

        container.Count++;
        NewLine();
    }

    private void NewLine()
    {
        if (_indented)
        {
            _builder.Append('\n').Append(' ', _containers.Count * 2);
        }
    }

    private sealed class Container
    {
        public int Count { get; set; }
    }
}
