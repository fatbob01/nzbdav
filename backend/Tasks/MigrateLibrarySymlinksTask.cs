using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;
using NzbWebDAV.Utils;
using NzbWebDAV.WebDav;
using NzbWebDAV.Websocket;
using Serilog;

namespace NzbWebDAV.Tasks;

public class MigrateLibrarySymlinksTask(
    ConfigManager configManager,
    WebsocketManager websocketManager,
    DatabaseStore store
) : BaseTask
{
    protected override async Task ExecuteInternal()
    {
        try
        {
            // read config
            var mountDir = configManager.GetRcloneMountDir();
            var libraryDir = configManager.GetLibraryDir();
            if (libraryDir is null)
                throw new InvalidOperationException("The library directory must first be configured.");

            // send initial progress report
            var processed = 0;
            var retargetted = 0;
            _ = websocketManager.SendMessage(WebsocketTopic.SymlinkTaskProgress, processed.ToString());

            // process all symlinks
            var debounce = DebounceUtil.CreateDebounce(TimeSpan.FromMilliseconds(200));
            var allFiles = Directory.EnumerateFileSystemEntries(libraryDir, "*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                var fileInfo = new FileInfo(file);
                var resolvedLinkTarget = fileInfo.LinkTarget is null ? null : ResolveLinkTargetPath(fileInfo);
                var isOldSymlink = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                                   && resolvedLinkTarget is not null
                                   && resolvedLinkTarget.StartsWith(
                                       Path.Combine(mountDir, DavItem.ContentFolder.Name));
                if (isOldSymlink)
                {
                    await UpdateSymlink(fileInfo, mountDir, resolvedLinkTarget!);
                    retargetted++;
                }

                processed++;
                var progress = $"{retargetted}/{processed}";
                debounce(() => websocketManager.SendMessage(WebsocketTopic.SymlinkTaskProgress, progress));
            }

            // send final progress report
            var finalReport = $"complete: {retargetted}/{processed} re-targetted";
            _ = websocketManager.SendMessage(WebsocketTopic.SymlinkTaskProgress, finalReport);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while migrating library Symlinks");
            _ = websocketManager.SendMessage(WebsocketTopic.SymlinkTaskProgress, $"failed: {ex.Message}");
        }
    }

    private async Task UpdateSymlink(FileInfo oldSymlink, string mountDir, string resolvedLinkTarget)
    {
        var davPath = resolvedLinkTarget
            .RemovePrefix(mountDir)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var storeItem = await store.GetItemAsync(davPath, default);
        var davItem = storeItem switch
        {
            DatabaseStoreNzbFile nzbFile => nzbFile.DavItem,
            DatabaseStoreRarFile rarFile => rarFile.DavItem,
            DatabaseStoreMultipartFile multipartFile => multipartFile.DavItem,
            _ => null
        };

        if (davItem == null)
        {
            Log.Warning($"Symlink at path `{oldSymlink.FullName}` points to an item that does not exist.");
            return;
        }

        var newPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);
        if (oldSymlink.Exists) oldSymlink.Delete();
        oldSymlink.CreateAsSymbolicLink(newPath);
    }

    private static string ResolveLinkTargetPath(FileInfo fileInfo)
    {
        return fileInfo.ResolveLinkTarget(true)?.FullName
               ?? Path.GetFullPath(fileInfo.LinkTarget!, fileInfo.DirectoryName!);
    }
}
