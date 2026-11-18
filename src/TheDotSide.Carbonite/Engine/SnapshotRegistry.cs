using System.Collections.Concurrent;

namespace TheDotSide.Carbonite.Engine;

/// <summary>
/// Remembers the value checked against each snapshot during the test run, to detect two
/// different values sharing one snapshot file (unnamed parameterized cases, loops, helpers).
/// </summary>
internal sealed class SnapshotRegistry
{
    private readonly ConcurrentDictionary<string, RegistryEntry> _entries = new(
        StringComparer.Ordinal
    );

    internal static SnapshotRegistry Shared { get; } = new();

    /// <summary>
    /// Records <paramref name="text"/> for the snapshot. Returns the earlier entry when a
    /// different value was already recorded for it in this run, otherwise <see langword="null"/>.
    /// </summary>
    internal RegistryEntry? Record(string verifiedPath, string text, string owner)
    {
        var candidate = new RegistryEntry(text, owner);
        var first = _entries.GetOrAdd(verifiedPath, candidate);
        return
            ReferenceEquals(first, candidate)
            || string.Equals(first.Text, text, StringComparison.Ordinal)
            ? null
            : first;
    }
}
