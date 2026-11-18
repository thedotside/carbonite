using System.Globalization;
using System.Runtime.CompilerServices;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>
/// C#-style type names without namespaces or assembly versions (<c>List&lt;string&gt;</c>,
/// <c>int?</c>), so that snapshots do not change between .NET versions.
/// </summary>
internal static class TypeNameFormatter
{
    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(void)] = "void",
    };

    internal static string Format(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (Keywords.TryGetValue(type, out var keyword))
        {
            return keyword;
        }

        if (type.IsArray)
        {
            return Format(type.GetElementType()!)
                + "["
                + new string(',', type.GetArrayRank() - 1)
                + "]";
        }

        if (type.IsPointer || type.IsByRef)
        {
            return Format(type.GetElementType()!) + (type.IsPointer ? "*" : "&");
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Format(underlying) + "?";
        }

        if (type.Name is ['<', ..] && type.IsDefined(typeof(CompilerGeneratedAttribute), false))
        {
            return type.Name.Contains("AnonymousType", StringComparison.Ordinal)
                ? "anonymous type"
                : "compiler-generated type";
        }

        if (IsValueTuple(type))
        {
            return "(" + string.Join(", ", type.GetGenericArguments().Select(Format)) + ")";
        }

        return FormatNamed(type);
    }

    private static bool IsValueTuple(Type type) =>
        type.IsConstructedGenericType
        && string.Equals(type.Namespace, "System", StringComparison.Ordinal)
        && type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal);

    // Nested generic types share one argument list: each level takes its own arity from it.
    private static string FormatNamed(Type type)
    {
        var arguments = type.GetGenericArguments();
        var levels = new List<Type>();
        for (Type? current = type; current is not null; current = current.DeclaringType)
        {
            levels.Insert(0, current);
            if (!current.IsNested)
            {
                break;
            }
        }

        var used = 0;
        var parts = new List<string>(levels.Count);
        foreach (var level in levels)
        {
            var name = level.Name;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick < 0)
            {
                parts.Add(name);
                continue;
            }

            var arity = int.Parse(name.AsSpan(tick + 1), CultureInfo.InvariantCulture);
            var levelArguments = arguments.Skip(used).Take(arity).Select(Format);
            used += arity;
            parts.Add(name[..tick] + "<" + string.Join(", ", levelArguments) + ">");
        }

        return string.Join('.', parts);
    }
}
