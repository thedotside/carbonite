namespace Sample.Facturation.Tests;

/// <summary>Une ligne d'une facture.</summary>
public sealed record LigneDeFacture(string Libelle, int Quantite, decimal PrixUnitaireHt)
{
    public decimal TotalHt => Quantite * PrixUnitaireHt;
}
