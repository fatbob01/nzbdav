using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NzbWebDAV.Clients.Usenet.Connections;
using NzbWebDAV.Clients.Usenet.Models;
using NzbWebDAV.Config;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Extensions;
using NzbWebDAV.Models;
using NzbWebDAV.Streams;
using NzbWebDAV.Websocket;
using Usenet.Nntp.Responses;
using Usenet.Nzb;

namespace NzbWebDAV.Clients.Usenet;

public class UsenetStreamingClient
{
    private readonly WebsocketManager _websocketManager;
    private readonly object _providerLock = new();
    private List<(MultiConnectionNntpClient Client, ProviderType Type, ConnectionPool<INntpClient> Pool)> _providers = [];
    private INntpClient _client;
    private readonly Dictionary<int, (int Live, int Max, int Idle)> _providerStats = new();

    public UsenetStreamingClient(ConfigManager configManager, WebsocketManager websocketManager)
    {
        // initialize private members
        _websocketManager = websocketManager;

        ConfigureProviders(configManager.GetUsenetProviderConfig());

        // when config changes, update the connection-pool
        configManager.OnConfigChanged += (_, configEventArgs) =>
        {
            // if unrelated config changed, do nothing
            var touchedKeys = new[]
            {
                "usenet.host", "usenet.port", "usenet.use-ssl", "usenet.user", "usenet.pass",
                "usenet.connections", "usenet.providers"
            };

            if (!configEventArgs.ChangedConfig.Keys.Any(key => touchedKeys.Contains(key))) return;

            var providerConfig = GetUpdatedConfig(configEventArgs) ?? configManager.GetUsenetProviderConfig();
            ConfigureProviders(providerConfig);
        };
    }

    public async Task CheckAllSegmentsAsync
    (
        IEnumerable<string> segmentIds,
        int concurrency,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        using var childCt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var _ = childCt.Token.SetScopedContext(cancellationToken.GetContext<object>());
        var token = childCt.Token;

        var tasks = segmentIds
            .Select(x => _client.StatAsync(x, token))
            .WithConcurrencyAsync(concurrency);

        var processed = 0;
        await foreach (var result in tasks)
        {
            progress?.Report(++processed);
            if (result.ResponseType == NntpStatResponseType.ArticleExists) continue;
            await childCt.CancelAsync();
            throw new UsenetArticleNotFoundException(result.MessageId.Value);
        }
    }

    public async Task<NzbFileStream> GetFileStream(NzbFile nzbFile, int concurrentConnections, CancellationToken ct)
    {
        var segmentIds = nzbFile.GetSegmentIds();
        var fileSize = await _client.GetFileSizeAsync(nzbFile, cancellationToken: ct);
        return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
    }

    public NzbFileStream GetFileStream(NzbFile nzbFile, long fileSize, int concurrentConnections)
    {
        return new NzbFileStream(nzbFile.GetSegmentIds(), fileSize, _client, concurrentConnections);
    }

    public NzbFileStream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
    }

    public Task<YencHeaderStream> GetSegmentStreamAsync(string segmentId, bool includeHeaders, CancellationToken ct)
    {
        return _client.GetSegmentStreamAsync(segmentId, includeHeaders, ct);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return _client.GetFileSizeAsync(file, cancellationToken);
    }

    public Task<UsenetArticleHeaders> GetArticleHeadersAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.GetArticleHeadersAsync(segmentId, cancellationToken);
    }

    private void ConfigureProviders(UsenetProviderConfig providerConfig)
    {
        lock (_providerLock)
        {
            foreach (var provider in _providers)
            {
                provider.Pool.OnConnectionPoolChanged -= OnProviderConnectionPoolChanged;
                provider.Client.Dispose();
            }

            _providers = providerConfig.Providers
                .Where(x => x.Type != ProviderType.Disabled && x.MaxConnections > 0)
                .Select((provider, index) =>
                {
                    var connectionPool = CreateNewConnectionPool(
                        provider.MaxConnections,
                        cancellationToken => CreateNewConnection(
                            provider.Host,
                            provider.Port,
                            provider.UseSsl,
                            provider.User,
                            provider.Pass,
                            cancellationToken));

                    connectionPool.OnConnectionPoolChanged += OnProviderConnectionPoolChanged;
                    return (new MultiConnectionNntpClient(connectionPool), provider.Type, connectionPool);
                })
                .ToList();

            var multiProviderClient = new MultiProviderNntpClient(
                _providers.Select(x => (x.Client, x.Type)).ToList());
            var cache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 8192 });
            _client = new CachingNntpClient(multiProviderClient, cache);

            _providerStats.Clear();
            for (var i = 0; i < _providers.Count; i++)
            {
                _providerStats[i] = (0, 0, 0);
            }
        }
    }

    private void OnProviderConnectionPoolChanged(object? sender,
        ConnectionPool<INntpClient>.ConnectionPoolChangedEventArgs args)
    {
        lock (_providerLock)
        {
            var providerIndex = _providers.FindIndex(x => x.Pool == sender);
            if (providerIndex < 0) return;

            _providerStats[providerIndex] = (args.Live, args.Max, args.Idle);

            var live = _providerStats.Values.Sum(x => x.Live);
            var max = _providerStats.Values.Sum(x => x.Max);
            var idle = _providerStats.Values.Sum(x => x.Idle);
            var message = $"{live}|{max}|{idle}";
            _websocketManager.SendMessage(WebsocketTopic.UsenetConnections, message);
        }
    }

    private static UsenetProviderConfig? GetUpdatedConfig(ConfigEventArgs configEventArgs)
    {
        if (!configEventArgs.NewConfig.TryGetValue("usenet.providers", out var providerJson)) return null;

        try
        {
            return JsonSerializer.Deserialize<UsenetProviderConfig>(providerJson);
        }
        catch
        {
            return null;
        }
    }

    private ConnectionPool<INntpClient> CreateNewConnectionPool
    (
        int maxConnections,
        Func<CancellationToken, ValueTask<INntpClient>> connectionFactory
    )
    {
        return new ConnectionPool<INntpClient>(maxConnections, connectionFactory);
    }

    public static async ValueTask<INntpClient> CreateNewConnection
    (
        string host,
        int port,
        bool useSsl,
        string user,
        string pass,
        CancellationToken cancellationToken
    )
    {
        var connection = new ThreadSafeNntpClient();
        if (!await connection.ConnectAsync(host, port, useSsl, cancellationToken))
            throw new CouldNotConnectToUsenetException("Could not connect to usenet host. Check connection settings.");
        if (!await connection.AuthenticateAsync(user, pass, cancellationToken))
            throw new CouldNotLoginToUsenetException("Could not login to usenet host. Check username and password.");
        return connection;
    }
}