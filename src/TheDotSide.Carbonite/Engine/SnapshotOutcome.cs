namespace TheDotSide.Carbonite.Engine;

/// <summary>What happened to a snapshot, with the message to show when the test must fail.</summary>
internal sealed record SnapshotOutcome(
    SnapshotOutcomeKind Kind,
    string VerifiedPath,
    string? ReceivedPath,
    string? FailureMessage
)
{
    internal bool Passed =>
        Kind
            is SnapshotOutcomeKind.Matched
                or SnapshotOutcomeKind.Created
                or SnapshotOutcomeKind.Updated;
}
