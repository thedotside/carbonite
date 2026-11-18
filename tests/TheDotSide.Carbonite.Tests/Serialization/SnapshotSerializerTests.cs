using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite.Tests.Serialization;

public class SnapshotSerializerTests
{
    internal static string Json(object? value, Action<SnapshotOptions>? configure = null)
    {
        var options = new SnapshotOptions();
        configure?.Invoke(options);
        var snapshot = SnapshotSerializer.Serialize(value, options);
        if (snapshot.Format != SnapshotFormat.Json)
        {
            throw new InvalidOperationException($"Expected JSON, got {snapshot.Format}.");
        }

        return snapshot.Text;
    }

    [Test]
    public async Task Writes_base_members_first_then_properties_then_fields_in_declaration_order()
    {
        var product = new Product
        {
            Id = 1,
            Name = "Chair",
            Price = 49.90m,
        };

        await Assert
            .That(Json(product))
            .IsEqualTo(
                """
                {
                  "Id": 1,
                  "Name": "Chair",
                  "Price": 49.90,
                  "Field": "f"
                }
                """
            );
    }

    [Test]
    public async Task Writes_the_members_of_the_runtime_type()
    {
        var owner = new Owner
        {
            Pet = new Dog { Name = "Rex", GoodBoy = true },
        };

        await Assert
            .That(Json(owner))
            .IsEqualTo(
                """
                {
                  "Pet": {
                    "Name": "Rex",
                    "GoodBoy": true
                  }
                }
                """
            );
    }

    [Test]
    public async Task Writes_records_anonymous_types_and_tuples()
    {
        var value = new
        {
            Person = new Person("Ada", 36),
            Pair = (1, "x"),
            Single = Tuple.Create(true),
            Entry = KeyValuePair.Create("k", 1),
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Person": {
                    "Name": "Ada",
                    "Age": 36
                  },
                  "Pair": {
                    "Item1": 1,
                    "Item2": "x"
                  },
                  "Single": {
                    "Item1": true
                  },
                  "Entry": {
                    "Key": "k",
                    "Value": 1
                  }
                }
                """
            );
    }

    [Test]
    public async Task Writes_nulls()
    {
        await Assert.That(Json(null)).IsEqualTo("null");
        await Assert
            .That(
                Json(
                    new
                    {
                        A = (int?)null,
                        B = (int?)5,
                        C = (string?)null,
                    }
                )
            )
            .IsEqualTo(
                """
                {
                  "A": null,
                  "B": 5,
                  "C": null
                }
                """
            );
    }

    [Test]
    public async Task Sorts_string_keys_ordinally()
    {
        var value = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["b"] = 1,
            ["a"] = 2,
            ["B"] = 3,
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "B": 3,
                  "a": 2,
                  "b": 1
                }
                """
            );
    }

    [Test]
    public async Task Sorts_comparable_keys_by_value()
    {
        var value = new Dictionary<int, string> { [10] = "x", [2] = "y" };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "2": "y",
                  "10": "x"
                }
                """
            );
    }

    [Test]
    public async Task Keeps_the_order_of_scrubbed_guid_keys()
    {
        var value = new Dictionary<Guid, int> { [Guid.NewGuid()] = 1, [Guid.NewGuid()] = 2 };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Guid_1": 1,
                  "Guid_2": 2
                }
                """
            );
    }

    [Test]
    public async Task Sorts_unordered_sets_of_scalars_and_keeps_sorted_sets()
    {
        var value = new
        {
            Hash = new HashSet<string>(StringComparer.Ordinal) { "b", "a" },
            Sorted = new SortedSet<int> { 3, 1, 2 },
            Immutable = ImmutableHashSet.Create(StringComparer.Ordinal, "z", "y"),
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Hash": [
                    "a",
                    "b"
                  ],
                  "Sorted": [
                    1,
                    2,
                    3
                  ],
                  "Immutable": [
                    "y",
                    "z"
                  ]
                }
                """
            );
    }

    [Test]
    public async Task Writes_bytes_as_base64_and_memory_as_arrays()
    {
        var value = new
        {
            Bytes = new byte[] { 1, 2, 3 },
            Memory = new ReadOnlyMemory<int>([4, 5]),
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Bytes": "AQID",
                  "Memory": [
                    4,
                    5
                  ]
                }
                """
            );
    }

    [Test]
    public async Task Writes_multidimensional_arrays_as_nested_arrays()
    {
        var value = new[,]
        {
            { 1, 2 },
            { 3, 4 },
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                [
                  [
                    1,
                    2
                  ],
                  [
                    3,
                    4
                  ]
                ]
                """
            );
    }

    [Test]
    public async Task Writes_groupings_with_their_key()
    {
        var value = new[] { "apple", "avocado", "banana" }.GroupBy(fruit => fruit[0]).First();

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Key": "a",
                  "Items": [
                    "apple",
                    "avocado"
                  ]
                }
                """
            );
    }

    [Test]
    public async Task Writes_lazy_values()
    {
        await Assert
            .That(Json(new { Lazy = new Lazy<int>(() => 42) }))
            .IsEqualTo(
                """
                {
                  "Lazy": 42
                }
                """
            );
    }

    [Test]
    public async Task Writes_json_documents_and_nodes_as_json_keeping_their_order()
    {
        using var document = JsonDocument.Parse("""{"b":1,"a":[true,null,"x",1.50]}""");
        var value = new { Document = document, Node = JsonNode.Parse("""{"x":2}""") };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Document": {
                    "b": 1,
                    "a": [
                      true,
                      null,
                      "x",
                      1.50
                    ]
                  },
                  "Node": {
                    "x": 2
                  }
                }
                """
            );
    }

    [Test]
    public async Task Writes_xml_as_text_with_lf_line_endings()
    {
        var xml = new XElement("root", new XElement("child", "é"));

        await Assert
            .That(Json(new { Xml = xml }))
            .IsEqualTo(
                """
                {
                  "Xml": "<root>\n  <child>é</child>\n</root>"
                }
                """
            );
        var document = SnapshotSerializer.Serialize(new XDocument(xml), new SnapshotOptions());
        await Assert.That(document.Format).IsEqualTo(SnapshotFormat.Text);
        await Assert.That(document.Text).IsEqualTo("<root>\n  <child>é</child>\n</root>");
    }

    [Test]
    public async Task Writes_exceptions_without_stack_traces()
    {
        var captured = Assert.Throws<ArgumentException>(() => Pay(-1));

        await Assert
            .That(Json(captured))
            .IsEqualTo(
                """
                {
                  "$type": "ArgumentException",
                  "Message": "Bad value (Parameter 'amount')",
                  "ParamName": "amount",
                  "InnerException": {
                    "$type": "InvalidOperationException",
                    "Message": "Inner"
                  }
                }
                """
            );
    }

    [Test]
    public async Task Marks_circular_references()
    {
        var root = new Node { Name = "root" };
        root.Children.Add(new Node { Name = "child", Parent = root });

        await Assert
            .That(Json(root))
            .IsEqualTo(
                """
                {
                  "Name": "root",
                  "Parent": null,
                  "Children": [
                    {
                      "Name": "child",
                      "Parent": "{Circular reference}",
                      "Children": []
                    }
                  ]
                }
                """
            );
    }

    [Test]
    public async Task Rejects_structures_deeper_than_64_levels()
    {
        var chain = new Chain();
        for (var level = 0; level < 70; level++)
        {
            chain = new Chain { Next = chain };
        }

        var exception = Assert.Throws<SnapshotException>(() => Json(chain));

        await Assert.That(exception.Message).Contains("64 levels");
        await Assert.That(exception.Message).Contains("$.Next.Next");
    }

    [Test]
    public async Task Rejects_collections_larger_than_10000_items()
    {
        var exception = Assert.Throws<SnapshotException>(() => Json(Enumerable.Range(0, 10_001)));

        await Assert.That(exception.Message).Contains("10000 items");
    }

    [Test]
    public async Task Writes_getter_exceptions_inline()
    {
        await Assert
            .That(Json(new Faulty()))
            .IsEqualTo(
                """
                {
                  "Ok": 1,
                  "Broken": "{Exception: InvalidOperationException: No value}"
                }
                """
            );
    }

    [Test]
    public async Task Writes_placeholders_for_opaque_values_without_reading_them()
    {
        // Reading Task.Result here would block forever.
        var pending = new TaskCompletionSource<int>().Task;
        using var stream = new MemoryStream();
        var value = new
        {
            Pending = pending,
            Callback = (Func<int>)(() => 1),
            Stream = stream,
            Token = CancellationToken.None,
        };

        await Assert
            .That(Json(value))
            .IsEqualTo(
                """
                {
                  "Pending": "{Task<int>}",
                  "Callback": "{Func<int>}",
                  "Stream": "{MemoryStream}",
                  "Token": "{CancellationToken}"
                }
                """
            );
    }

    [Test]
    public async Task Skips_members_that_reflection_cannot_read()
    {
        await Assert
            .That(Json(new WithSpan()))
            .IsEqualTo(
                """
                {
                  "Count": 2
                }
                """
            );
    }

    [Test]
    public async Task Rejects_values_that_must_be_awaited_or_read_first()
    {
        var task = Assert.Throws<SnapshotException>(() => Json(Task.FromResult(1)));
        var valueTask = Assert.Throws<SnapshotException>(() => Json(new ValueTask<int>(1)));
        var sequence = Assert.Throws<SnapshotException>(() => Json(Numbers()));
        using var stream = new MemoryStream();
        var streamError = Assert.Throws<SnapshotException>(() => Json(stream));

        await Assert.That(task.Message).Contains("await");
        await Assert.That(valueTask.Message).Contains("await");
        await Assert.That(sequence.Message).Contains("ToListAsync()");
        await Assert.That(streamError.Message).Contains("StreamReader");
    }

    [Test]
    public async Task Writes_a_root_string_as_normalized_text()
    {
        var snapshot = SnapshotSerializer.Serialize("line 1\r\nline 2\n", new SnapshotOptions());

        await Assert
            .That(snapshot)
            .IsEqualTo(new SerializedSnapshot("line 1\nline 2", SnapshotFormat.Text));
    }

    [Test]
    public async Task Writes_the_same_text_in_every_culture()
    {
        var value = new
        {
            Price = 1234.5m,
            Ratio = 0.25,
            When = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc),
            Day = DayOfWeek.Monday,
        };
        const string expected = """
            {
              "Price": 1234.5,
              "Ratio": 0.25,
              "When": "2026-11-18T00:00:00Z",
              "Day": "Monday"
            }
            """;

        foreach (var culture in new[] { "fr-FR", "tr-TR", "ar-SA", "ja-JP" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            await Assert.That(Json(value, options => options.DontScrubDates())).IsEqualTo(expected);
        }
    }

    private static void Pay(decimal amount) =>
        throw new ArgumentException(
            "Bad value",
            nameof(amount),
            new InvalidOperationException("Inner")
        );

    private static async IAsyncEnumerable<int> Numbers()
    {
        await Task.Yield();
        yield return 1;
    }

    public class Entity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class Product : Entity
    {
        public string Field = "f";

        public decimal Price { get; set; }
    }

    public class Animal
    {
        public string Name { get; set; } = "";
    }

    public sealed class Dog : Animal
    {
        public bool GoodBoy { get; set; }
    }

    public sealed class Owner
    {
        public Animal? Pet { get; set; }
    }

    public sealed record Person(string Name, int Age);

    public sealed class Node
    {
        public string Name { get; set; } = "";

        public Node? Parent { get; set; }

        public IList<Node> Children { get; } = new List<Node>();
    }

    public sealed class Chain
    {
        public Chain? Next { get; set; }
    }

    public sealed class Faulty
    {
        public int Ok => 1;

        public int Broken => throw new InvalidOperationException("No value");
    }

    public sealed class WithSpan
    {
        private readonly int[] _values = [1, 2];

        public Span<int> Values => _values;

        public int Count => _values.Length;
    }
}
