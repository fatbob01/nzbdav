using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav;

public class FileSystemStoreCollection : BaseStoreCollection
{
    private readonly string _name;
    private readonly string _directoryPath;
    private readonly string _rootPath;

    public FileSystemStoreCollection(string name, string directoryPath, string? rootPath = null)
    {
        _name = name;
        _directoryPath = directoryPath;
        _rootPath = rootPath ?? directoryPath;
    }

    public override string Name => _name;
    public override string UniqueKey => _directoryPath;
    public override DateTime CreatedAt => Directory.Exists(_directoryPath)
        ? Directory.GetCreationTimeUtc(_directoryPath)
        : DateTime.UtcNow;
    internal string DirectoryPath => _directoryPath;
    internal bool IsChildPathAllowed(string candidatePath) => IsPathWithinRoot(candidatePath);

    protected override Task<IStoreItem?> GetItemAsync(GetItemRequest request)
    {
        var targetPath = Path.GetFullPath(Path.Combine(_directoryPath, request.Name));
        if (!IsPathWithinRoot(targetPath))
            return Task.FromResult<IStoreItem?>(null);

        if (Directory.Exists(targetPath))
            return Task.FromResult<IStoreItem?>(new FileSystemStoreCollection(request.Name, targetPath, _rootPath));

        if (File.Exists(targetPath))
            return Task.FromResult<IStoreItem?>(new FileSystemStoreFile(request.Name, targetPath));

        return Task.FromResult<IStoreItem?>(null);
    }

    protected override Task<IStoreItem[]> GetAllItemsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_directoryPath))
            return Task.FromResult(Array.Empty<IStoreItem>());

        try
        {
            var items = new List<IStoreItem>();
            foreach (var directory in Directory.EnumerateDirectories(_directoryPath))
            {
                var info = new DirectoryInfo(directory);
                items.Add(new FileSystemStoreCollection(info.Name, info.FullName, _rootPath));
            }

            foreach (var file in Directory.EnumerateFiles(_directoryPath))
            {
                var info = new FileInfo(file);
                items.Add(new FileSystemStoreFile(info.Name, info.FullName));
            }

            return Task.FromResult(items.ToArray());
        }
        catch (IOException)
        {
            return Task.FromResult(Array.Empty<IStoreItem>());
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(Array.Empty<IStoreItem>());
        }
    }

    protected override bool SupportsFastMove(SupportsFastMoveRequest request)
    {
        return false;
    }

    protected override Task<StoreItemResult> CreateItemAsync(CreateItemRequest request)
    {
        var targetPath = Path.GetFullPath(Path.Combine(_directoryPath, request.Name));
        if (!IsPathWithinRoot(targetPath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        if (File.Exists(targetPath) && !request.Overwrite)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Conflict));

        Directory.CreateDirectory(_directoryPath);
        using (var fileStream = new FileStream(
                   targetPath,
                   request.Overwrite ? FileMode.Create : FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            request.Stream.CopyTo(fileStream);
        }

        var status = request.Overwrite ? DavStatusCode.Ok : DavStatusCode.Created;
        return Task.FromResult(new StoreItemResult(status, new FileSystemStoreFile(request.Name, targetPath)));
    }

    protected override Task<StoreCollectionResult> CreateCollectionAsync(CreateCollectionRequest request)
    {
        var targetPath = Path.GetFullPath(Path.Combine(_directoryPath, request.Name));
        if (!IsPathWithinRoot(targetPath))
            return Task.FromResult(new StoreCollectionResult(DavStatusCode.Forbidden));

        if (Directory.Exists(targetPath) && !request.Overwrite)
            return Task.FromResult(new StoreCollectionResult(DavStatusCode.Conflict));

        Directory.CreateDirectory(targetPath);
        var collection = new FileSystemStoreCollection(request.Name, targetPath, _rootPath);
        return Task.FromResult(new StoreCollectionResult(DavStatusCode.Created, collection));
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        if (request.Destination is not FileSystemStoreCollection destination)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        var sourcePath = Path.GetFullPath(_directoryPath);
        if (!IsPathWithinRoot(sourcePath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        var destinationPath = Path.GetFullPath(Path.Combine(destination._directoryPath, request.Name));
        if (!destination.IsPathWithinRoot(destinationPath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        if ((File.Exists(destinationPath) || Directory.Exists(destinationPath)) && !request.Overwrite)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Conflict));

        CopyDirectory(sourcePath, destinationPath, request.Overwrite);
        var copied = new FileSystemStoreCollection(request.Name, destinationPath, destination._rootPath);
        return Task.FromResult(new StoreItemResult(DavStatusCode.Created, copied));
    }

    protected override Task<StoreItemResult> MoveItemAsync(MoveItemRequest request)
    {
        if (request.Destination is not FileSystemStoreCollection destination)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        var sourcePath = Path.GetFullPath(Path.Combine(_directoryPath, request.SourceName));
        if (!IsPathWithinRoot(sourcePath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        var destinationPath = Path.GetFullPath(Path.Combine(destination._directoryPath, request.DestinationName));
        if (!destination.IsPathWithinRoot(destinationPath))
            return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

        if ((File.Exists(destinationPath) || Directory.Exists(destinationPath)) && !request.Overwrite)
            return Task.FromResult(new StoreItemResult(DavStatusCode.Conflict));

        if (Directory.Exists(sourcePath))
        {
            if (Directory.Exists(destinationPath))
                Directory.Delete(destinationPath, true);
            Directory.Move(sourcePath, destinationPath);
            var collection = new FileSystemStoreCollection(request.DestinationName, destinationPath, destination._rootPath);
            return Task.FromResult(new StoreItemResult(DavStatusCode.Created, collection));
        }

        if (File.Exists(sourcePath))
        {
            Directory.CreateDirectory(destination._directoryPath);
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(sourcePath, destinationPath);
            return Task.FromResult(new StoreItemResult(
                DavStatusCode.Created,
                new FileSystemStoreFile(request.DestinationName, destinationPath)));
        }

        return Task.FromResult(new StoreItemResult(DavStatusCode.NotFound));
    }

    protected override Task<DavStatusCode> DeleteItemAsync(DeleteItemRequest request)
    {
        var targetPath = Path.GetFullPath(Path.Combine(_directoryPath, request.Name));
        if (!IsPathWithinRoot(targetPath))
            return Task.FromResult(DavStatusCode.Forbidden);

        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, true);
            return Task.FromResult(DavStatusCode.NoContent);
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            return Task.FromResult(DavStatusCode.NoContent);
        }

        return Task.FromResult(DavStatusCode.NotFound);
    }

    private bool IsPathWithinRoot(string candidatePath)
    {
        var rootPath = Path.GetFullPath(_rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(candidatePath, rootPath, comparison)
               || candidatePath.StartsWith(rootPath + Path.DirectorySeparatorChar, comparison)
               || candidatePath.StartsWith(rootPath + Path.AltDirectorySeparatorChar, comparison);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, bool overwrite)
    {
        if (Directory.Exists(destinationPath))
        {
            if (!overwrite)
                return;
            Directory.Delete(destinationPath, true);
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            var destinationFile = Path.Combine(destinationPath, fileName);
            File.Copy(file, destinationFile, true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourcePath))
        {
            var directoryName = Path.GetFileName(directory);
            var destinationDirectory = Path.Combine(destinationPath, directoryName);
            CopyDirectory(directory, destinationDirectory, true);
        }
    }
}
