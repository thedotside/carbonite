# 0001 — Conception de Carbonite

- **Statut :** accepté le 18/11/2026.
- **Portée :** version `0.1.0`.
- **Demande d'origine :** David (Odonno), le 18/11/2026 : « lib Snapshot Testing C# net10.0 et net11.0,
  simple à utiliser pour un dev noob, avec une belle doc, make no mistake. TUnit compliant aussi. »
  `Meziantou.Framework.SnapshotTesting` avait été jugée insuffisante.

## Intention

Le snapshot testing consiste à sérialiser une valeur produite par le code testé, puis à la comparer à
un fichier de référence versionné avec les tests. Le premier passage crée la référence. Les suivants
signalent le moindre écart. Accepter une évolution revient à mettre à jour un fichier et à le
relire dans la pull request.

Carbonite vise un développeur débutant : une ligne suffit, les erreurs expliquent quoi faire, et
les pièges classiques sont fermés par défaut. Le collectif impose TUnit (ADR OUT-16), qui est donc
le framework de première classe. Les autres frameworks fonctionnent aussi.

## Objectifs

- Une seule ligne : `Snapshot.Match(valeur);`, synchrone pour qu'un `await` oublié ne puisse pas
  rendre un test faussement vert.
- Une sortie lisible, déterministe et indépendante de la culture et du système d'exploitation.
- Un cycle de vie sûr : création locale automatique, jamais de création ni de mise à jour silencieuse
  en CI, mise à jour explicite par `SNAPSHOT_UPDATE=1`.
- TUnit en première classe : un snapshot par cas de test paramétré, plusieurs snapshots par test,
  `[Retry]` et `[Repeat]`, et une assertion `await Assert.That(valeur).MatchesSnapshot()` compatible
  avec `Assert.Multiple()`.
- Les autres frameworks (xUnit, NUnit, MSTest) fonctionnent sans adaptateur.
- Cibles `net10.0` et `net11.0`.
- Une documentation soignée : README en anglais pour NuGet, guide complet en français.

## Non-objectifs de la 0.1

- Snapshots en ligne qui réécrivent le code source.
- Lancement automatique d'un outil de diff.
- Snapshots binaires ou d'images.
- Nettoyage automatique des snapshots orphelins.
- Compatibilité trimming / Native AOT (la sérialisation repose sur la réflexion).
- Adaptateurs dédiés xUnit, NUnit ou MSTest.

## Anti-objectifs

Carbonite échoue même s'il « marche » si :

- un snapshot change d'une exécution à l'autre sans changement du code (Guid, dates, ordre d'un
  `HashSet`, culture `fr-FR` qui écrit `1,5`, fins de ligne Windows) ;
- un test passe alors que la valeur est fausse (création silencieuse en CI, `Task` passée sans
  `await`, mise à jour forcée en CI, deux cas de test qui partagent un fichier) ;
- un débutant ne peut pas expliquer ce qui s'est passé à la lecture du message d'erreur ;
- la bibliothèque impose un arbre de dépendances.

## Contraintes

- Conventions du collectif : `TheDotSide.<Projet>.<Composant>`, `src/`, `tests/`, `samples/`, `eng/`,
  versions centralisées et verrouillées (OUT-25), CSharpier (OUT-07, TST-10), commitlint (OUT-08,
  TST-11), tests TUnit (OUT-16).
- Aucune dépendance tierce dans le paquet cœur, donc rien à ajouter au référentiel ADR (OUT-26).
  L'adaptateur dépend seulement de TUnit, déjà imposé.
- Dépôt public `thedotside/carbonite`, licence MIT, auteurs `The Dot Side`.

## Approches envisagées

| Approche | Avantages | Inconvénients | Décision |
| --- | --- | --- | --- |
| S'appuyer sur Verify | Moteur éprouvé | Complexité et concepts de Verify, dépendances, ce n'est plus « notre » outil simple | Rejetée |
| Bibliothèque autonome + adaptateur TUnit | Contrôle total de la sortie et des garde-fous, cœur sans dépendance, fonctionne partout | Sérialiseur à écrire et à tester | **Retenue** |
| Bibliothèque TUnit uniquement | Code plus court | Exclut les autres frameworks, alors que la demande dit « TUnit compliant *aussi* » | Rejetée |

Pour l'intégration TUnit, deux variantes ont été comparées : un paquet unique qui détecte TUnit par
réflexion, ou un paquet d'adaptation typé. Le paquet d'adaptation `TheDotSide.Carbonite.TUnit` est
retenu : il est compilé contre l'API réelle de TUnit et testé. Le cœur le découvre seul s'il est
présent : le développeur n'a rien à configurer.

## Conception

### API publique

Namespace `TheDotSide.Carbonite` :

```csharp
Snapshot.Match(valeur);                                  // nom déduit du test
Snapshot.Match(valeur, "apres-paiement");                // nom explicite
Snapshot.Match(valeur, options => options.IgnoreMember("Id"));
Snapshot.Match(valeur, "apres-paiement", options => options.ScrubMember("Token"));

SnapshotDefaults.Configure(options => options.IgnoreMember("ETag")); // une fois au démarrage
```

Il y a deux surcharges seulement, pour éviter qu'une chaîne ne soit liée par erreur à un paramètre
`[CallerFilePath]` : `Match(object?, string? name = null, Action<SnapshotOptions>? configure = null)`
et `Match(object?, Action<SnapshotOptions> configure)`.

`SnapshotOptions` :

| Méthode | Effet |
| --- | --- |
| `IgnoreMember("Nom")` / `IgnoreMember<T>(x => x.Membre)` | Retire le membre de la sortie. |
| `ScrubMember("Nom")` / `ScrubMember<T>(x => x.Membre)` | Garde la clé, remplace la valeur par `{Scrubbed}`. |
| `Scrub(motif, remplacement)` | Expression régulière appliquée au texte final. |
| `DontScrubGuids()` / `DontScrubDates()` | Désactive le remplacement automatique. |

Avec l'adaptateur TUnit :

```csharp
await Assert.That(valeur).MatchesSnapshot();
await Assert.That(valeur).MatchesSnapshot("apres-paiement", options => options.IgnoreMember("Id"));
```

Erreurs : `SnapshotMismatchException` pour un écart, `SnapshotException` pour le reste (référence
absente en CI, `Task` sans `await`, fichier de test introuvable).

### Fichiers

- Dossier : `__snapshots__/`, à côté du fichier source du test.
- Nom : `<FichierDeTest>.<MéthodeDeTest>[_<cas>][_<n>]`, puis `.verified.json` pour une valeur
  sérialisée et `.verified.txt` pour une chaîne. L'extension `.snap` est volontairement écartée :
  Jest considère tout fichier `*.snap` sans test associé comme obsolète et le supprime avec `-u`.
- Un écart produit `<même nom>.received.json` (ou `.txt`) à côté de la référence, à ignorer dans Git.
- Les noms sont ramenés à l'ASCII (`été` → `ete`), les autres caractères deviennent `_`, et un nom
  trop long est tronqué avec une empreinte courte pour rester unique sous Windows.

### Cycle de vie

| Situation | Poste local | CI détectée |
| --- | --- | --- |
| Référence absente | Créée, test vert | Échec : « référence absente, l'avez-vous commitée ? » |
| Identique | Vert, `.received` périmé supprimé | Vert |
| Écart | `.received` écrit, échec avec diff | Idem |
| Écart et `SNAPSHOT_UPDATE=1` | Référence réécrite, test vert | Variable ignorée, échec avec diff et explication |

La CI est détectée par `CI` (sauf `false` ou `0`), `TF_BUILD`, `GITHUB_ACTIONS`, `GITLAB_CI`,
`JENKINS_URL`, `TEAMCITY_VERSION`, `BUILDKITE`, `CIRCLECI`, `TRAVIS`, `APPVEYOR`,
`BITBUCKET_BUILD_NUMBER` et `CODEBUILD_BUILD_ID`.

La comparaison ignore le BOM UTF-8, les fins de ligne (`\r\n` = `\n`) et les sauts de ligne finaux.
Les fichiers sont écrits en UTF-8 sans BOM, avec `\n` et un saut de ligne final, par écriture
atomique (fichier temporaire puis renommage).

Le message d'échec contient un diff unifié lisible (contexte de trois lignes, tronqué au-delà d'une
limite), les deux chemins, et les deux façons d'accepter : renommer `.received` en `.verified`, ou
relancer avec `SNAPSHOT_UPDATE=1` (syntaxes bash et PowerShell).

### Sérialisation

Sérialiseur maison, sans `System.Text.Json`, pour contrôler chaque règle :

- chaîne à la racine : texte brut ; tout le reste : JSON indenté de deux espaces, `\n` ;
- propriétés publiques puis champs publics, dans l'ordre de déclaration, classes de base d'abord,
  toujours selon le type réel de l'objet (pas le type déclaré) ;
- types et formats invariants : nombres en culture invariante (`double` aller-retour, `NaN` en
  chaîne), dates ISO 8601 sans zéros inutiles, `Guid` en minuscules, enums par leur nom, `Type` en
  nom C# lisible sans version d'assembly ;
- dictionnaires et ensembles non ordonnés toujours triés : chaînes en ordinal, valeurs simples
  comparables par valeur, objets et `Guid` aléatoires par leur contenu (amendé le 18/11 après revue :
  un `ImmutableHashSet` ou un `FrozenSet` d'objets changeait d'ordre d'un processus à l'autre) ;
- tableaux d'octets en Base64, tableaux multidimensionnels imbriqués, `IGrouping` avec sa clé ;
- `JsonElement`, `JsonDocument`, `JsonNode` réécrits en JSON indenté ; `XDocument` et `XElement` en
  texte ;
- exceptions : `$type`, `Message`, membres propres au type, exception interne ; jamais la pile
  d'appels ;
- caractères non ASCII conservés (`é`, emoji) ; seuls les caractères de contrôle, `"` et `\` sont
  échappés ; dans toute chaîne, `\r\n` s'écrit `\n`, comme pour une chaîne à la racine (amendé : un
  texte construit sous Windows différait de Linux) ;
- référence circulaire : `{Circular reference}` ; profondeur au-delà de 64 : erreur explicite ;
  collection au-delà de 10 000 éléments : erreur explicite ;
- getter qui lève une exception : `{Exception: Type: message}` ;
- types opaques ou dangereux à parcourir (`Stream`, `CancellationToken`, délégués, `WaitHandle`,
  pointeurs, types `ref struct`) : marqueur `{Type}`.

Une `Task`, une `ValueTask` ou un `IAsyncEnumerable<T>` qui ne se lit pas de façon synchrone lèvent
une `SnapshotException` **à toute profondeur**, avec le chemin du membre et la suggestion
`options.IgnoreMember("…")`, sans jamais lire `Result`. À la racine, un `Stream` est aussi refusé.
(Amendé le 18/11 : le marqueur `{Task<int>}` laissait passer un `await` oublié dans un objet.)

### Valeurs dynamiques

Par défaut, chaque `Guid`, `DateTime` et `DateTimeOffset` est remplacé par `Guid_1`, `DateTime_1`,
`DateTimeOffset_1`… dans l'ordre d'apparition. Une même valeur reçoit le même marqueur, ce qui garde
les relations visibles. `Guid.Empty`, `DateTime.MinValue` et `DateTime.MaxValue` restent en clair.
`DontScrubGuids()` et `DontScrubDates()` désactivent ce comportement.

### Intégration TUnit

Le cœur cherche `TheDotSide.Carbonite.TUnit.TUnitTestContextProvider` par son nom d'assembly. Si
l'adaptateur est référencé, il l'utilise ; sinon il passe en mode générique.

| Information | Mode TUnit | Mode générique |
| --- | --- | --- |
| Fichier du test | `TestDetails.TestFilePath` | `[CallerFilePath]` |
| Nom du test | `TestDetails.MethodName` | `[CallerMemberName]` |
| Cas paramétré | Suffixe tiré des arguments de méthode et des arguments simples de classe, écrits par le sérialiseur du snapshot : culture invariante et mêmes marqueurs `Guid_1`, `DateTime_1` (amendé : `ToString()` dépendait de la culture) | Nom explicite à fournir |
| Plusieurs snapshots | Compteur par test et par tentative (`_2`, `_3`…) | Nom explicite à fournir |

Dans les deux modes, un registre de l'exécution détecte deux valeurs différentes écrites dans le
même snapshot (cas paramétrés sans nom, boucle, méthode utilitaire partagée), et deux noms qui ne
diffèrent que par la casse (un seul fichier sous Windows et macOS). Le contrôle a lieu avant la
lecture du fichier : la seconde valeur échoue en « conflict » quel que soit l'ordre des cas, même
si elle égale le fichier, avec le diff entre les deux valeurs et la façon de nommer chaque cas. En
mode mise à jour, ce conflit est aussi une erreur. (Amendé le 18/11 après revue.)

L'adaptateur dépend de `TUnit.Core` et `TUnit.Assertions` à partir de `1.40.0`. Cette API est
identique en `1.40.0`, `1.80.0` et `1.87.0`. Un plancher bas évite de désaligner le moteur TUnit
du consommateur, car les dépendances de TUnit sont des minimums.

### Builds déterministes

Avec `ContinuousIntegrationBuild=true`, le compilateur remplace les chemins source par `/_/…`. Le
paquet cœur ajoute donc, par `buildTransitive`, l'attribut
`AssemblyMetadata("TheDotSide.Carbonite.ProjectDirectory", "$(MSBuildProjectDirectory)")` au projet
de test. À l'exécution, un chemin introuvable est recherché sous les dossiers parents du projet. En
cas d'échec, l'erreur explique la cause et la correction.

### Paquets et dépôt

- `TheDotSide.Carbonite` : cœur, aucune dépendance, `buildTransitive` pour les chemins.
- `TheDotSide.Carbonite.TUnit` : dépend du cœur à la même version et de TUnit.
- Lien source (SourceLink du SDK), symboles `.snupkg`, documentation XML, `TreatWarningsAsErrors`,
  `Meziantou.Analyzer` en référence globale privée.
- CI Linux, Windows et macOS : formatage, compilation, tests des deux cibles avec `CI=true`, paquets,
  essai des paquets dans des projets consommateurs isolés, avec TUnit 1.40.0 et la dernière version. Publication par étiquette avec trusted
  publishing NuGet, inactive tant que la politique NuGet et l'environnement `nuget` ne sont pas
  configurés.

## Choix faits par Claude, à relire

- Extensions `.verified.json` / `.verified.txt` au lieu de `.snap` (risque de suppression par Jest).
- Remplacement des `Guid` et des dates activé par défaut, comme Verify.
- Création de la référence au premier passage local, comme Jest ; jamais en CI.
- Comparaison insensible aux sauts de ligne finaux.
- Messages d'erreur en anglais, documentation en français, README en anglais.
- Plancher TUnit `1.40.0`, vérifié à l'exécution par l'essai des paquets ; pas de borne haute.
- Scripts `eng/*.sh` en bash, vérifiables sur macOS, plutôt qu'en PowerShell.
- Une chaîne CRLF et une chaîne LF donnent le même snapshot : pour tester les fins de ligne
  elles-mêmes, utiliser une assertion classique.
- Pas de mode d'emploi pour Native AOT dans la 0.1.
