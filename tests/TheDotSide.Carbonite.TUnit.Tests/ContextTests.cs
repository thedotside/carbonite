using System.Globalization;
using TheDotSide.Carbonite.Extensibility;
using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.TUnit.Tests;

public class ContextTests
{
    private static SnapshotTestContext Current() => new TUnitTestContextProvider().GetCurrent()!;

    [Test]
    public async Task The_core_discovers_the_tunit_adapter()
    {
        await Assert.That(TestContextProviders.Discover()).IsTypeOf<TUnitTestContextProvider>();
    }

    [Test]
    public async Task Describes_the_running_test()
    {
        var context = Current();

        await Assert.That(context.TestName).IsEqualTo(nameof(Describes_the_running_test));
        await Assert.That(context.SourceFilePath).EndsWith("ContextTests.cs");
        await Assert.That(context.ClassArguments).IsEmpty();
        await Assert.That(context.MethodArguments).IsEmpty();
        await Assert.That(context.TestAssembly).IsEqualTo(typeof(ContextTests).Assembly);
        await Assert.That(context.Description).Contains("ContextTests");
    }

    [Test]
    [Arguments("fr-FR", 3)]
    public async Task Names_a_case_after_its_arguments(string culture, int count)
    {
        var context = Current();

        await Assert.That(context.MethodArguments).IsEquivalentTo(new object?[] { culture, count });
        await Assert
            .That(
                ArgumentFormatter.FormatCase(
                    context.ClassArguments,
                    context.MethodArguments,
                    new SnapshotOptions()
                )
            )
            .IsEqualTo(string.Create(CultureInfo.InvariantCulture, $"{culture}_{count}"));
    }

    [Test]
    public async Task Numbers_successive_snapshots_of_one_test()
    {
        var first = Current();

        await Assert.That(first.NextOccurrence!("key")).IsEqualTo(1);
        await Assert.That(first.NextOccurrence!("key")).IsEqualTo(2);
        await Assert.That(Current().NextOccurrence!("key")).IsEqualTo(3);
        await Assert.That(first.NextOccurrence!("other")).IsEqualTo(1);
    }

    [Test]
    public async Task Is_empty_outside_a_test()
    {
        Task<SnapshotTestContext?> outside;
        using (ExecutionContext.SuppressFlow())
        {
            outside = Task.Run(() => new TUnitTestContextProvider().GetCurrent());
        }

        await Assert.That(await outside).IsNull();
    }

    [Test]
    public async Task A_helper_method_keeps_the_name_of_the_test()
    {
        await Assert
            .That(TestNameSeenByHelper())
            .IsEqualTo(nameof(A_helper_method_keeps_the_name_of_the_test));
        CheckFromHelper(new { Checked = "from a helper" });
    }

    private static string TestNameSeenByHelper() => Current().TestName;

    private static void CheckFromHelper(object value) => Snapshot.Match(value);
}
