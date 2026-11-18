namespace TheDotSide.Carbonite.TUnit.Tests;

[ClassDataSource<Catalog>(Shared = SharedType.PerTestSession)]
public class FixtureTests(Catalog catalog)
{
    [Test]
    public async Task A_shared_fixture_does_not_change_the_snapshot_name()
    {
        await Assert.That(new TUnitTestContextProvider().GetCurrent()!.CaseName).IsNull();
        Snapshot.Match(catalog.Items);
    }
}
