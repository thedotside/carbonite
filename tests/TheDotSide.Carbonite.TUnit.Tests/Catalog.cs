namespace TheDotSide.Carbonite.TUnit.Tests;

/// <summary>A fixture shared by the whole test session, injected through the constructor.</summary>
public sealed class Catalog
{
    public IReadOnlyList<string> Items { get; } = ["Lampe", "Chaise"];
}
