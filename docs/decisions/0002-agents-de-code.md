# 0002 — Carbonite pour les agents de code

- **Statut :** accepté le 19/11/2026.
- **Portée :** le dépôt. Les paquets NuGet ne changent pas.
- **Demande :** un mainteneur, le 19/11/2026 : des options pour que les agents de code mettent
  Carbonite en place simplement (skills…), dans la suite de la demande de David (voir
  [0001](0001-conception.md)).

## Intention

Un développeur qui débute demande de plus en plus à un agent de code (Claude Code, GitHub Copilot,
Codex, Cursor…) d'écrire ses tests. Cet agent ne connaît pas Carbonite : la bibliothèque est
récente, et elle ressemble à d'autres (Verify écrit aussi des fichiers `.verified` et `.received`).
Sans aide, il devine l'API. Surtout, il peut rendre un test vert en acceptant n'importe quel
snapshot, ce que Carbonite s'efforce d'empêcher chez un humain.

Le but : qu'un agent installe Carbonite, écrive les tests et traite un échec comme le guide le
demande à un humain, sans que le développeur ait à le lui expliquer.

## Objectifs

- Un seul texte pour tous les agents : un skill au standard ouvert Agent Skills,
  `skills/carbonite/SKILL.md`, en anglais.
- Les promesses de Carbonite deviennent des règles pour l'agent : n'accepter un changement qu'après
  avoir vérifié que le diff est voulu, ne jamais committer de fichier `.received`, ne jamais créer
  ni mettre à jour un snapshot en CI.
- Une commande d'installation par écosystème, toutes vers ce même dossier.
- Un bloc à coller dans `AGENTS.md` pour les agents qui ne lisent pas les skills.
- Le skill ne peut pas s'écarter de l'API sans que la CI le voie.

## Non-objectifs

- Un serveur MCP : ce serait un produit de plus à publier et à maintenir, alors qu'un agent fait
  déjà tout en ligne de commande (`dotnet test`, `SNAPSHOT_UPDATE=1`, `git diff`).
- Un `llms.txt` : il sert à la racine d'un site de documentation, et Carbonite n'en a pas. À
  reconsidérer le jour où un site existe.
- Des fichiers propres à un outil (`.cursor/rules`, `copilot-instructions.md` du dépôt) : le skill
  et `AGENTS.md` couvrent déjà ces agents.
- Publier ou annoncer : release, topic du dépôt, annuaires. C'est une décision d'équipe.

## Anti-objectifs

- Un skill qui pousse l'agent à accepter un snapshot pour faire passer un test.
- Un skill qui exécute du code : il ne contient aucun script, donc rien à auditer avant de
  l'installer.
- Un second texte de référence maintenu à la main, sans contrôle automatique.

## Contraintes

- Spécification Agent Skills : `name` identique au nom du dossier (minuscules, chiffres, tirets,
  64 caractères au plus), `description` de 1 024 caractères au plus, `SKILL.md` de moins de
  500 lignes, fichiers de référence à un seul niveau.
- Claude Code lit la marketplace dans `.claude-plugin/marketplace.json` à la racine du dépôt.
- Copilot CLI lit aussi `.claude-plugin/marketplace.json`, mais exige un `plugin.json` pour chaque
  plugin ; il accepte `.claude-plugin/plugin.json` (son format historique).
- Les paquets ne sont pas encore publiés sur NuGet : `dotnet add package` ne fonctionnera qu'après
  la première publication.
- Le dépôt est public : ce qui arrive sur `main` est installable aussitôt.

## Approches envisagées

| Approche | Pour | Contre |
| --- | --- | --- |
| **Un skill au standard, réutilisé par chaque canal (retenue)** | Un seul texte, lu par Claude Code, Copilot, Codex, Cursor et les autres ; vérifiable en CI | Dépend de l'adoption du standard, déjà large |
| Un fichier d'instructions par outil | Rien à installer pour l'utilisateur | Autant de copies que d'outils, formats propriétaires, remplacés par les skills |
| Un serveur MCP (`dotnet tool`) | Des outils typés pour lister et accepter les snapshots | Un produit à publier et à maintenir, pour rien que la ligne de commande ne fasse déjà |

## Conception

### Le skill

`skills/carbonite/SKILL.md` décrit le travail d'un agent avec Carbonite :

- vérifier que le projet utilise Carbonite, et pas Verify ou une autre bibliothèque ;
- installer le bon paquet (TUnit ou cœur) ;
- écrire `Snapshot.Match(valeur)` ou `await Assert.That(valeur).MatchesSnapshot()` ;
- lancer les tests, relire le snapshot créé, le committer ;
- lire un échec, décider si le changement est voulu, l'accepter ou corriger le code ;
- nommer les cas paramétrés avec xUnit, NUnit et MSTest ;
- traiter les valeurs dynamiques et la CI.

Les détails (options, CI, chaque message d'erreur) vont dans `skills/carbonite/references/`,
que l'agent ne lit qu'au besoin. Chaque bloc ` ```csharp ` du skill est un fichier complet ; les
extraits plus courts restent du code en ligne.

### Canaux d'installation

| Outil | Commande |
| --- | --- |
| GitHub CLI 2.90 ou plus (aperçu) | `gh skill install thedotside/carbonite carbonite`, pour Copilot par défaut ; `--agent` pour les autres |
| Node.js 22.20 ou plus | `npx skills add thedotside/carbonite` |
| Claude Code 2.1.275 ou plus | `/plugin install carbonite --marketplace thedotside/carbonite` |
| Claude Code plus ancien | `/plugin marketplace add thedotside/carbonite`, puis `/plugin install carbonite@carbonite` |
| Copilot CLI | `copilot plugin marketplace add thedotside/carbonite`, puis `copilot plugin install carbonite@carbonite` |

`gh skill install` prend le skill de la dernière release du dépôt, ou de `main` s'il n'y en a pas.
`npx skills add` suit `main`. Les plugins suivent leur `version`.

Les manifestes vivent dans `.claude-plugin/` : `marketplace.json` déclare la marketplace
`carbonite` et son plugin `carbonite`, dont la source est la racine du dépôt (`"./"`) ;
`plugin.json` décrit ce plugin pour Claude Code et Copilot CLI. Sa `version` est celle des
paquets : le validateur strict de Claude Code exige ce champ, et un plugin installé reçoit ainsi
le skill de chaque nouvelle version de Carbonite.

### Bloc pour `AGENTS.md`

`docs/agents.md` et le README donnent un bloc court à coller dans `AGENTS.md`, `CLAUDE.md` ou
`.github/copilot-instructions.md` : quel paquet, quelle API, et les trois règles.

### Garde-fous

- La CI lance les validateurs officiels : `claude plugin validate --strict .`, dans une version
  épinglée, et `gh skill publish --dry-run`, qui valide sans rien publier. Elle vérifie aussi que
  la `version` de `plugin.json` est celle de `Directory.Build.props`.
- `eng/package-smoke.sh` compile et exécute chaque bloc ` ```csharp ` du skill contre les paquets,
  d'abord comme sur un poste de développeur (création), puis en mode CI (comparaison).
- `AGENTS.md` demande de mettre à jour le skill dans le même changement que l'API, un message
  d'erreur ou une variable d'environnement.

### Vérification

Un agent neuf, sur un modèle intermédiaire, met Carbonite en place dans un projet jetable avec les
paquets locaux, d'abord sans le skill, puis avec. Le scénario inclut un changement voulu et une
régression : l'agent doit accepter le premier après relecture et refuser la seconde. Le skill est
corrigé jusqu'à ce que l'agent fasse juste.

Résultat, le 19/11/2026, avec Claude Sonnet :

- **Sans le skill**, l'agent a trouvé l'API en ouvrant le paquet et en lisant son README. Il a
  réussi l'installation, puis le mode CI. Mais il a lancé `SNAPSHOT_UPDATE=1` avant de lire le
  diff, et il a accepté la régression qu'il venait de remarquer : « the commit presents it as
  deliberate », « the snapshot update is easy to revert ».
- **Avec le skill**, deux agents neufs sur deux ont lu le diff d'abord, corrigé la régression dans
  le code, et accepté seulement le changement voulu. L'installation et le mode CI ont réussi aussi.

Le poids du skill porte donc sur la décision d'accepter : la règle, le tableau des excuses
relevées et les signaux d'alerte.

## Laissé à l'équipe

- Créer une release : `gh skill install` installera alors le skill de cette version.
- Ajouter le topic `agent-skills` au dépôt, pour que `gh skill search` le trouve.
- Rendre obligatoire le check `agents` de la CI dans le ruleset de `main`.
- Inscrire le dépôt dans des annuaires (skills.sh, Context7).

## Choix faits par Claude, à relire

- Le nom `carbonite@carbonite` : marketplace et plugin portent le nom de la bibliothèque.
- Le skill est en anglais, comme le README : les agents et les équipes étrangères le lisent mieux.
- Le plugin a la racine du dépôt pour source : son cache contient tout le dépôt, qui est petit.
  En échange, le skill reste dans `skills/`, où tous les installeurs le cherchent.
- Le validateur de Claude Code est un outil d'Anthropic, installé en CI par npm dans une version
  épinglée, uniquement pour valider les manifestes.
- La `version` du plugin suit celle des paquets : une correction du skill n'atteint les plugins
  déjà installés qu'à la version suivante de Carbonite.
