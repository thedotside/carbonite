using System.Collections.Concurrent;
using System.Globalization;
using TheDotSide.Carbonite.Extensibility;
using TUnit.Core;

namespace TheDotSide.Carbonite.TUnit;

/// <summary>
/// Tells Carbonite which TUnit test is running. The core finds this type by name: referencing
/// TheDotSide.Carbonite.TUnit is all a test project has to do.
/// </summary>
internal sealed class TUnitTestContextProvider : ITestContextProvider
{
    private const string OccurrencesKey = "TheDotSide.Carbonite.Occurrences";

    public SnapshotTestContext? GetCurrent()
    {
        var context = TestContext.Current;
        if (context is null)
        {
            return null;
        }

        var details = context.Metadata.TestDetails;
        // One counter set per attempt: a retried test names its snapshots like the first attempt.
        var occurrences = context.StateBag.GetOrAdd(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{OccurrencesKey}#{context.Execution.CurrentRetryAttempt}"
            ),
            _ => new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        );

        return new SnapshotTestContext(
            details.TestFilePath ?? "",
            details.MethodName,
            details.TestClassArguments ?? [],
            details.TestMethodArguments ?? [],
            details.ClassType.Assembly,
            details.ClassType.Name + "." + context.Metadata.DisplayName,
            key => occurrences.AddOrUpdate(key, 1, (_, count) => count + 1)
        );
    }
}
