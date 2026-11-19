# Carbonite messages and symptoms

Carbonite messages are in English. Search this page for the beginning of the message you see. Whatever the fix, a snapshot is accepted only after the decision described in SKILL.md, "When a snapshot test fails".

## Messages

### Snapshot mismatch

The value differs from the verified snapshot. The message shows the diff, then the paths of the verified and received files. Decide as SKILL.md says. A diff that you cannot explain may come from an unstable value or from invisible characters: see the symptoms below.

### Snapshot missing

There is no verified file and the tests run on CI, or in a shell where a CI variable is set. Usually the snapshot was never committed. Other causes:

- a `.gitignore` rule excludes it: `git check-ignore -v <file>`;
- the snapshot name changed: a renamed test, an argument that changes on every run, a name built with the machine culture, a rename that only changes letter case on a Linux CI.

Fix: when the name changed, fix the name first (for a renamed test, `git mv` the old snapshot file to the new name). For a snapshot that was never committed: on a developer machine, run the tests (the snapshot is created), review the file and commit it; in a shell where a CI variable is set, review the `.received` file and rename it from `.received.` to `.verified.`.

### Snapshot format changed

The value was a string or XML, stored as `.verified.txt`, and is now an object, stored as `.verified.json`, or the reverse. No `.received` file is written. If that change is intended, run with `SNAPSHOT_UPDATE=1` on a developer machine, which writes the new file and deletes the old one. In a shell where a CI variable is set, `git rm` the old file, run the tests, then review the `.received` file and rename it to `.verified`. Otherwise, pass the same kind of value as before.

### Snapshot conflict

`This snapshot was already checked earlier in this run with a different value`: two values target one file in one run. The second one always fails, even when it matches the verified file, and `SNAPSHOT_UPDATE` cannot choose. Causes:

- a parameterized test without names, with xUnit, NUnit or MSTest;
- `Snapshot.Match` in a loop or in a shared helper, without names;
- two TUnit cases whose arguments give the same name (`"a b"` and `"a_b"`, or a difference only in a Guid or a date);
- a value that is not deterministic, for example in a `[Repeat]` test.

Fix: name each value, `Snapshot.Match(value, $"case-{id}")`, or hide what changes.

### Snapshot name conflict

Two snapshot names differ only by letter case. Windows and macOS see one file where Linux sees two. Rename one of the tests or snapshot names.

### Carbonite cannot find the test source file

Carbonite stores snapshots next to the test source file and cannot find it: the tests were compiled with deterministic paths (`ContinuousIntegrationBuild=true`, paths in `/_/…`) without the recorded project directory, or the compiled tests run on a machine without their sources. Run the tests from the source checkout, reference `TheDotSide.Carbonite` directly or through `TheDotSide.Carbonite.TUnit`, and do not set `CarboniteRecordProjectDirectory` to `false`.

### Carbonite needs the path of the test source file

`Snapshot.Match` was called through reflection, or with an empty `sourceFilePath`. Call it directly from C# test code and never pass `sourceFilePath` or `memberName`: the compiler fills them.

### Carbonite received a Task, not its result

Await it: `Snapshot.Match(await LoadAsync())`. The same applies to a `ValueTask`. With TUnit, `await Assert.That(LoadAsync()).MatchesSnapshot()` awaits a `Task` for you; await a `ValueTask` yourself first.

### Carbonite received an IAsyncEnumerable

Read it first: `Snapshot.Match(await values.ToListAsync())`.

### Carbonite received a Stream, not its content

Read it first, after rewinding a stream your code just wrote: `stream.Position = 0; Snapshot.Match(await new StreamReader(stream).ReadToEndAsync());`. A `byte[]` is written in Base64; image and binary snapshots are not supported.

### The value is nested more than 64 levels deep

Almost always an endless structure where each level is a new object, such as a property that creates an object on every read. The path in the message shows the repeating member: ignore it, `options.IgnoreMember("Next")`, or snapshot a smaller object.

### The collection has more than 10000 items

Snapshot a part of it, `.Take(100)`, or a summary such as `new { Count = lines.Count, First = lines.Take(10) }`, and check that the sequence ends.

### The snapshot file is not valid UTF-8

An editor or a tool saved the verified file in another encoding. Restore it with `git restore <file>`, or delete it so that the next run on a developer machine creates it again, then review it.

### The snapshot name has no letter or digit

An explicit name became empty once cleaned. Use a name such as `"after-payment"`.

### Use a direct member access

`IgnoreMember<T>` and `ScrubMember<T>` accept only `x => x.Member`. For a nested member, target the type that declares it, `IgnoreMember<Customer>(c => c.Name)`, or use the name alone, `IgnoreMember("Name")`, which applies everywhere.

### Is not a valid regular expression

The pattern given to `Scrub` is not a valid .NET regular expression. Write it as a verbatim string, `@"\d{4}"`.

### Carbonite: the TUnit adapter could not be loaded

The TUnit package is referenced but does not load, and snapshots fall back to the generic mode: parameterized cases lose their suffix and conflict. Reference only `TheDotSide.Carbonite.TUnit`, without another version of `TheDotSide.Carbonite`, and use TUnit 1.40 or later.

## Symptoms

### The test fails one run out of two, or only on another machine

A value changes on its own. Find it in the diff and hide it or make it fixed (see options.md): an unordered collection, a random value, a `DateOnly` or a date inside a string, a Guid inside a string, the machine name or a path, text formatted with the machine culture.

### The lines of the diff look identical

The message adds `Note: the only differences are whitespace (spaces, tabs or line breaks).` Usually a non-breaking space from culture formatting (U+00A0 or U+202F), a tab, trailing spaces, or an editor that touched the verified file. Compare the bytes, `od -c <file>` or `Format-Hex <file>`, then fix the source of the difference. For culture-dependent text, format with `CultureInfo.InvariantCulture`, or replace those spaces: `options.Scrub(@"[\u00A0\u202F]", " ")`.

### The test passes locally but fails on CI

Check in this order: the snapshot is committed; the text does not depend on the culture; no file name differs only by letter case; the value does not depend on the machine or the time; deterministic builds find the sources. Reproduce with `CI=true dotnet test`.

### SNAPSHOT_UPDATE has no effect

A CI variable is set in the shell or the IDE (the message says `SNAPSHOT_UPDATE is ignored on continuous integration`); the value is not `1`, `true`, `yes` or `on`; the syntax does not match the shell (PowerShell needs `$env:SNAPSHOT_UPDATE=1; dotnet test`); or two values target one file (`Snapshot conflict`).

### Snapshots update by themselves

`$env:SNAPSHOT_UPDATE` stays set for the whole PowerShell session, and an IDE run configuration can set it too. Remove it, `Remove-Item Env:SNAPSHOT_UPDATE`, then review every changed snapshot with `git diff`.

### .received files show in git status

They hold the values of the last failures and are never committed. Add `*.received.*` to `.gitignore`. Carbonite deletes a received file as soon as its snapshot passes.
