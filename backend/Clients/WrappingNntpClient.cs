﻿using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public abstract class WrappingNntpClient(INntpClient client) : INntpClient
{
    public virtual Task<bool> ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken)
    {
        return client.ConnectAsync(host, port, useSsl, cancellationToken);
    }

    public virtual Task<bool> AuthenticateAsync(string user, string pass, CancellationToken cancellationToken)
    {
        return client.AuthenticateAsync(user, pass, cancellationToken);
    }

    public virtual Task<NntpStatResponse> StatAsync(string segmentId, CancellationToken cancellationToken)
    {
        return client.StatAsync(segmentId, cancellationToken);
    }

    public virtual Task<NntpDateResponse> DateAsync(CancellationToken cancellationToken)
    {
        return client.DateAsync(cancellationToken);
    }

    public virtual Task<YencHeaderStream> GetSegmentStreamAsync(string segmentId, CancellationToken cancellationToken)
    {
        return client.GetSegmentStreamAsync(segmentId, cancellationToken);
    }

    public virtual Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
    }

    public virtual Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return client.GetFileSizeAsync(file, cancellationToken);
    }

    public virtual Task WaitForReady(CancellationToken cancellationToken)
    {
        return client.WaitForReady(cancellationToken);
    }

    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }
}