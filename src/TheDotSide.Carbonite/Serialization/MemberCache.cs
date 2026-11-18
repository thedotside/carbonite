using System.Collections.Concurrent;
using System.Reflection;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>
/// The members a snapshot shows for a type: public instance properties, then public fields,
/// base classes first, each in declaration order. A redeclared member keeps its first position.
/// </summary>
internal static class MemberCache
{
    private const BindingFlags DeclaredPublicInstance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<Type, SnapshotMember[]> Members = new();

    internal static SnapshotMember[] Get(Type type) => Members.GetOrAdd(type, Discover);

    private static SnapshotMember[] Discover(Type type)
    {
        var levels = new List<Type>();
        for (
            Type? current = type;
            current is not null && current != typeof(object) && current != typeof(ValueType);
            current = current.BaseType
        )
        {
            levels.Insert(0, current);
        }

        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var members = new List<SnapshotMember>();
        foreach (var level in levels)
        {
            var properties = level
                .GetProperties(DeclaredPublicInstance)
                .Where(IsReadable)
                .OrderBy(property => property.MetadataToken)
                .Select(property => new SnapshotMember(property.Name, property.GetValue));
            var fields = level
                .GetFields(DeclaredPublicInstance)
                .Where(field => IsReadableType(field.FieldType))
                .OrderBy(field => field.MetadataToken)
                .Select(field => new SnapshotMember(field.Name, field.GetValue));

            foreach (var member in properties.Concat(fields))
            {
                if (positions.TryGetValue(member.Name, out var position))
                {
                    members[position] = member;
                }
                else
                {
                    positions.Add(member.Name, members.Count);
                    members.Add(member);
                }
            }
        }

        return [.. members];
    }

    private static bool IsReadable(PropertyInfo property) =>
        property.GetMethod is { IsPublic: true }
        && property.GetIndexParameters().Length == 0
        && IsReadableType(property.PropertyType);

    // Reflection cannot box ref structs (Span<T>…), references or pointers.
    private static bool IsReadableType(Type type) =>
        !type.IsByRefLike && !type.IsByRef && !type.IsPointer && !type.IsFunctionPointer;
}
