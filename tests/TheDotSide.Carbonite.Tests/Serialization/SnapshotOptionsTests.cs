using System.Globalization;
using System.Text.Json;
using TheDotSide.Carbonite.Serialization;
using static TheDotSide.Carbonite.Tests.Serialization.SnapshotSerializerTests;

namespace TheDotSide.Carbonite.Tests.Serialization;

public class SnapshotOptionsTests
{
    [Test]
    public async Task Replaces_guids_and_dates_with_numbered_placeholders()
    {
        var id = Guid.NewGuid();
        var value = new
        {
            Id = id,
            Other = Guid.NewGuid(),
            Again = id,
            Empty = Guid.Empty,
            Created = DateTime.UtcNow,
            Min = DateTime.MinValue,
            At = DateTimeOffset.Now,
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Id": "Guid_1",
                  "Other": "Guid_2",
                  "Again": "Guid_1",
                  "Empty": "00000000-0000-0000-0000-000000000000",
                  "Created": "DateTime_1",
                  "Min": "0001-01-01T00:00:00",
                  "At": "DateTimeOffset_1"
                }
                """
            );
    }

    [Test]
    public async Task DontScrubGuids_and_DontScrubDates_keep_the_values()
    {
        var value = new
        {
            Id = Guid.Parse("8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c"),
            When = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        await Assert
            .That(Json(value, options => options.DontScrubGuids().DontScrubDates()))
            .IsEqualTo(
                """
                {
                  "Id": "8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c",
                  "When": "2026-01-02T03:04:05Z"
                }
                """
            );
    }

    [Test]
    public async Task IgnoreMember_by_name_removes_members_keys_and_json_properties()
    {
        using var body = JsonDocument.Parse("""{"Password":"j","ok":true}""");
        var value = new
        {
            Password = "secret",
            User = new { Name = "a", Password = "x" },
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Password"] = "p",
                ["Accept"] = "json",
            },
            Body = body,
        };

        await Assert
            .That(Json(value, options => options.IgnoreMember("Password")))
            .IsEqualTo(
                """
                {
                  "User": {
                    "Name": "a"
                  },
                  "Headers": {
                    "Accept": "json"
                  },
                  "Body": {
                    "ok": true
                  }
                }
                """
            );
    }

    [Test]
    public async Task IgnoreMember_by_expression_targets_one_type_and_its_derived_types()
    {
        var value = new
        {
            Invoice = new Invoice { Number = 1, Total = 2 },
            Credit = new CreditNote { Number = 3, Total = 4 },
            Line = new Line { Number = 5 },
        };

        await Assert
            .That(Json(value, options => options.IgnoreMember<Invoice>(invoice => invoice.Number)))
            .IsEqualTo(
                """
                {
                  "Invoice": {
                    "Total": 2
                  },
                  "Credit": {
                    "Total": 4
                  },
                  "Line": {
                    "Number": 5
                  }
                }
                """
            );
    }

    [Test]
    public async Task ScrubMember_keeps_the_member_and_hides_its_value()
    {
        var value = new { Token = "abc", Line = new Line { Number = 7 } };

        await Assert
            .That(
                Json(
                    value,
                    options => options.ScrubMember("Token").ScrubMember<Line>(line => line.Number)
                )
            )
            .IsEqualTo(
                """
                {
                  "Token": "{Scrubbed}",
                  "Line": {
                    "Number": "{Scrubbed}"
                  }
                }
                """
            );
    }

    [Test]
    public async Task Member_expressions_must_be_direct_member_accesses()
    {
        var options = new SnapshotOptions();

        var call = Assert.Throws<ArgumentException>(() =>
            options.IgnoreMember<Invoice>(invoice =>
                invoice.Number.ToString(CultureInfo.InvariantCulture)
            )
        );
        var path = Assert.Throws<ArgumentException>(() =>
            options.ScrubMember<Holder>(holder => holder.Line.Number)
        );

        await Assert.That(call.Message).Contains("x => x.Name");
        await Assert.That(path.Message).Contains("x => x.Name");
    }

    [Test]
    public async Task Member_names_cannot_be_empty()
    {
        var options = new SnapshotOptions();

        Assert.Throws<ArgumentException>(() => options.IgnoreMember(""));
        Assert.Throws<ArgumentNullException>(() => options.ScrubMember(null!));
        await Assert.That(Json(1, o => o.IgnoreMember("A"))).IsEqualTo("1");
    }

    [Test]
    public async Task Scrub_replaces_matches_in_the_final_text()
    {
        var json = Json(
            new { Ref = "INV-2026-0042" },
            options => options.Scrub(@"INV-\d{4}-\d{4}", "INV-XXXX")
        );
        var options = new SnapshotOptions().Scrub(@"\d{2}:\d{2}", "HH:mm");
        var text = SnapshotSerializer.Serialize("Order 42 at 10:15", options);

        await Assert
            .That(json)
            .IsEqualTo(
                """
                {
                  "Ref": "INV-XXXX"
                }
                """
            );
        await Assert.That(text.Text).IsEqualTo("Order 42 at HH:mm");
    }

    [Test]
    public async Task Scrub_rejects_an_invalid_pattern_when_it_is_configured()
    {
        var options = new SnapshotOptions();

        var exception = Assert.Throws<ArgumentException>(() => options.Scrub("([a", "x"));

        await Assert.That(exception.Message).Contains("([a");
    }

    public class Invoice
    {
        public int Number { get; set; }

        public decimal Total { get; set; }
    }

    public sealed class CreditNote : Invoice;

    public sealed class Line
    {
        public int Number { get; set; }
    }

    public sealed class Holder
    {
        public Line Line { get; set; } = new();
    }
}
