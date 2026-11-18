using System.Collections;
using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Naming;

/// <summary>Turns the arguments of a parameterized test case into a snapshot name suffix.</summary>
internal static class ArgumentFormatter
{
    private const int MaxCollectionItems = 10;

    /// <summary>
    /// Formats the method arguments and the simple class arguments (fixtures are skipped).
    /// Returns <see langword="null"/> when the test has no arguments.
    /// </summary>
    internal static string? FormatCase(
        IReadOnlyList<object?> classArguments,
        IReadOnlyList<object?> methodArguments
    )
    {
        var parts = classArguments
            .Where(IsSimple)
            .Concat(methodArguments.Where(argument => argument is not CancellationToken))
            .Select(Format)
            .ToList();
        if (parts.Count == 0)
        {
            return null;
        }

        var suffix = SnapshotNamer.Sanitize(string.Join('_', parts));
        return suffix.Length == 0 ? "empty" : suffix;
    }

    // Class arguments are often shared fixtures (a web application, a database): only plain
    // values describe the case.
    private static bool IsSimple(object? argument) =>
        argument is null || ScalarFormatter.TryFormat(argument, out _);

    private static string Format(object? argument)
    {
        switch (argument)
        {
            case null:
                return "null";
            case string { Length: 0 }:
                return "empty";
            case string text:
                return text;
        }

        if (ScalarFormatter.TryFormat(argument, out var scalar))
        {
            return scalar.Text;
        }

        if (argument is IEnumerable sequence)
        {
            var items = sequence.Cast<object?>().Take(MaxCollectionItems + 1).ToList();
            var shown = string.Join('-', items.Take(MaxCollectionItems).Select(Format));
            return items.Count > MaxCollectionItems ? shown + "-more" : shown;
        }

        return OverridesToString(argument.GetType())
            ? argument.ToString() ?? ""
            : TypeNameFormatter.Format(argument.GetType());
    }

    private static bool OverridesToString(Type type)
    {
        var declaringType = type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType;
        return declaringType != typeof(object) && declaringType != typeof(ValueType);
    }
}
