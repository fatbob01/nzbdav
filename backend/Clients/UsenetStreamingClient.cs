using Microsoft.Extensions.Caching.Memory;
using NzbWebDAV.Clients.Connections;
using NzbWebDAV.Config;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Extensions;
using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class UsenetStreamingClient
{
    private readonly INntpClient _client;

    public UsenetStreamingClient(ConfigManager configManager)
    {
        // Use primary provider configuration
        var providerIndex = configManager.GetPrimaryProviderIndex();
        var host = configManager.GetProviderConfigValue(providerIndex, "host") ?? string.Empty;
        var port = int.Parse(configManager.GetProviderConfigValue(providerIndex, "port") ?? "119");
        var useSsl = bool.Parse(configManager.GetProviderConfigValue(providerIndex, "use-ssl") ?? "false");
        var user = configManager.GetProviderConfigValue(providerIndex, "user") ?? string.Empty;
        var pass = configManager.GetProviderConfigValue(providerIndex, "pass") ?? string.Empty;
        var connections = int.Parse(configManager.GetProviderConfigValue(providerIndex, "connections") ?? "10");

        // initialize the nntp-client
        var createNewConnection = (CancellationToken ct) => CreateNewConnection(host, port, useSsl, user, pass, ct);
        ConnectionPool<INntpClient> connectionPool = new(connections, createNewConnection);
        var multiConnectionClient = new MultiConnectionNntpClient(connectionPool);
        var cache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 8192 });
        _client = new CachingNntpClient(multiConnectionClient, cache);

        // when config changes, update the connection-pool
        configManager.OnConfigChanged += (_, configEventArgs) =>
        {
            providerIndex = configManager.GetPrimaryProviderIndex();
            var prefix = $"usenet.provider.{providerIndex}.";

            // if unrelated config changed, do nothing
            var relevantChange = configEventArgs.ChangedConfig.Keys.Any(key =>
                key == "usenet.providers.primary" || key.StartsWith(prefix));
            if (!relevantChange) return;

            // update the connection-pool according to the new config
            var connectionCount = int.Parse(configManager.GetProviderConfigValue(providerIndex, "connections") ?? "10");
            var newHost = configManager.GetProviderConfigValue(providerIndex, "host") ?? string.Empty;
            var newPort = int.Parse(configManager.GetProviderConfigValue(providerIndex, "port") ?? "119");
            var newUseSsl = bool.Parse(configManager.GetProviderConfigValue(providerIndex, "use-ssl") ?? "false");
            var newUser = configManager.GetProviderConfigValue(providerIndex, "user") ?? string.Empty;
            var newPass = configManager.GetProviderConfigValue(providerIndex, "pass") ?? string.Empty;
            multiConnectionClient.UpdateConnectionPool(new(connectionCount, cancellationToken =>
                CreateNewConnection(newHost, newPort, newUseSsl, newUser, newPass, cancellationToken)));
        };
    }

    public async Task<bool> CheckNzbFileHealth(NzbFile nzbFile, CancellationToken cancellationToken = default)
    {
        var childCt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = nzbFile.Segments
            .Select(x => x.MessageId.Value)
            .Select(x => _client.StatAsync(x, childCt.Token))
            .ToHashSet();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            var completedResult = await completedTask;
            if (completedResult.ResponseType != NntpStatResponseType.ArticleExists)
            {
                await childCt.CancelAsync();
                return false;
            }
        }

        return true;
    }

    public async Task<NzbFileStream> GetFileStream(NzbFile nzbFile, int concurrentConnections, CancellationToken ct)
    {
        var firstSegmentId = nzbFile.GetOrderedSegmentIds().First();
        var firstSegmentStream = await _client.GetSegmentStreamAsync(firstSegmentId, ct);
        return new NzbFileStream(nzbFile, firstSegmentStream, _client, concurrentConnections);
    }

    public Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
    }

    public Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return _client.GetFileSizeAsync(file, cancellationToken);
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