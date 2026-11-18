namespace TheDotSide.Carbonite;

/// <summary>Thread-safe holder of the options every snapshot starts from.</summary>
internal sealed class SnapshotDefaultsStore
{
    private readonly Lock _gate = new();
    private SnapshotOptions _template = new();

    /// <summary>
    /// Applies <paramref name="configure"/> to a copy, then publishes it: a configuration that
    /// throws (an invalid pattern, for example) leaves the defaults unchanged.
    /// </summary>
    internal void Configure(Action<SnapshotOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (_gate)
        {
            var next = _template.Clone();
            configure(next);
            _template = next;
        }
    }

    internal SnapshotOptions CreateOptions()
    {
        lock (_gate)
        {
            return _template.Clone();
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _template = new SnapshotOptions();
        }
    }
}
