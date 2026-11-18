using System.Xml.Linq;
using TheDotSide.Carbonite.Text;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>Turns any value into deterministic, culture-invariant snapshot text.</summary>
internal static class SnapshotSerializer
{
    /// <summary>Deepest nesting before the value is considered endless.</summary>
    internal const int MaxDepth = 64;

    /// <summary>Largest collection a snapshot accepts.</summary>
    internal const int MaxItems = 10_000;

    /// <summary>
    /// A string (or XML) is stored as raw text; every other value as indented JSON. Text
    /// scrubbers run on the result.
    /// </summary>
    internal static SerializedSnapshot Serialize(object? value, SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RejectUnreadableRoot(value);

        switch (value)
        {
            case string text:
                return new SerializedSnapshot(Finish(text, options), SnapshotFormat.Text);
            case XNode xml:
                return new SerializedSnapshot(
                    Finish(SnapshotWriter.XmlText(xml), options),
                    SnapshotFormat.Text
                );
            default:
                var writer = new SnapshotWriter(options);
                writer.Write(value);
                return new SerializedSnapshot(
                    Finish(writer.ToString(), options),
                    SnapshotFormat.Json
                );
        }
    }

    private static string Finish(string text, SnapshotOptions options) =>
        TextNormalizer.Normalize(options.ApplyTextScrubbers(TextNormalizer.Normalize(text)));

    // These values have no content of their own yet: snapshotting them is always a mistake.
    private static void RejectUnreadableRoot(object? value)
    {
        if (value is null)
        {
            return;
        }

        var type = value.GetType();
        if (value is Task || ValueKinds.IsValueTask(type))
        {
            var name = value is Task ? ValueKinds.TaskName(type) : TypeNameFormatter.Format(type);
            throw new SnapshotException(
                $"Carbonite received a {name}, not its result. Did you forget 'await'? Write Snapshot.Match(await GetValueAsync())."
            );
        }

        if (value is Stream)
        {
            throw new SnapshotException(
                "Carbonite received a Stream, not its content. Read it first, for example: Snapshot.Match(await new StreamReader(stream).ReadToEndAsync())."
            );
        }

        if (ValueKinds.OnlyAsyncEnumerable(type) is { } sequence)
        {
            throw new SnapshotException(
                $"Carbonite received an {TypeNameFormatter.Format(sequence)}, which can only be read asynchronously. Read it first: Snapshot.Match(await values.ToListAsync())."
            );
        }
    }
}
