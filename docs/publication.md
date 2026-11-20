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
   - le job `publish` attend l'approbation d'un relecteur de l'environnement `nuget`, s'authentifie auprès de NuGet par OIDC (aucune clé n'est stockée), puis pousse le cœur, ensuite l'adaptateur. Les symboles (`.snupkg`) partent avec chaque paquet.
4. Une fois les paquets publiés, créez la release GitHub de l'étiquette : `gh skill install` installe le skill de la dernière release (voir [Agents de code](agents-de-code.md)).

   ```bash
   gh release create 0.1.0 --verify-tag --generate-notes
   ```

5. Passez `<Version>` à la version suivante, ainsi que la `version` de `.claude-plugin/plugin.json` (la CI vérifie qu'elles sont égales), et ajoutez `## <suivante> - Unreleased` au changelog.

## Mise en place, une seule fois

Le workflow de publication reste inactif tant que ces trois réglages n'existent pas. Il échoue alors avec un message explicite, sans rien publier.

1. **Politique Trusted Publishing sur nuget.org.** Depuis le compte qui administre l'organisation NuGet `TheDotSide`, créez une politique avec :
   - propriétaire : l'organisation ;
   - dépôt : `thedotside/carbonite` ;
   - fichier de workflow : `publish.yml` ;
   - environnement : `nuget`.
2. **Environnement GitHub `nuget`** (Settings → Environments) : relecteurs obligatoires, et règle de déploiement limitée aux étiquettes `[0-9]*.[0-9]*.[0-9]*`.
3. **Variable de dépôt `NUGET_USER`** (Settings → Secrets and variables → Actions → Variables) : le nom d'utilisateur nuget.org qui a créé la politique.

Le préfixe `TheDotSide.*` suit la convention du collectif décrite dans le dépôt `thedotside/.github`. Sa réservation auprès de NuGet est une démarche distincte.

## Sources

- [Trusted Publishing sur nuget.org](https://learn.microsoft.com/fr-fr/nuget/nuget-org/trusted-publishing)
- [Action `NuGet/login`](https://github.com/NuGet/login)
- [Environnements GitHub](https://docs.github.com/fr/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)

---

Voir aussi : [la CI et les builds déterministes](ci.md) · [contribuer au projet](developpement.md).
