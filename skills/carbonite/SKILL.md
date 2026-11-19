---
name: carbonite
description: Use when writing, running or fixing snapshot tests in a .NET project that uses Carbonite (TheDotSide.Carbonite or TheDotSide.Carbonite.TUnit packages, Snapshot.Match, MatchesSnapshot, __snapshots__ folders with .verified.json or .verified.txt files), when a test fails with "Snapshot mismatch", "Snapshot missing", "Snapshot conflict" or "Snapshot format changed", or when adding snapshot tests to a .NET project that has no snapshot library yet.
license: MIT
---

# Carbonite snapshot tests

Carbonite saves a value in a file next to the test, `__snapshots__/<File>.<Test>.verified.json`, and fails with a diff when the value changes. A verified file is the expected value, reviewed by a human. **Accepting a snapshot approves the new value**: a snapshot changes only after you have checked that the new value is right.

Violating the letter of these rules is violating their spirit.

## Check the project first

- Carbonite is in use when a test project references `TheDotSide.Carbonite` or `TheDotSide.Carbonite.TUnit`, or has `__snapshots__` folders.
- Another snapshot library (Verify, Snapshooter…) also writes `.verified` and `.received` files. Follow that library instead, and do not add Carbonite next to it.
- No snapshot library yet: install Carbonite.

## Install

| Test framework | Command, in the test project folder |
| --- | --- |
| TUnit 1.40 or later | `dotnet add package TheDotSide.Carbonite.TUnit` |
| xUnit, NUnit, MSTest | `dotnet add package TheDotSide.Carbonite` |

Test projects target `net10.0` or `net11.0`. The TUnit package brings the core package: reference only that one. There is nothing to configure.

Once per repository, add what is missing:

- `.gitignore`: `*.received.*`
- `.gitattributes`: `**/__snapshots__/** text eol=lf`
- `.editorconfig`: a `[**/__snapshots__/**]` section with `trim_trailing_whitespace = false`

## Write the test

```csharp
using TheDotSide.Carbonite;
using TheDotSide.Carbonite.TUnit;

namespace Shop.Tests;

public sealed record OrderLine(string Label, int Quantity, decimal UnitPrice);

public sealed record Order(Guid Id, DateTime CreatedAt, string Country, IReadOnlyList<OrderLine> Lines);

public class OrderSnapshotTests
{
    private static Order NewOrder(string country) =>
        new(Guid.NewGuid(), DateTime.UtcNow, country, [new OrderLine("Lamp", 2, 39.5m)]);

    [Test]
    public void A_new_order() => Snapshot.Match(NewOrder("FR"));

    [Test]
    [Arguments("FR")]
    [Arguments("DE")]
    public async Task An_order_per_country(string country) =>
        await Assert.That(NewOrder(country)).MatchesSnapshot();
}
```

- `Snapshot.Match(value)` works with every test framework. It is synchronous: pass a result, never a `Task` (`Snapshot.Match(await LoadAsync())`).
- With TUnit, `await Assert.That(value).MatchesSnapshot()` is the assertion form, and it also accepts a `Task`.
- A `string` is stored as text (`.verified.txt`), any other value as indented JSON (`.verified.json`).
- `Guid`, `DateTime` and `DateTimeOffset` values become `Guid_1`, `DateTime_1`, `DateTimeOffset_1`… by themselves. Hide every other value that changes between runs (a generated number, a token, a `DateOnly`, the current year, a machine path): `options => options.ScrubMember("Token")`, `.IgnoreMember("Password")` or `.Scrub(@"INV-\d{4}-\d{4}", "INV-XXXX-XXXX")`. See [options](references/options.md).
- Text that your code formats uses the machine culture (`1299,80` on a French machine): snapshot an object, or format with `CultureInfo.InvariantCulture`.

Names:

- The file is `__snapshots__/<File>.<Test>[_<case>][.<name>][.<n>].verified.json`, next to the test file.
- TUnit names each case after its arguments (`OrderSnapshotTests.An_order_per_country_FR`) and numbers further snapshots of one test.
- xUnit, NUnit and MSTest: Carbonite cannot see the arguments. Give each case, loop iteration and shared helper call its own name, `Snapshot.Match(value, $"case-{id}")`, or the run fails with `Snapshot conflict`.

## Create the snapshots

1. Run `dotnet test`. On a developer machine, a missing snapshot is created and the test passes.
2. Read every created file. Its values become the expected values: check them against what the code should produce. Never write or edit a `.verified` file by hand.
3. Run `dotnet test` again. A failure now means a value that changes between runs: hide it, delete the snapshots you just created, and start again at 1.
4. Run the tests as CI does: `CI=true dotnet test` (PowerShell: `$env:CI="true"; dotnet test`, then `Remove-Item Env:CI`).
5. Commit the `__snapshots__` files with the tests. Never commit `*.received.*`.

## When a snapshot test fails

`Snapshot mismatch` prints a unified diff (`-` verified, `+` received) and writes the new value to a `.received` file next to the verified one. That is everything you need to decide: run nothing else first.

Decide for every changed line, from the task you were given or from the commit that changed the code:

| The changed line is | Do this |
| --- | --- |
| Explained by that change | Accept it (below). |
| Not explained by it: a refactor or a "simplification" that moves values, a total that loses a part, a field that disappears | Do not accept. Fix the code until the value is back to the verified one, or leave the test red and show the diff to the developer. |
| Impossible to judge | Do not accept. Show the diff to the developer and ask. |

When the diff mixes both, fix the code first, run the tests again, and accept only when every remaining line is explained.

Accept only after that decision, with one of:

- bash, zsh: `SNAPSHOT_UPDATE=1 dotnet test`
- PowerShell: `$env:SNAPSHOT_UPDATE=1; dotnet test`, then `Remove-Item Env:SNAPSHOT_UPDATE` (otherwise the variable stays set)
- moving the `.received` file onto the `.verified` file.

Then read `git diff` on the `__snapshots__` files: only the lines you explained may move. Name the accepted changes in your commit message and your report.

Other messages (`Snapshot missing`, `Snapshot conflict`, `Snapshot format changed`, a `Task` or a `Stream` instead of a value…): see [failures](references/failures.md).

### Excuses that lead to a wrong snapshot

| Excuse | Reality |
| --- | --- |
| "The commit says the change is deliberate." | A commit states an intent. A changed line that the intent does not explain is a regression until someone confirms it. Refactors and simplifications change no values. |
| "It is easy to revert later." | Once accepted, the test certifies the bug and passes. Nobody re-reads green tests. |
| "I was asked for a green build, the release is today." | A green build with a wrong snapshot ships the bug. A red test with an explanation is the useful answer. |
| "I'll update first, then read the diff." | `SNAPSHOT_UPDATE=1` rewrites every differing snapshot at once. The failure message and the `.received` files already show the diff. |
| "I don't want to change a colleague's code without asking." | Then don't. Leave the test red and ask; do not accept the value either. |
| "I accepted it and asked them to confirm." | If it needs confirmation, it is not accepted yet. |

### Red flags: stop

- You are about to set `SNAPSHOT_UPDATE` before you have read the diff.
- A changed line that you cannot explain.
- A report that says "accepted, please confirm".
- Deleting a `.verified` file to make a test pass: the next run creates it with the new value, which accepts it.

## CI and cloud sandboxes

On CI, Carbonite never creates or updates a snapshot: a missing snapshot fails with `Snapshot missing`, and `SNAPSHOT_UPDATE` is ignored. It recognizes CI through `CI`, `GITHUB_ACTIONS`, `TF_BUILD`, `GITLAB_CI`, `JENKINS_URL` and other variables listed in [ci](references/ci.md).

- Never set `SNAPSHOT_UPDATE`, or turn those variables off, in a pipeline.
- If your own shell has one of those variables (a coding agent that runs on a CI runner, such as a cloud agent on GitHub Actions), you are on CI too. A new snapshot fails with `Snapshot missing`, and its value is written to a `.received` file. Review that file as you would a created snapshot, rename it from `.received.` to `.verified.`, and run the tests again. A changed value works the same way, after the decision above.

## Reference files

- [references/options.md](references/options.md): options, defaults for the whole run, names, TUnit and the other frameworks.
- [references/failures.md](references/failures.md): every Carbonite message, its cause and its fix.
- [references/ci.md](references/ci.md): pipelines, artifacts, deterministic builds, culture, line endings.
