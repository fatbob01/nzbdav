namespace NzbWebDAV.Clients.Connections;

/// <summary>
/// Disposable wrapper that automatically returns a borrowed connection to the
/// originating <see cref="ConnectionPool{T}"/>.
///
/// Note: This class was authored by ChatGPT 3o
/// </summary>
public sealed class ConnectionLock<T> : IDisposable, IAsyncDisposable
{
    private readonly Action<T>            _syncReturn;
    private readonly Func<T, ValueTask>?  _asyncReturn;
    private          T?                   _connection;
    private          int                  _disposed; // 0 == false, 1 == true

    internal ConnectionLock(
        T                       connection,
        Action<T>               syncReturn,
        Func<T,ValueTask>?      asyncReturn = null)
    {
        _connection  = connection;
        _syncReturn  = syncReturn;
        _asyncReturn = asyncReturn;
    }

    public T Connection
        => _connection ?? throw new ObjectDisposedException(nameof(ConnectionLock<T>));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;   // already done
        var conn = Interlocked.Exchange(ref _connection, default);
        if (conn is not null) _syncReturn(conn);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        var conn = Interlocked.Exchange(ref _connection, default);
        if (conn is not null)
        {
            if (_asyncReturn is not null)
                await _asyncReturn(conn).ConfigureAwait(false);
            else
                _syncReturn(conn);
        }
        GC.SuppressFinalize(this);
    }
}