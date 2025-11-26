using System.Runtime.ExceptionServices;
using System.Linq;
using NzbWebDAV.Clients.Usenet.Models;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Models;
using NzbWebDAV.Streams;
using Serilog;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients.Usenet;

public class MultiProviderNntpClient(List<(MultiConnectionNntpClient Client, ProviderType Type)> providers) : INntpClient
{
    private readonly List<(MultiConnectionNntpClient Client, ProviderType Type)> _providers = providers;
    private int _lastSuccessfulIndex = -1;

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
        return RunFromPoolWithBackup(connection => connection.StatAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<NntpDateResponse> DateAsync(CancellationToken cancellationToken)
    {
        return RunFromPoolWithBackup(connection => connection.DateAsync(cancellationToken), cancellationToken);
    }

    public Task<UsenetArticleHeaders> GetArticleHeadersAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunFromPoolWithBackup(connection => connection.GetArticleHeadersAsync(segmentId, cancellationToken),
            cancellationToken);
    }

    public Task<YencHeaderStream> GetSegmentStreamAsync(string segmentId, bool includeHeaders,
        CancellationToken cancellationToken)
    {
        return RunFromPoolWithBackup(
            connection => connection.GetSegmentStreamAsync(segmentId, includeHeaders, cancellationToken),
            cancellationToken);
    }

    public Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunFromPoolWithBackup(connection => connection.GetSegmentYencHeaderAsync(segmentId, cancellationToken),
            cancellationToken);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return RunFromPoolWithBackup(connection => connection.GetFileSizeAsync(file, cancellationToken),
            cancellationToken);
    }

    public Task WaitForReady(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<T> RunFromPoolWithBackup<T>
    (
        Func<INntpClient, Task<T>> task,
        CancellationToken cancellationToken
    )
    {
        ExceptionDispatchInfo? lastException = null;
        var orderedProviders = GetOrderedProviders();
        foreach (var provider in orderedProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (lastException is not null && lastException.SourceException is not UsenetArticleNotFoundException)
            {
                var msg = lastException.SourceException.Message;
                Log.Debug($"Encountered error during NNTP Operation: `{msg}`. Trying another provider.");
            }

            try
            {
                var result = await task(provider.Client).ConfigureAwait(false);
                _lastSuccessfulIndex = _providers.IndexOf(provider);
                return result;
            }
            catch (Exception e)
            {
                lastException = ExceptionDispatchInfo.Capture(e);
                if (e is UsenetArticleNotFoundException)
                {
                    // try the next provider to see if it has the missing article
                    continue;
                }
            }
        }

        lastException?.Throw();
        throw new InvalidOperationException("No providers available for NNTP operation.");
    }

    private IEnumerable<(MultiConnectionNntpClient Client, ProviderType Type)> GetOrderedProviders()
    {
        IEnumerable<(MultiConnectionNntpClient Client, ProviderType Type)> Ordered(IEnumerable<ProviderType> types)
        {
            return _providers
                .Select((provider, index) => (provider, index))
                .Where(x => types.Contains(x.provider.Type))
                .OrderByDescending(x => x.index == _lastSuccessfulIndex)
                .Select(x => x.provider);
        }

        return Ordered(new[] { ProviderType.Pooled })
            .Concat(Ordered(new[] { ProviderType.BackupAndStats }))
            .Concat(Ordered(new[] { ProviderType.BackupOnly }));
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Client.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
