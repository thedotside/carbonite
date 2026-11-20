# Repository workflow

Carbonite is a snapshot testing library for beginners: one line, explicit errors, no trap. Every change keeps three promises: a snapshot never changes without a change of the value (culture, OS, line endings, ordering, time and Guids), a test never passes silently with a wrong value (CI never creates or updates snapshots), and every failure message says what to do next.

- `src/TheDotSide.Carbonite`: the core, without package dependency. `Serialization/` writes deterministic JSON, `Naming/` names files and maps deterministic build paths, `Engine/` runs the lifecycle and owns the failure messages, `Snapshot.cs` is the one-line entry point.
- `src/TheDotSide.Carbonite.TUnit`: the TUnit adapter, found by the core through its type name. It compiles against TUnit 1.40.0 on purpose: raise that floor only for a needed API, because TUnit's dependencies are minimums.
- `tests/TheDotSide.Carbonite.Tests`: core tests without the adapter (the generic mode of xUnit, NUnit and MSTest users) and committed dogfood snapshots.
- `tests/TheDotSide.Carbonite.TUnit.Tests`: real TUnit features (arguments, data sources, class arguments, fixtures, retries, repeats, `Assert.Multiple`).
- `samples/Sample.Facturation.Tests`: the example quoted by the French documentation in `docs/`.
- `skills/carbonite`: the Agent Skills skill that teaches coding agents to use Carbonite in their projects. `.claude-plugin/` publishes it as the plugin `carbonite@carbonite` for Claude Code and Copilot CLI.
- `docs/decisions/`: the design records (`0001-conception.md`, `0002-agents-de-code.md`). Update them when a decision changes.

Target .NET 10 and .NET 11. Build with the SDK of `global.json` and install the .NET 10 runtime too to run both test frameworks: `dotnet build`, `dotnet test`. Run the tests with `CI=true` before pushing: CI runs in strict mode with `ContinuousIntegrationBuild=true` (source paths become `/_/…`). Snapshot files are committed: review every created or changed `*.verified.*` file; never commit `*.received.*`. Test code that formats text itself must use the invariant culture.

The skill describes the behavior of `main`, like the README: change it in the same commit as a public API, a failure message, an environment variable or a file name rule. Every ```csharp block in `skills/carbonite` is a complete file that `eng/package-smoke.sh` compiles and runs against the packages; shorter code stays inline, and other code blocks use a known tag. The `version` of `.claude-plugin/plugin.json` is the `<Version>` of `Directory.Build.props`. The repository root is the root of the plugin: never put plugin components there (`.mcp.json`, `hooks/`, `agents/`, `commands/`…), they would reach every user of the plugin. Never name a file `agents.md`, `claude.md` or `gemini.md` in another letter case than `AGENTS.md`, `CLAUDE.md` or `GEMINI.md`: macOS and Windows ignore case, and coding agents would read it as the instructions of its folder. Outside `tests/`, a PowerShell command that sets `SNAPSHOT_UPDATE` removes it, even when the tests fail: `$env:SNAPSHOT_UPDATE=1; try { dotnet test } finally { Remove-Item Env:SNAPSHOT_UPDATE }`. Check with `eng/check-agents.sh`, `claude plugin validate --strict .` and `gh skill publish --dry-run .`; CI runs all three.

For packaging changes, run `eng/pack.sh`, then `eng/package-smoke.sh` (throwaway consumer projects through the whole lifecycle, with an isolated NuGet cache). `CARBONITE_FRAMEWORKS=net11.0` limits both scripts to one framework, for example on a machine without the .NET 10 runtime; the repository still needs the SDK of `global.json`. Do not publish packages or push changes unless requested.

# Formatting and analysis

CSharpier is the only C#/XML formatter: `dotnet tool restore`, `dotnet csharpier format .`, `dotnet csharpier check .` (rely on its exit code). `eng/setup.sh` enables the versioned pre-commit hook. Meziantou.Analyzer is a private global package reference; warnings are errors, and the culture and comparison rules matter here more than anywhere. Commit messages follow Conventional Commits (commitlint in CI).
