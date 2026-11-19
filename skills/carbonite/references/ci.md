# Carbonite on CI

## Strict mode

| Situation | On a developer machine | On CI |
| --- | --- | --- |
| No snapshot yet | Created; the test passes. | Fails with `Snapshot missing`; the value goes to a `.received` file. |
| Same value | Passes. | Passes. |
| Different value | Fails with a diff and a `.received` file. | Same. |
| Different value and `SNAPSHOT_UPDATE=1` | Snapshot rewritten; the test passes. | The variable is ignored and the message says so; fails with the diff. |

A test must check a value that a human reviewed. A snapshot created or rewritten by a server would be reviewed by nobody, so never add `SNAPSHOT_UPDATE` to a pipeline, and never turn off the CI variables there.

## CI variables

Carbonite is on CI as soon as one of these variables holds anything other than nothing, `0`, `false`, `no` or `off` (case and surrounding spaces ignored): `CI`, `TF_BUILD` (Azure Pipelines), `GITHUB_ACTIONS`, `GITLAB_CI`, `JENKINS_URL`, `TEAMCITY_VERSION`, `BUILDKITE`, `CIRCLECI`, `TRAVIS`, `APPVEYOR`, `BITBUCKET_BUILD_NUMBER`, `CODEBUILD_BUILD_ID`. One is enough: on GitHub Actions, `CI=false` changes nothing because `GITHUB_ACTIONS` stays set.

The CI services set them. A coding agent whose shell runs on a CI runner sees them too: it follows SKILL.md, "CI and cloud sandboxes".

Before pushing, run the tests as CI does:

- bash, zsh: `CI=true dotnet test`
- PowerShell: `$env:CI="true"; dotnet test`, then `Remove-Item Env:CI`

## Read the received values of a CI run

Upload the `.received` files when the tests fail. With GitHub Actions:

```yaml
- name: Tests
  run: dotnet test
- name: Upload the values received by Carbonite
  if: failure()
  uses: actions/upload-artifact@v7
  with:
    name: received-snapshots
    path: '**/__snapshots__/*.received.*'
    if-no-files-found: ignore
```

If a difference appears only on CI, for example on another operating system, find its cause first (culture, line endings, letter case, machine-specific values). Copy a received value onto the verified file only once you know it is the right one.

## Deterministic builds

With `ContinuousIntegrationBuild=true`, the compiler rewrites source paths to `/_/…`. Carbonite needs the real path to find `__snapshots__`, so its package records the test project directory at build time and searches there. It works without configuration when:

- the tests run from the source checkout, with the `.cs` files present;
- the test project references `TheDotSide.Carbonite`, directly or through `TheDotSide.Carbonite.TUnit`.

`<CarboniteRecordProjectDirectory>false</CarboniteRecordProjectDirectory>` keeps the absolute path out of the test assembly, but deterministic builds can then no longer find the source files.

## Several target frameworks

A test project that targets `net10.0` and `net11.0` runs once per framework, and both runs use the same files: the snapshot name has no framework in it. The value must be the same on both frameworks: hide a member that depends on the .NET version, such as a framework exception message. With `SNAPSHOT_UPDATE=1`, if the frameworks produce different values, the last write wins and the next run fails on the other framework: fix the cause instead.

## Culture

Carbonite writes the values it serializes with the invariant culture: a `decimal` inside an object is always `1299.80`. A string that your code already built is written as it is: `$"{amount}"` gives `1299,80` on a French machine and `1299.80` on an English CI server, and currency formats contain non-breaking spaces that depend on the operating system. Snapshot an object, or format the text with `CultureInfo.InvariantCulture`.

## Line endings

Carbonite always writes `\n`. Comparisons ignore `\r\n`, a UTF-8 byte order mark and trailing line feeds, and strings inside objects are written with `\n` too. Keep the snapshots byte for byte identical on every machine with `**/__snapshots__/** text eol=lf` in `.gitattributes`. Carbonite cannot check line endings themselves: test a CSV that must use `\r\n` with an ordinary assertion.

## Letter case

Windows and macOS file systems often ignore case; Linux does not. A test renamed from `An_Order` to `An_order` still finds its old snapshot locally, but not on a Linux CI. Rename the file in Git too: `git mv __snapshots__/OrderTests.An_Order.verified.json __snapshots__/OrderTests.An_order.verified.json`.
