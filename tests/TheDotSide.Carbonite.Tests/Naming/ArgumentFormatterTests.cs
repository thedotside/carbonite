using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.Tests.Naming;

public class ArgumentFormatterTests
{
    public enum Access
    {
        Read,
    }

    [Test]
    public async Task Returns_null_without_arguments()
    {
        await Assert.That(ArgumentFormatter.FormatCase([], [])).IsNull();
    }

    [Test]
    public async Task Formats_simple_arguments_in_order()
    {
        await Assert
            .That(ArgumentFormatter.FormatCase([], [1, "a b", null, Access.Read]))
            .IsEqualTo("1_a_b_null_Read");
        await Assert
            .That(ArgumentFormatter.FormatCase([], [1.5, new DateOnly(2026, 11, 18), true]))
            .IsEqualTo("1.5_2026-11-18_true");
        await Assert.That(ArgumentFormatter.FormatCase([], [""])).IsEqualTo("empty");
    }

    [Test]
    public async Task Formats_collections_records_and_other_objects()
    {
        await Assert.That(ArgumentFormatter.FormatCase([], [new[] { 1, 2, 3 }])).IsEqualTo("1-2-3");
        await Assert
            .That(ArgumentFormatter.FormatCase([], [new Point(1, 2)]))
            .IsEqualTo("Point_X_1_Y_2");
        await Assert.That(ArgumentFormatter.FormatCase([], [new object()])).IsEqualTo("object");
    }

    [Test]
    public async Task Keeps_simple_class_arguments_and_skips_fixtures_and_tokens()
    {
        await Assert
            .That(
                ArgumentFormatter.FormatCase(["fr-FR", new Fixture()], [CancellationToken.None, 2])
            )
            .IsEqualTo("fr-FR_2");
        await Assert.That(ArgumentFormatter.FormatCase([new Fixture()], [])).IsNull();
    }

    public sealed record Point(int X, int Y);

    public sealed class Fixture;
}
