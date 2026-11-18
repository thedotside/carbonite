using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Naming;

/// <summary>Turns the arguments of a parameterized test case into a snapshot name suffix.</summary>
internal static class ArgumentFormatter
{
    /// <summary>
    /// Formats the method arguments and the simple class arguments (fixtures are skipped), with
    /// the snapshot's own serializer: invariant whatever the culture, and with the same
    /// placeholders, so that a <c>Guid.NewGuid()</c> or a <c>DateTime.Now</c> argument cannot
    /// give the snapshot a new name at every run. Returns <see langword="null"/> without arguments.
    /// </summary>
    internal static string? FormatCase(
        IReadOnlyList<object?> classArguments,
        IReadOnlyList<object?> methodArguments,
        SnapshotOptions options
    )
    {
        var arguments = classArguments
            .Where(IsSimple)
            .Concat(methodArguments.Where(argument => argument is not CancellationToken))
            .ToList();
        if (arguments.Count == 0)
        {
            return null;
        }

        // One numbering for the whole case: two arguments with the same Guid share Guid_1.
        var placeholders = new Placeholders();
        var parts = arguments.Select(argument =>
            argument switch
            {
                null => "null",
                string { Length: 0 } => "empty",
                string text => text,
                _ => SnapshotWriter.WriteCompact(argument, options, placeholders),
            }
        );
        var suffix = SnapshotNamer.Sanitize(string.Join('_', parts));
        return suffix.Length == 0 ? "empty" : suffix;
    }

    // Class arguments are often shared fixtures (a web application, a database): only plain
    // values describe the case.
    private static bool IsSimple(object? argument) =>
        argument is null || ScalarFormatter.TryFormat(argument, out _);
}
