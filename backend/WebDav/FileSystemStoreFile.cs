using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav;

public class FileSystemStoreFile(string name, string fullPath) : BaseStoreItem
{
    public override string Name => name;
    public override string UniqueKey => fullPath;
    public override long FileSize => File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
    public override DateTime CreatedAt => File.Exists(fullPath)
        ? File.GetCreationTimeUtc(fullPath)
        : DateTime.UtcNow;

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Task.FromResult(stream);
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            request.Source.CopyTo(fileStream);
        }

        return Task.FromResult(DavStatusCode.NoContent);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        if (request.Destination is not FileSystemStoreCollection destination)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        var destinationPath = Path.GetFullPath(Path.Combine(destination.DirectoryPath, request.Name));
        if (!destination.IsChildPathAllowed(destinationPath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
        if (File.Exists(destinationPath) && !request.Overwrite)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Conflict));

        Directory.CreateDirectory(destination.DirectoryPath);
        File.Copy(fullPath, destinationPath, request.Overwrite);
        return Task.FromResult(new StoreItemResult(
            DavStatusCode.Created,
            new FileSystemStoreFile(request.Name, destinationPath)));
    }
}
