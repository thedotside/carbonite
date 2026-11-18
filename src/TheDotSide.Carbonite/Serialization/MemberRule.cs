namespace TheDotSide.Carbonite.Serialization;

/// <summary>A member to ignore or scrub: by name everywhere, or by name on one type and its subtypes.</summary>
internal sealed record MemberRule(Type? DeclaringType, string Name)
{
    internal bool Matches(Type? ownerType, string name) =>
        string.Equals(Name, name, StringComparison.Ordinal)
        && (
            DeclaringType is null
            || (ownerType is not null && DeclaringType.IsAssignableFrom(ownerType))
        );
}
