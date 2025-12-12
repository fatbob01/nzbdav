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
                // FINAL STRATEGY: Hardcoded Drive-Relative Path.
                // We force the path to be "/nzbdav/mount/.ids/..."
                // 1. Forward slashes '/' prevent Rclone from mangling backslashes.
                // 2. No drive letter (no 'C:') prevents Rclone from mangling colons.
                // 3. Leading slash '/' tells Windows to look at the drive root.
                // This will resolve to "C:\nzbdav\mount\.ids\..." on your machine.
                
                var target = GetHardcodedDriveRelativePath();
                
                Log.Error("[SYMLINK] Generated Hardcoded Target: '{Target}'", target);
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

    private string GetHardcodedDriveRelativePath()
    {
        // HARDCODED FIX: 
        // We assume your mount is at "C:\nzbdav\mount".
        // We strip "C:" to make it drive-relative -> "/nzbdav/mount"
        const string ManualMountPath = "/nzbdav/mount";

        var sb = new StringBuilder();
        sb.Append(ManualMountPath);
        sb.Append('/'); // Standard separator
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
        var clean = mountDir.Replace('\uF03A', ':').Replace('\uF05C', '\\').Replace('\uF02F', '/');
        return clean.TrimEnd('\\', '/').Replace('\\', '/');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }
}
