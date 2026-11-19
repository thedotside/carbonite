# Carbonite options, names and frameworks

## Options of one snapshot

Pass them as the last argument: `Snapshot.Match(value, options => …)`, `Snapshot.Match(value, "name", options => …)`, or `MatchesSnapshot(options => …)` with TUnit. Every method returns the options, for chaining.

| Option | Effect |
| --- | --- |
| `IgnoreMember("Password")` | Removes every member with this exact, case-sensitive name: properties, fields, string dictionary keys, JSON properties. |
| `IgnoreMember<Invoice>(x => x.Number)` | Removes one member of `Invoice` and of the types derived from it. Only a direct member access is accepted. |
| `ScrubMember("Token")`, `ScrubMember<Invoice>(x => x.Token)` | Keeps the member and writes `"{Scrubbed}"` instead of its value. |
| `Scrub(@"INV-\d{4}-\d{4}", "INV-XXXX-XXXX")` | Replaces every match of a .NET regular expression in the final snapshot text. `$1` refers to the first group. |
| `DontScrubGuids()`, `DontScrubDates()` | Writes `Guid`, `DateTime` and `DateTimeOffset` values as they are, instead of `Guid_1`, `DateTime_1`… |

```csharp
using TheDotSide.Carbonite;

namespace Shop.Tests;

public sealed record Account(string Login, string Password, string Token, string Reference);

public class AccountSnapshotTests
{
    [Test]
    public void An_account_without_its_secrets()
    {
        var account = new Account("ada", "s3cret!", Guid.NewGuid().ToString("N"), "ACC-2026-0042");

        Snapshot.Match(
            account,
            options =>
                options
                    .IgnoreMember("Password")
                    .ScrubMember("Token")
                    .Scrub(@"ACC-\d{4}-\d{4}", "ACC-XXXX-XXXX")
        );
    }
}
```

Placeholders are numbered in order of appearance, and the same value always gets the same placeholder. `Guid.Empty`, `MinValue` and `MaxValue` dates stay as they are.

Not replaced automatically, so hide them or make them fixed:

| Value | Fix |
| --- | --- |
| `DateOnly`, `TimeOnly`, `TimeSpan`, a date or a Guid inside a string | A fixed clock injected in the code (`TimeProvider`), `ScrubMember` or `Scrub`. |
| A random token, a generated password, `Random` | `ScrubMember`, or a fixed value given by the test. |
| A number built from a counter or from the current year | `Scrub` with a pattern, or `IgnoreMember`. |
| The machine name, an absolute path, the current folder | `IgnoreMember`, `ScrubMember` or `Scrub`. |
| A list or a query without `OrderBy`, results of parallel tasks | Sort before the snapshot. Carbonite already sorts sets (`HashSet<T>`, any `ISet<T>`). |

To keep only what matters, snapshot an anonymous type: `Snapshot.Match(new { invoice.Number, invoice.Total })`.

## Defaults for the whole test run

Configure them once, at startup. Each call adds to what earlier calls configured, and the options of each snapshot come on top.

```csharp
using System.Runtime.CompilerServices;
using TheDotSide.Carbonite;

namespace Shop.Tests;

internal static class CarboniteSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        SnapshotDefaults.Configure(options => options.IgnoreMember("ETag"));
}
```

In a test project that builds as a class library (usually xUnit, NUnit or MSTest), the analyzer warning CA2255 flags `[ModuleInitializer]`; disable it around this method (`#pragma warning disable CA2255`). With TUnit, a `[Before(TestSession)]` hook works too.

## File names

`__snapshots__/<File>.<Test>[_<case>][.<name>][.<n>].verified.json`, next to the test file (`.verified.txt` for a string).

- `<File>.<Test>`: the file and the method that call `Snapshot.Match`. A lambda or an `async` method inside the test keeps the name of the test. A helper method keeps it with TUnit only: with the core package alone, the snapshot takes the name of the helper and goes next to the helper's file.
- `_<case>`: the arguments of a parameterized test, with TUnit only. `[Arguments("fr-FR", 3)]` gives `_fr-FR_3`.
- `.<name>`: the name given to `Snapshot.Match(value, "after-payment")` or `MatchesSnapshot("after-payment")`.
- `.<n>`: the number of a further snapshot with the same name in the same test, with TUnit only (`.2`, `.3`…).

Names are cleaned so that every shell accepts them: accents are removed, characters other than ASCII letters, digits, `.` and `-` become `_`, and a name longer than 120 characters is cut and ends with an 8-character hash. A name without any letter or digit, such as `"???"`, is refused.

Renaming a test leaves the old file in place: Carbonite never deletes an orphaned snapshot. Rename the snapshot file with `git mv` before running the tests: Carbonite then compares the value with it instead of creating a new snapshot. A rename that only changes letter case needs `git mv` too, or Linux CI reports `Snapshot missing`.

## TUnit

- Each case of a parameterized test gets its own file, named after its arguments: method arguments, data sources and simple class arguments count, shared fixtures do not. Arguments are written like snapshot values: `9.99m` gives `9.99`, `Guid.NewGuid()` gives `Guid_1`, a record gives its content (`X_1_Y_2`), a `CancellationToken` gives nothing.
- Two cases can end up with the same name: `"a b"` and `"a_b"`, or arguments that differ only by a Guid or a date. Name each value then: `Snapshot.Match(order, $"order-{order.Number}")`.
- `[Retry]` and `[Repeat]` check the same files on every attempt.
- `MatchesSnapshot()` is an ordinary TUnit assertion: `Assert.Multiple()` collects every mismatch instead of stopping at the first one, and a `Task` is awaited first.
- To update only some tests, filter them: `SNAPSHOT_UPDATE=1 dotnet test --treenode-filter "/*/*/CartSnapshotTests/*"`.

```csharp
using TheDotSide.Carbonite.TUnit;

namespace Shop.Tests;

public sealed record Cart(IReadOnlyList<string> Items, decimal Total, bool Paid);

public class CartSnapshotTests
{
    private static Task<Cart> LoadCartAsync() => Task.FromResult(new Cart(["Lamp", "Chair"], 189.4m, false));

    [Test]
    public async Task A_cart_before_and_after_payment()
    {
        var cart = await LoadCartAsync();

        using (Assert.Multiple())
        {
            await Assert.That(cart).MatchesSnapshot("before-payment");
            await Assert.That(cart with { Paid = true }).MatchesSnapshot("after-payment");
        }
    }

    [Test]
    public async Task A_cart_loaded_asynchronously() => await Assert.That(LoadCartAsync()).MatchesSnapshot();
}
```

## xUnit, NUnit and MSTest

The core package takes the names from the compiler: the file that calls `Snapshot.Match` and the method around the call. It cannot see the arguments of a test or the turns of a loop, so every call in one test targets the same file unless you name it:

- a parameterized test: `Snapshot.Match(Totals(quantity), $"case-{quantity}")`;
- a loop: `Snapshot.Match(line, $"line-{line.Number}")`;
- a helper shared by several tests: call `Snapshot.Match` in each test and keep only the preparation of the value in the helper. A helper that calls `Snapshot.Match` names the snapshot after itself and stores it next to its own file.

Build these names from invariant values: a number formatted with the machine culture (`1,5` on a French machine) gives a different file name on CI.

`Snapshot.Match` throws at the first difference, so later snapshots of the same test are checked at the next run. `SNAPSHOT_UPDATE=1` handles them all in one run.
