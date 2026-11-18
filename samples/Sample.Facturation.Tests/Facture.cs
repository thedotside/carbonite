namespace Sample.Facturation.Tests;

/// <summary>Une facture, telle que l'application la produit.</summary>
public sealed record Facture(
    Guid Id,
    string Numero,
    string Client,
    string Pays,
    DateTime CreeeLe,
    IReadOnlyList<LigneDeFacture> Lignes,
    decimal TauxDeTva
)
{
    public decimal TotalHt => Lignes.Sum(ligne => ligne.TotalHt);

    public decimal Tva => decimal.Round(TotalHt * TauxDeTva, 2);

    public decimal TotalTtc => TotalHt + Tva;
}
