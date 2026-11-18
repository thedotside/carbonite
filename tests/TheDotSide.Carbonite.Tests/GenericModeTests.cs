namespace TheDotSide.Carbonite.Tests;

/// <summary>
/// Real snapshots, committed in __snapshots__: the generic mode that xUnit, NUnit and MSTest
/// users get. On CI these also prove that deterministic build paths are mapped back.
/// </summary>
public class GenericModeTests
{
    [Test]
    public void An_invoice_with_dynamic_values()
    {
        var invoice = new Invoice(
            Guid.NewGuid(),
            "INV-2026-0042",
            DateTime.UtcNow,
            [new InvoiceLine("Chaise ergonomique", 2, 149.90m), new InvoiceLine("Lampe", 1, 39.5m)],
            Status.Sent
        );

        Snapshot.Match(invoice);
    }

    [Test]
    public void A_text_value()
    {
        Snapshot.Match("Bonjour !\nCeci est un snapshot texte, avec des accents : é, à, ç.");
    }

    [Test]
    public void Several_named_snapshots_in_one_test()
    {
        var cart = new List<string> { "Lampe" };
        Snapshot.Match(cart, "before");

        cart.Add("Chaise");
        Snapshot.Match(cart, "after");
    }

    [Test]
    public void Options_for_one_call()
    {
        var user = new
        {
            Name = "Ada",
            Password = "secret",
            Token = "abc123",
        };

        Snapshot.Match(user, options => options.IgnoreMember("Password").ScrubMember("Token"));
    }

    public enum Status
    {
        Draft,
        Sent,
    }

    public sealed record InvoiceLine(string Label, int Quantity, decimal UnitPrice)
    {
        public decimal Total => Quantity * UnitPrice;
    }

    public sealed record Invoice(
        Guid Id,
        string Number,
        DateTime CreatedAt,
        IReadOnlyList<InvoiceLine> Lines,
        Status Status
    )
    {
        public decimal Total => Lines.Sum(line => line.Total);
    }
}
