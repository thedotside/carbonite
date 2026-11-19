# Contribuer à Carbonite

Cette page s'adresse aux personnes qui modifient Carbonite lui-même : outils, commandes, organisation du dépôt et des tests.

## Prérequis

| Outil | Pourquoi |
| --- | --- |
| SDK .NET 11 | Il compile les deux cibles, `net10.0` et `net11.0`. `global.json` épingle le SDK 11.0.100 et accepte une version 11.0 plus récente (`rollForward: latestFeature`), mais pas un SDK 10. |
| Runtime .NET 10 | Il exécute les tests `net10.0`. |
| CSharpier 1.3.0 | Le formateur, installé comme outil local par `dotnet tool restore`. |
| Bash, Git, Python 3.8 ou plus récent | Pour `eng/pack.sh` et `eng/package-smoke.sh`. Sous Windows, lancez-les dans Git Bash. |
| Node.js (facultatif) | Pour vérifier les messages de commit sur votre poste. |

Une fois par copie du dépôt :

```bash
eng/setup.sh
```

Ce script restaure les outils locaux (`dotnet tool restore`) et active le hook `.githooks/pre-commit`, qui refuse un commit dont le code n'est pas formaté. Si votre copie a déjà un autre hook `pre-commit`, le script s'arrête et vous demande de les réunir d'abord.

## Compiler et tester

```bash
dotnet build
dotnet test
```

`global.json` choisit Microsoft Testing Platform pour `dotnet test` : la commande lance tous les projets de test, pour les deux frameworks.

Avant de pousser, lancez les tests comme la CI. Avec bash ou zsh :

```bash
CI=true dotnet test
```

Avec PowerShell :

```powershell
$env:CI="true"; dotnet test
Remove-Item Env:CI
```

Avec `CI=true`, deux choses changent : Carbonite passe en mode strict (aucun snapshot créé ni réécrit), et `Directory.Build.props` active `ContinuousIntegrationBuild`, donc des chemins sources en `/_/…`. Les tests doivent passer dans ce mode.

Quelques règles du dépôt :

- **Les versions des paquets sont centralisées** dans `Directory.Packages.props`, et les versions restaurées sont verrouillées dans les fichiers `packages.lock.json`, committés. La CI restaure en mode verrouillé : si vous changez une dépendance, committez les fichiers de verrouillage mis à jour.
- **Les avertissements sont des erreurs** (`TreatWarningsAsErrors`). `Meziantou.Analyzer` est référencé par tous les projets, et `.editorconfig` élève ses règles sur la culture et les comparaisons de chaînes au rang d'avertissements, donc d'erreurs. Une bibliothèque de snapshots ne doit jamais dépendre de la culture de la machine : passez toujours une culture ou un comparateur.
- **Le paquet cœur n'a aucune dépendance.** L'adaptateur dépend seulement de TUnit.

## Formater le code

CSharpier est le seul formateur du dépôt, pour le C# comme pour les fichiers XML de MSBuild :

```bash
dotnet csharpier format .
dotnet csharpier check .
```

`format` réécrit les fichiers ; `check` vérifie seulement, et c'est son code de sortie qui compte. La CI et le hook `pre-commit` lancent `check`.

## Messages de commit

Les messages suivent [Conventional Commits](https://www.conventionalcommits.org/), vérifiés par commitlint avec la configuration `@commitlint/config-conventional`. En CI, chaque commit d'une pull request est vérifié, ainsi que le titre de la pull request. L'historique du dépôt donne le ton :

```text
feat(core): check values against snapshot files with Snapshot.Match
feat(tunit): name snapshots per test case and add MatchesSnapshot
build: pack, check and smoke-test the NuGet packages, with a sample
docs: record the Carbonite design decision
```

Pour vérifier vos commits sur votre poste :

```bash
npm ci
npx commitlint --from origin/main --to HEAD --verbose
```

## Organisation du dépôt

| Chemin | Contenu |
| --- | --- |
| `src/TheDotSide.Carbonite/` | Le cœur, sans dépendance. `Snapshot.cs` est le point d'entrée ; `Engine/` porte le cycle de vie et les messages d'échec ; `Serialization/` écrit le JSON déterministe ; `Naming/` nomme les fichiers et retrouve les chemins des builds déterministes ; `Diff/` et `Text/` produisent le diff et normalisent le texte ; `Extensibility/` définit le contrat interne des adaptateurs ; `buildTransitive/` contient les cibles MSBuild livrées avec le paquet. |
| `src/TheDotSide.Carbonite.TUnit/` | L'adaptateur TUnit, que le cœur trouve par le nom de son type. Il est compilé contre TUnit 1.40.0 : ne relevez ce plancher que pour une API nécessaire, car les dépendances de TUnit sont des minimums. |
| `tests/TheDotSide.Carbonite.Tests/` | Les tests du cœur, en mode générique. |
| `tests/TheDotSide.Carbonite.TUnit.Tests/` | Les tests de l'adaptateur TUnit. |
| `samples/Sample.Facturation.Tests/` | L'exemple de facturation cité par cette documentation. |
| `eng/` | `setup.sh`, `pack.sh` et `package-smoke.sh`. |
| `skills/carbonite/` | Le skill pour les agents de code des utilisateurs (voir [Agents de code](agents.md)). |
| `.claude-plugin/` | La marketplace et le plugin `carbonite@carbonite`, pour Claude Code et Copilot CLI. |
| `docs/` | Cette documentation ; `docs/decisions/` contient les décisions de conception. |
| `.github/workflows/`, `pipelines/build/` | La CI et la publication. |

## Comment les tests sont organisés

**Les tests du cœur tournent en mode générique.** `tests/TheDotSide.Carbonite.Tests` utilise TUnit comme exécuteur, mais sans l'adaptateur : chaque `Snapshot.Match` y passe par le mode générique, celui des utilisateurs de xUnit, NUnit et MSTest. Les tests unitaires suivent les dossiers du code (`Engine/`, `Naming/`, `Serialization/`, `Diff/`, `Text/`). Ceux du moteur le construisent avec un dossier temporaire et un environnement simulé, pour ne dépendre ni de la CI ni de `SNAPSHOT_UPDATE`. `SnapshotTests` couvre l'API publique.

**Les tests de l'adaptateur utilisent les vraies fonctions de TUnit** : arguments de méthode, sources de données, arguments de classe, fixtures partagées, `[Retry]`, `[Repeat]`, `Assert.Multiple()`, tâches passées à `Assert.That`.

**Des snapshots « dogfood ».** Une partie des tests vérifient Carbonite avec Carbonite, et leurs références sont committées dans les dossiers `__snapshots__` des deux projets de test (par exemple `GenericModeTests` et `ParameterizedTests`). En CI, elles prouvent aussi que les chemins des builds déterministes sont bien retrouvés. Relisez leurs changements comme ceux de n'importe quel snapshot, et ne committez jamais de fichier `.received`.

**L'exemple est aussi un test.** `samples/Sample.Facturation.Tests` est un vrai projet TUnit, avec ses références committées. La documentation française cite son code et ses fichiers : si vous le modifiez, mettez la documentation à jour.

Deux détails :

- les projets de test référencent les projets sources, pas les paquets. Ils importent donc eux-mêmes `src/TheDotSide.Carbonite/buildTransitive/TheDotSide.Carbonite.targets`, comme le ferait le paquet NuGet ;
- les deux tests qui ont besoin d'un vrai écart sont ignorés quand `SNAPSHOT_UPDATE=1` est défini : le mode mise à jour accepterait l'écart au lieu de le signaler.

## Fabriquer les paquets : eng/pack.sh

```bash
eng/pack.sh
```

Le script fabrique `TheDotSide.Carbonite` et `TheDotSide.Carbonite.TUnit` en Release dans `artifacts/packages`, avec leurs symboles (`.snupkg`). Il prend la version de `Directory.Build.props`, ou celle passée en argument (`eng/pack.sh 0.2.0-alpha.1`).

Il ouvre ensuite les archives et vérifie :

- qu'elles ne contiennent que leur propre assembly : les dépendances restent des références NuGet ;
- qu'elles contiennent le README, la documentation XML et, pour le cœur, les cibles `buildTransitive` ;
- la licence MIT ;
- les dépendances de l'adaptateur : le cœur dans exactement la même version (`[0.1.0]` pour la version 0.1.0), `TUnit.Core` et `TUnit.Assertions` à partir de 1.40.0.

## Essayer les paquets : eng/package-smoke.sh

Après `eng/pack.sh` :

```bash
eng/package-smoke.sh
```

Le script utilise les paquets comme le ferait une équipe, dans des projets jetables créés hors du dépôt, avec un cache NuGet isolé : une ancienne copie en cache ne peut pas masquer un paquet périmé. Pour chaque framework cible :

- **deux projets TUnit avec `TheDotSide.Carbonite.TUnit`**, l'un avec TUnit 1.40.0 (le plancher de l'adaptateur), l'autre avec la dernière version : chacun traverse tout le cycle de vie. Le premier passage crée les snapshots (avec un Guid remplacé). Un build déterministe, refait de zéro avec `CI=true` et `ContinuousIntegrationBuild=true`, les retrouve, et le script vérifie que l'assembly de test contient bien des chemins `/_/`. Un snapshot manquant échoue en CI et laisse un fichier reçu, puis une valeur modifiée échoue avec un diff ;
- **un projet avec le seul paquet cœur**, en mode générique : création du snapshot, puis build déterministe vérifié de la même façon ;
- **un projet avec les exemples C# du skill** (`skills/carbonite`) et la dernière version de TUnit : chaque bloc ` ```csharp ` du skill devient un fichier du projet, les tests créent leurs snapshots, puis passent en mode CI.

`CARBONITE_TUNIT_VERSIONS="1.87.0"` limite les versions de TUnit essayées.

Les journaux restent dans `artifacts/smoke-logs/`.

## Sans runtime .NET 10

`CARBONITE_FRAMEWORKS` limite les deux scripts aux frameworks indiqués, par exemple sur une machine qui n'a pas le runtime .NET 10 :

```bash
CARBONITE_FRAMEWORKS="net11.0" eng/pack.sh
CARBONITE_FRAMEWORKS="net11.0" eng/package-smoke.sh
```

Le SDK .NET 11 reste nécessaire pour compiler le dépôt et fabriquer les paquets : `global.json` l'impose.

## Le skill pour les agents

- `skills/carbonite` décrit le comportement publié de Carbonite aux agents de code. Changez-le dans le même commit qu'une API publique, un message d'échec, une variable d'environnement ou une règle de nommage.
- Chaque bloc ` ```csharp ` du skill est un fichier complet, que `eng/package-smoke.sh` compile et exécute contre les paquets. Les extraits plus courts restent du code en ligne.
- La `version` de `.claude-plugin/plugin.json` est la `<Version>` de `Directory.Build.props` : changez les deux ensemble.
- Vérifiez avec `claude plugin validate --strict .` et `gh skill publish --dry-run .`. Le job `agents` de la CI lance les deux, ainsi que la comparaison des versions.

## CI, publication et décisions

- La publication d'une version est décrite dans [Publication](publication.md).
- Le comportement de Carbonite en CI, et les pièges qu'il évite, sont décrits dans [CI](ci.md).
- Notez chaque changement visible dans `CHANGELOG.md`.
- La conception est consignée dans [docs/decisions/0001-conception.md](decisions/0001-conception.md). Si une décision change, mettez ce document à jour dans la même pull request.

## Avant d'ouvrir une pull request

- [ ] `dotnet csharpier check .` passe.
- [ ] `CI=true dotnet test` passe.
- [ ] Chaque fichier `*.verified.*` créé ou modifié a été relu ; aucun fichier `*.received.*` n'est committé.
- [ ] Pour un changement de packaging : `eng/pack.sh`, puis `eng/package-smoke.sh`.
- [ ] Les messages de commit suivent Conventional Commits.
- [ ] Un changement de comportement est documenté : `README.md` en anglais, `docs/` en français, et `skills/carbonite` pour les agents.

---

Voir aussi : [Publication](publication.md) · [CI](ci.md) · [Format des fichiers](format.md).
