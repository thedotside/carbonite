# Changelog

## 0.2.0 - Unreleased

## 0.1.0 - 2026-11-27

- First version of `TheDotSide.Carbonite` for .NET 10 and .NET 11: `Snapshot.Match(value)` compares any value with a snapshot file stored in `__snapshots__` next to the test.
- Deterministic, culture-invariant JSON for objects and raw text for strings: members of the runtime type in declaration order, sorted dictionaries and unordered sets, invariant numbers and ISO 8601 dates, readable non-ASCII text, LF line endings.
- Guids and dates replaced by numbered placeholders by default; `IgnoreMember`, `ScrubMember`, `Scrub`, `DontScrubGuids`, `DontScrubDates` and `SnapshotDefaults.Configure`.
- Safe lifecycle: snapshots are created on a developer machine, never created or updated on CI, rewritten with `SNAPSHOT_UPDATE=1`; failures show a unified diff, the verified and received files and how to accept the change.
- Guard rails: un-awaited tasks, async sequences and streams are rejected with the fix; two values checked against one snapshot in a run are pointed out; cycles, endless structures and huge collections give explicit results; BOM, CRLF and trailing line feeds never fail a comparison.
- Deterministic builds (`ContinuousIntegrationBuild=true`) are supported through the project directory recorded by the `buildTransitive` targets.
- `TheDotSide.Carbonite.TUnit` for TUnit 1.40 and later: one snapshot per test case (method and class arguments), numbered snapshots in one test, retries and repeats, and `await Assert.That(value).MatchesSnapshot()` with `Assert.Multiple()` and tasks.
- A skill for coding agents (`skills/carbonite`, Agent Skills format), installable with `gh skill`, `npx skills` or as the `carbonite@carbonite` plugin for Claude Code and Copilot CLI: agents add snapshot tests, review them, and accept a changed value only when they can explain every changed line.
