# Carbonite avec un agent de code

Cette page explique comment donner les règles de Carbonite à un agent de code (Claude Code, GitHub Copilot, Codex, Cursor…), pour qu'il écrive des tests de snapshot et traite leurs échecs comme vous le feriez.

## Ce que fait le skill

Le dépôt fournit un skill au standard ouvert [Agent Skills](https://agentskills.io), dans [`skills/carbonite`](../skills/carbonite/SKILL.md). Il est écrit en anglais, comme les messages de Carbonite. Un agent le charge quand sa tâche touche aux snapshots : il sait alors quel paquet installer, comment écrire `Snapshot.Match` et `MatchesSnapshot`, comment créer, relire et committer les références, et comment lire chaque message d'erreur.

Le skill impose surtout la règle de relecture. Un agent n'accepte une nouvelle valeur qu'après avoir expliqué chaque ligne du diff par sa tâche, ou par le commit qui a changé le code. Sinon, il corrige le code, ou il laisse le test rouge et vous montre le diff.

Cette règle vient d'un essai. On a demandé à un agent de « remettre le build au vert » après le commit d'un collègue qui mêlait un changement voulu et une régression. Sans le skill, l'agent a accepté les deux. Avec le skill, il a corrigé la régression dans le code et n'a accepté que le changement voulu.

Le skill ne contient aucun script : son installation n'exécute rien.

## Installer le skill

Installez-le dans le dépôt de votre projet, puis committez-le. Tous les agents de l'équipe en profitent, y compris ceux qui travaillent dans le cloud ou relisent les pull requests.

| Outil | Commande |
| --- | --- |
| GitHub CLI 2.90 ou plus | `gh skill install thedotside/carbonite carbonite` |
| Node.js 22.20 ou plus | `npx skills add thedotside/carbonite` |
| Claude Code 2.1.275 ou plus | `/plugin install carbonite --marketplace thedotside/carbonite` |
| Claude Code plus ancien | `/plugin marketplace add thedotside/carbonite`, puis `/plugin install carbonite@carbonite` |
| Copilot CLI | `copilot plugin marketplace add thedotside/carbonite`, puis `copilot plugin install carbonite@carbonite` |

- `gh skill install` installe pour Copilot par défaut, dans `.agents/skills/carbonite`, un dossier que lisent aussi Codex, Cursor, Gemini CLI et d'autres. `--agent claude-code` vise `.claude/skills/carbonite`, que Claude Code charge sans rien installer. Committez le dossier créé. `gh skill update` le met à jour à partir de la dernière release de Carbonite.
- `npx skills add` demande pour quels agents installer le skill, puis l'installe dans leurs dossiers du projet. Committez-les. `npx skills update` le met à jour depuis `main`.
- Un plugin Claude Code ou Copilot CLI s'installe pour vous seul, pas pour le dépôt, et suit les versions de Carbonite. Pour une équipe, préférez le dossier committé.

Tant que les paquets ne sont pas publiés sur NuGet, la commande `dotnet add package` que le skill indique échoue.

## Sans skill : un bloc pour AGENTS.md

Pour un agent qui ne lit pas les skills, ou en rappel dans les instructions du projet, collez ce bloc dans `AGENTS.md`, `CLAUDE.md` ou `.github/copilot-instructions.md`. Il est en anglais, comme le skill :

```markdown
## Snapshot tests (Carbonite)

- Snapshot tests use Carbonite: `Snapshot.Match(value)`, or `await Assert.That(value).MatchesSnapshot()` with TUnit. Snapshots live in `__snapshots__/*.verified.json` or `.verified.txt` next to the tests and are committed. `*.received.*` files are never committed.
- Create a snapshot by running `dotnet test` on a developer machine. Read every created file before committing it, then run the tests again: a failure on the second run means a value that changes between runs (hide it with `ScrubMember`, `IgnoreMember` or `Scrub`).
- When a snapshot test fails, read the diff first. Accept a change (`SNAPSHOT_UPDATE=1 dotnet test`, or move the `.received` file onto the `.verified` one) only when every changed line is explained by the task or by the commit that changed the code. Otherwise fix the code, or leave the test red and show the diff. Never accept a snapshot to make a build green.
- With xUnit, NUnit or MSTest, give each case of a parameterized test its own name: `Snapshot.Match(value, $"case-{id}")`.
- On CI, Carbonite never creates or updates snapshots. Never set `SNAPSHOT_UPDATE` in a pipeline. In a shell where `CI` or `GITHUB_ACTIONS` is set, review the `.received` file, then rename it to `.verified`.
- Details: https://github.com/thedotside/carbonite/tree/main/skills/carbonite
```

## Pour les mainteneurs

- Le skill est dans `skills/carbonite` : `SKILL.md` pour le travail courant, `references/` pour les détails.
- `.claude-plugin/marketplace.json` et `.claude-plugin/plugin.json` publient la racine du dépôt comme plugin `carbonite@carbonite`, pour Claude Code et Copilot CLI.
- Les règles d'entretien sont dans [Développement](developpement.md#le-skill-pour-les-agents) ; la décision est consignée dans [0002](decisions/0002-agents-de-code.md).

---

Voir aussi : [Démarrage](demarrage.md) · [Cycle de vie](cycle-de-vie.md) · [CI](ci.md).
