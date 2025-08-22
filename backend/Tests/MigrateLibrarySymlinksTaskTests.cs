using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Tasks;
using Xunit;

class FakeStore : IStore
{
    public string? LastPath;
    public Task<IStoreItem?> GetItemAsync(string path, CancellationToken cancellationToken)
    {
        LastPath = path;
        return Task.FromResult<IStoreItem?>(null);
    }
    public Task<IStoreItem?> GetItemAsync(Uri uri, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IStoreCollection?> GetCollectionAsync(Uri uri, CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class MigrateLibrarySymlinksTaskTests
{
    [Theory]
    [InlineData("C:/mount/folder/file", "C:/mount/folder/file")]
    [InlineData("/C:/mount/folder/file", "C:/mount/folder/file")]
    [InlineData("C:\\mount\\folder\\file", "C:/mount/folder/file")]
    [InlineData("\\C:\\mount\\folder\\file", "C:/mount/folder/file")]
    public async Task WindowsPathsAreNormalized(string input, string expected)
    {
        var store = new FakeStore();
        var task = new MigrateLibrarySymlinksTask(store, null);
        await task.GetItemForDavPath(input, CancellationToken.None);
        Assert.Equal(expected, store.LastPath);
    }
}
