namespace TheDotSide.Carbonite.Serialization;

/// <summary>The normalized text of a value and the format it is stored in.</summary>
internal readonly record struct SerializedSnapshot(string Text, SnapshotFormat Format);
