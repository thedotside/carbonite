# Carbonite avec un agent de code

Cette page explique comment donner les règles de Carbonite à un agent de code (Claude Code, GitHub Copilot, Codex, Cursor…), pour qu'il écrive des tests de snapshot et traite leurs échecs comme vous le feriez.

## Ce que fait le skill

Le dépôt fournit un skill au standard ouvert [Agent Skills](https://agentskills.io), dans [`skills/carbonite`](../skills/carbonite/SKILL.md). Il est écrit en anglais, comme les messages de Carbonite. Un agent le charge quand sa tâche touche aux snapshots : il sait alors quel paquet installer, comment écrire `Snapshot.Match` et `MatchesSnapshot`, comment créer, relire et committer les références, et comment lire chaque message d'erreur.

Le skill impose surtout la règle de relecture. Un agent n'accepte une nouvelle valeur qu'après avoir expliqué chaque ligne du diff par sa tâche, ou par le commit qui a changé le code. Sinon, il corrige le code, ou il laisse le test rouge et vous montre le diff.

Cette règle vient d'un essai. On a demandé à un agent de « remettre le build au vert » après le commit d'un collègue qui mêlait un changement voulu et une régression. Sans le skill, l'agent a accepté les deux. Avec le skill, il a corrigé la régression dans le code et n'a accepté que le changement voulu.

Le skill ne contient aucun script : son installation n'exécute rien.

## Installer le skill

Pour toute l'équipe, installez-le dans le dépôt de votre projet, puis committez le dossier créé. Tous les agents qui travaillent sur le dépôt en profitent, y compris ceux qui tournent dans le cloud ou relisent les pull requests.

| Outil | Commande |
| --- | --- |
| GitHub CLI 2.90 ou plus | `gh skill install thedotside/carbonite carbonite` |
| Node.js 22.20 ou plus | `npx skills add thedotside/carbonite` |

- `gh skill install` écrit `.agents/skills/carbonite`, un dossier que lisent Copilot, Codex, Cursor, Gemini CLI et d'autres. Ajoutez `--agent claude-code` pour `.claude/skills/carbonite`, que Claude Code charge sans rien installer. `gh skill update` met le dossier à jour.
- `npx skills add` demande pour quels agents installer le skill, puis l'installe dans leurs dossiers du projet. `npx skills update` le met à jour.

Pour vous seul, sous forme de plugin :

| Outil | Commande |
| --- | --- |
| Claude Code 2.1.275 ou plus | `/plugin install carbonite --marketplace thedotside/carbonite` |
| Claude Code plus ancien | `/plugin marketplace add thedotside/carbonite`, puis `/plugin install carbonite@carbonite` |
| Copilot CLI | `copilot plugin marketplace add thedotside/carbonite`, puis `copilot plugin install carbonite@carbonite` |

Tant que les paquets ne sont pas publiés sur NuGet, la commande `dotnet add package` que le skill indique échoue.

## Quelle version du skill

- `gh skill install` et `gh skill update` prennent le skill de la dernière release de Carbonite : il décrit les paquets publiés.
- `npx skills add`, `npx skills update` et les plugins prennent la branche `main`. Entre deux versions, elle peut décrire une nouveauté qui n'est pas encore sur NuGet. Un plugin Claude Code installé se met à jour quand le numéro de version de Carbonite change.

## Les agents dans le cloud

Un agent qui travaille dans le cloud, comme l'agent de Copilot sur GitHub, tourne souvent sur un runner de CI. Carbonite y voit les variables `CI` ou `GITHUB_ACTIONS`, et applique son mode strict : il ne crée aucun snapshot. Le skill le sait. L'agent relit la valeur écrite dans le fichier `.received`, renomme ce fichier en `.verified`, puis relance les tests. Il ne touche jamais au pipeline.

## Sans skill : un bloc pour AGENTS.md

Pour un agent qui ne lit pas les skills, ou en rappel dans les instructions du projet, le [README](../README.md#coding-agents) donne un bloc à coller dans `AGENTS.md`, `CLAUDE.md` ou `.github/copilot-instructions.md`. Il est en anglais, comme le skill. Il donne les paquets, l'API, la règle de relecture, la forme PowerShell qui retire `SNAPSHOT_UPDATE` après usage, et le comportement en CI.

## Pour les mainteneurs

- Le skill est dans `skills/carbonite` : `SKILL.md` pour le travail courant, `references/` pour les détails.
- `.claude-plugin/marketplace.json` et `.claude-plugin/plugin.json` publient la racine du dépôt comme plugin `carbonite@carbonite`, pour Claude Code et Copilot CLI.
- Les règles d'entretien sont dans [Développement](developpement.md#le-skill-pour-les-agents). La release GitHub dont dépend `gh skill install` fait partie de la [publication](publication.md). La décision est consignée dans [0002](decisions/0002-agents-de-code.md).

---

Voir aussi : [Démarrage](demarrage.md) · [Cycle de vie](cycle-de-vie.md) · [CI](ci.md).
