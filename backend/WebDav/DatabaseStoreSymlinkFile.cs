using System.Text;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Utils;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, string parentPath) : BaseStoreItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;

    private string TargetPath =>
        Path
            .Combine("..", "..", DavItem.ContentFolder.Name, parentPath, davFile.Name)
            .Replace('\\', '/');

    private byte[] ContentBytes =>
        Encoding.UTF8.GetBytes(TargetPath);

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        Log.Error("symlinks files cannot be modified. They simply mirror items in the /content root");
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        Log.Error("symlinks files cannot be copied. They simply mirror items in the /content root");
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}