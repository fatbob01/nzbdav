using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;
using NzbWebDAV.Utils;

namespace NzbWebDAV.Api.Controllers.RemoveUnlinkedFiles;

[ApiController]
[Route("api/remove-unlinked-files")]
public class RemoveUnlinkedFilesController(
    DavDatabaseClient dbClient,
    ConfigManager configManager
) : BaseApiController
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly HashSet<Guid> _removedItems = [];

    protected override async Task<IActionResult> HandleRequest()
    {
        if (HttpContext.GetQueryParam("confirm") != "true")
            throw new BadHttpRequestException("`confirm` parameter needs to be set to true");

        var entered = await Semaphore.WaitAsync(TimeSpan.FromMilliseconds(500));
        if (!entered) return Conflict($"The task is already in progress.");
        try
        {
            return await RemoveUnlinkedFilesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async Task<IActionResult> RemoveUnlinkedFilesAsync()
    {
        var allDavItems = await dbClient.Ctx.Items.ToListAsync();

        // get linked file paths
        var linkedFilePaths = GetLinkedFilePaths();
        if (linkedFilePaths.Count < 100)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "error: " +
                                                              "There are less than one hundred linked files found in the library. " +
                                                              "Cancelling operation to prevent accidental bulk deletion.");
        }

        // determine paths to delete
        // only delete paths that have existed longer than a day
        var minExistance = TimeSpan.FromDays(1);
        var allEmptyDirectories = allDavItems
            .Where(x => x.Type == DavItem.ItemType.Directory)
            .Where(x => x.CreatedAt < DateTime.Now.Subtract(minExistance))
            .Where(x => x.Children.All(y => _removedItems.Contains(y.Id)));
        var allUnlinkedFiles = allDavItems
            .Where(x => x.Type is DavItem.ItemType.NzbFile or DavItem.ItemType.RarFile)
            .Where(x => x.CreatedAt < DateTime.Now.Subtract(minExistance))
            .Where(x => !linkedFilePaths.Contains(GetPath(x)))
            .ToList();

        // remove all empty directories
        foreach (var emptyDirectory in allEmptyDirectories)
            RemoveItem(emptyDirectory);

        // remove all unlinked files
        foreach (var unlinkedFile in allUnlinkedFiles)
            RemoveItem(unlinkedFile);

        // save changes to database
        await dbClient.Ctx.SaveChangesAsync();

        // return all removed paths
        var allRemovedPaths = allDavItems
            .Where(x => _removedItems.Contains(x.Id))
            .Select(GetPath)
            .ToList();
        return Ok(allRemovedPaths);
    }

    private void RemoveItem(DavItem item)
    {
        // ignore protected folders
        if (item.IsProtected()) return;

        // ignore already removed items
        if (_removedItems.Contains(item.Id)) return;

        // remove the item
        dbClient.Ctx.Items.Remove(item);
        _removedItems.Add(item.Id);

        // remove the parent directory, if it is empty.
        if (item.Parent!.Children.All(x => _removedItems.Contains(x.Id)))
            RemoveItem(item.Parent!);
    }

    private static string GetPath(DavItem? davItem)
    {
        ArgumentNullException.ThrowIfNull(davItem);
        return davItem.Id != DavItem.Root.Id
            ? Path.Combine(GetPath(davItem.Parent), davItem.Name)
            : "/";
    }

    private HashSet<string> GetLinkedFilePaths()
    {
        var mountDir = configManager.GetRcloneMountDir();
        var libraryRoot = EnvironmentUtil.GetVariable("LIBRARY_DIR")!;
        return Directory.EnumerateFileSystemEntries(libraryRoot, "*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x))
            .Where(x => x.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(x => x.LinkTarget)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => x.StartsWith(mountDir))
            .Select(x => x.RemovePrefix(mountDir))
            .Select(x => x.StartsWith("/") ? x : $"/{x}")
            .ToHashSet();
    }
}