using System.Text.RegularExpressions;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav;
using Serilog;

namespace NzbWebDAV.Utils;

public static partial class OrganizedSymlinksUtil
{
    /// <summary>
    /// Generate a path for an organized symlink
    /// </summary>
    public static string? GetOrganizedSymlinkPath(DavItem item, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            Log.Warning(
                "Attempted to create an organized symlink for item with an empty name. Id `{Id}`, ParentId `{ParentId}`.",
                item.Id,
                item.ParentId);
            return null;
        }

        category ??= GetCategoryFromPath(item.Path);

        if (string.IsNullOrWhiteSpace(category))
        {
            Log.Warning(
                "Attempted to create an organized symlink for item without a category. Name `{Name}`, Id `{Id}`, ParentId `{ParentId}`.",
                item.Name,
                item.Id,
                item.ParentId);
            return null;
        }

        var cleanedName = CleanItemName(item.Name);

        return $"/{DavItem.SymlinkFolder.Name}/{category}/{cleanedName}/{item.Name}";
    }

    /// <summary>
    /// Get symlink (used by HealthCheckService)
    /// </summary>
    public static string? GetSymlink(DavItem item, object? categoryOrConfig = null)
    {
        // Handle both string category and ConfigManager being passed
        string? category = null;
        
        if (categoryOrConfig is string cat)
        {
            category = cat;
        }
        else if (categoryOrConfig != null)
        {
            // ConfigManager was passed, extract category from item path instead
            category = GetCategoryFromPath(item.Path);
        }
        
        return GetOrganizedSymlinkPath(item, category);
    }

    /// <summary>
    /// Get library symlink targets (used by RemoveUnlinkedFilesTask)
    /// </summary>
    public static IEnumerable<(Guid DavItemId, string Target)> GetLibrarySymlinkTargets(DavItem item)
    {
        // Generate all possible symlink targets for this item
        var targets = new List<(Guid, string)>();
        
        // Main organized symlink target
        var organizedPath = GetOrganizedSymlinkPath(item);
        if (!string.IsNullOrEmpty(organizedPath))
        {
            // Return the item ID and its organized path
            targets.Add((item.Id, organizedPath));
        }
        
        return targets;
    }

    private static string? GetCategoryFromPath(string path)
    {
        // Extract category from path like /content/movies/... or /completed-symlinks/tv/...
        var segments = path?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments != null && segments.Length >= 2)
        {
            return segments[1]; // Return "movies" or "tv"
        }
        return null;
    }

    /// <summary>
    /// Cleans an item name by removing common patterns like quality tags, release groups, etc.
    /// </summary>
    private static string CleanItemName(string name)
    {
        // Remove file extension
        var nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(name);

        // Remove common patterns
        var cleaned = RemoveQualityTags().Replace(nameWithoutExtension, "");
        cleaned = RemoveReleaseInfo().Replace(cleaned, "");
        cleaned = RemoveExtraSpaces().Replace(cleaned, " ");

        return cleaned.Trim();
    }

    [GeneratedRegex(@"\b(480p|576p|720p|1080p|2160p|4K|8K)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RemoveQualityTags();

    [GeneratedRegex(@"\b(WEB-?DL|WEBRip|BluRay|BDRip|DVDRip|HDTV|x264|x265|HEVC|AAC|AC3|DTS)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex RemoveReleaseInfo();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RemoveExtraSpaces();

    public static string ResolveSymlinkTargetForItem(DavItem item, string mountDir)
    {
        var contentAwareMountDir = EnsureContentAwareMountDir(mountDir);
        return ResolveSymlinkTarget(item, contentAwareMountDir);
    }

    private static string ResolveSymlinkTarget(DavItem item, string contentAwareMountDir)
    {
        var normalizedPath = DatabaseStoreSymlinkFile.NormalizePathSeparators(item.Path).Trim('/');
        var segments = normalizedPath.Split('/');
        
        // Replace "completed-symlinks" with "content"
        if (segments.Length > 0 && segments[0].Equals("completed-symlinks", StringComparison.OrdinalIgnoreCase))
        {
            segments[0] = "content";
        }
        
        var contentPath = string.Join('/', segments);
        var mountRoot = DatabaseStoreSymlinkFile.NormalizeMountDir(contentAwareMountDir);
        
        return $"{mountRoot}/{contentPath}";
    }

    private static string EnsureContentAwareMountDir(string mountDir)
    {
        var normalizedMountDir = DatabaseStoreSymlinkFile.NormalizeMountDir(mountDir);
        var normalizedContentPath = DatabaseStoreSymlinkFile.NormalizePathSeparators(DavItem.ContentFolder.Path);

        if (normalizedMountDir.EndsWith(normalizedContentPath))
        {
            return normalizedMountDir;
        }

        return $"{normalizedMountDir.TrimEnd('/')}/{normalizedContentPath.TrimStart('/')}";
    }

    private static bool HasDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2)
        {
            return false;
        }

        return path[1] == ':' && char.IsLetter(path[0]);
    }
}
