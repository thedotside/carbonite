# Format des fichiers

Cette page donne toutes les règles qu'applique Carbonite pour écrire une valeur dans un snapshot.

Ces règles ont un but : le même fichier, octet pour octet, sur toutes les machines. Quelle que soit la culture, le système d'exploitation ou la version de .NET, une valeur qui ne change pas produit un snapshot qui ne change pas.

## Texte ou JSON

| Valeur passée à `Snapshot.Match` | Fichier | Contenu |
| --- | --- | --- |
| une chaîne (`string`) | `.verified.txt` | le texte tel quel |
| un `XDocument` ou un `XElement` (tout `XNode`) | `.verified.txt` | le XML indenté |
| toute autre valeur, `null` compris | `.verified.json` | du JSON indenté |

Exemples réels :

| Appel | Fichier | Contenu |
| --- | --- | --- |
| `Snapshot.Match("a\r\nb\r\n")` | `.txt` | `a` et `b` sur deux lignes |
| `Snapshot.Match(42)` | `.json` | `42` |
| `Snapshot.Match(null)` | `.json` | `null` |
| `Snapshot.Match(new StringBuilder("x"))` | `.json` | `"x"` |
| `Snapshot.Match(new XElement("facture", new XElement("total", "407.16")))` | `.txt` | le XML ci-dessous |

```xml
<facture>
  <total>407.16</total>
</facture>
```

## La mise en page du JSON

| Règle | Exemple |
| --- | --- |
| Indentation de deux espaces, fins de ligne `\n` | voir tous les exemples de cette page |
| Une propriété s'écrit `"Nom": valeur` | `"Total": 42` |
| Un objet ou une liste vide tient sur une ligne | `{}`, `[]` |
| Le fichier se termine par un saut de ligne | |

## Les membres d'un objet

| Règle | Détail |
| --- | --- |
| Membres écrits | Les propriétés publiques d'instance qui ont un `get` public, puis les champs publics. |
| Ordre | La classe de base d'abord. Dans chaque classe, les propriétés puis les champs, dans l'ordre de déclaration. |
| Type réel | Carbonite écrit les membres du type réel de l'objet, pas ceux du type déclaré. |
| Membre redéclaré (`new`, `override`) | Il garde la place du premier. |
| Membres ignorés | Membres statiques ou non publics, indexeurs, membres de type `Span<T>` ou autre `ref struct`, pointeurs. |
| Valeur `null` | `null` |
| Accesseur qui lève une exception | `"{Exception: InvalidOperationException: No value}"`, et le reste de l'objet est écrit normalement. |

Exemple tiré des tests du dépôt : une propriété déclarée `Animal` qui contient un `Dog`.

```csharp
public class Animal
{
    public string Name { get; set; } = "";
}

public sealed class Dog : Animal
{
    public bool GoodBoy { get; set; }
}

public sealed class Owner
{
    public Animal? Pet { get; set; }
}

Snapshot.Match(new Owner { Pet = new Dog { Name = "Rex", GoodBoy = true } });
```

```json
{
  "Pet": {
    "Name": "Rex",
    "GoodBoy": true
  }
}
```

| Type | Écrit comme |
| --- | --- |
| record | ses propriétés publiques, dans l'ordre de déclaration |
| type anonyme | ses propriétés, dans l'ordre d'écriture |
| tuple, `(1, "x")` ou `Tuple.Create(true)` | `{ "Item1": 1, "Item2": "x" }` |
| `KeyValuePair` | `{ "Key": …, "Value": … }` |

## Valeurs simples

Par défaut, les `Guid`, `DateTime` et `DateTimeOffset` sont remplacés par des marqueurs (`Guid_1`, `DateTime_1`…, voir [Valeurs dynamiques](valeurs-dynamiques.md)). Les formats ci-dessous s'appliquent quand le remplacement est désactivé, et aux valeurs qui restent toujours en clair (`Guid.Empty`, `MinValue`, `MaxValue`).

| Type | Valeur | Écrit |
| --- | --- | --- |
| `string` | `"café"` | `"café"` |
| `char`, `Rune` | `'é'` | `"é"` |
| `bool` | `true` | `true` |
| entiers (`int`, `long`, `byte`…, `Int128`, `BigInteger`) | `42`, `ulong.MaxValue` | `42`, `18446744073709551615` |
| `double`, `float`, `Half` | `1.5`, `0.1 + 0.2`, `1e20`, `-0.0` | `1.5`, `0.30000000000000004`, `1E+20`, `-0` |
| `NaN` et infinis | `double.NaN`, `double.PositiveInfinity` | `"NaN"`, `"Infinity"` (des chaînes : ce ne sont pas des nombres JSON) |
| `decimal` | `1234567.50m`, `39.5m` | `1234567.50`, `39.5` |
| enum | `DayOfWeek.Monday` | `"Monday"` |
| enum `[Flags]` | `Access.Read \| Access.Write` | `"Read, Write"` |
| enum sans nom pour cette valeur | `(Access)8` | `"8"` |
| `DateTime` UTC | `new DateTime(2026, 11, 18, 14, 30, 0, DateTimeKind.Utc)` | `"2026-11-18T14:30:00Z"` |
| `DateTime` UTC avec millisecondes | `new DateTime(2026, 11, 18, 14, 30, 0, 123, DateTimeKind.Utc)` | `"2026-11-18T14:30:00.123Z"` |
| `DateTime` local ou non précisé | `new DateTime(2026, 11, 18)` | `"2026-11-18T00:00:00"` |
| `DateTimeOffset` | `new DateTimeOffset(2026, 11, 18, 14, 30, 0, TimeSpan.FromHours(1))` | `"2026-11-18T14:30:00+01:00"` |
| `DateOnly` | `new DateOnly(2026, 11, 18)` | `"2026-11-18"` |
| `TimeOnly` | `new TimeOnly(14, 30, 5, 250)` | `"14:30:05.25"` |
| `TimeSpan` | `new TimeSpan(1, 2, 3, 4, 500)` | `"1.02:03:04.5000000"` |
| `Guid` | `Guid.Parse("8F1C2B7A-4D3E-4F5A-9B6C-7D8E9F0A1B2C")` | `"8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c"` |
| `Uri` | `new Uri("https://example.com/a?b=c#d")` | `"https://example.com/a?b=c#d"` (le texte d'origine) |
| `Version` | `new Version(1, 2, 3)` | `"1.2.3"` |
| `Type` | `typeof(List<int>)` | `"List<int>"` |
| `StringBuilder` | `new StringBuilder("built")` | `"built"` |
| `CultureInfo` | `CultureInfo.GetCultureInfo("fr-FR")` | `"fr-FR"` |
| `TimeZoneInfo` | un fuseau horaire | son identifiant (`Id`) |
| `Encoding` | `Encoding.UTF8` | `"utf-8"` |
| `IPAddress` | une adresse IP | son texte |
| `DBNull` | `DBNull.Value` | `null` |

Trois remarques :

- **Les nombres à virgule flottante** sont écrits sous leur forme la plus courte qui redonne exactement la même valeur. `0.1 + 0.2` s'écrit donc `0.30000000000000004` : c'est la vraie valeur calculée.
- **Un `decimal` garde ses décimales**, zéros compris. Dans l'exemple du dépôt, `149.90m * 0.9m` s'écrit `134.910`, et `39.5m` s'écrit `39.5`. Le snapshot montre la valeur telle que votre code l'a calculée.
- **Un `DateTime` local s'écrit sans décalage horaire** : le décalage dépend de la machine qui lance le test, pas de la valeur. Un `DateTime` UTC se termine par `Z`. Les fractions de seconde nulles disparaissent, le point compris.

Les noms de types suivent la syntaxe C#, sans espace de noms ni version d'assembly, pour ne pas changer d'une version de .NET à l'autre :

| Type | Nom écrit |
| --- | --- |
| `int`, `string`, `object`… | le mot-clé C# |
| `int?` | `int?` |
| `List<string>`, `Dictionary<string, int[]>` | `List<string>`, `Dictionary<string, int[]>` |
| `int[,]`, `byte[][]` | `int[,]`, `byte[][]` |
| `(int, string)` | `(int, string)` |
| un type imbriqué | avec ses types englobants : `Outer.Inner` |
| un type anonyme | `anonymous type` |
| un autre type généré par le compilateur | `compiler-generated type` |

## La culture ne change rien

Carbonite écrit chaque valeur avec la culture invariante. Les tests du dépôt vérifient que cette valeur, écrite avec `DontScrubDates()`, donne exactement le même JSON en `fr-FR`, `tr-TR`, `ar-SA` et `ja-JP` :

```csharp
var value = new
{
    Price = 1234.5m,
    Ratio = 0.25,
    When = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc),
    Day = DayOfWeek.Monday,
};
```

```json
{
  "Price": 1234.5,
  "Ratio": 0.25,
  "When": "2026-11-18T00:00:00Z",
  "Day": "Monday"
}
```

Attention : cette garantie vaut pour les valeurs que Carbonite formate lui-même. Une chaîne que votre code a déjà construite, par exemple `$"{montant}"`, dépend de la culture de la machine. Voir [CI](ci.md#culture-et-icu--quand-votre-code-formate-du-texte).

## Les chaînes dans le JSON

Seul ce qui est invisible ou ambigu est échappé. Le reste, accents et emoji compris, reste lisible.

| Caractère | Écrit |
| --- | --- |
| `"` | `\"` |
| `\` | `\\` |
| saut de ligne, tabulation | `\n`, `\t` |
| fin de ligne Windows `\r\n` | `\n`, comme sous Linux : un texte construit avec `AppendLine` sous Windows donne le même snapshot |
| retour chariot seul | `\r` : c'est du contenu |
| retour arrière, saut de page | `\b`, `\f` |
| autre caractère de contrôle, U+2028, U+2029, moitié isolée d'un caractère Unicode | `\u` suivi de quatre chiffres hexadécimaux, par exemple `\u0001`, `\u2028` |
| tout le reste : `é`, `😀`, `<`, `&`, `'` | tel quel |

Exemple réel : `"Emoji": "café 😀"` et `"Tab": "a\tb"`.

Les espaces insécables (U+00A0, U+202F) ne sont pas des caractères de contrôle : ils restent tels quels, donc invisibles à l'œil dans un diff. Voir [Dépannage](depannage.md#les-lignes-du-diff-semblent-identiques).

## Collections

| Valeur | Écrite comme | Ordre |
| --- | --- | --- |
| tableau, liste, tout `IEnumerable` | tableau JSON | l'ordre d'énumération |
| ensemble non trié (`HashSet<T>`, `ImmutableHashSet<T>`, `FrozenSet<T>`, tout `ISet<T>` ou `IReadOnlySet<T>`) de valeurs simples d'un même type | tableau JSON | trié par valeur ; le texte est trié en ordinal |
| ensemble non trié d'objets, ou de `Guid` remplacés par des marqueurs | tableau JSON | trié par contenu : le JSON de chaque élément, avec ses propres marqueurs |
| `SortedSet<T>`, `ImmutableSortedSet<T>` | tableau JSON | leur propre ordre |
| tableau multidimensionnel, `new[,] { { 1, 2 }, { 3, 4 } }` | tableaux imbriqués, `[[1, 2], [3, 4]]` | ligne par ligne |
| `byte[]` | une chaîne Base64 : `new byte[] { 1, 2, 3 }` donne `"AQID"` | |
| `Memory<T>`, `ReadOnlyMemory<T>` | tableau JSON | |
| groupe de `GroupBy` (`IGrouping<TKey, T>`) | `{ "Key": …, "Items": [ … ] }` | l'ordre du groupe |

Un ensemble n'a pas d'ordre : celui d'un `ImmutableHashSet` ou d'un `FrozenSet` change même d'un processus à l'autre. Carbonite trie donc toujours les ensembles non triés, objets compris. Une liste ou une requête LINQ garde au contraire son ordre : si cet ordre n'a pas de sens, triez-la vous-même, `Snapshot.Match(clients.OrderBy(c => c.Nom))`.

Un objet qui implémente `IEnumerable` est écrit comme la liste de ses éléments : ses autres propriétés, un `TotalCount` par exemple, n'apparaissent pas. Pour les voir, passez un type anonyme : `new { page.TotalCount, Elements = page.ToList() }`.

## Dictionnaires

Un dictionnaire (`IDictionary`, `IDictionary<TKey, TValue>` ou `IReadOnlyDictionary<TKey, TValue>`) s'écrit comme un objet JSON dont les propriétés sont les clés.

| Clés | Ordre | Exemple |
| --- | --- | --- |
| toutes des chaînes | tri ordinal : les majuscules avant les minuscules | `"B"`, `"a"`, `"b"` |
| valeurs simples comparables d'un même type (nombres, dates…) | tri par valeur | `"2"` avant `"10"` |
| `Guid` remplacés par des marqueurs, objets, types mélangés | tri par contenu : le JSON de la clé, puis celui de la valeur | les entrées d'un `Dictionary<Guid, int>` sont rangées par valeur |

| Clé | Écrite comme |
| --- | --- |
| valeur simple | son texte : `"10"`, `"Monday"` |
| `Guid`, `DateTime`, `DateTimeOffset` remplacés | leur marqueur : `"Guid_1"` |
| objet | son JSON sur une ligne : `"{\"X\":1}"` |

Les règles `IgnoreMember` et `ScrubMember` par nom s'appliquent aux clés texte.

## Types particuliers

| Valeur | Écrite comme |
| --- | --- |
| `JsonDocument`, `JsonElement`, `JsonNode` | le JSON lui-même, réindenté, dans l'ordre du document. Les nombres gardent leur écriture (`1.50`). Les règles `IgnoreMember` et `ScrubMember` par nom s'appliquent à ses propriétés. |
| `XDocument`, `XElement` à l'intérieur d'un objet | une chaîne qui contient le XML indenté, avec des `\n` |
| `Lazy<T>` | sa valeur : Carbonite l'évalue |
| `Assembly` | son nom simple |
| délégués, `Stream`, `CancellationToken`, `CancellationTokenSource`, `WaitHandle`, `Thread`, `SynchronizationContext`, `TaskScheduler`, `SafeHandle`, objet qui implémente `IServiceProvider` | un marqueur avec le nom du type : `"{Func<int>}"`, `"{MemoryStream}"`, `"{CancellationToken}"` |
| `Task`, `ValueTask`, `IAsyncEnumerable<T>` | refusés, même à l'intérieur d'un objet : voir [Valeurs refusées](#valeurs-refusées) |

Carbonite ne lit jamais ces valeurs opaques : parcourir un flux ou un conteneur d'injection de dépendances n'a pas de sens.

## Exceptions

```csharp
var exception = new ArgumentException(
    "Bad value",
    "amount",
    new InvalidOperationException("Inner")
);

Snapshot.Match(exception);
```

```json
{
  "$type": "ArgumentException",
  "Message": "Bad value (Parameter 'amount')",
  "ParamName": "amount",
  "InnerException": {
    "$type": "InvalidOperationException",
    "Message": "Inner"
  }
}
```

| Partie | Règle |
| --- | --- |
| `$type` | toujours en premier : le nom du type de l'exception |
| `Message` | toujours en second |
| membres propres au type | ensuite, par exemple `ParamName` |
| `Data` | seulement s'il contient des entrées |
| `InnerException` | seulement s'il y en a une ; une `AggregateException` liste déjà les siennes dans `InnerExceptions` |
| jamais écrits | `StackTrace`, `TargetSite`, `Source`, `HResult`, `HelpLink` : ils changent avec chaque ligne de code |

## Les marqueurs

| Marqueur | Sens |
| --- | --- |
| `"Guid_1"`, `"DateTime_1"`, `"DateTimeOffset_1"` | une valeur dynamique remplacée (voir [Valeurs dynamiques](valeurs-dynamiques.md)) |
| `"{Scrubbed}"` | un membre masqué par `ScrubMember` |
| `"{Circular reference}"` | un objet qui se contient lui-même |
| `"{Exception: Type: message}"` | un accesseur qui a levé une exception |
| `"{Type}"`, par exemple `"{MemoryStream}"` | une valeur opaque, jamais lue |

## Limites

| Situation | Résultat |
| --- | --- |
| Un objet qui se contient lui-même (un enfant qui pointe vers son parent) | `"{Circular reference}"` à l'endroit de la boucle. Un même objet présent dans deux branches différentes est simplement écrit deux fois. |
| Plus de 64 niveaux d'imbrication | `SnapshotException` : `The value is nested more than 64 levels deep at $.Next.Next…. It is probably an endless structure: ignore the member that loops with options.IgnoreMember("Name"), or snapshot a smaller object.` |
| Une collection de plus de 10 000 éléments | `SnapshotException` : `The collection at $ has more than 10000 items. Snapshot a smaller part of it (for example .Take(100)), or check that it is not endless.` |

Le chemin (`$`, `$.Next`, `$.Lignes[3]`…) indique où se trouve le problème : `$` est la valeur passée à `Snapshot.Match`.

Exemple de référence circulaire, tiré des tests du dépôt :

```json
{
  "Name": "root",
  "Parent": null,
  "Children": [
    {
      "Name": "child",
      "Parent": "{Circular reference}",
      "Children": []
    }
  ]
}
```

## Valeurs refusées

Certaines valeurs n'ont pas encore de contenu : les passer à `Snapshot.Match` est toujours une erreur. Carbonite les refuse et explique la correction.

| Valeur passée | Message |
| --- | --- |
| `Task`, `Task<T>` | `Carbonite received a Task<int>, not its result. Did you forget 'await'? Write Snapshot.Match(await GetValueAsync()).` |
| `ValueTask`, `ValueTask<T>` | `Carbonite received a ValueTask<int>, not its result. Did you forget 'await'? Write Snapshot.Match(await GetValueAsync()).` |
| `Stream` | `Carbonite received a Stream, not its content. Read it first, for example: Snapshot.Match(await new StreamReader(stream).ReadToEndAsync()).` |
| `IAsyncEnumerable<T>` | `Carbonite received an IAsyncEnumerable<int>, which can only be read asynchronously. Read it first: Snapshot.Match(await values.ToListAsync()).` |

Une tâche ou une séquence asynchrone est refusée **à toute profondeur** : un `await` oublié dans un objet ne doit pas donner un test vert. Le message donne le chemin du membre, et la façon de le masquer s'il ne s'agit pas d'une donnée :

```text
The value contains a Task<int> at $.Order.Invoice, not its result. Did you forget 'await'? Await it before taking the snapshot. If this member is not data, hide it: options.IgnoreMember("Invoice").
```

Carbonite ne lit jamais `Result` : sur une tâche inachevée, cela bloquerait le test pour toujours. Un type qui est à la fois `IEnumerable` et `IAsyncEnumerable<T>`, comme un `DbSet` d'EF Core, est lu comme une liste ordinaire. Un `Stream` n'est refusé qu'à la racine : à l'intérieur d'un objet, il devient `"{MemoryStream}"`.

## Le texte

Avant d'être comparé ou écrit, tout snapshot est mis sous une forme unique :

| Règle | Exemple |
| --- | --- |
| La marque d'ordre des octets (BOM) en tête est retirée. | |
| Les fins de ligne `\r\n` deviennent `\n`. | `"a\r\nb"` donne `a` et `b` sur deux lignes |
| Les sauts de ligne à la fin sont retirés. | `"a\n\n"` donne `a` |
| Un `\r` seul est gardé : c'est du contenu. | |
| Les lignes vides au début et au milieu sont gardées. | |

Cette mise en forme s'applique au texte final. Dans un snapshot JSON, une chaîne qui contient `\r\n` garde donc son `\r` : il est échappé en `\r\n` dans le fichier. Voir [CI](ci.md#fins-de-ligne).

Le fichier est écrit en UTF-8 sans BOM, avec des fins de ligne `\n` et un saut de ligne final.

---

Voir aussi : [Valeurs dynamiques](valeurs-dynamiques.md) · [CI](ci.md) · [Dépannage](depannage.md).
