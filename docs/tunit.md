# Carbonite avec TUnit

Cette page décrit l'adaptateur TUnit : l'assertion `MatchesSnapshot()`, le nom des snapshots dans les tests paramétrés, plusieurs snapshots par test, `[Retry]` et `[Repeat]`.

## Rien à configurer

```bash
dotnet add package TheDotSide.Carbonite.TUnit
```

C'est tout. Au premier snapshot, le cœur de Carbonite cherche l'adaptateur par son nom et l'utilise s'il est présent. Pas d'attribut, pas de hook, pas de ligne de configuration.

Ce que l'adaptateur change par rapport au [mode générique](autres-frameworks.md) :

| | Avec l'adaptateur TUnit | Mode générique (xUnit, NUnit, MSTest) |
| --- | --- | --- |
| Dossier `__snapshots__` | à côté du fichier qui déclare le test | à côté du fichier qui appelle `Snapshot.Match` |
| Nom du test | la méthode de test TUnit | la méthode qui appelle `Snapshot.Match` |
| Test paramétré | un fichier par cas, nommé d'après les arguments | un nom à fournir pour chaque cas |
| Plusieurs snapshots sans nom dans un test | numérotés `.2`, `.3`… | un nom à fournir pour chacun |
| `await Assert.That(valeur).MatchesSnapshot()` | oui | non |

Conditions :

- le projet de test utilise TUnit 1.40 ou plus récent ;
- l'adaptateur dépend de `TUnit.Core` et `TUnit.Assertions` à partir de 1.40.0, et du paquet cœur dans exactement la même version que lui. Référencez seulement `TheDotSide.Carbonite.TUnit` : il apporte le bon cœur.

Si l'adaptateur ne peut pas être chargé, Carbonite le dit sur la sortie d'erreur et continue en mode générique :

```text
Carbonite: the TUnit adapter could not be loaded (<raison>). Snapshots use the generic mode.
```

## Deux façons d'écrire la vérification

```csharp
using TheDotSide.Carbonite;
using TheDotSide.Carbonite.TUnit;

// 1. L'appel direct, synchrone : il lève une exception en cas d'écart.
Snapshot.Match(facture);

// 2. L'assertion TUnit : à attendre avec await, comme toute assertion TUnit.
await Assert.That(facture).MatchesSnapshot();
```

Les deux écrivent le même fichier, avec les mêmes règles. L'exemple `samples/Sample.Facturation.Tests` utilise les deux. Préférez l'assertion pour regrouper plusieurs vérifications avec `Assert.Multiple()` ou pour vérifier le résultat d'une tâche.

## L'assertion MatchesSnapshot()

Elle accepte un nom, des options, ou les deux. Ces exemples viennent des tests du dépôt :

```csharp
await Assert.That(new { Total = 42 }).MatchesSnapshot();

await Assert
    .That(new { Id = 7, Name = "Ada" })
    .MatchesSnapshot("without-id", options => options.IgnoreMember("Id"));
```

Le second crée `AssertionTests.Takes_a_name_and_options.without-id.verified.json` :

```json
{
  "Name": "Ada"
}
```

Les quatre formes possibles :

| Appel | Effet |
| --- | --- |
| `MatchesSnapshot()` | nom déduit du test |
| `MatchesSnapshot("nom")` | ajoute `.nom` au nom du fichier |
| `MatchesSnapshot(options => …)` | options pour cette vérification (voir [Valeurs dynamiques](valeurs-dynamiques.md)) |
| `MatchesSnapshot("nom", options => …)` | les deux |

Un écart devient un échec d'assertion TUnit, avec le message de Carbonite. Exemple réel, après le changement de prix de la lampe décrit dans [Cycle de vie](cycle-de-vie.md#3-la-valeur-est-différente) (chemins raccourcis) :

```text
Expected to match its snapshot
but Snapshot mismatch: FactureTests.La_TVA_depend_du_pays_FR

The value is different from the verified snapshot.

--- verified
+++ received
@@ -1,6 +1,6 @@
 {
   "Pays": "FR",
-  "TotalHt": 339.30,
-  "Tva": 67.86,
-  "TotalTtc": 407.16
+  "TotalHt": 341.80,
+  "Tva": 68.36,
+  "TotalTtc": 410.16
 }

Verified: …/__snapshots__/FactureTests.La_TVA_depend_du_pays_FR.verified.json
Received: …/__snapshots__/FactureTests.La_TVA_depend_du_pays_FR.received.json

If the new value is correct, accept it, then review the change in your pull request:
  - replace the verified file with the received file, or
  - run the tests again with SNAPSHOT_UPDATE=1:
      bash, zsh:   SNAPSHOT_UPDATE=1 dotnet test
      PowerShell:  $env:SNAPSHOT_UPDATE=1; dotnet test

at Assert.That(new { … }).MatchesSnapshot()
```

### Avec Assert.Multiple()

L'assertion se comporte comme les autres assertions TUnit : dans un bloc `Assert.Multiple()`, un écart de snapshot est collecté avec les autres échecs, et tous sont signalés ensemble à la fin du bloc. Ce test du dépôt le vérifie :

```csharp
using (Assert.Multiple())
{
    await Assert.That(new { Total = 2 }).MatchesSnapshot();
    await Assert.That(new[] { 1, 1 }.Sum()).IsEqualTo(3);
}
```

Le rapport contient alors le diff du snapshot, puis l'échec de la seconde assertion (`Expected to be 3`, `but found 2`).

`Snapshot.Match`, lui, lève une exception : dans un bloc `Assert.Multiple()`, utilisez l'assertion.

### Avec une tâche

Passez la tâche à `Assert.That` : TUnit l'attend, puis Carbonite vérifie son résultat.

```csharp
[Test]
public async Task Awaits_a_task_given_to_assert_that()
{
    await Assert.That(LoadAsync()).MatchesSnapshot();
}

private static async Task<int[]> LoadAsync()
{
    await Task.Yield();
    return [1, 2, 3];
}
```

La référence contient le résultat, pas la tâche :

```json
[
  1,
  2,
  3
]
```

Une `ValueTask` s'attend d'abord : `await Assert.That(await ChargerAsync()).MatchesSnapshot();`. L'analyseur de TUnit le demande.

Si la tâche ou le délégué lève une exception, l'assertion échoue avec, par exemple :

```text
Expected to match its snapshot
but threw InvalidOperationException: boum
```

## Tests paramétrés : un fichier par cas

Chaque cas d'un test paramétré reçoit son propre fichier, nommé d'après ses arguments. Vous n'avez rien à écrire.

### Arguments de méthode

```csharp
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
```

Trois références : `FactureTests.La_TVA_depend_du_pays_FR.verified.json`, `_DE` et `_LU`. Celle du cas `FR` :

```json
{
  "Pays": "FR",
  "TotalHt": 339.30,
  "Tva": 67.86,
  "TotalTtc": 407.16
}
```

### Sources de données

`[MethodDataSource]` fonctionne de la même façon :

```csharp
[Test]
[MethodDataSource(nameof(Prices))]
public void Data_sources_name_their_cases_too(decimal price, string currency)
{
    // An object, not $"{price} {currency}": interpolation would use the machine culture (9,99 in French).
    Snapshot.Match(new { price, currency });
}

public static IEnumerable<(decimal, string)> Prices()
{
    yield return (9.99m, "EUR");
    yield return (12m, "USD");
}
```

Deux références : `ParameterizedTests.Data_sources_name_their_cases_too_9.99_EUR.verified.json` et `…_12_USD.verified.json`.

### Arguments de classe

Les arguments simples passés au constructeur de la classe entrent aussi dans le nom :

```csharp
[Arguments("fr")]
[Arguments("en")]
public class LocalizedTests(string language)
{
    [Test]
    public void Class_arguments_name_the_case()
    {
        Snapshot.Match(
            new
            {
                language,
                Greeting = string.Equals(language, "fr", StringComparison.Ordinal)
                    ? "Bonjour"
                    : "Hello",
            }
        );
    }
}
```

Deux références : `LocalizedTests.Class_arguments_name_the_case_fr.verified.json` et `…_en.verified.json`. Quand la classe et la méthode ont toutes deux des arguments, ceux de la classe viennent en premier.

### Les fixtures partagées n'entrent pas dans le nom

Un objet injecté dans le constructeur, comme une fixture partagée par toute la session, ne change pas le nom :

```csharp
[ClassDataSource<Catalog>(Shared = SharedType.PerTestSession)]
public class FixtureTests(Catalog catalog)
{
    [Test]
    public void A_shared_fixture_does_not_change_the_snapshot_name()
    {
        Snapshot.Match(catalog.Items);
    }
}
```

Une seule référence : `FixtureTests.A_shared_fixture_does_not_change_the_snapshot_name.verified.json`.

La règle : tous les arguments de la méthode comptent, sauf un `CancellationToken` ; parmi les arguments de la classe, seules les valeurs simples comptent (texte, nombre, booléen, date, enum, `Guid`, `null`…).

### Comment un argument devient un nom

Carbonite écrit chaque argument avec le même sérialiseur que le snapshot : le nom ne dépend pas de la culture de la machine, et les valeurs imprévisibles y sont remplacées par les mêmes marqueurs.

| Argument | Dans le nom |
| --- | --- |
| `"FR"` | `FR` |
| `"été"` | `ete` |
| `""` | `empty` |
| `null` | `null` |
| `9.99m`, `1.5` | `9.99`, `1.5` (toujours avec un point, même sur un poste en français) |
| `true` | `true` |
| `new DateOnly(2026, 11, 18)` | `2026-11-18` |
| une valeur d'enum, `Access.Read` | `Read` |
| `new[] { 1, 2, 3 }` | `1_2_3` |
| un objet ou un record, `new Point(1, 2)` | son contenu : `X_1_Y_2` |
| `Guid.NewGuid()`, `DateTime.Now` | `Guid_1`, `DateTime_1` |
| un `CancellationToken` | rien |

Les arguments sont reliés par `_`, puis le tout est nettoyé comme tout nom de fichier (voir [Démarrage](demarrage.md#le-nom-et-lemplacement-des-fichiers)) : `[Arguments("fr-FR", 3)]` donne le suffixe `_fr-FR_3`. Les options du snapshot comptent aussi : avec `DontScrubGuids()`, un `Guid` apparaît en clair dans le nom.

Un piège : **deux cas peuvent recevoir le même nom**, par exemple `"a b"` et `"a_b"` une fois nettoyés, ou deux arguments qui ne diffèrent que par un `Guid` ou une date remplacés par un marqueur. Si leurs valeurs diffèrent, le second échoue avec `Snapshot conflict` (voir [Cycle de vie](cycle-de-vie.md#deux-valeurs-pour-un-même-snapshot)). Nommez alors chaque cas : `Snapshot.Match(commande, $"commande-{commande.Numero}")`.

## Plusieurs snapshots dans un test

Sans nom, le premier snapshot garde le nom du test, les suivants sont numérotés :

```csharp
[Test]
public void Two_unnamed_snapshots_in_one_test()
{
    Snapshot.Match(new { Step = 1 }); // ParameterizedTests.Two_unnamed_snapshots_in_one_test.verified.json
    Snapshot.Match(new { Step = 2 }); // ParameterizedTests.Two_unnamed_snapshots_in_one_test.2.verified.json
}
```

Avec un nom, chaque snapshot porte le sien :

```csharp
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
```

Les fichiers : `FactureTests.Une_facture_avant_et_apres_remise.avant-remise.verified.json` et `….apres-remise.verified.json`. Le même nom utilisé deux fois dans un test est numéroté à son tour : `.etape`, puis `.etape.2`.

Le compteur vaut aussi pour une boucle : trois tours sans nom donnent trois fichiers, le premier sans numéro, puis `.2` et `.3`.

Préférez les noms : ils disent ce que vérifie chaque fichier, et ils ne bougent pas. Avec la numérotation, insérer un snapshot au milieu du test décale les numéros de tous les suivants, qui échouent alors.

## [Retry] et [Repeat]

- **`[Retry]`** : chaque nouvelle tentative recompte ses snapshots à partir de 1. Une tentative relancée vérifie donc les mêmes fichiers que la première, sans créer de `.2` en trop.
- **`[Repeat]`** : chaque répétition vérifie le même fichier. Si la valeur change d'une répétition à l'autre, Carbonite le signale comme deux valeurs pour un même snapshot : c'est justement ce qu'une répétition doit révéler.

Les tests `A_retried_test_counts_its_snapshots_from_one_again` et `A_repeated_test_shares_its_snapshot` du dépôt couvrent ces deux cas.

## Les méthodes utilitaires gardent le nom du test

Avec l'adaptateur, une méthode utilitaire qui appelle `Snapshot.Match` produit le snapshot du test en cours :

```csharp
[Test]
public async Task A_helper_method_keeps_the_name_of_the_test()
{
    // …
    CheckFromHelper(new { Checked = "from a helper" });
}

private static void CheckFromHelper(object value) => Snapshot.Match(value);
```

La référence s'appelle `ContextTests.A_helper_method_keeps_the_name_of_the_test.verified.json`, pas `ContextTests.CheckFromHelper`. Même si l'utilitaire est rangé dans un autre fichier, ou un autre dossier, le snapshot va dans le dossier `__snapshots__` du fichier de test.

En mode générique, c'est différent : voir [Autres frameworks](autres-frameworks.md#boucles-et-méthodes-utilitaires).

## Mettre à jour les snapshots de quelques tests

`SNAPSHOT_UPDATE=1` réécrit toutes les références qui diffèrent parmi les tests exécutés. Pour viser une classe, filtrez les tests avec `--treenode-filter`, dont le chemin a la forme `/<assembly>/<namespace>/<classe>/<test>`.

Avec bash ou zsh :

```bash
SNAPSHOT_UPDATE=1 dotnet test --treenode-filter "/*/*/FactureTests/*"
```

Avec PowerShell :

```powershell
$env:SNAPSHOT_UPDATE=1; dotnet test --treenode-filter "/*/*/FactureTests/*"
Remove-Item Env:SNAPSHOT_UPDATE
```

---

Voir aussi : [Cycle de vie](cycle-de-vie.md) · [Valeurs dynamiques](valeurs-dynamiques.md) · [Dépannage](depannage.md).
