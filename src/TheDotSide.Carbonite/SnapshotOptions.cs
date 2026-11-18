using System.Linq.Expressions;
using System.Text.RegularExpressions;
using TheDotSide.Carbonite.Serialization;

namespace TheDotSide.Carbonite;

/// <summary>What a snapshot hides or replaces. Every method returns the same instance, for chaining.</summary>
public sealed class SnapshotOptions
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private readonly List<MemberRule> _ignored;
    private readonly List<MemberRule> _scrubbed;
    private readonly List<TextScrubber> _textScrubbers;

    internal SnapshotOptions()
        : this([], [], [], scrubGuids: true, scrubDates: true) { }

    private SnapshotOptions(
        List<MemberRule> ignored,
        List<MemberRule> scrubbed,
        List<TextScrubber> textScrubbers,
        bool scrubGuids,
        bool scrubDates
    )
    {
        _ignored = ignored;
        _scrubbed = scrubbed;
        _textScrubbers = textScrubbers;
        ScrubGuids = scrubGuids;
        ScrubDates = scrubDates;
    }

    internal bool ScrubGuids { get; private set; }

    internal bool ScrubDates { get; private set; }

    /// <summary>Removes every member (property, field, dictionary key, JSON property) with this name.</summary>
    /// <param name="name">The exact, case-sensitive member name, for example <c>"Password"</c>.</param>
    public SnapshotOptions IgnoreMember(string name)
    {
        _ignored.Add(new MemberRule(null, ValidateName(name)));
        return this;
    }

    /// <summary>Removes one member of <typeparamref name="T"/> and of the types that derive from it.</summary>
    /// <param name="member">A direct member access, for example <c>x =&gt; x.CreatedBy</c>.</param>
    public SnapshotOptions IgnoreMember<T>(Expression<Func<T, object?>> member)
    {
        _ignored.Add(new MemberRule(typeof(T), MemberName(member)));
        return this;
    }

    /// <summary>Keeps every member with this name but replaces its value with <c>{Scrubbed}</c>.</summary>
    /// <param name="name">The exact, case-sensitive member name, for example <c>"Token"</c>.</param>
    public SnapshotOptions ScrubMember(string name)
    {
        _scrubbed.Add(new MemberRule(null, ValidateName(name)));
        return this;
    }

    /// <summary>Keeps one member of <typeparamref name="T"/> but replaces its value with <c>{Scrubbed}</c>.</summary>
    /// <param name="member">A direct member access, for example <c>x =&gt; x.Token</c>.</param>
    public SnapshotOptions ScrubMember<T>(Expression<Func<T, object?>> member)
    {
        _scrubbed.Add(new MemberRule(typeof(T), MemberName(member)));
        return this;
    }

    /// <summary>Replaces every match of a regular expression in the final snapshot text.</summary>
    /// <param name="pattern">A .NET regular expression, checked immediately.</param>
    /// <param name="replacement">The replacement text; <c>$1</c> refers to the first group.</param>
    public SnapshotOptions Scrub(string pattern, string replacement)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        try
        {
            _textScrubbers.Add(
                new TextScrubber(
                    new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout),
                    replacement
                )
            );
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                $"'{pattern}' is not a valid regular expression: {exception.Message}",
                nameof(pattern),
                exception
            );
        }

        return this;
    }

    /// <summary>Writes <see cref="Guid"/> values as they are instead of <c>Guid_1</c>, <c>Guid_2</c>…</summary>
    public SnapshotOptions DontScrubGuids()
    {
        ScrubGuids = false;
        return this;
    }

    /// <summary>
    /// Writes <see cref="DateTime"/> and <see cref="DateTimeOffset"/> values as they are instead of
    /// <c>DateTime_1</c>, <c>DateTimeOffset_1</c>…
    /// </summary>
    public SnapshotOptions DontScrubDates()
    {
        ScrubDates = false;
        return this;
    }

    internal MemberTreatment GetTreatment(Type? ownerType, string name)
    {
        if (_ignored.Exists(rule => rule.Matches(ownerType, name)))
        {
            return MemberTreatment.Ignore;
        }

        return _scrubbed.Exists(rule => rule.Matches(ownerType, name))
            ? MemberTreatment.Scrub
            : MemberTreatment.Keep;
    }

    internal string ApplyTextScrubbers(string text)
    {
        foreach (var scrubber in _textScrubbers)
        {
            text = scrubber.Pattern.Replace(text, scrubber.Replacement);
        }

        return text;
    }

    internal SnapshotOptions Clone() =>
        new([.. _ignored], [.. _scrubbed], [.. _textScrubbers], ScrubGuids, ScrubDates);

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return name;
    }

    private static string MemberName<T>(Expression<Func<T, object?>> member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var body = member.Body
            is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
            } conversion
            ? conversion.Operand
            : member.Body;
        if (body is MemberExpression { Expression: ParameterExpression } access)
        {
            return access.Member.Name;
        }

        throw new ArgumentException(
            $"Use a direct member access such as x => x.Name, not '{member}'.",
            nameof(member)
        );
    }

    private sealed record TextScrubber(Regex Pattern, string Replacement);
}
