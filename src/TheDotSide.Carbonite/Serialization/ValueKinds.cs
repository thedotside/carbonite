using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace TheDotSide.Carbonite.Serialization;

/// <summary>Recognizes values that need special handling, once per type.</summary>
internal static class ValueKinds
{
    private static readonly ConcurrentDictionary<Type, Type?> DictionaryPairTypes = new();
    private static readonly ConcurrentDictionary<Type, bool> UnorderedSets = new();
    private static readonly ConcurrentDictionary<Type, Type?> AsyncEnumerables = new();

    /// <summary>
    /// Values whose content does not exist yet: a task, or a sequence that can only be read
    /// asynchronously. Snapshotting one is always a forgotten <c>await</c>, so the serializer
    /// refuses it at any depth, without ever reading <c>Result</c> (which can block forever).
    /// Returns the type name to show, or <see langword="null"/>.
    /// </summary>
    internal static string? PendingName(object value) =>
        value switch
        {
            Task task => TaskName(task.GetType()),
            _ when IsValueTask(value.GetType()) => TypeNameFormatter.Format(value.GetType()),
            _ when OnlyAsyncEnumerable(value.GetType()) is { } sequence => TypeNameFormatter.Format(
                sequence
            ),
            _ => null,
        };

    /// <summary>
    /// Values that are pointless or dangerous to walk: streams, delegates, threading and
    /// infrastructure objects. Returns the placeholder name, or <see langword="null"/>.
    /// </summary>
    internal static string? OpaqueName(object value) =>
        value switch
        {
            CancellationToken => "CancellationToken",
            System.Reflection.Pointer => "Pointer",
            Delegate
            or Stream
            or WaitHandle
            or Thread
            or SynchronizationContext
            or TaskScheduler
            or SafeHandle
            or CancellationTokenSource
            or IServiceProvider => TypeNameFormatter.Format(value.GetType()),
            _ => null,
        };

    /// <summary>
    /// The <c>IAsyncEnumerable&lt;T&gt;</c> of a type that cannot be enumerated synchronously.
    /// Types that are both (an EF Core <c>DbSet</c>, for example) are read as ordinary sequences.
    /// </summary>
    internal static Type? OnlyAsyncEnumerable(Type type) =>
        typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            ? null
            : AsyncEnumerableInterface(type);

    internal static string TaskName(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return TypeNameFormatter.Format(current);
            }
        }

        return "Task";
    }

    internal static bool IsValueTask(Type type) =>
        type == typeof(ValueTask)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>));

    internal static Type? AsyncEnumerableInterface(Type type) =>
        AsyncEnumerables.GetOrAdd(
            type,
            static candidate =>
                candidate
                    .GetInterfaces()
                    .FirstOrDefault(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)
                    )
        );

    /// <summary>The <c>KeyValuePair&lt;K, V&gt;</c> type of a generic dictionary, if it is one.</summary>
    internal static Type? DictionaryPairType(Type type) =>
        DictionaryPairTypes.GetOrAdd(
            type,
            static candidate =>
            {
                var dictionary = candidate
                    .GetInterfaces()
                    .FirstOrDefault(contract =>
                        contract.IsGenericType
                        && (
                            contract.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                            || contract.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                        )
                    );
                return dictionary is null
                    ? null
                    : typeof(KeyValuePair<,>).MakeGenericType(dictionary.GetGenericArguments());
            }
        );

    /// <summary>Sets whose enumeration order is not meaningful (sorted sets keep theirs).</summary>
    internal static bool IsUnorderedSet(Type type) =>
        UnorderedSets.GetOrAdd(
            type,
            static candidate =>
            {
                if (candidate.IsGenericType)
                {
                    var definition = candidate.GetGenericTypeDefinition();
                    if (
                        definition == typeof(SortedSet<>)
                        || definition == typeof(ImmutableSortedSet<>)
                    )
                    {
                        return false;
                    }
                }

                return candidate
                    .GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && (
                            contract.GetGenericTypeDefinition() == typeof(ISet<>)
                            || contract.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)
                        )
                    );
            }
        );

    /// <summary>The <c>Key</c> property of an <c>IGrouping&lt;K, V&gt;</c>, if the type is one.</summary>
    internal static System.Reflection.PropertyInfo? GroupingKey(Type type) =>
        type.GetInterfaces()
            .FirstOrDefault(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IGrouping<,>)
            )
            ?.GetProperty(nameof(IGrouping<,>.Key));
}
