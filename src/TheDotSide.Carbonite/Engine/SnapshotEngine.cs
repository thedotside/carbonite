using System.Collections.Concurrent;
using System.Text;
using TheDotSide.Carbonite.Extensibility;
using TheDotSide.Carbonite.Naming;
using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Engine;

/// <summary>Checks a value against its snapshot file: create, compare, update or explain.</summary>
internal sealed class SnapshotEngine
{
    // Serializes the work on one snapshot file inside the process (parallel tests, retries).
    // Ignoring case like the registry: on Windows and macOS two such names are one file.
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly Lazy<SnapshotEngine> Shared = new(() =>
        new SnapshotEngine(
            SnapshotEnvironment.FromProcess,
            SnapshotRegistry.Shared,
            TestContextProviders.Discover(),
            SnapshotDefaults.Store.CreateOptions,
            SourcePathResolver.Default,
            Console.Out.WriteLine
        )
    );

    private readonly Func<SnapshotEnvironment> _environment;
    private readonly SnapshotRegistry _registry;
    private readonly ITestContextProvider? _provider;
    private readonly Func<SnapshotOptions> _defaults;
    private readonly SourcePathResolver _resolver;
    private readonly Action<string> _log;

    internal SnapshotEngine(
        Func<SnapshotEnvironment> environment,
        SnapshotRegistry registry,
        ITestContextProvider? provider,
        Func<SnapshotOptions> defaults,
        SourcePathResolver resolver,
        Action<string> log
    )
    {
        _environment = environment;
        _registry = registry;
        _provider = provider;
        _defaults = defaults;
        _resolver = resolver;
        _log = log;
    }

    internal static SnapshotEngine Default => Shared.Value;

    internal SnapshotOutcome Check(
        object? value,
        string? name,
        Action<SnapshotOptions>? configure,
        string callerFilePath,
        string callerMemberName
    )
    {
        var options = _defaults();
        configure?.Invoke(options);
        var snapshot = SnapshotSerializer.Serialize(value, options);

        var context = _provider?.GetCurrent();
        var sourceFile = string.IsNullOrEmpty(context?.SourceFilePath)
            ? callerFilePath
            : context.SourceFilePath;
        var testName = context?.TestName ?? callerMemberName;
        var source = _resolver.Resolve(sourceFile, context?.TestAssembly);
        // Case names follow the options of the snapshot: invariant, with the same placeholders.
        var caseName = context is null
            ? null
            : ArgumentFormatter.FormatCase(
                context.ClassArguments,
                context.MethodArguments,
                options
            );
        var firstStem = SnapshotNamer.GetStemPath(source, testName, caseName, name, 1);
        var occurrence = context?.NextOccurrence?.Invoke(firstStem) ?? 1;
        var stem =
            occurrence == 1
                ? firstStem
                : SnapshotNamer.GetStemPath(source, testName, caseName, name, occurrence);

        var files = SnapshotFiles.For(stem, snapshot.Format);
        var title = Path.GetFileName(stem);
        var owner = context?.Description ?? title;
        var environment = _environment();
        lock (FileLocks.GetOrAdd(files.Verified, _ => new Lock()))
        {
            return Compare(snapshot, files, title, owner, environment);
        }
    }

    private SnapshotOutcome Compare(
        SerializedSnapshot snapshot,
        SnapshotFiles files,
        string title,
        string owner,
        SnapshotEnvironment environment
    )
    {
        var actual = snapshot.Text;
        // Checked before the file: two values (or two letter cases) sharing one snapshot must
        // fail whatever the order of the cases, even when this value matches the file.
        if (_registry.Record(files.Verified, actual, owner) is { } conflict)
        {
            var message = string.Equals(
                conflict.VerifiedPath,
                files.Verified,
                StringComparison.Ordinal
            )
                ? SnapshotMessages.Conflict(title, conflict, actual)
                : SnapshotMessages.CaseCollision(conflict.VerifiedPath, files.Verified);
            return Failed(SnapshotOutcomeKind.Conflict, files, receivedPath: null, message);
        }

        var canUpdate = environment.UpdateRequested && !environment.IsContinuousIntegration;
        var expected = Read(files.Verified);

        if (expected is null)
        {
            if (File.Exists(files.OtherVerified))
            {
                if (!canUpdate)
                {
                    return Failed(
                        SnapshotOutcomeKind.FormatChanged,
                        files,
                        receivedPath: null,
                        SnapshotMessages.FormatChanged(
                            title,
                            files.Verified,
                            files.OtherVerified,
                            snapshot.Format == SnapshotFormat.Text
                        )
                    );
                }

                // Write the new file before removing the old one: a failure loses nothing.
                var replaced = Write(files, actual, SnapshotOutcomeKind.Updated);
                SnapshotFile.DeleteIfExists(files.OtherVerified);
                return replaced;
            }

            if (environment.IsContinuousIntegration)
            {
                SnapshotFile.WriteAtomic(files.Received, actual);
                return Failed(
                    SnapshotOutcomeKind.MissingOnContinuousIntegration,
                    files,
                    files.Received,
                    SnapshotMessages.MissingOnContinuousIntegration(
                        title,
                        files.Verified,
                        files.Received
                    )
                );
            }

            if (SnapshotFile.TryCreateNew(files.Verified, actual))
            {
                DeleteReceived(files);
                _log(SnapshotMessages.Created(files.Verified));
                return new SnapshotOutcome(SnapshotOutcomeKind.Created, files.Verified, null, null);
            }

            // Another test process (the same tests on another target framework) created it.
            expected = Read(files.Verified) ?? "";
        }

        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            DeleteReceived(files);
            return new SnapshotOutcome(SnapshotOutcomeKind.Matched, files.Verified, null, null);
        }

        if (canUpdate)
        {
            return Write(files, actual, SnapshotOutcomeKind.Updated);
        }

        SnapshotFile.WriteAtomic(files.Received, actual);
        return Failed(
            SnapshotOutcomeKind.Mismatched,
            files,
            files.Received,
            SnapshotMessages.Mismatch(
                title,
                expected,
                actual,
                files.Verified,
                files.Received,
                environment.UpdateRequested && environment.IsContinuousIntegration
            )
        );
    }

    private SnapshotOutcome Write(SnapshotFiles files, string actual, SnapshotOutcomeKind kind)
    {
        SnapshotFile.WriteAtomic(files.Verified, actual);
        DeleteReceived(files);
        _log(SnapshotMessages.Updated(files.Verified));
        return new SnapshotOutcome(kind, files.Verified, null, null);
    }

    private static SnapshotOutcome Failed(
        SnapshotOutcomeKind kind,
        SnapshotFiles files,
        string? receivedPath,
        string message
    ) => new(kind, files.Verified, receivedPath, message);

    private static void DeleteReceived(SnapshotFiles files)
    {
        SnapshotFile.DeleteIfExists(files.Received);
        SnapshotFile.DeleteIfExists(files.OtherReceived);
    }

    private static string? Read(string path)
    {
        try
        {
            return SnapshotFile.ReadNormalized(path);
        }
        catch (DecoderFallbackException exception)
        {
            throw new SnapshotException(SnapshotMessages.InvalidEncoding(path), exception);
        }
    }

    private sealed record SnapshotFiles(
        string Verified,
        string Received,
        string OtherVerified,
        string OtherReceived
    )
    {
        internal static SnapshotFiles For(string stem, SnapshotFormat format)
        {
            var (extension, other) =
                format == SnapshotFormat.Json ? (".json", ".txt") : (".txt", ".json");
            return new SnapshotFiles(
                stem + ".verified" + extension,
                stem + ".received" + extension,
                stem + ".verified" + other,
                stem + ".received" + other
            );
        }
    }
}
