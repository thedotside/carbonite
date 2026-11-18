# Démarrage

Cette page vous mène de l'installation au premier snapshot committé.

## Installer le paquet

| Votre framework de test | Commande |
| --- | --- |
| TUnit 1.40 ou plus récent | `dotnet add package TheDotSide.Carbonite.TUnit` |
| xUnit, NUnit, MSTest | `dotnet add package TheDotSide.Carbonite` |

Le paquet TUnit apporte le paquet cœur `TheDotSide.Carbonite` avec lui. Il n'y a rien d'autre à configurer : Carbonite trouve l'adaptateur TUnit tout seul.

Le projet de test doit cibler .NET 10 ou .NET 11 (`net10.0` ou `net11.0`).

## Écrire le premier test

L'exemple vient du projet `samples/Sample.Facturation.Tests` du dépôt : une petite facturation, testée avec TUnit.

```csharp
using TheDotSide.Carbonite;

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

    [Test]
    public void Une_facture_complete()
    {
        Snapshot.Match(UneFacture());
    }
}
```

Un **snapshot** est une photo de la valeur, enregistrée dans un fichier. `Snapshot.Match` compare la valeur à cette photo.

`Snapshot.Match` est synchrone : il n'y a rien à attendre, donc pas d'`await` à oublier. Impossible, donc, qu'un test passe parce que la vérification n'a jamais eu lieu.

## Le premier passage

```bash
dotnet test
```

Le test passe, et Carbonite crée un fichier à côté de `FactureTests.cs` :

```text
samples/Sample.Facturation.Tests/
├── FactureTests.cs
└── __snapshots__/
    └── FactureTests.Une_facture_complete.verified.json
```

Voici son contenu, tel qu'il est committé dans le dépôt :

```json
{
  "Id": "Guid_1",
  "Numero": "FAC-2026-0042",
  "Client": "Ada Lovelace",
  "Pays": "FR",
  "CreeeLe": "DateTime_1",
  "Lignes": [
    {
      "Libelle": "Chaise ergonomique",
      "Quantite": 2,
      "PrixUnitaireHt": 149.90,
      "TotalHt": 299.80
    },
    {
      "Libelle": "Lampe de bureau",
      "Quantite": 1,
      "PrixUnitaireHt": 39.50,
      "TotalHt": 39.50
    }
  ],
  "TauxDeTva": 0.20,
  "TotalHt": 339.30,
  "Tva": 67.86,
  "TotalTtc": 407.16
}
```

Trois choses à remarquer :

- `Id` et `CreeeLe` changent à chaque exécution (`Guid.NewGuid()` et `DateTime.UtcNow`). Carbonite les remplace par `Guid_1` et `DateTime_1` ; sans cela, le test échouerait à chaque fois. Voir [Valeurs dynamiques](valeurs-dynamiques.md).
- Les propriétés calculées (`TotalHt`, `Tva`, `TotalTtc`) sont présentes : Carbonite écrit toutes les propriétés publiques, dans l'ordre de déclaration.
- Les nombres s'écrivent avec un point, quelle que soit la culture de la machine : `149.90`, jamais `149,90`. Voir [Format des fichiers](format.md).

Carbonite signale aussi la création dans la sortie standard du test :

```text
Carbonite created the snapshot /src/carbonite/samples/Sample.Facturation.Tests/__snapshots__/FactureTests.Une_facture_complete.verified.json. Review it before committing it.
```

Cette ligne s'affiche dans le détail du test : dans votre IDE, ou avec `dotnet test --output detailed` sous Microsoft Testing Platform (le cas de TUnit).

## Relire, puis committer

Le fichier créé devient la **référence** : la valeur attendue. Le premier passage ne vérifie rien, il enregistre. Relisez donc ce fichier comme une assertion écrite à la main : les montants sont-ils justes ? manque-t-il une ligne ?

Committez ensuite le dossier `__snapshots__` avec le test :

```bash
git add samples/Sample.Facturation.Tests/__snapshots__
git commit -m "test: snapshot d'une facture complète"
```

C'est indispensable : en CI, Carbonite ne crée jamais de référence. Un snapshot oublié fait échouer le test avec le message `Snapshot missing` (voir [Cycle de vie](cycle-de-vie.md)).

## Le nom et l'emplacement des fichiers

Carbonite range les snapshots dans un dossier `__snapshots__`, à côté du fichier source du test. Le nom suit ce modèle :

```text
<Fichier>.<Test>[_<cas>][.<nom>][.<n>].verified.json
```

| Partie | Origine | Exemple réel |
| --- | --- | --- |
| `<Fichier>.<Test>` | le fichier de test et la méthode de test | `FactureTests.Une_facture_complete` |
| `_<cas>` | les arguments d'un test paramétré (TUnit seulement) | `FactureTests.La_TVA_depend_du_pays_FR` |
| `.<nom>` | le nom passé à `Snapshot.Match(valeur, "nom")` | `FactureTests.Une_facture_avant_et_apres_remise.avant-remise` |
| `.<n>` | le numéro d'un snapshot qui en suit un autre de même nom, dans le même test (TUnit seulement) | `ParameterizedTests.Two_unnamed_snapshots_in_one_test.2` |

L'extension dépend de la valeur :

- une chaîne (`string`) est enregistrée telle quelle, dans un fichier `.verified.txt` ;
- toute autre valeur est écrite en JSON indenté, dans un fichier `.verified.json`.

Le détail, avec le cas du XML, est dans [Format des fichiers](format.md#texte-ou-json).

Par exemple, le test `Le_courriel_de_confirmation` de l'exemple vérifie une chaîne. Sa référence, `FactureTests.Le_courriel_de_confirmation.verified.txt`, contient simplement le texte :

```text
Bonjour Ada Lovelace,

Votre facture FAC-2026-0042 est disponible :
- Chaise ergonomique x 2 : 299.80 EUR
- Lampe de bureau x 1 : 39.50 EUR

Total TTC : 407.16 EUR
```

Les noms restent utilisables dans n'importe quel shell :

- les accents sont retirés : `après paiement` devient `apres_paiement` ;
- Carbonite garde les lettres ASCII, les chiffres, `.` et `-` ; tout autre caractère devient `_`, et plusieurs caractères de suite ne donnent qu'un seul `_` ;
- les `_`, `.` et `-` sont retirés aux deux extrémités ;
- au-delà de 120 caractères (sans compter le dossier ni `.verified.json`), le nom est coupé et complété par une empreinte de 8 caractères, pour rester unique et tenir dans les chemins Windows ;
- un nom explicite sans lettre ni chiffre, comme `"???"`, est refusé.

Les détails propres à chaque framework sont dans [TUnit](tunit.md) et [Autres frameworks](autres-frameworks.md).

## Régler Git et l'éditeur

Ajoutez ces lignes au dépôt de votre projet. Ce sont celles du dépôt de Carbonite.

Dans `.gitignore`, pour ne jamais committer un fichier reçu :

```gitignore
# Carbonite : valeurs qui ne correspondaient pas à leur snapshot. À relire, jamais à committer.
*.received.*
```

Un **fichier reçu** (`*.received.json` ou `*.received.txt`) est écrit à côté de la référence quand la valeur diffère. Il sert à relire ou à accepter la nouvelle valeur, puis Carbonite le supprime au prochain passage réussi.

Dans `.gitattributes`, pour garder les références en fins de ligne LF :

```gitattributes
# Les références restent en LF sur toutes les plateformes.
**/__snapshots__/** text eol=lf
```

Carbonite écrit toujours des fins de ligne LF et ignore déjà `\r\n` quand il compare. Cette ligne garde en plus les fichiers identiques octet pour octet sur toutes les machines, quel que soit le réglage `core.autocrlf` de chacun.

Dans `.editorconfig`, pour qu'un éditeur ne retouche pas une référence :

```editorconfig
[**/__snapshots__/**]
# Écrit par Carbonite : un éditeur ne doit pas réécrire une référence.
trim_trailing_whitespace = false
```

La comparaison ignore les fins de ligne, mais pas les espaces en fin de ligne. Un éditeur qui les supprime casse un snapshot texte.

---

Voir aussi : [Cycle de vie](cycle-de-vie.md) · [TUnit](tunit.md) · [Autres frameworks](autres-frameworks.md).
