# Carbonite

[![CI](https://github.com/thedotside/carbonite/actions/workflows/ci.yml/badge.svg)](https://github.com/thedotside/carbonite/actions/workflows/ci.yml)

**Snapshot testing for .NET, in one line.** Freeze any value in a readable file next to your test. When the value moves, Carbonite shows you exactly what changed, and how to accept it.

```csharp
Snapshot.Match(invoice);
```

## Why Carbonite

- **One line, nothing to set up.** `Snapshot.Match(value)` is synchronous: there is no `await` to forget, so a test cannot turn green by accident.
- **Files you can read and review.** Objects become indented JSON, strings stay plain text. The output is the same on every machine: invariant numbers, ISO 8601 dates, members in declaration order, dictionaries and sets sorted, LF line endings everywhere, even inside strings.
- **Safe defaults.** Guids and dates become `Guid_1`, `DateTime_1`… Snapshots are created on your machine, never on CI, and rewritten only when you ask.
- **Failures that say what to do.** A unified diff, the paths of both files, and the two ways to accept the change. A forgotten `await` anywhere in the value fails with the fix instead of passing.
- **TUnit first, other frameworks welcome.** One snapshot per test case and `await Assert.That(value).MatchesSnapshot()` with TUnit; xUnit, NUnit and MSTest use the core package.
- **No dependencies.** The core package depends on nothing.

## Install

| Test framework | Command |
| --- | --- |
| TUnit 1.40 or later | `dotnet add package TheDotSide.Carbonite.TUnit` |
| xUnit, NUnit, MSTest | `dotnet add package TheDotSide.Carbonite` |

The TUnit package brings the core package with it and plugs itself in: there is nothing to configure. Test projects target .NET 10 or .NET 11.

## A 30-second tour

```csharp
using TheDotSide.Carbonite;

public class InvoiceTests
{
    [Test]
    public void An_invoice_with_dynamic_values()
    {
        var invoice = new Invoice(
            Guid.NewGuid(),
            "INV-2026-0042",
            DateTime.UtcNow,
            [new InvoiceLine("Chaise ergonomique", 2, 149.90m), new InvoiceLine("Lampe", 1, 39.5m)],
            Status.Sent
        );

        Snapshot.Match(invoice);
    }
}

public enum Status { Draft, Sent }

public sealed record InvoiceLine(string Label, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record Invoice(Guid Id, string Number, DateTime CreatedAt, IReadOnlyList<InvoiceLine> Lines, Status Status)
{
    public decimal Total => Lines.Sum(line => line.Total);
}
```

The first `dotnet test` passes and creates `__snapshots__/InvoiceTests.An_invoice_with_dynamic_values.verified.json` next to the test file:

```json
{
  "Id": "Guid_1",
  "Number": "INV-2026-0042",
  "CreatedAt": "DateTime_1",
  "Lines": [
    {
      "Label": "Chaise ergonomique",
      "Quantity": 2,
      "UnitPrice": 149.90,
      "Total": 299.80
    },
    {
      "Label": "Lampe",
      "Quantity": 1,
      "UnitPrice": 39.5,
      "Total": 39.5
    }
  ],
  "Status": "Sent",
  "Total": 339.30
}
```

Review it, then commit it with the test. `Id` and `CreatedAt` change on every run, so Carbonite replaced them with placeholders.

## What happens next

| Situation | On your machine | On CI |
| --- | --- | --- |
| No snapshot yet | Created; the test passes. | Fails with `Snapshot missing`. A snapshot is never created on CI. |
| Same value | Passes. | Passes. |
| Different value | Fails with a diff; the new value is saved next to the snapshot in a `.received` file. | Same. |
| Different value and `SNAPSHOT_UPDATE=1` | Snapshot rewritten; the test passes. | The variable is ignored; fails with the diff. |

When the lamp quantity goes from 1 to 2, the test fails with:

```text
Snapshot mismatch: InvoiceTests.An_invoice_with_dynamic_values

The value is different from the verified snapshot.

--- verified
+++ received
@@ -11,11 +11,11 @@
     },
     {
       "Label": "Lampe",
-      "Quantity": 1,
+      "Quantity": 2,
       "UnitPrice": 39.5,
-      "Total": 39.5
+      "Total": 79.0
     }
   ],
   "Status": "Sent",
-  "Total": 339.30
+  "Total": 378.80
 }

Verified: /repo/tests/__snapshots__/InvoiceTests.An_invoice_with_dynamic_values.verified.json
Received: /repo/tests/__snapshots__/InvoiceTests.An_invoice_with_dynamic_values.received.json

If the new value is correct, accept it, then review the change in your pull request:
  - replace the verified file with the received file, or
  - run the tests again with SNAPSHOT_UPDATE=1:
      bash, zsh:   SNAPSHOT_UPDATE=1 dotnet test
      PowerShell:  $env:SNAPSHOT_UPDATE=1; try { dotnet test } finally { Remove-Item Env:SNAPSHOT_UPDATE }
```

## Accepting a change

bash, zsh:

```bash
SNAPSHOT_UPDATE=1 dotnet test
```

PowerShell would keep the variable for the rest of the session, and every later `dotnet test` would rewrite the snapshots that differ. This form removes it, even when tests fail:

```powershell
$env:SNAPSHOT_UPDATE=1; try { dotnet test } finally { Remove-Item Env:SNAPSHOT_UPDATE }
```

Or replace the `.verified` file with the `.received` one. Either way, the diff of the snapshot file is what your reviewers read in the pull request. Keep received files out of Git:

```gitignore
*.received.*
```

## TUnit

```csharp
using TheDotSide.Carbonite.TUnit;

await Assert.That(invoice).MatchesSnapshot();
await Assert.That(invoice).MatchesSnapshot("after-payment", options => options.IgnoreMember("Id"));
await Assert.That(LoadInvoiceAsync()).MatchesSnapshot(); // TUnit awaits the task first
```

A mismatch is an ordinary TUnit assertion failure, so `Assert.Multiple()` collects it.

- **Parameterized tests** get one snapshot per case, named after their arguments: `[Arguments(1, 2)]` gives `ParameterizedTests.Each_case_gets_its_own_snapshot_1_2.verified.json`. Method arguments, data sources and simple class arguments count; shared fixtures do not.
- **Several snapshots in one test** are numbered (`….verified.json`, `….2.verified.json`) or named: `Snapshot.Match(cart, "after-payment")`.
- **`[Retry]` and `[Repeat]`** check the same files on every attempt, and helper methods keep the name of the test.

## xUnit, NUnit and MSTest

The core package names each snapshot after the file and the method that call `Snapshot.Match`. It cannot see the arguments of a test, so give each case of a parameterized test its own name, and each value checked in a loop:

```csharp
[Theory]
[InlineData(1)]
[InlineData(3)]
public void Total_follows_the_quantity(int quantity)
{
    Snapshot.Match(Totals(quantity), $"case-{quantity}");
}
```

If you forget, the second case fails with `Snapshot conflict`, which shows both values, whatever the order in which the cases ran.

## Dynamic values

```csharp
Snapshot.Match(user, options => options
    .IgnoreMember("Password")                  // removed from the snapshot
    .ScrubMember("Token")                      // kept, written as "{Scrubbed}"
    .Scrub(@"INV-\d{4}-\d{4}", "INV-XXXX"));   // regular expression on the final text
```

- Every `Guid`, `DateTime` and `DateTimeOffset` becomes `Guid_1`, `DateTime_1`, `DateTimeOffset_1`… in order of appearance, and the same value always gets the same placeholder. `Guid.Empty` and the `MinValue` and `MaxValue` dates stay as they are. `DontScrubGuids()` and `DontScrubDates()` turn this off.
- A member rule by name applies everywhere: properties, fields, string dictionary keys and JSON properties. `IgnoreMember<Invoice>(x => x.Number)` targets one type and the types derived from it.
- Defaults for the whole test run are set once, at startup:

```csharp
using System.Runtime.CompilerServices;
using TheDotSide.Carbonite;

internal static class CarboniteSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        SnapshotDefaults.Configure(options => options.IgnoreMember("ETag"));
}
```

## On CI

- Carbonite never creates or updates a snapshot on CI. It recognizes CI through `CI`, `TF_BUILD`, `GITHUB_ACTIONS`, `GITLAB_CI`, `JENKINS_URL`, `TEAMCITY_VERSION`, `BUILDKITE`, `CIRCLECI`, `TRAVIS`, `APPVEYOR`, `BITBUCKET_BUILD_NUMBER` and `CODEBUILD_BUILD_ID`.
- A failing snapshot leaves the new value in a `*.received.*` file: upload `**/__snapshots__/*.received.*` as a build artifact to read it.
- Deterministic builds (`ContinuousIntegrationBuild=true`, which rewrites source paths to `/_/`) work out of the box: the package records the test project directory at build time.
- Comparisons ignore a UTF-8 byte order mark, CRLF line endings and trailing line feeds. Text inside objects is written with LF too, so Windows and Linux agree.

## Requirements and limits

- .NET 10 or .NET 11.
- `TheDotSide.Carbonite.TUnit`: TUnit 1.40 or later.
- Not in this version: inline snapshots, launching a diff tool, binary or image snapshots, removal of orphaned snapshot files, trimming and Native AOT (serialization relies on reflection).

## Documentation

The complete guide is written in French: [Carbonite documentation](https://github.com/thedotside/carbonite/blob/main/docs/README.md).

## License

[MIT](https://github.com/thedotside/carbonite/blob/main/LICENSE), © The Dot Side.
