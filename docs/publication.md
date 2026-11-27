# Versions et publication sur NuGet

Cette page décrit comment une version de Carbonite est numérotée, vérifiée puis publiée sur nuget.org. Elle s'adresse aux mainteneurs du collectif.

## Numéro de version

- Les deux paquets, `TheDotSide.Carbonite` et `TheDotSide.Carbonite.TUnit`, portent toujours le même numéro. L'adaptateur dépend du cœur **dans cette version exacte** (`[0.1.0]`), car il utilise son contrat interne.
- Le numéro suit [SemVer](https://semver.org/lang/fr/) : `0.1.0`, `0.2.0-alpha.1`, sans `v` ni métadonnées de build.
- `<Version>` dans `Directory.Build.props` désigne **la prochaine version non publiée**. Juste après une publication, augmentez-la et ouvrez une nouvelle entrée dans `CHANGELOG.md`.
- La CI refuse une branche dont la version n'a pas d'entrée `## <version> - Unreleased` dans `CHANGELOG.md`, ou dont la version existe déjà sur nuget.org. Un paquet de test ne peut donc jamais porter le numéro d'une version publiée.

## Publier une version

1. Sur `main`, datez l'entrée du changelog : `## 0.1.0 - 2026-11-20`.
2. Créez l'étiquette Git **avec exactement le numéro de version**, puis poussez-la :

   ```bash
   git tag 0.1.0
   ```

   ```bash
   git push origin 0.1.0
   ```

3. Le workflow `publish-nuget` (`.github/workflows/publish.yml`) démarre :
   - le job `verify` rejoue toute la chaîne de la CI avec la version de l'étiquette : formatage, compilation, tests des deux frameworks, paquets, essais dans des projets consommateurs. Il vérifie aussi que l'étiquette pointe sur un commit de `main` et que l'entrée du changelog est datée ;
   - le job `publish` démarre dès que `verify` réussit : l'environnement `nuget` n'a pas de relecteur, donc **pousser l'étiquette, c'est publier**, et une version publiée sur nuget.org ne se retire pas. Il s'authentifie auprès de NuGet par OIDC (aucune clé n'est stockée), puis pousse le cœur, ensuite l'adaptateur. Les symboles (`.snupkg`) partent avec chaque paquet.
4. Une fois les paquets publiés, créez la release GitHub de l'étiquette : `gh skill install` installe le skill de la dernière release (voir [Agents de code](agents-de-code.md)).

   ```bash
   gh release create 0.1.0 --verify-tag --generate-notes
   ```

5. Passez `<Version>` à la version suivante, ainsi que la `version` de `.claude-plugin/plugin.json` (la CI vérifie qu'elles sont égales), et ajoutez `## <suivante> - Unreleased` au changelog. Faites-le aussitôt : tant que `<Version>` désigne une version publiée, la CI refuse toutes les branches.
6. Dans la même pull request, rafraîchissez les fichiers de verrouillage et committez les deux `packages.lock.json` modifiés : ils notent la version du cœur. La CI passe sans cela, mais ces fichiers garderaient l'ancien numéro.

   ```bash
   dotnet restore --force-evaluate
   ```

## Mise en place, une seule fois

Le workflow de publication a besoin de trois réglages, en place depuis la version 0.1.0. Sans eux, il échoue avec un message explicite, sans rien publier.

1. **Politique Trusted Publishing sur nuget.org.** Depuis un compte personnel membre de l'organisation NuGet `TheDotSide`, créez une politique avec :
   - propriétaire des paquets (*Package Owner*) : `TheDotSide` ;
   - dépôt : propriétaire `thedotside`, nom `carbonite` ;
   - fichier de workflow : `publish.yml`, sans chemin ;
   - environnement : `nuget` ;
   - portée (*Select Scopes*) : « Push new packages and package versions », sans délistage ;
   - motif (*Glob Patterns and Packages*) : `TheDotSide.Carbonite*`, sans point avant l'étoile. `TheDotSide.Carbonite.*` exclurait le paquet cœur.

   La politique reste liée au compte qui l'a créée : s'il quitte l'organisation, elle devient inactive.
2. **Environnement GitHub `nuget`** (Settings → Environments) : sous « Deployment branches and tags », choisissez « Selected branches and tags » et ajoutez une seule règle, de type étiquette : `[0-9]*.[0-9]*.[0-9]*`. Aucune branche ne peut alors utiliser l'environnement. Il n'a pas de relecteur obligatoire : pour qu'une personne approuve chaque publication, cochez « Required reviewers », et laissez « Prevent self-review » décoché si cette personne pousse aussi les étiquettes.
3. **Variable de dépôt `NUGET_USER`** (Settings → Secrets and variables → Actions → Variables) : le nom de profil nuget.org du compte personnel qui a créé la politique. Ni son adresse e-mail, ni le nom de l'organisation.

Qui peut publier : seules les étiquettes de version atteignent l'environnement `nuget`, et le ruleset « Tags » du dépôt réserve la création d'étiquettes à ses administrateurs. Publier revient donc à pousser une étiquette en tant qu'administrateur.

Le préfixe `TheDotSide.*` suit la convention du collectif décrite dans le dépôt `thedotside/.github`. Sa réservation auprès de NuGet est une démarche distincte.

## Sources

- [Trusted Publishing sur nuget.org](https://learn.microsoft.com/fr-fr/nuget/nuget-org/trusted-publishing)
- [Action `NuGet/login`](https://github.com/NuGet/login)
- [Environnements GitHub](https://docs.github.com/fr/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [Règles de déploiement par branche ou étiquette](https://docs.github.com/en/rest/deployments/branch-policies), pour la syntaxe des motifs

---

Voir aussi : [la CI et les builds déterministes](ci.md) · [contribuer au projet](developpement.md).
