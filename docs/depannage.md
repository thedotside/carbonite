# Dépannage

Cette page reprend chaque message d'erreur de Carbonite et chaque symptôme courant : la cause, puis la correction.

Les messages de Carbonite sont en anglais ; cherchez dans cette page le début du message que vous voyez.

## Les messages d'erreur

### Snapshot mismatch

```text
Snapshot mismatch: FactureTests.Une_facture_complete

The value is different from the verified snapshot.
```

**Cause.** La valeur produite par le code diffère de la référence. Le message montre le diff, puis les chemins de la référence (`Verified:`) et du fichier reçu (`Received:`).

**Correction.** Lisez le diff. Si le changement est voulu, acceptez-le avec `SNAPSHOT_UPDATE=1`, ou en remplaçant la référence par le fichier reçu, puis relisez la modification dans votre pull request (voir [Cycle de vie](cycle-de-vie.md#4-accepter-un-changement)). Sinon, corrigez le code. Si le diff vous paraît inexplicable, voyez [Valeurs instables](#valeurs-instables) et [Les lignes du diff semblent identiques](#les-lignes-du-diff-semblent-identiques).

### Snapshot missing

```text
Snapshot missing: FactureTests.Une_facture_complete

There is no verified snapshot at /src/carbonite/samples/Sample.Facturation.Tests/__snapshots__/FactureTests.Une_facture_complete.verified.json.
On continuous integration, Carbonite never creates snapshots: a missing snapshot usually means that it was not committed.
```

**Cause.** En CI, la référence n'existe pas. Le plus souvent, elle n'a pas été committée. Autres causes :

- une règle du `.gitignore` exclut le fichier : vérifiez avec `git check-ignore -v <fichier>` ;
- le nom du snapshot a changé : test renommé, argument qui change à chaque exécution (voir [TUnit](tunit.md#comment-un-argument-devient-un-nom)), nom construit avec la culture de la machine (voir [Autres frameworks](autres-frameworks.md#tests-paramétrés--un-nom-par-cas)), majuscule changée et CI sous Linux (voir [CI](ci.md#majuscules-et-minuscules-dans-les-noms)).

**Correction.** Lancez les tests sur votre poste : Carbonite crée la référence. Relisez-la, puis committez-la. La valeur calculée en CI est dans le fichier reçu, à récupérer dans les artefacts (voir [CI](ci.md#récupérer-les-fichiers-reçus)).

### Snapshot format changed

```text
Snapshot format changed: FactureTests.Le_courriel_de_confirmation

The verified snapshot is …/FactureTests.Le_courriel_de_confirmation.verified.txt, but the value is now an object, stored as JSON in FactureTests.Le_courriel_de_confirmation.verified.json.
```

**Cause.** La valeur était une chaîne, enregistrée en `.txt`, et c'est maintenant un objet, enregistré en `.json`. Ou l'inverse. Par exemple, une méthode qui renvoyait un texte renvoie maintenant un objet.

**Correction.** Si le changement est voulu, supprimez l'ancienne référence, ou relancez avec `SNAPSHOT_UPDATE=1` : Carbonite écrit la nouvelle, puis supprime l'ancienne. Sinon, repassez la même sorte de valeur qu'avant.

### Snapshot conflict, ou « already checked earlier in this run »

```text
Snapshot conflict: FactureTests.Le_total_suit_la_quantite

This snapshot was already checked earlier in this run with a different value (the first one by FactureTests.Le_total_suit_la_quantite). One snapshot file cannot hold both, and SNAPSHOT_UPDATE cannot choose between them.

--- first value
+++ this value
...
```

**Cause.** Pendant la même exécution, deux valeurs différentes visent le même fichier. La seconde échoue toujours, même si elle correspond à la référence, en mode normal comme en mode mise à jour. Les causes habituelles :

- un test paramétré sans nom de cas, en mode générique (xUnit, NUnit, MSTest) ;
- `Snapshot.Match` dans une boucle, sans nom ;
- une méthode utilitaire partagée, en mode générique ;
- avec TUnit, deux cas dont les arguments donnent le même nom : `"a b"` et `"a_b"`, ou deux arguments qui ne diffèrent que par un `Guid` ou une date, remplacés par `Guid_1` ou `DateTime_1` dans le nom comme dans le snapshot ;
- une valeur qui n'est pas déterministe, par exemple dans un test `[Repeat]` ;
- deux noms qui ne diffèrent que par la casse (`Snapshot name conflict`) : Windows et macOS n'y voient qu'un seul fichier.

**Correction.** Donnez un nom à chaque valeur : `Snapshot.Match(valeur, $"cas-{id}")`. Voir [Autres frameworks](autres-frameworks.md#tests-paramétrés--un-nom-par-cas) et [TUnit](tunit.md#comment-un-argument-devient-un-nom). Si la valeur change toute seule, voyez [Valeurs instables](#valeurs-instables).

### Carbonite cannot find the test source file

```text
Carbonite cannot find the test source file '/_/tests/Facturation.Tests/FactureTests.cs', which tells it where the __snapshots__ folder is.
```

**Cause.** Carbonite range les snapshots à côté du fichier source du test, et ne trouve pas ce fichier. Deux situations :

- les tests ont été compilés avec des chemins déterministes (`ContinuousIntegrationBuild=true`, chemins en `/_/…`), et le dossier du projet n'a pas été enregistré à la compilation ;
- les tests compilés tournent sur une machine qui n'a pas leurs sources, par exemple quand seuls les binaires sont copiés vers l'agent de test.

**Correction.** Lancez les tests depuis la copie des sources. Vérifiez que le projet de test référence le paquet `TheDotSide.Carbonite`, directement ou par `TheDotSide.Carbonite.TUnit`, et qu'il ne définit pas `CarboniteRecordProjectDirectory` à `false`. Le mécanisme est décrit dans [CI](ci.md#builds-déterministes).

### Carbonite needs the path of the test source file

```text
Carbonite needs the path of the test source file to find its __snapshots__ folder, and none was provided. Call Snapshot.Match directly from C# test code.
```

**Cause.** Le compilateur C# n'a pas fourni le chemin du fichier appelant : appel par réflexion, ou paramètre `sourceFilePath` passé à la main avec une chaîne vide.

**Correction.** Appelez `Snapshot.Match` directement depuis le code C# du test, sans renseigner `sourceFilePath` ni `memberName` : le compilateur s'en charge.

### Carbonite received a Task, not its result

```text
Carbonite received a Task<int>, not its result. Did you forget 'await'? Write Snapshot.Match(await GetValueAsync()).
```

**Cause.** Vous avez passé une tâche (`Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`) au lieu de son résultat. Une tâche n'a pas encore de contenu : Carbonite refuse plutôt que d'enregistrer une valeur vide ou de bloquer le test.

**Correction.** Attendez la tâche :

```csharp
Snapshot.Match(await ChargerFactureAsync());
```

Avec TUnit, l'assertion attend une `Task` pour vous : `await Assert.That(ChargerFactureAsync()).MatchesSnapshot();`.

### Carbonite received an IAsyncEnumerable

```text
Carbonite received an IAsyncEnumerable<int>, which can only be read asynchronously. Read it first: Snapshot.Match(await values.ToListAsync()).
```

**Cause.** Une séquence asynchrone ne se lit qu'avec `await`.

**Correction.** Lisez-la d'abord : `Snapshot.Match(await valeurs.ToListAsync());`.

### Carbonite received a Stream, not its content

```text
Carbonite received a Stream, not its content. Read it first, for example: Snapshot.Match(await new StreamReader(stream).ReadToEndAsync()).
```

**Cause.** Un flux n'est qu'un accès à des données ; Carbonite ne le lit pas à votre place.

**Correction.** Lisez son contenu. Si votre code vient d'écrire dans le flux, revenez d'abord au début :

```csharp
flux.Position = 0;
Snapshot.Match(await new StreamReader(flux).ReadToEndAsync());
```

Pour un contenu binaire, passez un `byte[]` (par exemple `memoryStream.ToArray()`) : il s'écrit en Base64. Les snapshots d'images ou de fichiers binaires ne font pas partie de cette version.

### The value is nested more than 64 levels deep

```text
The value is nested more than 64 levels deep at $.Next.Next.Next…. It is probably an endless structure: ignore the member that loops with options.IgnoreMember("Name"), or snapshot a smaller object.
```

**Cause.** La valeur descend sur plus de 64 niveaux. C'est presque toujours une structure sans fin qui n'est pas une vraie boucle : chaque niveau est un nouvel objet. Par exemple, une propriété qui crée un nouvel objet à chaque lecture, ou un graphe d'entités chargé de proche en proche. Le chemin du message montre le membre qui se répète (ici `Next`).

**Correction.** Ignorez ce membre, `options.IgnoreMember("Next")`, ou vérifiez un objet plus petit, par exemple un type anonyme avec seulement les membres utiles (voir [Valeurs dynamiques](valeurs-dynamiques.md#garder-seulement-ce-qui-compte--les-types-anonymes)).

### The collection has more than 10000 items

```text
The collection at $ has more than 10000 items. Snapshot a smaller part of it (for example .Take(100)), or check that it is not endless.
```

**Cause.** Une collection dépasse 10 000 éléments, ou ne s'arrête jamais (une séquence `yield return` sans fin, par exemple). Le chemin (`$`, `$.Lignes`…) indique laquelle.

**Correction.** Vérifiez une partie de la collection, ou un résumé :

```csharp
Snapshot.Match(new { Nombre = lignes.Count, Premieres = lignes.Take(10) });
```

### The snapshot file is not valid UTF-8

```text
The snapshot file /src/carbonite/samples/Sample.Facturation.Tests/__snapshots__/FactureTests.Une_facture_complete.verified.json is not valid UTF-8. Save it as UTF-8, or delete it so that Carbonite creates it again.
```

**Cause.** La référence a été enregistrée dans un autre encodage, par exemple Windows-1252 ou UTF-16, par un éditeur ou un outil.

**Correction.** Enregistrez le fichier en UTF-8, ou supprimez-le : le prochain passage sur votre poste le recrée. Relisez-le avant de le committer.

### The snapshot name has no letter or digit

```text
The snapshot name '???' has no letter or digit. Use a name such as "after-payment". (Parameter 'explicitName')
```

**Cause.** Le nom passé à `Snapshot.Match` devient vide une fois nettoyé (voir [Démarrage](demarrage.md#le-nom-et-lemplacement-des-fichiers)). C'est une `ArgumentException`.

**Correction.** Utilisez un nom avec des lettres ou des chiffres, comme `"apres-paiement"`.

### Use a direct member access

```text
Use a direct member access such as x => x.Name, not 'facture => facture.Client.Nom'. (Parameter 'member')
```

**Cause.** `IgnoreMember<T>` et `ScrubMember<T>` n'acceptent qu'un accès direct à un membre de `T`. Un chemin (`f => f.Client.Nom`) ou un appel de méthode (`f => f.Numero.ToString()`) est refusé dès la configuration.

**Correction.** Visez le type qui déclare le membre, `IgnoreMember<Client>(client => client.Nom)`, ou utilisez le nom seul, `IgnoreMember("Nom")`, qui s'applique partout.

### Is not a valid regular expression

```text
'([a' is not a valid regular expression: …
```

**Cause.** Le motif passé à `Scrub` n'est pas une expression régulière .NET valide. Carbonite le vérifie dès la configuration.

**Correction.** Corrigez le motif. Écrivez-le dans une chaîne verbatim, `@"\d{4}"`, pour ne pas doubler chaque barre oblique inverse.

### Carbonite: the TUnit adapter could not be loaded

```text
Carbonite: the TUnit adapter could not be loaded (<raison>). Snapshots use the generic mode.
```

**Cause.** Le paquet `TheDotSide.Carbonite.TUnit` est présent, mais son assembly ne se charge pas, par exemple à cause de versions incompatibles. Les snapshots fonctionnent toujours, en mode générique : les cas paramétrés perdent leur suffixe, ce qui provoque des conflits.

**Correction.** Référencez seulement `TheDotSide.Carbonite.TUnit`, qui apporte le cœur dans la bonne version, sans référencer une autre version de `TheDotSide.Carbonite`. Utilisez TUnit 1.40 ou plus récent.

## Les symptômes

### Valeurs instables

**Symptôme.** Un test échoue une fois sur deux, ou sur le poste d'un collègue, sans changement de code. Le diff change à chaque fois, ou le message dit `Otherwise the value is not deterministic`.

**Causes et corrections.**

| Cause | Exemple | Correction |
| --- | --- | --- |
| Un ordre non garanti | un ensemble d'objets (`HashSet<Client>`), une requête sans tri, des résultats de tâches parallèles | Triez avant de vérifier : `.OrderBy(c => c.Nom)`. |
| Une valeur tirée au hasard | un jeton, un mot de passe généré, `Random` | `ScrubMember("Jeton")`, ou une valeur fixe injectée par le test. |
| Une date ou une heure non remplacée | `DateOnly`, `TimeOnly`, `TimeSpan`, une date dans une chaîne, `DontScrubDates()` | Une horloge fixe (`TimeProvider`), `ScrubMember` ou `Scrub`. |
| Un Guid dans une chaîne | `"commande c4c4ea33-…"` | `Scrub` avec un motif de Guid (voir [Valeurs dynamiques](valeurs-dynamiques.md#remplacer-du-texte--scrubmotif-remplacement)). |
| Une valeur propre à la machine | le nom de la machine, un chemin absolu, le dossier courant | `IgnoreMember`, `ScrubMember` ou `Scrub`. |
| La culture de la machine | `$"{montant}"` | Voir ci-dessous. |

### Le texte dépend de la culture

**Symptôme.** La référence contient `1299,80` et la CI reçoit `1299.80`, ou une date `18/11/2026` devient `11/18/2026`. Le test passe sur les postes en français et échoue ailleurs.

**Cause.** Votre code construit le texte avec la culture de la machine : interpolation `$"{montant}"`, `ToString()`, `string.Format`. Carbonite formate de façon invariante les valeurs qu'il écrit lui-même, mais une chaîne déjà construite lui arrive telle quelle.

**Correction.** Vérifiez un objet plutôt qu'un texte, ou formatez avec `CultureInfo.InvariantCulture`. Le détail et les exemples sont dans [CI](ci.md#culture-et-icu--quand-votre-code-formate-du-texte).

### Les lignes du diff semblent identiques

**Symptôme.** Les lignes `-` et `+` ont l'air identiques, et le message ajoute une note :

```text
--- verified
+++ received
@@ -1,1 +1,1 @@
-Total : 1 299,80 €
+Total : 1 299,80 €

Note: the only differences are whitespace (spaces, tabs or line breaks).
```

Dans cet exemple réel, la référence contient un espace insécable (U+00A0) entre `1` et `299`, et la nouvelle valeur un espace insécable étroit (U+202F).

**Causes.**

- des espaces invisibles venus du formatage selon la culture (voir [CI](ci.md#culture-et-icu--quand-votre-code-formate-du-texte)) ;
- une tabulation à la place d'espaces, ou des espaces en fin de ligne ;
- un éditeur qui a retouché la référence (voir la question suivante).

**Correction.** Affichez les caractères invisibles dans votre éditeur, ou comparez les octets des deux fichiers : `od -c <fichier>` sous Linux et macOS, `Format-Hex <fichier>` en PowerShell. Corrigez ensuite la source de l'écart.

### Un éditeur a modifié une référence

**Symptôme.** Un test échoue juste après que vous avez ouvert et enregistré un fichier `.verified`, avec la note sur les espaces, ou le message `is not valid UTF-8`.

**Cause.** L'éditeur a supprimé les espaces en fin de ligne, réindenté le JSON, ou changé l'encodage. Les fins de ligne `\r\n`, la marque d'ordre des octets (BOM) et les sauts de ligne finaux, eux, sont sans effet : Carbonite les ignore.

**Correction.** Restaurez le fichier avec `git restore <fichier>`. Ajoutez la section `.editorconfig` recommandée dans [Démarrage](demarrage.md#régler-git-et-léditeur). Si vous modifiez une référence à la main, gardez exactement la mise en forme de Carbonite.

### Le test passe sur mon poste mais échoue en CI

Passez en revue, dans cet ordre :

1. la référence a-t-elle été committée ? (`Snapshot missing`) ;
2. le texte dépend-il de la culture ? ([plus haut](#le-texte-dépend-de-la-culture)) ;
3. une chaîne, dans un objet, contient-elle des `\r\n` sous Windows ? ([CI](ci.md#fins-de-ligne)) ;
4. un nom de fichier ne diffère-t-il que par une majuscule ? ([CI](ci.md#majuscules-et-minuscules-dans-les-noms)) ;
5. la valeur dépend-elle de la machine ou de l'heure ? ([Valeurs instables](#valeurs-instables)) ;
6. les tests sont-ils compilés en mode déterministe ? ([Carbonite cannot find the test source file](#carbonite-cannot-find-the-test-source-file)).

Pour voir le problème avant de pousser, lancez `CI=true dotnet test` (bash, zsh) ou `$env:CI="true"; dotnet test` (PowerShell, puis `Remove-Item Env:CI`).

### SNAPSHOT_UPDATE n'a aucun effet

**Causes.**

- Une variable de CI est définie sur votre poste, par exemple `CI=true` dans votre profil de shell ou dans la configuration de votre IDE. Carbonite se croit en CI et ignore la mise à jour ; le message le dit (`SNAPSHOT_UPDATE is ignored on continuous integration`).
- La valeur n'est pas reconnue. Carbonite accepte `1`, `true`, `yes` et `on`, sans tenir compte de la casse.
- La syntaxe ne correspond pas au shell : `SNAPSHOT_UPDATE=1 dotnet test` ne fonctionne pas en PowerShell. Utilisez `$env:SNAPSHOT_UPDATE=1; dotnet test`.
- Deux valeurs visent le même snapshot : la mise à jour est refusée (`Snapshot conflict`).

### Les snapshots se mettent à jour tout seuls

**Cause.** Dans une session PowerShell, `$env:SNAPSHOT_UPDATE=1` reste défini jusqu'à la fermeture du terminal. Chaque `dotnet test` réécrit alors les références qui diffèrent, et la sortie des tests indique `Carbonite updated the snapshot …`. Une configuration d'exécution de l'IDE qui définit la variable a le même effet.

**Correction.** Retirez la variable, `Remove-Item Env:SNAPSHOT_UPDATE`, puis relisez avec `git diff` les références modifiées.

### J'ai renommé un test et l'ancien fichier reste

**Cause.** Carbonite ne supprime jamais une référence orpheline : cette version ne fait pas de nettoyage automatique.

**Correction.** Après le renommage, lancez les tests : la nouvelle référence est créée. Comparez-la à l'ancienne, puis supprimez l'ancienne dans la même pull request (`git rm <fichier>`).

### Des fichiers .received apparaissent dans git status

**Cause.** Ce sont les valeurs des derniers échecs. Ils ne doivent jamais être committés.

**Correction.** Ajoutez `*.received.*` au `.gitignore` (voir [Démarrage](demarrage.md#régler-git-et-léditeur)). Carbonite supprime un fichier reçu dès que son snapshot passe.

---

Voir aussi : [Cycle de vie](cycle-de-vie.md) · [CI](ci.md) · [Valeurs dynamiques](valeurs-dynamiques.md).
