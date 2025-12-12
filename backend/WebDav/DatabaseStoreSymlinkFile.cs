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
                // STRATEGY: Drive-Relative Absolute Path.
                // 1. We cannot use "C:\" because Rclone corrupts the colon (C).
                // 2. We cannot use "..\..\" because Radarr moves the file, breaking relative links.
                // 3. SOLUTION: Use "/nzbdav/mount/.ids/...".
                //    - Rclone sees a standard path (no colon).
                //    - Windows interprets "/" as "Root of Current Drive" (C:\nzbdav\mount).
                var target = GetDriveRelativePath();
                
                // Log for verification
                Log.Error("[SYMLINK] Generated Drive-Relative Target: '{Target}'", target);
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

    private string GetDriveRelativePath()
    {
        // 1. Get the configured mount directory (e.g. "C:\nzbdav\mount")
        var mountDir = configManager.GetRcloneMountDir() ?? "";

        // 2. Strip Drive Letter if present (e.g. "C:")
        // This prevents the Rclone colon corruption.
        if (mountDir.Length > 1 && mountDir[1] == ':')
        {
            mountDir = mountDir.Substring(2); // "C:\path" -> "\path"
        }

        // 3. Normalize to Forward Slashes and ensure leading slash
        // Forward slashes are standard for WebDAV and Rclone handles them natively.
        // Windows is happy to resolve them too.
        mountDir = mountDir.Replace('\\', '/');
        if (!mountDir.StartsWith("/"))
        {
            mountDir = "/" + mountDir;
        }
        
        // 4. Trim trailing slash to prepare for join
        mountDir = mountDir.TrimEnd('/');

        // 5. Build final path: /nzbdav/mount/.ids/p/r/e/GUID
        var sb = new StringBuilder();
        sb.Append(mountDir);
        sb.Append('/');
        sb.Append(DavItem.IdsFolder.Name); // .ids
        
        foreach (var c in davFile.IdPrefix)
        {
            sb.Append('/'); 
            sb.Append(c);
        }
        
        sb.Append('/'); 
        sb.Append(davFile.Id);

        return sb.ToString();
    }

    // Unused but required for compilation compatibility
    public static string GetTargetPath(DavItem davFile, string mountDir) => ""; 

    // ==============================================================================
    // HELPER METHODS (Kept to prevent build errors in other files)
    // ==============================================================================

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        var clean = mountDir.Replace('\uF03A', ':').Replace('\uF05C', '\\').Replace('\uF02F', '/');
        return clean.TrimEnd('\\', '/').Replace('\\', '/');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }
}
