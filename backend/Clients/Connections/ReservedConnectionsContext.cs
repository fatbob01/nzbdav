namespace NzbWebDAV.Clients.Connections;

public sealed class ReservedConnectionsContext
{
    public ReservedConnectionsContext(int reservedConnections)
    {
        ReservedConnections = reservedConnections;
    }

    public int ReservedConnections { get; }
}

