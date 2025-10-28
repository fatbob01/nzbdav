using System.Collections.Concurrent;

namespace NzbWebDAV.Extensions;

public static class CancellationTokenExtensions
{
    private static readonly ConcurrentDictionary<CancellationToken, ConcurrentDictionary<Type, object>> _contexts = new();

    public static CancellationTokenScopedContext<T> SetScopedContext<T>(this CancellationToken cancellationToken, T context)
    {
        var typeMap = _contexts.GetOrAdd(cancellationToken, _ => new ConcurrentDictionary<Type, object>());
        typeMap[typeof(T)] = context!;
        return new CancellationTokenScopedContext<T>(cancellationToken);
    }

    public static T? GetContext<T>(this CancellationToken cancellationToken)
    {
        if (_contexts.TryGetValue(cancellationToken, out var typeMap) && typeMap.TryGetValue(typeof(T), out var value))
        {
            return (T)value;
        }

        return default;
    }

    internal static void RemoveContext<T>(CancellationToken cancellationToken)
    {
        if (_contexts.TryGetValue(cancellationToken, out var typeMap))
        {
            typeMap.TryRemove(typeof(T), out _);

            if (typeMap.IsEmpty)
            {
                _contexts.TryRemove(cancellationToken, out _);
            }
        }
    }
}

public sealed class CancellationTokenScopedContext<T> : IDisposable
{
    private readonly CancellationToken _cancellationToken;
    private bool _disposed;

    internal CancellationTokenScopedContext(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenExtensions.RemoveContext<T>(_cancellationToken);
        _disposed = true;
    }
}
