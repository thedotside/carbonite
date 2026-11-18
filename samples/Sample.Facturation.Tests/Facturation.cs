using System.Globalization;
using System.Text;

namespace Sample.Facturation.Tests;

/// <summary>Le code « métier » que les tests de l'exemple vérifient.</summary>
public static class Facturation
{
    private static readonly Dictionary<string, decimal> TauxParPays = new(StringComparer.Ordinal)
    {
        ["FR"] = 0.20m,
        ["DE"] = 0.19m,
        ["LU"] = 0.17m,
    };

    public static Facture Creer(
        string numero,
        string client,
        string pays,
        params LigneDeFacture[] lignes
    ) => new(Guid.NewGuid(), numero, client, pays, DateTime.UtcNow, lignes, TauxParPays[pays]);

    /// <summary>
    /// Le texte du courriel envoyé au client. Les montants sont formatés avec la culture
    /// invariante : le format monétaire fr-FR dépend de la version d'ICU du système (espaces
    /// insécables), ce qui rendrait le snapshot différent entre macOS, Linux et Windows.
    /// </summary>
    public static string CourrielDeConfirmation(Facture facture)
    {
        var texte = new StringBuilder()
            .Append("Bonjour ")
            .Append(facture.Client)
            .AppendLine(",")
            .AppendLine()
            .Append("Votre facture ")
            .Append(facture.Numero)
            .AppendLine(" est disponible :");
        foreach (var ligne in facture.Lignes)
        {
            texte.AppendLine(
                CultureInfo.InvariantCulture,
                $"- {ligne.Libelle} x {ligne.Quantite} : {ligne.TotalHt:0.00} EUR"
            );
        }

        return texte
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Total TTC : {facture.TotalTtc:0.00} EUR")
            .ToString();
    }
}
