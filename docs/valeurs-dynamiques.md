# Valeurs dynamiques

Cette page montre comment Carbonite traite les valeurs qui changent à chaque exécution, et comment masquer ou remplacer ce qui ne concerne pas le test.

Une valeur **dynamique** change d'une exécution à l'autre sans que le code change : un identifiant tiré au hasard, la date du jour. Si elle arrive telle quelle dans un snapshot, le test échoue à chaque passage.

## Guid et dates : remplacés par défaut

```csharp
var id = Guid.NewGuid();

Snapshot.Match(new
{
    Id = id,
    Other = Guid.NewGuid(),
    Again = id,
    Empty = Guid.Empty,
    Created = DateTime.UtcNow,
    Min = DateTime.MinValue,
    At = DateTimeOffset.Now,
});
```

```json
{
  "Id": "Guid_1",
  "Other": "Guid_2",
  "Again": "Guid_1",
  "Empty": "00000000-0000-0000-0000-000000000000",
  "Created": "DateTime_1",
  "Min": "0001-01-01T00:00:00",
  "At": "DateTimeOffset_1"
}
```

Les règles :

- chaque `Guid` devient `Guid_1`, `Guid_2`…, chaque `DateTime` devient `DateTime_1`…, chaque `DateTimeOffset` devient `DateTimeOffset_1`…, dans l'ordre où ils apparaissent dans le fichier ;
- une même valeur reçoit toujours le même marqueur : `Again` vaut `Guid_1`, comme `Id`. Les relations restent visibles, par exemple une facture qui pointe bien vers son client ;
- la numérotation repart de 1 pour chaque snapshot ;
- les clés de dictionnaire sont traitées de la même façon : un `Dictionary<Guid, string>` s'écrit `{ "Guid_1": … }` ;
- restent en clair, parce qu'elles ont un sens : `Guid.Empty`, `DateTime.MinValue`, `DateTime.MaxValue`, `DateTimeOffset.MinValue` et `DateTimeOffset.MaxValue`.

Ne sont **pas** remplacés :

| Valeur | Pourquoi c'est un piège | Que faire |
| --- | --- | --- |
| `DateOnly`, `TimeOnly`, `TimeSpan` | `DateOnly.FromDateTime(DateTime.Today)` change chaque jour. | Une valeur fixe, `IgnoreMember` ou `ScrubMember`. |
| Un Guid ou une date **dans une chaîne** | `"commande c4c4ea33-57a7-4497-9acc-10a3ad737a52"` reste tel quel. | `Scrub(motif, remplacement)`, plus bas. |

## Garder les vraies valeurs : DontScrubGuids() et DontScrubDates()

Quand votre test fixe lui-même les valeurs, et que ces valeurs comptent, désactivez le remplacement :

```csharp
Snapshot.Match(
    new
    {
        Id = Guid.Parse("8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c"),
        When = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
    },
    options => options.DontScrubGuids().DontScrubDates()
);
```

```json
{
  "Id": "8f1c2b7a-4d3e-4f5a-9b6c-7d8e9f0a1b2c",
  "When": "2026-01-02T03:04:05Z"
}
```

C'est utile pour vérifier un calcul de date, par exemple une échéance à 30 jours. Le plus simple est alors de donner à votre code une horloge que le test contrôle, par exemple un `TimeProvider`. Les formats des dates sont décrits dans [Format des fichiers](format.md#valeurs-simples).

## Masquer un membre : IgnoreMember et ScrubMember

```csharp
var user = new
{
    Name = "Ada",
    Password = "secret",
    Token = "abc123",
};

Snapshot.Match(user, options => options.IgnoreMember("Password").ScrubMember("Token"));
```

```json
{
  "Name": "Ada",
  "Token": "{Scrubbed}"
}
```

| Méthode | Effet |
| --- | --- |
| `IgnoreMember("Nom")` | Le membre disparaît du snapshot. |
| `ScrubMember("Nom")` | Le membre reste, sa valeur devient `"{Scrubbed}"`. |

`ScrubMember` prouve que le membre existe sans figer sa valeur : un jeton est bien généré, mais sa valeur change à chaque fois. `IgnoreMember` convient quand le membre ne concerne pas le test.

### Par nom : partout dans la valeur

Un nom vise tous les membres qui le portent, à tous les niveaux : propriétés et champs des objets, clés texte des dictionnaires, propriétés des documents JSON (`JsonDocument`, `JsonElement`, `JsonNode`). Le nom est exact et sensible à la casse.

```csharp
using var body = JsonDocument.Parse("""{"Password":"j","ok":true}""");

var value = new
{
    Password = "secret",
    User = new { Name = "a", Password = "x" },
    Headers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Password"] = "p",
        ["Accept"] = "json",
    },
    Body = body,
};

Snapshot.Match(value, options => options.IgnoreMember("Password"));
```

```json
{
  "User": {
    "Name": "a"
  },
  "Headers": {
    "Accept": "json"
  },
  "Body": {
    "ok": true
  }
}
```

### Par expression : un type et ses types dérivés

Pour viser le membre d'un seul type, passez une expression. L'exemple vient du test `Seulement_les_montants` du dépôt :

```csharp
Snapshot.Match(
    UneFacture(),
    options =>
        options.IgnoreMember("Lignes").IgnoreMember<Facture>(facture => facture.Client)
);
```

```json
{
  "Id": "Guid_1",
  "Numero": "FAC-2026-0042",
  "Pays": "FR",
  "CreeeLe": "DateTime_1",
  "TauxDeTva": 0.20,
  "TotalHt": 339.30,
  "Tva": 67.86,
  "TotalTtc": 407.16
}
```

Les règles :

- la règle vaut pour le type indiqué et pour les types qui en dérivent ; un membre du même nom dans un autre type reste visible ;
- l'expression doit être un accès direct à un membre, comme `x => x.Client`. Sinon, Carbonite lève une `ArgumentException` dès la configuration : `Use a direct member access such as x => x.Name, not '…'.` ;
- une règle par expression ne s'applique ni aux clés de dictionnaire ni aux propriétés JSON. Utilisez le nom pour celles-là.

## Remplacer du texte : Scrub(motif, remplacement)

`Scrub` applique une expression régulière .NET au texte final du snapshot : le JSON tel qu'il sera écrit, ou le texte d'une chaîne.

```csharp
Snapshot.Match(
    new { Reference = "FAC-2026-0042", Lien = "https://exemple.fr/factures/FAC-2026-0042" },
    options => options.Scrub(@"FAC-\d{4}-\d{4}", "FAC-XXXX-XXXX")
);
```

```json
{
  "Reference": "FAC-XXXX-XXXX",
  "Lien": "https://exemple.fr/factures/FAC-XXXX-XXXX"
}
```

Le cas classique : un Guid au milieu d'un texte, que le remplacement automatique ne voit pas.

```csharp
var id = Guid.NewGuid();

Snapshot.Match(
    $"Commande {id} créée",
    options => options.Scrub(
        @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "{Guid}"
    )
);
```

```text
Commande {Guid} créée
```

Les règles :

- le motif est vérifié tout de suite : un motif invalide lève une `ArgumentException` à la configuration, par exemple `'([a' is not a valid regular expression: …` ;
- dans le remplacement, `$1` désigne le premier groupe capturé ;
- `Scrub` passe en dernier, après le remplacement des Guid et des dates et après `IgnoreMember` et `ScrubMember` ;
- plusieurs `Scrub` s'appliquent dans l'ordre où vous les avez déclarés, ceux des réglages globaux d'abord.

Le motif voit le JSON écrit, guillemets compris. Gardez-le précis : un motif trop large masque aussi des valeurs qui comptent.

## Garder seulement ce qui compte : les types anonymes

Souvent, le plus simple est de ne pas envoyer toute la valeur. Un type anonyme choisit les membres utiles. L'exemple vient du test `La_TVA_depend_du_pays` du dépôt :

```csharp
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
```

```json
{
  "Pays": "FR",
  "TotalHt": 339.30,
  "Tva": 67.86,
  "TotalTtc": 407.16
}
```

Pourquoi c'est mieux : le snapshot est court et se lit d'un coup d'œil ; il ne contient ni Guid ni date, donc aucune option n'est nécessaire ; un changement ailleurs dans la facture ne casse pas ce test. Pour une liste, projetez chaque élément : `factures.Select(f => new { f.Numero, f.TotalTtc })`.

## Les réglages globaux : SnapshotDefaults.Configure

Certaines règles valent pour tous les tests : un en-tête `ETag`, un jeton technique. Déclarez-les une seule fois, au démarrage.

Avec n'importe quel framework, dans une méthode `[ModuleInitializer]`, exécutée au chargement de l'assembly de test :

```csharp
using System.Runtime.CompilerServices;
using TheDotSide.Carbonite;

internal static class CarboniteSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        SnapshotDefaults.Configure(options => options.ScrubMember("Jeton").IgnoreMember("ETag"));
}
```

Si votre projet de test est une bibliothèque de classes, l'analyseur CA2255 signale cet attribut, prévu pour le code d'application. Dans un projet de test, vous pouvez désactiver l'avertissement autour de la méthode (`#pragma warning disable CA2255`).

Avec TUnit, dans un hook qui s'exécute avant toute la session de tests :

```csharp
using TheDotSide.Carbonite;

public static class CarboniteSetup
{
    [Before(TestSession)]
    public static void ConfigurerCarbonite() =>
        SnapshotDefaults.Configure(options => options.IgnoreMember("ETag"));
}
```

Les règles :

- les réglages globaux s'appliquent à chaque snapshot de l'exécution, avant les options de chaque appel ;
- chaque appel à `Configure` ajoute ses règles à celles des appels précédents ;
- les options d'un appel s'ajoutent aux réglages globaux, sans pouvoir en retirer. Seuls `DontScrubGuids()` et `DontScrubDates()` désactivent quelque chose ;
- une configuration qui échoue, par exemple un motif invalide, laisse les réglages globaux inchangés ;
- les réglages valent pour tout le processus de test. Configurez-les une fois au démarrage, pas dans un test : un autre test, exécuté en parallèle, les verrait aussi.

## Récapitulatif des options

Chaque méthode renvoie les mêmes options : vous pouvez les enchaîner.

| Méthode | Effet |
| --- | --- |
| `IgnoreMember("Nom")` | Retire partout les membres, clés texte de dictionnaire et propriétés JSON de ce nom. |
| `IgnoreMember<T>(x => x.Membre)` | Retire ce membre de `T` et des types dérivés. |
| `ScrubMember("Nom")` | Garde la clé, remplace la valeur par `"{Scrubbed}"`, partout. |
| `ScrubMember<T>(x => x.Membre)` | Pareil, pour ce membre de `T` et des types dérivés. |
| `Scrub(motif, remplacement)` | Applique une expression régulière au texte final. |
| `DontScrubGuids()` | Écrit les `Guid` tels quels. |
| `DontScrubDates()` | Écrit les `DateTime` et `DateTimeOffset` tels quels. |

---

Voir aussi : [Format des fichiers](format.md) · [TUnit](tunit.md) · [Dépannage](depannage.md#valeurs-instables).
