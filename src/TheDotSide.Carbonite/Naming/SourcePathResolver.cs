using System.Collections.Concurrent;
using System.Reflection;

namespace TheDotSide.Carbonite.Naming;

/// <summary>
/// Finds the test source file on disk, including when a deterministic build rewrote its path
/// to <c>/_/…</c>: the path is then searched under the recorded project directories.
/// </summary>
internal sealed class SourcePathResolver
{
    internal const string ProjectDirectoryKey = "TheDotSide.Carbonite.ProjectDirectory";

    private static readonly char[] Separators = ['/', '\\'];

    private readonly Func<string, bool> _fileExists;
    private readonly Func<IEnumerable<string>> _projectDirectories;
    private readonly ConcurrentDictionary<string, string> _resolved = new(StringComparer.Ordinal);

    internal SourcePathResolver(
        Func<string, bool> fileExists,
        Func<IEnumerable<string>> projectDirectories
    )
    {
        _fileExists = fileExists;
        _projectDirectories = projectDirectories;
    }

    internal static SourcePathResolver Default { get; } =
        new(File.Exists, LoadedProjectDirectories);

    internal string Resolve(string sourceFilePath, Assembly? testAssembly)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new SnapshotException(
                "Carbonite needs the path of the test source file to find its __snapshots__ folder, and none was provided. Call Snapshot.Match directly from C# test code."
            );
        }

        if (_fileExists(sourceFilePath))
        {
            return sourceFilePath;
        }

        return _resolved.TryGetValue(sourceFilePath, out var known)
            ? known
            : _resolved.GetOrAdd(sourceFilePath, Search(sourceFilePath, testAssembly));
    }

    /// <summary>The project directories recorded in an assembly by the Carbonite build targets.</summary>
    internal static IEnumerable<string> ProjectDirectoriesOf(Assembly assembly)
    {
        try
        {
            return
            [
                .. assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Where(attribute =>
                        string.Equals(attribute.Key, ProjectDirectoryKey, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(attribute.Value)
                    )
                    .Select(attribute => attribute.Value!),
            ];
        }
        catch (Exception exception)
            when (exception is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            return [];
        }
    }

    private static IEnumerable<string> LoadedProjectDirectories() =>
        AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(ProjectDirectoriesOf);

    // Tries the longest tail of the mapped path first, in each project directory and its parents.
    private string Search(string sourceFilePath, Assembly? testAssembly)
    {
        var segments = sourceFilePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var projects = (testAssembly is null ? [] : ProjectDirectoriesOf(testAssembly))
            .Concat(_projectDirectories())
            .Distinct(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            for (
                var directory = project;
                !string.IsNullOrEmpty(directory);
                directory = Path.GetDirectoryName(directory)
            )
            {
                for (var take = segments.Length; take >= 1; take--)
                {
                    var candidate = Path.Combine([directory, .. segments[^take..]]);
                    if (_fileExists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new SnapshotException(
            $"Carbonite cannot find the test source file '{sourceFilePath}', which tells it where the __snapshots__ folder is.\n"
                + "This happens when the tests were compiled with deterministic paths (ContinuousIntegrationBuild=true) and Carbonite does not know the project directory, or when compiled tests run on a machine without their sources.\n"
                + "Run the tests from the source checkout, and make sure the test project references TheDotSide.Carbonite (directly or through TheDotSide.Carbonite.TUnit) so that its build records the project directory."
        );
    }
}
