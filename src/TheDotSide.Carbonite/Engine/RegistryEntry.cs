namespace TheDotSide.Carbonite.Engine;

/// <summary>
/// The first value checked against a snapshot in this run, the test that checked it, and the
/// exact path it used.
/// </summary>
internal sealed record RegistryEntry(string Text, string Owner, string VerifiedPath);
