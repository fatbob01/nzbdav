using System.Text;
using System.Linq;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, ConfigManager configManager) : BaseStoreReadonlyItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;
    public override DateTime CreatedAt => davFile.CreatedAt;
    private byte[] ContentBytes => Encoding.UTF8.GetBytes(GetTargetPath());

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    private string GetTargetPath()
    {
        return GetTargetPath(davFile, configManager.GetRcloneMountDir());
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        // mountDir is intentionally unused but preserved for compatibility with
        // callers that still supply it (e.g. migration tasks)
        _ = mountDir;

        // Normalize the WebDAV path and convert the completed-symlinks prefix
        // to the content prefix so the symlink target mirrors the actual media
        // location beneath the mounted content directory.
        var normalizedPath = NormalizePathSeparators(davFile.Path).Trim('/');
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (pathSegments.Count == 0)
        {
            return string.Empty;
        }

        if (pathSegments[0].Equals(DavItem.SymlinkFolder.Name, StringComparison.OrdinalIgnoreCase))
        {
            pathSegments[0] = DavItem.ContentFolder.Name;
        }

        var contentPath = string.Join('/', pathSegments);

        // Walk up to the filesystem root (20 levels is effectively unlimited on
        // Windows) so that the link resolves correctly whether it is used inside
        // or outside the rclone mount. Once at the root, append the content path.
        var rootWalk = string.Concat(Enumerable.Repeat("../", 20));
        return rootWalk + contentPath;
    }

    // --- Added missing methods to fix build errors ---

    public static string NormalizePathSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        // Rclone and WebDAV typically prefer forward slashes
        return path.Replace('\\', '/');
    }

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        // Normalize separators and ensure no trailing slash for clean path joining
        return NormalizePathSeparators(mountDir).TrimEnd('/');
    }
}
