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
                // STRATEGY: Root-Walking Relative Path.
                // 1. Rclone corrupts "C:" to "C" (Absolute paths fail).
                // 2. Rclone corrupts "\" to "" (Backslashes fail).
                // 3. Simple relative paths fail because Radarr moves the file.
                //
                // FIX: Use excessive "../" segments.
                // "C:\Any\Folder\..\..\" -> "C:\"
                // This forces resolution to the Drive Root without using a colon.
                // We use 15 levels up to be safe (deeper than any likely folder structure).
                
                var target = GetRootWalkingPath();
                
                // Log the final target. Expected: "../../../../../nzbdav/mount/.ids/..."
                Log.Error("[SYMLINK] Generated Root-Walking Target: '{Target}'", target);
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

    private string GetRootWalkingPath()
    {
        // 1. Get the configured path, e.g. "C:\nzbdav\mount"
        var mountDir = configManager.GetRcloneMountDir() ?? "";

        // 2. Strip Drive Letter if present (e.g. "C:")
        if (mountDir.Length > 1 && mountDir[1] == ':')
        {
            mountDir = mountDir.Substring(2); // "C:\path" -> "\path"
        }

        // 3. Normalize to Forward Slashes and TRIM leading slash
        // We want "nzbdav/mount", NOT "/nzbdav/mount"
        mountDir = mountDir.Replace('\\', '/').Trim('/');
        
        var sb = new StringBuilder();

        // 4. Go up 15 levels. 
        // On Windows, going up from Root stays at Root.
        // This guarantees we start resolution at C:\ regardless of where the symlink sits.
        for(int i=0; i<15; i++) 
        {
            sb.Append("../");
        }

        // 5. Build path: [../../] + [nzbdav/mount] + [/.ids/p/r/e/GUID]
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
