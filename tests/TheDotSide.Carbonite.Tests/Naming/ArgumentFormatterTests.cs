using System.Globalization;
using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.Tests.Naming;

public class ArgumentFormatterTests
{
    public enum Access
    {
        Read,
    }

    private static string? Format(
        IReadOnlyList<object?> classArguments,
        IReadOnlyList<object?> methodArguments
    ) => ArgumentFormatter.FormatCase(classArguments, methodArguments, new SnapshotOptions());

    [Test]
    public async Task Returns_null_without_arguments()
    {
        await Assert.That(Format([], [])).IsNull();
    }

    [Test]
    public async Task Formats_simple_arguments_in_order()
    {
        await Assert.That(Format([], [1, "a b", null, Access.Read])).IsEqualTo("1_a_b_null_Read");
        await Assert
            .That(Format([], [1.5, new DateOnly(2026, 11, 18), true]))
            .IsEqualTo("1.5_2026-11-18_true");
        await Assert.That(Format([], [""])).IsEqualTo("empty");
    }

    [Test]
    public async Task Formats_collections_records_and_other_objects_as_their_content()
    {
        await Assert.That(Format([], [new[] { 1, 2, 3 }])).IsEqualTo("1_2_3");
        await Assert.That(Format([], [new Point(1, 2)])).IsEqualTo("X_1_Y_2");
        await Assert.That(Format([], [new object()])).IsEqualTo("empty");
    }

    [Test]
    public async Task Formats_objects_the_same_way_in_every_culture()
    {
        var price = new Price(
            9.99m,
            new DateOnly(2026, 11, 18),
            new DateTime(2026, 11, 18, 8, 30, 0, DateTimeKind.Utc)
        );
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var invariant = ArgumentFormatter.FormatCase(
            [],
            [price],
            new SnapshotOptions().DontScrubDates()
        );

        foreach (var culture in new[] { "fr-FR", "tr-TR", "ar-SA" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            await Assert
                .That(
                    ArgumentFormatter.FormatCase(
                        [],
                        [price],
                        new SnapshotOptions().DontScrubDates()
                    )
                )
                .IsEqualTo(invariant);
        }

        await Assert
            .That(invariant)
            .IsEqualTo("Amount_9.99_Day_2026-11-18_At_2026-11-18T08_30_00Z");
    }

    [Test]
    public async Task Replaces_unpredictable_values_like_the_snapshot_does()
    {
        await Assert
            .That(Format([], [new Order(Guid.NewGuid(), DateTime.Now), Guid.NewGuid()]))
            .IsEqualTo("Id_Guid_1_At_DateTime_1_Guid_2");
    }

    [Test]
    public async Task Different_arguments_can_share_a_name_and_are_caught_as_conflicts()
    {
        // The engine reports two different values checked against one snapshot.
        await Assert.That(Format([], ["a b"])).IsEqualTo(Format([], ["a_b"]));
    }

    [Test]
    public async Task Keeps_simple_class_arguments_and_skips_fixtures_and_tokens()
    {
        await Assert
            .That(Format(["fr-FR", new Fixture()], [CancellationToken.None, 2]))
            .IsEqualTo("fr-FR_2");
        await Assert.That(Format([new Fixture()], [])).IsNull();
    }

    public sealed record Point(int X, int Y);

    public sealed record Price(decimal Amount, DateOnly Day, DateTime At);

    public sealed record Order(Guid Id, DateTime At);

    public sealed class Fixture
    {
        public string Connection { get; } = "Server=local";
    }
}
