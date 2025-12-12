using System.Text;
using System.Linq;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, ConfigManager configManager) : BaseStoreReadonlyItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    
    // Generate content once and cache it
    private byte[]? _contentBytes;
    private byte[] ContentBytes 
    {
        get 
        {
            if (_contentBytes == null)
            {
                var target = GetTargetPath();
                // Log this as ERROR to guarantee visibility
                Log.Error("Generated Symlink Target: {Target}", target);
                _contentBytes = Encoding.UTF8.GetBytes(target);
            }
            return _contentBytes;
        }
    }

    public override long FileSize => ContentBytes.Length;
    public override DateTime CreatedAt => davFile.CreatedAt;

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    private string GetTargetPath()
    {
        // NUCLEAR FIX: Ignore the Config/Env variable entirely.
        // We hardcode the clean Windows path here to bypass any potential corruption.
        const string ForceCleanMountDir = @"C:\nzbdav\mount";
        
        return GetTargetPath(davFile, ForceCleanMountDir);
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        var idSegments = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Append(davFile.Id.ToString());

        // Since we are forcing a hardcoded path, we know it is Windows style
        // and uses backslashes.
        return JoinWithSeparator(mountDir, '\\', idSegments);
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

    // ==============================================================================
    // PUBLIC HELPER METHODS RESTORED TO FIX BUILD ERRORS
    // These are called by OrganizedSymlinksUtil.cs and other parts of the app.
    // ==============================================================================

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        // Basic normalization for other parts of the app that still use this
        var trimmed = mountDir.TrimEnd('\\', '/');
        return IsWindowsStylePath(trimmed) ? trimmed.Replace('\\', '/') : trimmed;
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
