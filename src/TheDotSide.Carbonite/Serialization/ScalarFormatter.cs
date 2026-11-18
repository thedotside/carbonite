using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>Culture-invariant text of the values a snapshot shows as a single JSON value.</summary>
internal static class ScalarFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats <paramref name="value"/> when it is a scalar (string, number, date, enum, Guid…);
    /// returns <see langword="false"/> for objects and collections.
    /// </summary>
    internal static bool TryFormat(object value, out Scalar scalar)
    {
        scalar = value switch
        {
            string text => Quoted(text),
            char character => Quoted(character.ToString()),
            Rune rune => Quoted(rune.ToString()),
            bool boolean => Literal(boolean ? "true" : "false"),
            Enum enumeration => Quoted(FormatEnum(enumeration)),
            double number => FormatFloatingPoint(number, double.IsFinite(number)),
            float number => FormatFloatingPoint(number, float.IsFinite(number)),
            Half number => FormatFloatingPoint(number, Half.IsFinite(number)),
            decimal number => Literal(number.ToString(Invariant)),
            sbyte or byte or short or ushort or int or uint or long or ulong => Literal(
                ((IFormattable)value).ToString(null, Invariant)
            ),
            nint or nuint or Int128 or UInt128 or BigInteger => Literal(
                ((IFormattable)value).ToString(null, Invariant)
            ),
            DateTime dateTime => Quoted(FormatDateTime(dateTime)),
            DateTimeOffset dateTimeOffset => Quoted(
                dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", Invariant)
            ),
            DateOnly date => Quoted(date.ToString("yyyy-MM-dd", Invariant)),
            TimeOnly time => Quoted(time.ToString("HH:mm:ss.FFFFFFF", Invariant)),
            TimeSpan duration => Quoted(duration.ToString("c", Invariant)),
            Guid guid => Quoted(guid.ToString("D", Invariant)),
            Uri uri => Quoted(uri.OriginalString),
            Version version => Quoted(version.ToString()),
            Type type => Quoted(TypeNameFormatter.Format(type)),
            StringBuilder builder => Quoted(builder.ToString()),
            CultureInfo culture => Quoted(culture.Name),
            TimeZoneInfo timeZone => Quoted(timeZone.Id),
            Encoding encoding => Quoted(encoding.WebName),
            IPAddress address => Quoted(address.ToString()),
            DBNull => Literal("null"),
            _ => default,
        };

        return scalar.Text is not null;
    }

    /// <summary>
    /// UTC dates end with <c>Z</c>. Local dates are written without their offset: it belongs to
    /// the machine that ran the test, not to the value.
    /// </summary>
    internal static string FormatDateTime(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", Invariant)
            : value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", Invariant);

    private static string FormatEnum(Enum value) => Enum.Format(value.GetType(), value, "G");

    // NaN and infinities are not JSON numbers: they are written as strings.
    private static Scalar FormatFloatingPoint(IFormattable number, bool isFinite) =>
        isFinite
            ? Literal(number.ToString("R", Invariant))
            : Quoted(number.ToString(null, Invariant));

    private static Scalar Quoted(string text) => new(text, IsQuoted: true);

    private static Scalar Literal(string text) => new(text, IsQuoted: false);
}
