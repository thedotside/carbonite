using System.Text;
using TheDotSide.Carbonite.Diff;

namespace TheDotSide.Carbonite.Engine;

/// <summary>Failure messages: what happened, where the files are, and what to do next.</summary>
internal static class SnapshotMessages
{
    private const string NameEachValue =
        "If the test is parameterized, or calls Snapshot.Match in a loop or from a shared helper, give each value its own name, for example Snapshot.Match(value, $\"case-{id}\").";

    private const string AcceptInstructions =
        "If the new value is correct, accept it, then review the change in your pull request:\n"
        + "  - replace the verified file with the received file, or\n"
        + "  - run the tests again with SNAPSHOT_UPDATE=1:\n"
        + "      bash, zsh:   SNAPSHOT_UPDATE=1 dotnet test\n"
        + "      PowerShell:  $env:SNAPSHOT_UPDATE=1; dotnet test";

    internal static string Mismatch(
        string title,
        string expected,
        string actual,
        string verifiedPath,
        string receivedPath,
        RegistryEntry? conflict,
        bool updateIgnoredOnContinuousIntegration
    )
    {
        var message = new StringBuilder()
            .Append("Snapshot mismatch: ")
            .Append(title)
            .Append("\n\nThe value is different from the verified snapshot.\n\n")
            .Append(LineDiff.Unified(expected, actual))
            .Append("\n\n");
        if (LineDiff.DiffersOnlyByWhitespace(expected, actual))
        {
            message.Append(
                "Note: the only differences are whitespace (spaces, tabs or line breaks).\n\n"
            );
        }

        if (conflict is not null)
        {
            message
                .Append(
                    "Note: this snapshot was already checked earlier in this run with a different value (by "
                )
                .Append(conflict.Owner)
                .Append("). ")
                .Append(NameEachValue)
                .Append(" Otherwise the value is not deterministic.\n\n");
        }

        if (updateIgnoredOnContinuousIntegration)
        {
            message.Append(
                "Note: SNAPSHOT_UPDATE is ignored on continuous integration: snapshots are only updated on a developer machine.\n\n"
            );
        }

        return message
            .Append("Verified: ")
            .Append(verifiedPath)
            .Append("\nReceived: ")
            .Append(receivedPath)
            .Append("\n\n")
            .Append(AcceptInstructions)
            .ToString();
    }

    internal static string MissingOnContinuousIntegration(
        string title,
        string verifiedPath,
        string receivedPath
    ) =>
        $"Snapshot missing: {title}\n\n"
        + $"There is no verified snapshot at {verifiedPath}.\n"
        + "On continuous integration, Carbonite never creates snapshots: a missing snapshot usually means that it was not committed.\n"
        + "Run the tests on your machine, review the new file in __snapshots__, then commit it.\n\n"
        + $"The received value was written to {receivedPath}.";

    internal static string FormatChanged(
        string title,
        string verifiedPath,
        string otherVerifiedPath,
        bool isText
    ) =>
        $"Snapshot format changed: {title}\n\n"
        + $"The verified snapshot is {otherVerifiedPath}, but the value is now {(isText ? "a string, stored as text" : "an object, stored as JSON")} in {Path.GetFileName(verifiedPath)}.\n"
        + $"If this change is intended, delete {otherVerifiedPath}, or run the tests again with SNAPSHOT_UPDATE=1 to replace it.";

    internal static string Conflict(string title, RegistryEntry conflict) =>
        $"Snapshot conflict: {title}\n\n"
        + $"Two different values were checked against this snapshot in the same run (the first one by {conflict.Owner}), so SNAPSHOT_UPDATE cannot choose between them.\n"
        + NameEachValue;

    internal static string InvalidEncoding(string path) =>
        $"The snapshot file {path} is not valid UTF-8. Save it as UTF-8, or delete it so that Carbonite creates it again.";

    internal static string Created(string path) =>
        $"Carbonite created the snapshot {path}. Review it before committing it.";

    internal static string Updated(string path) => $"Carbonite updated the snapshot {path}.";
}
