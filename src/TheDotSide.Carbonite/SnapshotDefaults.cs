namespace TheDotSide.Carbonite;

/// <summary>
/// Options applied to every snapshot of the test run, before the options of each call.
/// Configure them once at startup, for example in a <c>[ModuleInitializer]</c> or a TUnit
/// <c>[Before(TestSession)]</c> hook.
/// </summary>
public static class SnapshotDefaults
{
    internal static SnapshotDefaultsStore Store { get; } = new();

    /// <summary>Adds settings to the defaults. Each call keeps what previous calls configured.</summary>
    public static void Configure(Action<SnapshotOptions> configure) => Store.Configure(configure);
}
