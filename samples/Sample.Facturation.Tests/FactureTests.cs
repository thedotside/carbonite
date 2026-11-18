using TheDotSide.Carbonite;
using TheDotSide.Carbonite.TUnit;

namespace Sample.Facturation.Tests;

public class FactureTests
{
    private static Facture UneFacture(string pays = "FR") =>
        Facturation.Creer(
            "FAC-2026-0042",
            "Ada Lovelace",
            pays,
            new LigneDeFacture("Chaise ergonomique", 2, 149.90m),
            new LigneDeFacture("Lampe de bureau", 1, 39.50m)
        );

    // Une ligne suffit : le premier passage crée __snapshots__/FactureTests.Une_facture_complete.verified.json.
    // Id et CreeeLe changent à chaque exécution : Carbonite les remplace par Guid_1 et DateTime_1.
    [Test]
    public void Une_facture_complete()
    {
        Snapshot.Match(UneFacture());
    }

    // Un snapshot par cas : FactureTests.La_TVA_depend_du_pays_FR, _DE et _LU.
    [Test]
    [Arguments("FR")]
    [Arguments("DE")]
    [Arguments("LU")]
    public async Task La_TVA_depend_du_pays(string pays)
    {
        var facture = UneFacture(pays);

        await Assert
            .That(
                new
                {
                    facture.Pays,
                    facture.TotalHt,
                    facture.Tva,
                    facture.TotalTtc,
                }
            )
            .MatchesSnapshot();
    }

    // Une chaîne est stockée telle quelle, dans un fichier .verified.txt.
    [Test]
    public void Le_courriel_de_confirmation()
    {
        Snapshot.Match(Facturation.CourrielDeConfirmation(UneFacture()));
    }

    // Plusieurs snapshots dans un même test : nommez-les.
    [Test]
    public void Une_facture_avant_et_apres_remise()
    {
        var facture = UneFacture();
        Snapshot.Match(facture.Lignes, "avant-remise");

        var remisee = facture with
        {
            Lignes =
            [
                .. facture.Lignes.Select(ligne =>
                    ligne with
                    {
                        PrixUnitaireHt = ligne.PrixUnitaireHt * 0.9m,
                    }
                ),
            ],
        };
        Snapshot.Match(remisee.Lignes, "apres-remise");
    }

    // Masquer ce qui ne concerne pas le test.
    [Test]
    public void Seulement_les_montants()
    {
        Snapshot.Match(
            UneFacture(),
            options =>
                options.IgnoreMember("Lignes").IgnoreMember<Facture>(facture => facture.Client)
        );
    }
}
