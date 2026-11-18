using System.Globalization;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>
/// Numbers unpredictable values in order of appearance: the same value always gets the same
/// placeholder within one snapshot, so relations between values stay visible.
/// </summary>
internal sealed class Placeholders
{
    private readonly Dictionary<Guid, int> _guids = [];
    private readonly Dictionary<DateTime, int> _dateTimes = [];
    private readonly Dictionary<DateTimeOffset, int> _dateTimeOffsets = [];

    internal string For(Guid value) => Name("Guid", _guids, value);

    internal string For(DateTime value) => Name("DateTime", _dateTimes, value);

    internal string For(DateTimeOffset value) => Name("DateTimeOffset", _dateTimeOffsets, value);

    private static string Name<T>(string prefix, Dictionary<T, int> seen, T value)
        where T : notnull
    {
        if (!seen.TryGetValue(value, out var number))
        {
            number = seen.Count + 1;
            seen.Add(value, number);
        }

        return string.Create(CultureInfo.InvariantCulture, $"{prefix}_{number}");
    }
}
