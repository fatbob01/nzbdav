using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Websocket;

namespace NzbWebDAV.Tasks;

public class MigrateLibrarySymlinksTask
{
    private readonly IStore _store;
    private readonly WebsocketManager? _websocket;

    public MigrateLibrarySymlinksTask(IStore store, WebsocketManager? websocket = null)
    {
        _store = store;
        _websocket = websocket;
    }

    public async Task<IStoreItem?> GetItemForDavPath(string davPath, CancellationToken cancellationToken)
    {
        davPath = davPath.TrimStart('/', Path.DirectorySeparatorChar, '\\');
        davPath = davPath.Replace("\\", "/");
        return await _store.GetItemAsync(davPath, cancellationToken);
    }

    public async Task ExecuteAsync(IEnumerable<string> davPaths, CancellationToken cancellationToken = default)
    {
        if (_websocket != null)
        {
            await _websocket.PublishAsync(WebsocketTopic.MigrateLibrarySymlinksProgress, new { status = "started" }, cancellationToken);
        }
        foreach (var path in davPaths)
        {
            await GetItemForDavPath(path, cancellationToken);
        }
        if (_websocket != null)
        {
            await _websocket.PublishAsync(WebsocketTopic.MigrateLibrarySymlinksProgress, new { status = "completed" }, cancellationToken);
        }
    }
}
