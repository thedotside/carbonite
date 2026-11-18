namespace TheDotSide.Carbonite.Serialization;

/// <summary>A readable public property or field, as the snapshot shows it.</summary>
internal sealed record SnapshotMember(string Name, Func<object, object?> Read);
