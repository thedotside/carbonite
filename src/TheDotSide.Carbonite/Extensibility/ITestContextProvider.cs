namespace TheDotSide.Carbonite.Extensibility;

/// <summary>Implemented by a test framework adapter, such as TheDotSide.Carbonite.TUnit.</summary>
internal interface ITestContextProvider
{
    /// <summary>The running test, or <see langword="null"/> outside a test.</summary>
    SnapshotTestContext? GetCurrent();
}
