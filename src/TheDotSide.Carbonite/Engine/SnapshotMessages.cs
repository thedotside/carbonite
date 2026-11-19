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
        // PowerShell keeps $env: variables for the session: remove it, even when the tests fail.
        + "      PowerShell:  $env:SNAPSHOT_UPDATE=1; try { dotnet test } finally { Remove-Item Env:SNAPSHOT_UPDATE }";

    internal static string Mismatch(
        string title,
        string expected,
        string actual,
        string verifiedPath,
        string receivedPath,
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

    internal static string Conflict(string title, RegistryEntry conflict, string actual) =>
        $"Snapshot conflict: {title}\n\n"
        + $"This snapshot was already checked earlier in this run with a different value (the first one by {conflict.Owner}). One snapshot file cannot hold both, and SNAPSHOT_UPDATE cannot choose between them.\n\n"
        + LineDiff.Unified(
            conflict.Text,
            actual,
            expectedLabel: "first value",
            actualLabel: "this value"
        )
        + "\n\n"
        + NameEachValue
        + " Otherwise the value is not deterministic: hide what changes with options.IgnoreMember or options.ScrubMember.";

    internal static string CaseCollision(string firstPath, string path) =>
        $"Snapshot name conflict: {Path.GetFileName(path)}\n\n"
        + $"The snapshot names {Path.GetFileName(firstPath)} and {Path.GetFileName(path)} differ only by letter case. Windows and macOS see a single file where Linux sees two, so the snapshots would overwrite each other.\n"
        + "Rename one of the tests or snapshots (the name argument of Snapshot.Match).";

    internal static string InvalidEncoding(string path) =>
        $"The snapshot file {path} is not valid UTF-8. Save it as UTF-8, or delete it so that Carbonite creates it again.";

    internal static string Created(string path) =>
        $"Carbonite created the snapshot {path}. Review it before committing it.";

    internal static string Updated(string path) => $"Carbonite updated the snapshot {path}.";
}
