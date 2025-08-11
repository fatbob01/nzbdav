using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Services;

public class SymlinkMirrorService(
    DavDatabaseClient dbClient,
    ConfigManager configManager
)
{
    public void Mirror(DavItem item)
    {
        var relativePath = GetRelativePath(item);
        MirrorItem(item, relativePath);
    }

    private string GetRelativePath(DavItem item)
    {
        var segments = new List<string> { item.Name };
        var current = item;
        while (current.ParentId is not null && current.ParentId != DavItem.ContentFolder.Id)
        {
            current = dbClient.Ctx.Items.First(x => x.Id == current.ParentId);
            segments.Add(current.Name);
        }
        segments.Reverse();
        return Path.Join(segments.ToArray());
    }

    private void MirrorItem(DavItem item, string relativePath)
    {
        var mirrorRoot = configManager.GetSymlinkMirrorDir();

        if (item.Type == DavItem.ItemType.Directory || item.Type == DavItem.ItemType.SymlinkRoot)
        {
            Directory.CreateDirectory(Path.Join(mirrorRoot, relativePath));
            var children = dbClient.Ctx.Items.Where(x => x.ParentId == item.Id).ToList();
            foreach (var child in children)
            {
                MirrorItem(child, Path.Join(relativePath, child.Name));
            }
            return;
        }

        var parentPath = Path.GetDirectoryName(relativePath) ?? "";
        Directory.CreateDirectory(Path.Join(mirrorRoot, parentPath));
        var fileName = Path.GetFileName(relativePath);
        var filePath = Path.Join(mirrorRoot, parentPath, fileName + ".symlink");
        var depth = parentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
        var ups = Enumerable.Repeat("..", depth + 1);
        var parts = new List<string>(ups) { DavItem.ContentFolder.Name };
        if (!string.IsNullOrEmpty(parentPath))
            parts.Add(parentPath);
        parts.Add(fileName);
        var target = Path.Combine(parts.ToArray()).Replace('\\', '/');
        File.WriteAllText(filePath, target);
    }
}
