using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Tests;

public class SnapshotDefaultsStoreTests
{
    private static string Json(object value, SnapshotOptions options) =>
        SnapshotSerializer.Serialize(value, options).Text;

    [Test]
    public async Task CreateOptions_starts_from_every_configured_default()
    {
        var store = new SnapshotDefaultsStore();
        store.Configure(options => options.IgnoreMember("A"));
        store.Configure(options => options.DontScrubGuids());

        var json = Json(
            new
            {
                A = 1,
                B = Guid.Empty,
                C = Guid.Parse("8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c"),
            },
            store.CreateOptions()
        );

        await Assert
            .That(json)
            .IsEqualTo(
                """
                {
                  "B": "00000000-0000-0000-0000-000000000000",
                  "C": "8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c"
                }
                """
            );
    }

    [Test]
    public async Task CreateOptions_returns_independent_copies()
    {
        var store = new SnapshotDefaultsStore();
        store.CreateOptions().IgnoreMember("A");

        await Assert.That(Json(new { A = 1 }, store.CreateOptions())).Contains("\"A\": 1");
    }

    [Test]
    public async Task A_failing_configuration_leaves_the_defaults_unchanged()
    {
        var store = new SnapshotDefaultsStore();

        Assert.Throws<ArgumentException>(() =>
            store.Configure(options => options.IgnoreMember("A").Scrub("([", "x"))
        );

        await Assert.That(Json(new { A = 1 }, store.CreateOptions())).Contains("\"A\": 1");
    }

    [Test]
    public async Task Reset_restores_the_built_in_defaults()
    {
        var store = new SnapshotDefaultsStore();
        store.Configure(options => options.IgnoreMember("A"));

        store.Reset();

        await Assert.That(Json(new { A = 1 }, store.CreateOptions())).Contains("\"A\": 1");
    }

    [Test]
    [NotInParallel("SnapshotDefaults")]
    public async Task SnapshotDefaults_Configure_feeds_the_shared_store()
    {
        try
        {
            SnapshotDefaults.Configure(options => options.IgnoreMember("Secret"));

            await Assert
                .That(Json(new { Secret = 1 }, SnapshotDefaults.Store.CreateOptions()))
                .IsEqualTo("{}");
        }
        finally
        {
            SnapshotDefaults.Store.Reset();
        }
    }

    [Test]
    public async Task Configure_rejects_a_null_action()
    {
        Assert.Throws<ArgumentNullException>(() => SnapshotDefaults.Configure(null!));
        await Assert.That(new SnapshotDefaultsStore().CreateOptions()).IsNotNull();
    }
}
