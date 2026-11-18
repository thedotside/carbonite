namespace TheDotSide.Carbonite.Engine;

/// <summary>The first value checked against a snapshot in this run, and the test that checked it.</summary>
internal sealed record RegistryEntry(string Text, string Owner);
