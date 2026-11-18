namespace TheDotSide.Carbonite;

/// <summary>
/// The value differs from its verified snapshot. The message shows the diff and how to accept
/// the new value; the received value is saved next to the verified file.
/// </summary>
public sealed class SnapshotMismatchException : SnapshotException
{
    /// <summary>Creates the exception for a value that differs from its snapshot.</summary>
    public SnapshotMismatchException(
        string message,
        string verifiedFilePath,
        string receivedFilePath
    )
        : base(message)
    {
        VerifiedFilePath = verifiedFilePath;
        ReceivedFilePath = receivedFilePath;
    }

    /// <summary>The reference file, committed with the tests.</summary>
    public string VerifiedFilePath { get; }

    /// <summary>The new value, written next to the reference so that it can be reviewed or accepted.</summary>
    public string ReceivedFilePath { get; }
}
