using System.Text;
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
        var idSegments = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Append(davFile.Id.ToString());

        if (IsWindowsStylePath(mountDir))
        {
            // Rclone symlink targets are read as plain UTF-8 strings. Radarr and Sonarr expect
            // Windows-style absolute paths (drive letter or UNC) to use backslashes immediately
            // after the root. If we normalize to forward slashes, the drive portion can be
            // treated as a relative folder name and the import will fail. Preserve the
            // backslash separators so the target is parsed as an absolute Windows path.
            var normalizedMount = NormalizeMountDirForWindowsTarget(mountDir);
            return JoinWithSeparator(normalizedMount, '\\', idSegments);
        }

        var pathParts = idSegments
            .Prepend(NormalizeMountDir(mountDir))
            .ToArray();

        return Path.Join(pathParts);
    }

    private static string JoinWithSeparator(string mountDir, char separator, IEnumerable<string> idSegments)
    {
        var builder = new StringBuilder(mountDir.TrimEnd('\\', '/'));
        foreach (var segment in idSegments)
        {
            builder.Append(separator);
            builder.Append(segment);
        }

        return builder.ToString();
    }

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        var trimmed = mountDir.TrimEnd('\\', '/');
        return IsWindowsStylePath(trimmed) ? trimmed.Replace('\\', '/') : trimmed;
    }

    private static string NormalizeMountDirForWindowsTarget(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        var trimmed = mountDir.TrimEnd('\\', '/');
        return trimmed.Replace('/', '\\');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static bool IsWindowsStylePath(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return false;

        return mountDir.StartsWith("\\\\")
               || mountDir.StartsWith("//")
               || (mountDir.Length >= 2 && char.IsLetter(mountDir[0]) && mountDir[1] == ':');
    }
}
