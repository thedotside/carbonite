namespace TheDotSide.Carbonite;

/// <summary>
/// A snapshot could not be checked: missing reference on CI, a value that cannot be snapshotted
/// (a <see cref="Task"/> without <c>await</c>, an endless structure…), or an unreachable test file.
/// The message always says what to do next.
/// </summary>
public class SnapshotException : Exception
{
    /// <summary>Creates the exception with an explanation and the action to take.</summary>
    public SnapshotException(string message)
        : base(message) { }

    /// <summary>Creates the exception with an explanation and the error that caused it.</summary>
    public SnapshotException(string message, Exception innerException)
        : base(message, innerException) { }
}
