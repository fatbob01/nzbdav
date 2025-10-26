using System.Threading;
using NzbWebDAV.Clients.Usenet.Connections;
using NzbWebDAV.Clients.Usenet.Models;
using NzbWebDAV.Streams;
using NzbWebDAV.Utils;
using Usenet.Exceptions;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients.Usenet;

public class MultiConnectionNntpClient : INntpClient
{
    private ConnectionPool<INntpClient> _connectionPool;
    private int _liveConnections;
    private int _idleConnections;

    public MultiConnectionNntpClient(ConnectionPool<INntpClient> connectionPool)
    {
        _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
        _connectionPool.OnConnectionPoolChanged += HandleConnectionPoolChanged;
        ResetConnectionCounts();
    }

    public Task<bool> ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Please connect within the connectionFactory");
    }

    public Task<bool> AuthenticateAsync(string user, string pass, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Please authenticate within the connectionFactory");
    }

    public Task<NntpStatResponse> StatAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.StatAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<NntpDateResponse> DateAsync(CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.DateAsync(cancellationToken), cancellationToken);
    }

    public Task<UsenetArticleHeaders> GetArticleHeadersAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetArticleHeadersAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<YencHeaderStream> GetSegmentStreamAsync(string segmentId, bool includeHeaders, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetSegmentStreamAsync(segmentId, includeHeaders, cancellationToken), cancellationToken);
    }

    public Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetSegmentYencHeaderAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetFileSizeAsync(file, cancellationToken), cancellationToken);
    }

    public async Task WaitForReady(CancellationToken cancellationToken)
    {
        await using var connectionLock = await _connectionPool.GetConnectionLockAsync(cancellationToken);
    }

    private async Task<T> RunWithConnection<T>
    (
        Func<INntpClient, Task<T>> task,
        CancellationToken cancellationToken,
        int retries = 1
    )
    {
        var connectionLock = await _connectionPool.GetConnectionLockAsync(cancellationToken);
        try
        {
            var result = await task(connectionLock.Connection);

            // we only want to release the connection-lock once the underlying connection is ready again.
            // ReSharper disable once MethodSupportsCancellation
            // we intentionally do not pass the cancellation token to ContinueWith,
            // since we want the continuation to always run.
            _ = connectionLock.Connection.WaitForReady(SigtermUtil.GetCancellationToken())
                .ContinueWith(_ => connectionLock.Dispose());
            return result;
        }
        catch (NntpException)
        {
            // we want to replace the underlying connection in cases of NntpExceptions.
            connectionLock.Replace();
            connectionLock.Dispose();

            // and try again with a new connection (max 1 retry)
            if (retries > 0)
                return await RunWithConnection<T>(task, cancellationToken, retries - 1);

            throw;
        }
        catch (Exception)
        {
            // we also want to release the connection-lock if there was any error getting the result.
            connectionLock.Dispose();
            throw;
        }
    }

    public void UpdateConnectionPool(ConnectionPool<INntpClient> connectionPool)
    {
        if (connectionPool is null)
            throw new ArgumentNullException(nameof(connectionPool));

        var oldConnectionPool = _connectionPool;
        if (ReferenceEquals(oldConnectionPool, connectionPool))
            return;

        oldConnectionPool.OnConnectionPoolChanged -= HandleConnectionPoolChanged;

        _connectionPool = connectionPool;
        _connectionPool.OnConnectionPoolChanged += HandleConnectionPoolChanged;
        ResetConnectionCounts();

        oldConnectionPool.Dispose();
    }

    public int GetActiveConnectionCount()
    {
        var live = Volatile.Read(ref _liveConnections);
        var idle = Volatile.Read(ref _idleConnections);
        var active = live - idle;
        return active < 0 ? 0 : active;
    }

    public int GetAvailableConnectionCount()
    {
        var available = Volatile.Read(ref _idleConnections);
        return available < 0 ? 0 : available;
    }

    public void Dispose()
    {
        _connectionPool.OnConnectionPoolChanged -= HandleConnectionPoolChanged;
        _connectionPool.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandleConnectionPoolChanged(
        object? sender,
        ConnectionPool<INntpClient>.ConnectionPoolChangedEventArgs e)
    {
        Volatile.Write(ref _liveConnections, e.Live);
        Volatile.Write(ref _idleConnections, e.Idle);
    }

    private void ResetConnectionCounts()
    {
        Volatile.Write(ref _liveConnections, 0);
        Volatile.Write(ref _idleConnections, 0);
    }
}
