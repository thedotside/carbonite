# Documentation de Carbonite

Cette page présente Carbonite et vous oriente dans le guide.

Carbonite est une bibliothèque de *snapshot testing* pour .NET 10 et .NET 11. Une ligne, `Snapshot.Match(valeur);`, enregistre la valeur produite par votre code dans un fichier rangé à côté du test. Les exécutions suivantes comparent la valeur à ce fichier et signalent le moindre écart avec un diff lisible.

Carbonite vise d'abord les développeurs qui débutent. Les messages d'erreur disent quoi faire, et les pièges classiques sont désamorcés par défaut : Guid et dates qui changent à chaque exécution, culture de la machine, fins de ligne Windows, création silencieuse de fichiers en intégration continue. TUnit est le framework de première classe ; xUnit, NUnit et MSTest fonctionnent aussi.

## Vocabulaire

| Terme | Sens |
| --- | --- |
| **Snapshot** | Une photo d'une valeur à un instant donné, enregistrée dans un fichier texte. Le test compare la valeur du jour à cette photo. |
| **Référence** | Le fichier `*.verified.json` ou `*.verified.txt`, rangé dans le dossier `__snapshots__` et committé avec les tests. C'est la valeur attendue. |
| **Fichier reçu** | Le fichier `*.received.json` ou `*.received.txt` que Carbonite écrit à côté de la référence quand la valeur diffère. Il sert à relire ou à accepter la nouvelle valeur. On ne le committe jamais. |
| **CI** | L'intégration continue : le serveur qui lance les tests à chaque push ou pull request. |

## Les pages du guide

| Page | Ce que vous y trouvez |
| --- | --- |
| [Démarrage](demarrage.md) | Installer le paquet, écrire le premier test, committer `__snapshots__`, régler Git. |
| [Cycle de vie](cycle-de-vie.md) | Ce qui se passe à chaque exécution, comment lire un échec et comment accepter un changement. |
| [TUnit](tunit.md) | L'assertion `MatchesSnapshot()`, les tests paramétrés, plusieurs snapshots par test, `[Retry]` et `[Repeat]`. |
| [Autres frameworks](autres-frameworks.md) | xUnit, NUnit et MSTest avec le paquet cœur : comment nommer chaque cas. |
| [Valeurs dynamiques](valeurs-dynamiques.md) | Guid et dates remplacés, membres masqués, expressions régulières, réglages globaux. |
| [Format des fichiers](format.md) | Toutes les règles d'écriture des snapshots, en tableaux. |
| [CI](ci.md) | Mode strict, builds déterministes, artefacts, culture et fins de ligne. |
| [Dépannage](depannage.md) | Chaque message d'erreur ou symptôme : la cause, puis la correction. |
| [Agents de code](agents-de-code.md) | Donner les règles de Carbonite à Claude Code, Copilot, Codex ou Cursor : le skill, et un bloc pour `AGENTS.md`. |
| [Développement](developpement.md) | Contribuer à Carbonite : outils, commandes, organisation du dépôt. |

Pour aller plus loin : la [décision de conception](decisions/0001-conception.md), et la [publication sur NuGet](publication.md) pour les mainteneurs.

## Carbonite en 30 secondes

1. Installez le paquet qui correspond à votre framework de test :

   ```bash
   dotnet add package TheDotSide.Carbonite.TUnit   # avec TUnit
   ```

   ```bash
   dotnet add package TheDotSide.Carbonite         # avec xUnit, NUnit ou MSTest
   ```

2. Écrivez un test. Cet exemple vient du projet `samples/Sample.Facturation.Tests` du dépôt :

   ```csharp
   [Test]
   public void Une_facture_complete()
   {
       Snapshot.Match(UneFacture());
   }
   ```

3. Lancez `dotnet test`. Le test passe, et Carbonite crée `__snapshots__/FactureTests.Une_facture_complete.verified.json` à côté du fichier de test. Relisez ce fichier, puis committez-le avec le test.

4. Le jour où la facture change, le test échoue et affiche le diff entre la référence et la nouvelle valeur. Si le changement est voulu, acceptez-le.

   Avec bash ou zsh :

   ```bash
   SNAPSHOT_UPDATE=1 dotnet test
   ```

   Avec PowerShell, retirez ensuite la variable, sinon elle reste active pour toute la session :

   ```powershell
   $env:SNAPSHOT_UPDATE=1; dotnet test
   Remove-Item Env:SNAPSHOT_UPDATE
   ```

5. Relisez la modification du fichier `.verified.json` dans votre pull request : ce diff est la relecture du changement.

En CI, Carbonite ne crée ni ne met jamais à jour une référence : un snapshot oublié ou faux fait échouer les tests.

---

Voir aussi : [Démarrage](demarrage.md) · [Cycle de vie](cycle-de-vie.md) · [Dépannage](depannage.md).
