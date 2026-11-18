using TheDotSide.Carbonite.Engine;
using TUnit.Assertions.Core;

namespace TheDotSide.Carbonite.TUnit;

/// <summary>
/// <c>await Assert.That(value).MatchesSnapshot()</c>: the TUnit form of <see cref="Snapshot.Match(object?, string?, Action{SnapshotOptions}?, string, string)"/>.
/// A mismatch is reported like any TUnit assertion, so <c>Assert.Multiple()</c> collects it.
/// </summary>
/// <typeparam name="TValue">The type of the asserted value.</typeparam>
public sealed class MatchesSnapshotAssertion<TValue> : Assertion<TValue>
{
    private readonly string? _name;
    private readonly Action<SnapshotOptions>? _configure;
    private readonly string _sourceFilePath;
    private readonly string _memberName;

    internal MatchesSnapshotAssertion(
        AssertionContext<TValue> context,
        string? name,
        Action<SnapshotOptions>? configure,
        string sourceFilePath,
        string memberName
    )
        : base(context)
    {
        _name = name;
        _configure = configure;
        _sourceFilePath = sourceFilePath;
        _memberName = memberName;
    }

    /// <inheritdoc />
    protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<TValue> metadata)
    {
        if (metadata.Exception is { } exception)
        {
            return Task.FromResult(
                AssertionResult.Failed(
                    $"threw {exception.GetType().Name}: {exception.Message}",
                    exception
                )
            );
        }

        try
        {
            var outcome = SnapshotEngine.Default.Check(
                metadata.Value,
                _name,
                _configure,
                _sourceFilePath,
                _memberName
            );
            return Task.FromResult(
                outcome.Passed
                    ? AssertionResult.Passed
                    : AssertionResult.Failed(outcome.FailureMessage!)
            );
        }
        catch (SnapshotException snapshotException)
        {
            return Task.FromResult(
                AssertionResult.Failed(snapshotException.Message, snapshotException)
            );
        }
    }

    /// <inheritdoc />
    protected override string GetExpectation() => "to match its snapshot";
}
