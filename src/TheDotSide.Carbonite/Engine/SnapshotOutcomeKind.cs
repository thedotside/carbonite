namespace TheDotSide.Carbonite.Engine;

/// <summary>The result of checking a value against its snapshot.</summary>
internal enum SnapshotOutcomeKind
{
    Matched,
    Created,
    Updated,
    Mismatched,
    MissingOnContinuousIntegration,
    FormatChanged,
    Conflict,
}
