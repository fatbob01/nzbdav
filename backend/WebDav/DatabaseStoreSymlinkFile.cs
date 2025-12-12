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
                // STRATEGY: Corrected Relative Path.
                // 1. Previous logs proved that "../../../" successfully reaches "C:\".
                // 2. Previous attempt failed because it looked for "C:\.ids".
                // 3. FIX: We must point to "C:\nzbdav\mount\.ids".
                // 
                // RESULT: "../../../nzbdav/mount/.ids/..."
                // This combines the safety of relative paths (no colons/backslashes to corrupt)
                // with the accuracy of your specific directory structure.
                
                var target = GetCorrectedRelativePath();
                
                Log.Error("[SYMLINK] Generated Corrected Relative Target: '{Target}'", target);
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

    private string GetCorrectedRelativePath()
    {
        // 1. Calculate depth from the file's perspective
        // Example Source: /completed-symlinks/movies/MovieName/file.mkv
        var parentPath = davFile.Parent?.Path ?? System.IO.Path.GetDirectoryName(davFile.Path)?.Replace('\\', '/') ?? "";
        
        // Segments: [completed-symlinks, movies, MovieName] -> Count: 3
        var segments = parentPath.Trim('/').Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        var depth = segments.Length;
        
        var sb = new StringBuilder();

        // 2. Go Up to Root (C:\) using standard ".."
        for (int i = 0; i < depth; i++)
        {
            sb.Append("../");
        }

        // 3. Inject the Mount Path (nzbdav/mount)
        // This is the missing piece from the last attempt!
        // We assume the mount path is "C:\nzbdav\mount", so we need "nzbdav/mount/"
        var mountDir = configManager.GetRcloneMountDir() ?? "";
        
        // Remove Drive Letter (C:) so we are left with "\nzbdav\mount"
        if (mountDir.Length > 1 && mountDir[1] == ':')
        {
            mountDir = mountDir.Substring(2); 
        }
        
        // Normalize to forward slashes and trim
        mountDir = mountDir.Replace('\\', '/').Trim('/');
        
        if (!string.IsNullOrEmpty(mountDir))
        {
            sb.Append(mountDir);
            sb.Append('/');
        }

        // 4. Append .ids folder and file ID
        // Final result example: "../../../nzbdav/mount/.ids/p/r/e/GUID"
        sb.Append(DavItem.IdsFolder.Name); 
        
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
