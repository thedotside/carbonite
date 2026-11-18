using System.Globalization;
using System.Numerics;
using System.Text;
using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Tests.Serialization;

public class ScalarFormatterTests
{
    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    private static Scalar Format(object value)
    {
        if (!ScalarFormatter.TryFormat(value, out var scalar))
        {
            throw new InvalidOperationException($"{value.GetType()} is not a scalar.");
        }

        return scalar;
    }

    public static IEnumerable<Func<(object Value, string Text, bool IsQuoted)>> Cases()
    {
        yield return () => ("café", "café", true);
        yield return () => ('é', "é", true);
        yield return () => (true, "true", false);
        yield return () => (false, "false", false);
        yield return () => (42, "42", false);
        yield return () => (-7L, "-7", false);
        yield return () => ((byte)255, "255", false);
        yield return () => (ulong.MaxValue, "18446744073709551615", false);
        yield return () => (Int128.MaxValue, "170141183460469231731687303715884105727", false);
        yield return () =>
            (
                BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture),
                "123456789012345678901234567890",
                false
            );
        yield return () => (1.5, "1.5", false);
        yield return () => (0.1 + 0.2, "0.30000000000000004", false);
        yield return () => (-0.0, "-0", false);
        yield return () => (1e20, "1E+20", false);
        yield return () => (0.1f, "0.1", false);
        yield return () => ((Half)0.5f, "0.5", false);
        yield return () => (1234567.50m, "1234567.50", false);
        yield return () => (double.NaN, "NaN", true);
        yield return () => (double.PositiveInfinity, "Infinity", true);
        yield return () => (float.NegativeInfinity, "-Infinity", true);
        yield return () => (Access.Read | Access.Write, "Read, Write", true);
        yield return () => ((Access)8, "8", true);
        yield return () =>
            (new DateTime(2026, 11, 18, 14, 30, 0, DateTimeKind.Utc), "2026-11-18T14:30:00Z", true);
        yield return () =>
            (
                new DateTime(2026, 11, 18, 14, 30, 0, 123, DateTimeKind.Utc),
                "2026-11-18T14:30:00.123Z",
                true
            );
        yield return () =>
            (
                new DateTime(2026, 11, 18, 14, 30, 0, DateTimeKind.Local),
                "2026-11-18T14:30:00",
                true
            );
        yield return () => (new DateTime(2026, 11, 18), "2026-11-18T00:00:00", true);
        yield return () =>
            (
                new DateTimeOffset(2026, 11, 18, 14, 30, 0, TimeSpan.FromHours(1)),
                "2026-11-18T14:30:00+01:00",
                true
            );
        yield return () => (new DateOnly(2026, 11, 18), "2026-11-18", true);
        yield return () => (new TimeOnly(14, 30, 5), "14:30:05", true);
        yield return () => (new TimeOnly(14, 30, 5, 250), "14:30:05.25", true);
        yield return () => (new TimeSpan(1, 2, 3, 4, 500), "1.02:03:04.5000000", true);
        yield return () =>
            (
                Guid.Parse("8F1C2B7A-4D3E-4F5A-9B6C-7D8E9F0A1B2C"),
                "8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c",
                true
            );
        yield return () =>
            (new Uri("https://example.com/a?b=c#d"), "https://example.com/a?b=c#d", true);
        yield return () => (new Version(1, 2, 3), "1.2.3", true);
        yield return () => (typeof(List<int>), "List<int>", true);
        yield return () => (new StringBuilder("built"), "built", true);
        yield return () => (CultureInfo.GetCultureInfo("fr-FR"), "fr-FR", true);
        yield return () => (DBNull.Value, "null", false);
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task Formats_scalars_the_same_way_in_every_culture(
        object value,
        string text,
        bool isQuoted
    )
    {
        foreach (var culture in new[] { "fr-FR", "tr-TR", "ar-SA", "" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            await Assert.That(Format(value)).IsEqualTo(new Scalar(text, isQuoted));
        }
    }

    [Test]
    public async Task Does_not_treat_objects_and_collections_as_scalars()
    {
        await Assert.That(ScalarFormatter.TryFormat(new object(), out _)).IsFalse();
        await Assert.That(ScalarFormatter.TryFormat(new List<int>(), out _)).IsFalse();
        await Assert.That(ScalarFormatter.TryFormat(new { A = 1 }, out _)).IsFalse();
    }
}
