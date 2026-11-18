# xUnit, NUnit et MSTest

Cette page décrit le mode générique : Carbonite sans adaptateur, tel que l'utilisent xUnit, NUnit et MSTest.

## Installer

```bash
dotnet add package TheDotSide.Carbonite
```

Il n'y a rien à configurer. En cas d'écart, `Snapshot.Match` lève une exception (`SnapshotMismatchException`) : chaque framework la rapporte comme un test en échec, avec le message de Carbonite.

## D'où viennent les noms

Sans adaptateur, Carbonite ne sait pas quel test est en cours. Il utilise deux informations que le compilateur C# ajoute à chaque appel de `Snapshot.Match` :

- le **fichier** qui contient l'appel (`[CallerFilePath]`) : le dossier `__snapshots__` est créé à côté, et le nom du fichier commence le nom du snapshot ;
- la **méthode** qui contient l'appel (`[CallerMemberName]`) : elle donne le nom du test.

```csharp
// tests/Facturation.Tests/FactureTests.cs
public class FactureTests
{
    [Fact]
    public void Une_facture_complete()
    {
        Snapshot.Match(UneFacture());
    }
}
```

La référence est `tests/Facturation.Tests/__snapshots__/FactureTests.Une_facture_complete.verified.json`.

Dans une méthode `async`, ou dans une lambda écrite dans le test, le nom reste celui de la méthode de test. Le contenu du fichier suit les mêmes règles qu'avec TUnit : voir [Format des fichiers](format.md).

La conséquence importante : Carbonite ne voit ni les arguments d'un test paramétré, ni les tours d'une boucle. Tous ces appels visent le même fichier. Il faut donc nommer chaque valeur.

## Tests paramétrés : un nom par cas

Passez un nom en second argument, construit à partir du paramètre :

```csharp
public class FactureTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Le_total_suit_la_quantite(int x)
    {
        var facture = Facturation.Creer(
            "FAC-2026-0042",
            "Ada Lovelace",
            "FR",
            new LigneDeFacture("Lampe de bureau", x, 39.50m)
        );

        Snapshot.Match(new { facture.TotalHt, facture.Tva, facture.TotalTtc }, $"cas-{x}");
    }
}
```

Deux références, une par cas :

```text
__snapshots__/FactureTests.Le_total_suit_la_quantite.cas-1.verified.json
__snapshots__/FactureTests.Le_total_suit_la_quantite.cas-3.verified.json
```

Celle du cas `1` :

```json
{
  "TotalHt": 39.50,
  "Tva": 7.90,
  "TotalTtc": 47.40
}
```

Le principe est le même avec les autres frameworks :

| Framework | Tests paramétrés |
| --- | --- |
| xUnit | `[Theory]` avec `[InlineData]`, `[MemberData]` ou `[ClassData]` |
| NUnit | `[TestCase]` ou `[TestCaseSource]` |
| MSTest | `[DataRow]` ou `[DynamicData]` |

Construisez le nom avec des valeurs stables : du texte, des entiers. Pour un nombre décimal ou une date, précisez la culture, sinon le nom dépend de la machine : `$"prix-{9.99m}"` vaut `prix-9,99` sur un poste en français (fichier `….prix-9_99.verified.json`) et `prix-9.99` sur un serveur en anglais (fichier `….prix-9.99.verified.json`). Deux noms, donc deux fichiers, et un `Snapshot missing` en CI.

```csharp
Snapshot.Match(resultat, string.Create(CultureInfo.InvariantCulture, $"prix-{prix}"));
```

## Si vous oubliez le nom

Sans nom, les deux cas visent le même fichier : `FactureTests.Le_total_suit_la_quantite.verified.json`. Le premier cas vérifié crée ou compare la référence. Le second échoue, quel que soit l'ordre et même si sa valeur correspond au fichier, avec un message qui montre les deux valeurs :

```text
Snapshot conflict: FactureTests.Le_total_suit_la_quantite

This snapshot was already checked earlier in this run with a different value (the first one by FactureTests.Le_total_suit_la_quantite). One snapshot file cannot hold both, and SNAPSHOT_UPDATE cannot choose between them.

--- first value
+++ this value
@@ -1,5 +1,5 @@
 {
-  "TotalHt": 39.50,
-  "Tva": 7.90,
-  "TotalTtc": 47.40
+  "TotalHt": 118.50,
+  "Tva": 23.70,
+  "TotalTtc": 142.20
 }

If the test is parameterized, or calls Snapshot.Match in a loop or from a shared helper, give each value its own name, for example Snapshot.Match(value, $"case-{id}"). Otherwise the value is not deterministic: hide what changes with options.IgnoreMember or options.ScrubMember.
```

L'ordre des cas dépend du framework et de l'exécution en parallèle : Carbonite fait donc toujours échouer la seconde valeur vue, pour que le problème apparaisse à chaque exécution et pas une fois sur deux. Le message est le même avec `SNAPSHOT_UPDATE=1`, et rien n'est écrit. Ne remplacez pas la référence pour faire taire le message : nommez les cas.

Deux noms qui ne diffèrent que par la casse (`Total` et `total`) sont refusés de la même façon : Windows et macOS n'y voient qu'un seul fichier.

## Boucles et méthodes utilitaires

**Une boucle** vise elle aussi un seul fichier. Nommez chaque tour :

```csharp
foreach (var ligne in facture.Lignes)
{
    Snapshot.Match(ligne, $"ligne-{ligne.Libelle}");
}
```

**Une méthode utilitaire** qui appelle `Snapshot.Match` devient « le test » aux yeux de Carbonite : le snapshot porte le nom de l'utilitaire et se range à côté du fichier de l'utilitaire. Par exemple, avec cet utilitaire rangé dans `Outils/Outils.cs` :

```csharp
public static class Outils
{
    public static void Verifier(object valeur) => Snapshot.Match(valeur);
}
```

deux tests qui l'appellent partagent le fichier `Outils/__snapshots__/Outils.Verifier.verified.json`. Si leurs valeurs diffèrent, le second échoue avec `Snapshot conflict: Outils.Verifier`, qui montre les deux valeurs.

La correction la plus simple : appelez `Snapshot.Match` dans le test lui-même, et gardez dans l'utilitaire seulement la préparation de la valeur.

```csharp
[Fact]
public void Une_facture_complete()
{
    Snapshot.Match(Outils.PreparerFacture());
}
```

Avec l'adaptateur TUnit, ce problème n'existe pas : une méthode utilitaire garde le nom du test (voir [TUnit](tunit.md#les-méthodes-utilitaires-gardent-le-nom-du-test)).

## Plusieurs snapshots dans un test

Donnez un nom à chacun, comme dans l'exemple du dépôt :

```csharp
Snapshot.Match(facture.Lignes, "avant-remise");
// …
Snapshot.Match(remisee.Lignes, "apres-remise");
```

Sans nom, le second appel viserait le même fichier que le premier : en mode générique, il n'y a pas de numérotation automatique.

## Ce qui est testé dans ce dépôt

Soyons précis :

- les tests du cœur (`tests/TheDotSide.Carbonite.Tests`) tournent sans l'adaptateur : chaque `Snapshot.Match` y passe par le mode générique. TUnit n'y sert que d'exécuteur de tests ;
- `eng/package-smoke.sh` utilise le paquet cœur seul dans un projet jetable : création du snapshot, puis nouvelle vérification après un build déterministe ;
- le dépôt ne contient aucun test xUnit, NUnit ou MSTest. Ces frameworks ne sont pas testés un par un.

Le mode générique ne dépend d'aucun de ces frameworks : les noms viennent du compilateur C#, et un échec est une exception ordinaire. Si vous rencontrez un comportement propre à l'un d'eux, ouvrez une issue.

---

Voir aussi : [Cycle de vie](cycle-de-vie.md) · [Valeurs dynamiques](valeurs-dynamiques.md) · [Dépannage](depannage.md).
