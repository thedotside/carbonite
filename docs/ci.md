# Carbonite en intégration continue

Cette page décrit le comportement de Carbonite en CI, et les pièges qui font passer un test sur votre poste mais échouer sur le serveur : culture, fins de ligne, builds déterministes.

## Le mode strict

En CI, Carbonite ne crée ni ne réécrit jamais une référence :

| Situation | En CI |
| --- | --- |
| La référence n'existe pas | Le test échoue (`Snapshot missing`). La valeur est écrite dans un fichier reçu. |
| La valeur est identique | Le test passe. |
| La valeur est différente | Le test échoue avec le diff. La valeur est écrite dans un fichier reçu. |
| `SNAPSHOT_UPDATE=1` est défini | La variable est ignorée, et le message le signale. |

Pourquoi : un test doit vérifier une valeur relue par un humain. Une référence créée ou réécrite par le serveur ne serait relue par personne. Le détail de chaque cas est dans [Cycle de vie](cycle-de-vie.md).

### Les variables qui signalent la CI

Les services de CI définissent eux-mêmes une de ces variables d'environnement. Vous n'avez rien à ajouter.

| Variable | Service |
| --- | --- |
| `CI` | la plupart des services |
| `TF_BUILD` | Azure Pipelines |
| `GITHUB_ACTIONS` | GitHub Actions |
| `GITLAB_CI` | GitLab CI/CD |
| `JENKINS_URL` | Jenkins |
| `TEAMCITY_VERSION` | TeamCity |
| `BUILDKITE` | Buildkite |
| `CIRCLECI` | CircleCI |
| `TRAVIS` | Travis CI |
| `APPVEYOR` | AppVeyor |
| `BITBUCKET_BUILD_NUMBER` | Bitbucket Pipelines |
| `CODEBUILD_BUILD_ID` | AWS CodeBuild |

Une variable compte dès qu'elle contient autre chose que du vide, `0`, `false`, `no` ou `off`, sans tenir compte de la casse ni des espaces autour. Il suffit d'une seule : sur GitHub Actions, définir `CI=false` ne change rien, car `GITHUB_ACTIONS` reste définie.

### Reproduire la CI sur votre poste

Avant de pousser, lancez les tests comme la CI les verra. Avec bash ou zsh :

```bash
CI=true dotnet test
```

Avec PowerShell :

```powershell
$env:CI="true"; dotnet test
Remove-Item Env:CI
```

Un snapshot que vous avez oublié de créer, ou de committer, échoue alors tout de suite.

## Builds déterministes

Beaucoup de projets activent `ContinuousIntegrationBuild` en CI, par exemple dans `Directory.Build.props` (c'est le cas du dépôt de Carbonite) :

```xml
<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
```

Le compilateur remplace alors les chemins des fichiers sources par des chemins neutres, comme `/_/tests/Facturation.Tests/FactureTests.cs`. C'est voulu : le binaire ne dépend plus de la machine qui l'a compilé. Mais Carbonite lit justement ce chemin pour trouver le dossier `__snapshots__`, et `/_/…` n'existe pas sur le disque.

Carbonite s'en charge, sans configuration :

1. À la compilation, le paquet `TheDotSide.Carbonite` ajoute à l'assembly de test, par ses fichiers `buildTransitive`, le dossier du projet :

   ```csharp
   [assembly: AssemblyMetadata("TheDotSide.Carbonite.ProjectDirectory", "/src/facturation/tests/Facturation.Tests")]
   ```

2. À l'exécution, quand un chemin n'existe pas, Carbonite le cherche sous ce dossier et sous ses dossiers parents, en essayant d'abord la fin de chemin la plus longue. `/_/tests/Facturation.Tests/FactureTests.cs` redevient ainsi `/src/facturation/tests/Facturation.Tests/FactureTests.cs`.

Deux conditions :

- les tests tournent depuis la copie des sources : les fichiers `.cs` doivent être présents sur la machine ;
- le projet de test référence le paquet, directement ou par `TheDotSide.Carbonite.TUnit`, pour que sa compilation enregistre le dossier.

Sinon, le test échoue avec ce message :

```text
Carbonite cannot find the test source file '/_/tests/Facturation.Tests/FactureTests.cs', which tells it where the __snapshots__ folder is.
This happens when the tests were compiled with deterministic paths (ContinuousIntegrationBuild=true) and Carbonite does not know the project directory, or when compiled tests run on a machine without their sources.
Run the tests from the source checkout, and make sure the test project references TheDotSide.Carbonite (directly or through TheDotSide.Carbonite.TUnit) so that its build records the project directory.
```

Le dossier enregistré est le chemin absolu du projet sur la machine qui compile. Pour ne pas l'écrire dans l'assembly de test, ajoutez `<CarboniteRecordProjectDirectory>false</CarboniteRecordProjectDirectory>` dans un `<PropertyGroup>` du projet de test ; un build déterministe ne trouvera alors plus les fichiers sources.

## Récupérer les fichiers reçus

Quand un snapshot échoue en CI, la nouvelle valeur est dans un fichier reçu, sur le serveur. Publiez ces fichiers comme artefacts pour les lire. Avec GitHub Actions :

```yaml
name: tests

on:
  push:
    branches: [main]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: |
            10.0.x
            11.0.x
      - name: Tests
        run: dotnet test
      - name: Publier les valeurs reçues par Carbonite
        if: failure()
        uses: actions/upload-artifact@v7
        with:
          name: snapshots-recus
          path: '**/__snapshots__/*.received.*'
          if-no-files-found: ignore
```

GitHub Actions définit `CI` et `GITHUB_ACTIONS` : le mode strict est actif sans rien ajouter. La CI de Carbonite publie les mêmes fichiers (`pipelines/build/action.yml`).

Si l'écart n'apparaît qu'en CI, par exemple sur un autre système d'exploitation, téléchargez l'artefact, comprenez d'abord la cause (voir les sections suivantes), puis, si la valeur reçue est la bonne, copiez-la sur la référence et committez-la.

## Plusieurs frameworks cibles

Un projet de test qui cible `net10.0` et `net11.0` est exécuté une fois par framework. Les deux exécutions utilisent les mêmes fichiers : le nom d'un snapshot ne contient pas le framework.

- **La valeur doit être identique sur les deux frameworks.** La sortie de Carbonite ne dépend pas de la version de .NET (les noms de types n'ont pas de version, par exemple). Vos valeurs, elles, peuvent en dépendre : un message d'exception du framework peut changer d'une version à l'autre. Dans ce cas, masquez le membre concerné (`IgnoreMember("Message")`) ou vérifiez une valeur qui ne dépend pas de .NET.
- **La création est sûre en parallèle.** Si les deux exécutions créent la même référence au même moment, une seule l'écrit ; l'autre compare sa valeur à ce fichier.
- **Avec `SNAPSHOT_UPDATE=1`**, si les deux frameworks produisent des valeurs différentes, la dernière écriture gagne, et l'exécution suivante échoue sur l'autre framework. Corrigez la cause plutôt que de relancer la mise à jour.

## Culture et ICU : quand votre code formate du texte

Carbonite écrit toutes les valeurs qu'il sérialise avec la culture invariante : un `decimal` dans un objet s'écrit `1299.80` partout. Mais une chaîne que votre code a déjà construite est du texte : Carbonite la prend telle quelle.

Ce test, lancé avec la culture `fr-FR` :

```csharp
var montant = 1299.80m;

Snapshot.Match(
    $"Interpolation : {montant}\nMonnaie : {montant:C}\nInvariant : {montant.ToString(CultureInfo.InvariantCulture)}"
);
```

a produit cette référence sur un Mac :

```text
Interpolation : 1299,80
Monnaie : 1 299,80 €
Invariant : 1299.80
```

Deux pièges dans ce petit texte :

- **L'interpolation utilise la culture de la machine.** `$"{montant}"` donne `1299,80` sur un poste en français et `1299.80` sur un serveur en anglais. La référence créée sur votre poste échoue en CI.
- **Le format monétaire contient un caractère invisible.** Ici, le séparateur des milliers n'est pas un espace ordinaire, mais un espace insécable étroit (U+202F). Ce caractère vient des données de culture du système d'exploitation (la bibliothèque ICU), et il dépend de leur version : ailleurs, ce peut être un espace insécable (U+00A0). macOS, les distributions Linux et Windows ne sont donc pas toujours d'accord.

L'exemple du dépôt évite ces deux pièges. Son commentaire, dans `samples/Sample.Facturation.Tests/Facturation.cs`, l'explique :

> Les montants sont formatés avec la culture invariante : le format monétaire fr-FR dépend de la version d'ICU du système (espaces insécables), ce qui rendrait le snapshot différent entre macOS, Linux et Windows.

```csharp
texte.AppendLine(
    CultureInfo.InvariantCulture,
    $"- {ligne.Libelle} x {ligne.Quantite} : {ligne.TotalHt:0.00} EUR"
);
```

Et un test du dépôt vérifie un objet plutôt qu'une chaîne interpolée, pour la même raison :

```csharp
// An object, not $"{price} {currency}": interpolation would use the machine culture (9,99 in French).
Snapshot.Match(new { price, currency });
```

Les corrections, de la plus simple à la plus précise :

1. **Vérifiez un objet plutôt qu'un texte construit.** Carbonite formate alors les nombres et les dates lui-même, de façon invariante.
2. **Formatez le texte technique avec la culture invariante** : `string.Create(CultureInfo.InvariantCulture, $"…")`, ou `AppendLine(CultureInfo.InvariantCulture, $"…")` comme dans l'exemple.
3. **Si le texte doit être en français**, par exemple un courriel pour les clients, passez la culture explicitement (`montant.ToString("C", CultureInfo.GetCultureInfo("fr-FR"))`) et neutralisez les espaces qui dépendent d'ICU : `options.Scrub(@"[\u00A0\u202F]", " ")`.

Quand ce piège se déclenche, les lignes du diff semblent identiques, et le message ajoute : `Note: the only differences are whitespace (spaces, tabs or line breaks).`

Même logique pour l'heure : un `DateTime` local dépend du fuseau horaire de la machine. Par défaut, Carbonite remplace les dates par des marqueurs ; si vous les gardez (`DontScrubDates()`), utilisez des dates fixes.

## Fins de ligne

Carbonite écrit toujours des fins de ligne `\n`. En comparant, il ignore `\r\n`, la marque d'ordre des octets (BOM) et les sauts de ligne à la fin du fichier : une référence extraite en `\r\n` sous Windows passe. Ajoutez tout de même cette ligne à `.gitattributes`, pour que les références restent identiques sur toutes les machines :

```gitattributes
**/__snapshots__/** text eol=lf
```

Les chaînes à l'intérieur d'un objet suivent la même règle : un texte construit avec `AppendLine()` ou `Environment.NewLine` contient `\r\n` sous Windows et `\n` ailleurs, mais Carbonite écrit les deux `"a\nb"`. Le snapshot est donc le même sur tous les systèmes. Seul un retour chariot isolé, rare, reste visible sous la forme `\r`.

Conséquence : Carbonite ne peut pas vérifier les fins de ligne elles-mêmes. Pour tester qu'un fichier CSV utilise bien `\r\n`, écrivez une assertion classique.

## Majuscules et minuscules dans les noms

Les noms des snapshots viennent des noms de fichiers et de méthodes de test. Sous Windows et macOS, le système de fichiers ignore souvent la casse ; sous Linux, non. Deux snapshots d'une même exécution dont les noms ne diffèrent que par la casse sont refusés partout avec `Snapshot name conflict` (voir [Cycle de vie](cycle-de-vie.md#deux-valeurs-pour-un-même-snapshot)). Reste le renommage : si vous changez seulement une majuscule d'un test (`Une_Facture` devient `Une_facture`), l'ancienne référence est encore trouvée sur votre poste, mais pas en CI sous Linux : `Snapshot missing`. Renommez aussi le fichier dans Git :

```bash
git mv __snapshots__/FactureTests.Une_Facture.verified.json __snapshots__/FactureTests.Une_facture.verified.json
```

---

Voir aussi : [Cycle de vie](cycle-de-vie.md) · [Dépannage](depannage.md) · [Développement](developpement.md).
