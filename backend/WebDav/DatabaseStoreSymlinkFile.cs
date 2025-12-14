using System.Text;
using System.Linq;
using System.Collections.Generic;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using Serilog;

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
        var normalizedMountDir = NormalizeMountContentRoot(NormalizeMountDir(mountDir));

        if (string.IsNullOrWhiteSpace(normalizedMountDir))
        {
            Log.Error("Unable to build symlink target because no rclone mount directory is configured.");
            throw new InvalidOperationException("The rclone mount directory must be configured to build symlinks.");
        }

        // Normalize the WebDAV path and convert the completed-symlinks prefix
        // to the content prefix so the symlink target mirrors the actual media
        // location beneath the mounted content directory.
        var normalizedPath = NormalizePathSeparators(davFile.Path).Trim('/');
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (pathSegments.Count == 0)
        {
            pathSegments = BuildFallbackPathSegments(davFile);

            if (pathSegments.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Unable to build symlink target path for `{davFile.Id}` with original path `{davFile.Path}`.");
            }
        }

        var mountRoot = HasDriveLetter(normalizedMountDir)
            ? normalizedMountDir.TrimEnd('/')
            : EnsureLeadingSlash(RemoveDriveLetter(normalizedMountDir).TrimEnd('/'));

        if (string.IsNullOrWhiteSpace(mountRoot) || mountRoot == "/")
        {
            Log.Error(
                "Unable to build symlink target because the rclone mount directory `{MountDir}` cannot be converted into a mount root.",
                mountDir);
            throw new InvalidOperationException("Cannot determine mount root for symlink target generation.");
        }

        if (pathSegments[0].Equals(DavItem.SymlinkFolder.Name, StringComparison.OrdinalIgnoreCase))
        {
            pathSegments[0] = DavItem.ContentFolder.Name;
        }

        var mountRootSegments = NormalizePathSeparators(mountRoot)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (pathSegments.Count > 0
            && pathSegments[0].Equals(DavItem.ContentFolder.Name, StringComparison.OrdinalIgnoreCase)
            && mountRootSegments.LastOrDefault()?.Equals(DavItem.ContentFolder.Name, StringComparison.OrdinalIgnoreCase) == true)
        {
            pathSegments = pathSegments.Skip(1).ToList();
        }

        var contentPath = string.Join('/', pathSegments);

        return string.Join('/', new[] { mountRoot.TrimEnd('/'), contentPath }.Where(x => !string.IsNullOrEmpty(x)));
    }

    // --- Added missing methods to fix build errors ---

    public static string NormalizePathSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        // Rclone and WebDAV typically prefer forward slashes
        return path.Replace('\\', '/');
    }

    private static List<string> BuildFallbackPathSegments(DavItem davFile)
    {
        Log.Warning(
            "Attempted to build symlink target for item with empty path. Path `{Path}`, Id `{Id}`, ParentId `{ParentId}`.",
            davFile.Path,
            davFile.Id,
            davFile.ParentId);

        var segments = new List<string>();

        if (davFile.ParentId == DavItem.SymlinkFolder.Id || davFile.ParentId == DavItem.ContentFolder.Id)
        {
            segments.Add(DavItem.ContentFolder.Name);
        }
        else if (davFile.ParentId == DavItem.Root.Id || davFile.ParentId is null)
        {
            // No additional prefix required.
        }
        else
        {
            Log.Warning(
                "Fallback symlink reconstruction cannot determine parent path for ParentId `{ParentId}`; using filename only.",
                davFile.ParentId);
        }

        if (!string.IsNullOrWhiteSpace(davFile.Name))
        {
            segments.Add(davFile.Name);
        }

        return segments;
    }

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        // Normalize separators and ensure no trailing slash for clean path joining
        return NormalizePathSeparators(mountDir).TrimEnd('/');
    }

    private static string NormalizeMountContentRoot(string mountDir)
    {
        if (string.IsNullOrWhiteSpace(mountDir))
        {
            return mountDir;
        }

        var hasLeadingSlash = mountDir.StartsWith('/');
        var mountSegments = NormalizePathSeparators(mountDir)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (mountSegments.Count > 0
            && mountSegments[^1].Equals(DavItem.SymlinkFolder.Name, StringComparison.OrdinalIgnoreCase))
        {
            mountSegments[^1] = DavItem.ContentFolder.Name;
            var rebuilt = string.Join('/', mountSegments);
            return hasLeadingSlash ? "/" + rebuilt : rebuilt;
        }

        return mountDir;
    }

    public static string RemoveDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
        {
            return path[2..];
        }

        return path;
    }

    public static string EnsureLeadingSlash(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static bool HasDriveLetter(string path)
    {
        return !string.IsNullOrEmpty(path)
               && path.Length >= 2
               && path[1] == ':'
               && char.IsLetter(path[0]);
    }
}
