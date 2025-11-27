using System.Threading;
﻿using NzbWebDAV.Clients.Usenet.Models;
using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients.Usenet;

public abstract class WrappingNntpClient(INntpClient client) : INntpClient
{
    private INntpClient _client = client;

    protected INntpClient Client => _client;

    public virtual Task<bool> ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken)
    {
        return _client.ConnectAsync(host, port, useSsl, cancellationToken);
    }

    public virtual Task<bool> AuthenticateAsync(string user, string pass, CancellationToken cancellationToken)
    {
        return _client.AuthenticateAsync(user, pass, cancellationToken);
    }

    public virtual Task<NntpStatResponse> StatAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.StatAsync(segmentId, cancellationToken);
    }

    public virtual Task<NntpDateResponse> DateAsync(CancellationToken cancellationToken)
    {
        return _client.DateAsync(cancellationToken);
    }

    public Task<UsenetArticleHeaders> GetArticleHeadersAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.GetArticleHeadersAsync(segmentId, cancellationToken);
    }

    public virtual Task<YencHeaderStream> GetSegmentStreamAsync
    (
        string segmentId,
        bool includeHeaders,
        CancellationToken cancellationToken
    )
    {
        return _client.GetSegmentStreamAsync(segmentId, includeHeaders, cancellationToken);
    }

    public virtual Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
    }

    public virtual Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return _client.GetFileSizeAsync(file, cancellationToken);
    }

    public virtual Task WaitForReady(CancellationToken cancellationToken)
    {
        return _client.WaitForReady(cancellationToken);
    }

    protected INntpClient? SwapUnderlyingClient(INntpClient newClient)
    {
        var oldClient = Interlocked.Exchange(ref _client, newClient);
        return oldClient;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}