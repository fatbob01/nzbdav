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
                // STRATEGY: UNC Path via Localhost.
                // 1. "C:" gets corrupted to "C".
                // 2. "\" gets corrupted to "".
                // 3. Relative paths fail when Radarr moves the file.
                //
                // FIX: Use UNC format "//localhost/c$/path".
                // This is an absolute path that Windows understands, but it 
                // contains NO COLONS and NO BACKSLASHES (we use forward slash).
                
                var target = GetUncTargetPath();
                
                // Log the final target. Expected: "//localhost/c$/nzbdav/mount/.ids/..."
                Log.Error("[SYMLINK] Generated UNC Target: '{Target}'", target);
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

    private string GetUncTargetPath()
    {
        // 1. Get the configured path, e.g. "C:\nzbdav\mount"
        var mountDir = configManager.GetRcloneMountDir() ?? "";
        
        // Default to 'c' if we can't find a drive letter, but try to parse it.
        char driveLetter = 'c';
        string remainingPath = mountDir;

        // 2. Parse Drive Letter (e.g. "C:")
        if (mountDir.Length > 1 && mountDir[1] == ':')
        {
            driveLetter = char.ToLowerInvariant(mountDir[0]);
            remainingPath = mountDir.Substring(2); // "\nzbdav\mount"
        }

        // 3. Normalize path part to Forward Slashes
        remainingPath = remainingPath.Replace('\\', '/').Trim('/');

        // 4. Build UNC Path: //localhost/c$/nzbdav/mount/.ids/...
        var sb = new StringBuilder();
        sb.Append("//localhost/");
        sb.Append(driveLetter);
        sb.Append("$/");
        
        if (!string.IsNullOrEmpty(remainingPath))
        {
            sb.Append(remainingPath);
            sb.Append('/');
        }

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
    // HELPER METHODS (Kept to prevent build errors)
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
