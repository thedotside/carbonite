using System.Runtime.CompilerServices;
using TUnit.Assertions.Core;
using TUnit.Assertions.Sources;

namespace TheDotSide.Carbonite.TUnit;

/// <summary>Snapshot assertions for TUnit: <c>await Assert.That(value).MatchesSnapshot();</c></summary>
public static class SnapshotAssertionExtensions
{
    /// <summary>
    /// Asserts that the value matches its snapshot. The first run on a developer machine creates
    /// the snapshot; later runs fail with a readable diff when the value changes.
    /// </summary>
    /// <param name="source">The value given to <c>Assert.That</c>.</param>
    /// <param name="name">Distinguishes several snapshots of one test, for example <c>"after-payment"</c>.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <typeparam name="TValue">The type of the asserted value.</typeparam>
    public static MatchesSnapshotAssertion<TValue> MatchesSnapshot<TValue>(
        this IAssertionSource<TValue> source,
        string? name = null,
        Action<SnapshotOptions>? configure = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    ) => Create(source, name, configure, sourceFilePath, memberName);

    /// <summary>Asserts that the value matches its snapshot, with options for this assertion.</summary>
    /// <param name="source">The value given to <c>Assert.That</c>.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <typeparam name="TValue">The type of the asserted value.</typeparam>
    public static MatchesSnapshotAssertion<TValue> MatchesSnapshot<TValue>(
        this IAssertionSource<TValue> source,
        Action<SnapshotOptions> configure,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Create(source, null, configure, sourceFilePath, memberName);
    }

    /// <summary>
    /// Asserts that the result of the task matches its snapshot: <c>await Assert.That(LoadAsync()).MatchesSnapshot();</c>
    /// </summary>
    /// <param name="source">The task given to <c>Assert.That</c>; TUnit awaits it first.</param>
    /// <param name="name">Distinguishes several snapshots of one test, for example <c>"after-payment"</c>.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <typeparam name="TValue">The type of the task result.</typeparam>
    public static MatchesSnapshotAssertion<TValue> MatchesSnapshot<TValue>(
        this TaskAssertion<TValue> source,
        string? name = null,
        Action<SnapshotOptions>? configure = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    ) => Create<TValue>(source, name, configure, sourceFilePath, memberName);

    /// <summary>Asserts that the result of the task matches its snapshot, with options for this assertion.</summary>
    /// <param name="source">The task given to <c>Assert.That</c>; TUnit awaits it first.</param>
    /// <param name="configure">Hides or replaces parts of the value, for example <c>o =&gt; o.IgnoreMember("Id")</c>.</param>
    /// <param name="sourceFilePath">Filled in by the compiler: leave it out.</param>
    /// <param name="memberName">Filled in by the compiler: leave it out.</param>
    /// <typeparam name="TValue">The type of the task result.</typeparam>
    public static MatchesSnapshotAssertion<TValue> MatchesSnapshot<TValue>(
        this TaskAssertion<TValue> source,
        Action<SnapshotOptions> configure,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string memberName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Create<TValue>(source, null, configure, sourceFilePath, memberName);
    }

    // TaskAssertion<T> is a source of both T and Task<T>: snapshots always use the awaited T.
    private static MatchesSnapshotAssertion<TValue> Create<TValue>(
        IAssertionSource<TValue> source,
        string? name,
        Action<SnapshotOptions>? configure,
        string sourceFilePath,
        string memberName
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Context.ExpressionBuilder.Append(
            name is null ? ".MatchesSnapshot()" : $".MatchesSnapshot(\"{name}\")"
        );
        return new MatchesSnapshotAssertion<TValue>(
            source.Context,
            name,
            configure,
            sourceFilePath,
            memberName
        );
    }
}
