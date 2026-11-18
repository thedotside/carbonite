using System.Collections.Concurrent;

namespace TheDotSide.Carbonite.Engine;

/// <summary>
/// Remembers the value checked against each snapshot during the test run, to detect two
/// different values sharing one snapshot file (unnamed parameterized cases, loops, helpers).
/// Paths are compared ignoring case: Windows and macOS keep one file where Linux keeps two,
/// so a collision must be refused on every system alike.
/// </summary>
internal sealed class SnapshotRegistry
{
    private readonly ConcurrentDictionary<string, RegistryEntry> _entries = new(
        StringComparer.OrdinalIgnoreCase
    );

    internal static SnapshotRegistry Shared { get; } = new();

    /// <summary>
    /// Records <paramref name="text"/> for the snapshot. Returns the earlier entry when this run
    /// already checked a different value against it, or the same snapshot name with another
    /// letter case; otherwise <see langword="null"/>.
    /// </summary>
    internal RegistryEntry? Record(string verifiedPath, string text, string owner)
    {
        var candidate = new RegistryEntry(text, owner, verifiedPath);
        var first = _entries.GetOrAdd(verifiedPath, candidate);
        if (ReferenceEquals(first, candidate))
        {
            return null;
        }

        var sameName = string.Equals(first.VerifiedPath, verifiedPath, StringComparison.Ordinal);
        var sameValue = string.Equals(first.Text, text, StringComparison.Ordinal);
        return sameName && sameValue ? null : first;
    }
}
