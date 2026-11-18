using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>Walks an object graph and writes it as deterministic JSON.</summary>
internal sealed class SnapshotWriter
{
    private const string ScrubbedValue = "{Scrubbed}";
    private const string CircularReference = "{Circular reference}";

    // Written first (Message) or never: a stack trace changes with every line of code.
    private static readonly HashSet<string> ExceptionBaseMembers = new(StringComparer.Ordinal)
    {
        nameof(Exception.Message),
        nameof(Exception.StackTrace),
        nameof(Exception.TargetSite),
        nameof(Exception.Source),
        nameof(Exception.HResult),
        nameof(Exception.HelpLink),
        nameof(Exception.Data),
        nameof(Exception.InnerException),
    };

    private readonly SnapshotOptions _options;
    private readonly JsonTextWriter _json;
    private readonly Placeholders _placeholders;
    private readonly HashSet<object> _ancestors = new(ReferenceEqualityComparer.Instance);
    private readonly List<string> _location = ["$"];
    private int _depth;

    internal SnapshotWriter(SnapshotOptions options)
        : this(options, new JsonTextWriter(), new Placeholders()) { }

    private SnapshotWriter(SnapshotOptions options, JsonTextWriter json, Placeholders placeholders)
    {
        _options = options;
        _json = json;
        _placeholders = placeholders;
    }

    private string Location => string.Concat(_location);

    internal void Write(object? value) => WriteValue(value);

    public override string ToString() => _json.ToString();

    /// <summary>XML as indented text with LF line endings, whatever the operating system.</summary>
    internal static string XmlText(XNode node) =>
        node.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

    private void WriteValue(object? value)
    {
        if (value is null)
        {
            _json.RawValue("null");
        }
        else if (TryScrub(value, out var placeholder))
        {
            _json.StringValue(placeholder);
        }
        else if (ScalarFormatter.TryFormat(value, out var scalar))
        {
            if (scalar.IsQuoted)
            {
                _json.StringValue(scalar.Text);
            }
            else
            {
                _json.RawValue(scalar.Text);
            }
        }
        else if (!TryWriteSpecial(value))
        {
            WriteComposite(value);
        }
    }

    private bool TryScrub(object value, [NotNullWhen(true)] out string? placeholder)
    {
        placeholder = value switch
        {
            Guid guid when _options.ScrubGuids && guid != Guid.Empty => _placeholders.For(guid),
            DateTime date
                when _options.ScrubDates
                    && date != DateTime.MinValue
                    && date != DateTime.MaxValue => _placeholders.For(date),
            DateTimeOffset date
                when _options.ScrubDates
                    && date != DateTimeOffset.MinValue
                    && date != DateTimeOffset.MaxValue => _placeholders.For(date),
            _ => null,
        };
        return placeholder is not null;
    }

    private bool TryWriteSpecial(object value)
    {
        switch (value)
        {
            case byte[] bytes:
                _json.StringValue(Convert.ToBase64String(bytes));
                return true;
            case JsonElement element:
                WriteJsonElement(element);
                return true;
            case JsonDocument document:
                WriteJsonElement(document.RootElement);
                return true;
            case JsonNode node:
                using (var parsed = JsonDocument.Parse(node.ToJsonString()))
                {
                    WriteJsonElement(parsed.RootElement);
                }

                return true;
            case XNode xml:
                _json.StringValue(XmlText(xml));
                return true;
            case Assembly assembly:
                _json.StringValue(assembly.GetName().Name ?? "");
                return true;
        }

        if (ValueKinds.OpaqueName(value) is { } opaque)
        {
            _json.StringValue("{" + opaque + "}");
            return true;
        }

        var type = value.GetType();
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(Lazy<>))
        {
            WriteValue(type.GetProperty(nameof(Lazy<>.Value))!.GetValue(value));
            return true;
        }

        if (definition == typeof(Memory<>) || definition == typeof(ReadOnlyMemory<>))
        {
            WriteValue(type.GetMethod(nameof(Memory<>.ToArray))!.Invoke(value, null));
            return true;
        }

        return false;
    }

    private void WriteComposite(object value)
    {
        var tracked = !value.GetType().IsValueType;
        if (tracked && !_ancestors.Add(value))
        {
            _json.StringValue(CircularReference);
            return;
        }

        EnterLevel();
        try
        {
            if (value is Exception exception)
            {
                WriteException(exception);
            }
            else if (ValueKinds.GroupingKey(value.GetType()) is { } key)
            {
                WriteGrouping(value, key);
            }
            else if (TryGetEntries(value, out var entries))
            {
                WriteDictionary(entries);
            }
            else if (value is Array { Rank: > 1 } array)
            {
                CheckCount(array.Length);
                WriteDimension(array, 0, new int[array.Rank]);
            }
            else if (value is IEnumerable sequence)
            {
                WriteSequence(value.GetType(), sequence);
            }
            else
            {
                WriteObject(value);
            }
        }
        finally
        {
            _depth--;
            if (tracked)
            {
                _ancestors.Remove(value);
            }
        }
    }

    private void EnterLevel()
    {
        if (_depth >= SnapshotSerializer.MaxDepth)
        {
            throw new SnapshotException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The value is nested more than {SnapshotSerializer.MaxDepth} levels deep at {Location}. It is probably an endless structure: ignore the member that loops with options.IgnoreMember(\"Name\"), or snapshot a smaller object."
                )
            );
        }

        _depth++;
    }

    private void WriteObject(object value)
    {
        var type = value.GetType();
        _json.StartObject();
        foreach (var member in MemberCache.Get(type))
        {
            WriteMember(type, member.Name, () => member.Read(value));
        }

        _json.EndObject();
    }

    private void WriteException(Exception exception)
    {
        var type = exception.GetType();
        _json.StartObject();
        _json.PropertyName("$type");
        _json.StringValue(TypeNameFormatter.Format(type));
        WriteMember(type, nameof(Exception.Message), () => exception.Message);
        foreach (var member in MemberCache.Get(type))
        {
            if (!ExceptionBaseMembers.Contains(member.Name))
            {
                WriteMember(type, member.Name, () => member.Read(exception));
            }
        }

        if (exception.Data.Count > 0)
        {
            WriteMember(type, nameof(Exception.Data), () => exception.Data);
        }

        // AggregateException lists all of them in InnerExceptions already.
        if (exception.InnerException is { } inner && exception is not AggregateException)
        {
            WriteMember(type, nameof(Exception.InnerException), () => inner);
        }

        _json.EndObject();
    }

    private void WriteMember(Type? ownerType, string name, Func<object?> read)
    {
        var treatment = _options.GetTreatment(ownerType, name);
        if (treatment == MemberTreatment.Ignore)
        {
            return;
        }

        _json.PropertyName(name);
        if (treatment == MemberTreatment.Scrub)
        {
            _json.StringValue(ScrubbedValue);
            return;
        }

        object? value;
        try
        {
            value = read();
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner }
                ? inner
                : exception;
            _json.StringValue(
                $"{{Exception: {TypeNameFormatter.Format(cause.GetType())}: {cause.Message}}}"
            );
            return;
        }

        _location.Add("." + name);
        try
        {
            WriteValue(value);
        }
        finally
        {
            _location.RemoveAt(_location.Count - 1);
        }
    }

    private void WriteGrouping(object grouping, PropertyInfo key)
    {
        _json.StartObject();
        _json.PropertyName("Key");
        WriteValue(key.GetValue(grouping));
        _json.PropertyName("Items");
        WriteSequence(grouping.GetType(), (IEnumerable)grouping);
        _json.EndObject();
    }

    private bool TryGetEntries(
        object value,
        [NotNullWhen(true)] out List<KeyValuePair<object, object?>>? entries
    )
    {
        if (value is IDictionary dictionary)
        {
            entries = [];
            var enumerator = dictionary.GetEnumerator();
            while (enumerator.MoveNext())
            {
                entries.Add(new KeyValuePair<object, object?>(enumerator.Key, enumerator.Value));
                CheckCount(entries.Count);
            }

            return true;
        }

        if (ValueKinds.DictionaryPairType(value.GetType()) is { } pairType)
        {
            var keyProperty = pairType.GetProperty("Key")!;
            var valueProperty = pairType.GetProperty("Value")!;
            entries = [];
            foreach (var pair in (IEnumerable)value)
            {
                entries.Add(
                    new KeyValuePair<object, object?>(
                        keyProperty.GetValue(pair)!,
                        valueProperty.GetValue(pair)
                    )
                );
                CheckCount(entries.Count);
            }

            return true;
        }

        entries = null;
        return false;
    }

    private void WriteDictionary(List<KeyValuePair<object, object?>> entries)
    {
        _json.StartObject();
        foreach (var (key, value) in OrderEntries(entries))
        {
            var name = FormatKey(key);
            var treatment = key is string text
                ? _options.GetTreatment(null, text)
                : MemberTreatment.Keep;
            if (treatment == MemberTreatment.Ignore)
            {
                continue;
            }

            _json.PropertyName(name);
            if (treatment == MemberTreatment.Scrub)
            {
                _json.StringValue(ScrubbedValue);
                continue;
            }

            _location.Add("[" + JsonTextWriter.Quote(name) + "]");
            try
            {
                WriteValue(value);
            }
            finally
            {
                _location.RemoveAt(_location.Count - 1);
            }
        }

        _json.EndObject();
    }

    // Keys are sorted when that is deterministic: strings ordinally, comparable scalars by value.
    // Random keys (scrubbed Guids) and complex keys keep the dictionary's own order.
    private List<KeyValuePair<object, object?>> OrderEntries(
        List<KeyValuePair<object, object?>> entries
    )
    {
        if (entries.Count < 2)
        {
            return entries;
        }

        if (entries.TrueForAll(entry => entry.Key is string))
        {
            return [.. entries.OrderBy(entry => (string)entry.Key, StringComparer.Ordinal)];
        }

        return IsSortableScalar(entries.ConvertAll(entry => (object?)entry.Key))
            ? [.. entries.OrderBy(entry => entry.Key, Comparer<object>.Default)]
            : entries;
    }

    private string FormatKey(object key)
    {
        if (TryScrub(key, out var placeholder))
        {
            return placeholder;
        }

        if (ScalarFormatter.TryFormat(key, out var scalar))
        {
            return scalar.Text;
        }

        var nested = new SnapshotWriter(
            _options,
            new JsonTextWriter(indented: false),
            _placeholders
        );
        nested.Write(key);
        return nested.ToString();
    }

    private void WriteSequence(Type type, IEnumerable sequence)
    {
        var items = new List<object?>();
        foreach (var item in sequence)
        {
            items.Add(item);
            CheckCount(items.Count);
        }

        if (ValueKinds.IsUnorderedSet(type) && IsSortableScalar(items))
        {
            items = [.. items.OrderBy(item => item, Comparer<object?>.Create(CompareScalars))];
        }

        _json.StartArray();
        for (var index = 0; index < items.Count; index++)
        {
            _location.Add(string.Create(CultureInfo.InvariantCulture, $"[{index}]"));
            try
            {
                WriteValue(items[index]);
            }
            finally
            {
                _location.RemoveAt(_location.Count - 1);
            }
        }

        _json.EndArray();
    }

    private bool IsSortableScalar(List<object?> values)
    {
        if (values.Count < 2 || values[0] is null)
        {
            return false;
        }

        var type = values[0]!.GetType();
        if (!values.TrueForAll(value => value is not null && value.GetType() == type))
        {
            return false;
        }

        if (type == typeof(Guid) && _options.ScrubGuids)
        {
            return false;
        }

        return values[0] is IComparable && ScalarFormatter.TryFormat(values[0]!, out _);
    }

    private static int CompareScalars(object? left, object? right) =>
        left is string leftText && right is string rightText
            ? string.CompareOrdinal(leftText, rightText)
            : Comparer<object?>.Default.Compare(left, right);

    private void WriteDimension(Array array, int dimension, int[] indices)
    {
        _json.StartArray();
        for (
            var index = array.GetLowerBound(dimension);
            index <= array.GetUpperBound(dimension);
            index++
        )
        {
            indices[dimension] = index;
            if (dimension == array.Rank - 1)
            {
                WriteValue(array.GetValue(indices));
            }
            else
            {
                WriteDimension(array, dimension + 1, indices);
            }
        }

        _json.EndArray();
    }

    private void WriteJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                EnterLevel();
                _json.StartObject();
                foreach (var property in element.EnumerateObject())
                {
                    WriteMember(null, property.Name, () => property.Value);
                }

                _json.EndObject();
                _depth--;
                break;
            case JsonValueKind.Array:
                EnterLevel();
                _json.StartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteJsonElement(item);
                }

                _json.EndArray();
                _depth--;
                break;
            case JsonValueKind.String:
                _json.StringValue(element.GetString()!);
                break;
            case JsonValueKind.Number:
                _json.RawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                _json.RawValue("true");
                break;
            case JsonValueKind.False:
                _json.RawValue("false");
                break;
            default:
                _json.RawValue("null");
                break;
        }
    }

    private void CheckCount(int count)
    {
        if (count > SnapshotSerializer.MaxItems)
        {
            throw new SnapshotException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The collection at {Location} has more than {SnapshotSerializer.MaxItems} items. Snapshot a smaller part of it (for example .Take(100)), or check that it is not endless."
                )
            );
        }
    }
}
