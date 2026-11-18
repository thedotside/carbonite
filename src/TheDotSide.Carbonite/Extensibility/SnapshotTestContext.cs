using System.Reflection;

namespace TheDotSide.Carbonite.Extensibility;

/// <summary>What a test framework adapter knows about the running test.</summary>
/// <param name="SourceFilePath">The file that declares the test, or empty to use the caller's file.</param>
/// <param name="TestName">The test method name.</param>
/// <param name="ClassArguments">The arguments of a parameterized test class (fixtures included).</param>
/// <param name="MethodArguments">The arguments of a parameterized test case.</param>
/// <param name="TestAssembly">The assembly of the test, used to map deterministic build paths.</param>
/// <param name="Description">How messages name the test, for example <c>FactureTests.Calcule(1, 2)</c>.</param>
/// <param name="NextOccurrence">
/// Returns 1, 2, 3… for successive snapshots with the same name in the current test attempt, or
/// <see langword="null"/> when the framework cannot tell tests apart.
/// </param>
internal sealed record SnapshotTestContext(
    string SourceFilePath,
    string TestName,
    IReadOnlyList<object?> ClassArguments,
    IReadOnlyList<object?> MethodArguments,
    Assembly? TestAssembly,
    string Description,
    Func<string, int>? NextOccurrence
);
