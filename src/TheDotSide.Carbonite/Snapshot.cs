using System.Runtime.CompilerServices;
using TheDotSide.Carbonite.Engine;

namespace TheDotSide.Carbonite;

/// <summary>
/// Snapshot testing in one line: <c>Snapshot.Match(value);</c> compares the value with a file
/// stored next to the test, in <c>__snapshots__</c>.
/// </summary>
public static class Snapshot
{
    /// <summary>
    /// Compares <paramref name="value"/> with its snapshot. The first run on a developer machine
    /// creates the snapshot; later runs fail with a readable diff when the value changes.
    /// </summary>
    /// <param name="value">The value to check. A string is stored as text, anything else as JSON.</param>
    /// <param name="name">Distinguishes several snapshots of one test, for example <c>"after-payment"</c>.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <exception cref="SnapshotMismatchException">The value differs from the snapshot.</exception>
    /// <exception cref="SnapshotException">The snapshot cannot be checked; the message says why.</exception>
    public static void Match(
        object? value,
        string? name = null,
        Action<SnapshotOptions>? configure = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    ) =>
        ThrowIfFailed(
            SnapshotEngine.Default.Check(value, name, configure, sourceFilePath, memberName)
        );

    /// <summary>Compares <paramref name="value"/> with its snapshot, with options for this call.</summary>
    /// <param name="value">The value to check. A string is stored as text, anything else as JSON.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <exception cref="SnapshotMismatchException">The value differs from the snapshot.</exception>
    /// <exception cref="SnapshotException">The snapshot cannot be checked; the message says why.</exception>
    public static void Match(
        object? value,
        Action<SnapshotOptions> configure,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(configure);
        ThrowIfFailed(
            SnapshotEngine.Default.Check(value, null, configure, sourceFilePath, memberName)
        );
    }

    internal static void ThrowIfFailed(SnapshotOutcome outcome)
    {
        if (outcome.Passed)
        {
            return;
        }

        if (outcome.Kind == SnapshotOutcomeKind.Mismatched)
        {
            throw new SnapshotMismatchException(
                outcome.FailureMessage!,
                outcome.VerifiedPath,
                outcome.ReceivedPath!
            );
        }

        throw new SnapshotException(outcome.FailureMessage!);
    }
}
