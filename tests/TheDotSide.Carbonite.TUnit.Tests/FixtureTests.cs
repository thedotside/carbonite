using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.TUnit.Tests;

[ClassDataSource<Catalog>(Shared = SharedType.PerTestSession)]
public class FixtureTests(Catalog catalog)
{
    [Test]
    public async Task A_shared_fixture_does_not_change_the_snapshot_name()
    {
        var context = new TUnitTestContextProvider().GetCurrent()!;

        await Assert.That(context.ClassArguments).Contains(catalog);
        await Assert
            .That(
                ArgumentFormatter.FormatCase(
                    context.ClassArguments,
                    context.MethodArguments,
                    new SnapshotOptions()
                )
            )
            .IsNull();
        Snapshot.Match(catalog.Items);
    }
}
