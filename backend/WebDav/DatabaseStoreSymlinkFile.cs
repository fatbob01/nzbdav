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
                // STRATEGY: Absolute Path with FORWARD SLASHES.
                // 1. Previous attempts showed Rclone corrupts "C:\" to "C" (colon corruption).
                // 2. Previous attempts showed Rclone corrupts "\" to "" (backslash corruption).
                // 3. We MUST provide an absolute path ("C:") because Radarr moves the file, breaking relative links.
                // 
                // FIX: We use "C:/" (Forward Slash). 
                // Many Windows APIs accept forward slashes, and this format often bypasses 
                // the specific regex/encoding triggers in Rclone that corrupt backslashes.
                
                var target = GetHardcodedAbsolutePath();
                
                // Log the final target for debugging. 
                // Expected output: "C:/nzbdav/mount/.ids/..."
                Log.Error("[SYMLINK] Generated Absolute Target (Forward Slash): '{Target}'", target);
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

    private string GetHardcodedAbsolutePath()
    {
        // HARDCODED CONFIGURATION
        // Ensure this matches your exact mount path on Windows
        const string ManualMountPath = "C:/nzbdav/mount";

        var sb = new StringBuilder();
        sb.Append(ManualMountPath);
        // Ensure we don't double slash if the const has one
        if (!ManualMountPath.EndsWith("/")) 
        {
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
    // HELPER METHODS (Kept to prevent build errors in OrganizedSymlinksUtil.cs)
    // ==============================================================================

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        var clean = mountDir
            .Replace('\uF03A', ':')
            .Replace('\uF05C', '\\')
            .Replace('\uF02F', '/');
        return clean.TrimEnd('\\', '/').Replace('\\', '/');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }
}
