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
                // STRATEGY: Pure Relative Path.
                // 1. Previous logs confirmed that "../../../" successfully walks up the folder tree
                //    without triggering Rclone's character corruption (C: -> C).
                // 2. The previous error showed we landed at "C:\nzbdav\mount" correctly, 
                //    but then appended "nzbdav\mount" again, causing duplication.
                // 
                // FIX: We rely solely on ".." to reach the mount root, then point directly to ".ids".
                // Result: "../../../.ids/..." -> Resolves to "C:\nzbdav\mount\.ids\..."
                
                var target = GetSimpleRelativePath();
                
                Log.Error("[SYMLINK] Generated Simple Relative Target: '{Target}'", target);
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

    private string GetSimpleRelativePath()
    {
        // 1. Calculate depth from the file's perspective
        // Source: /completed-symlinks/movies/MovieName/file.mkv
        var parentPath = davFile.Parent?.Path ?? System.IO.Path.GetDirectoryName(davFile.Path)?.Replace('\\', '/') ?? "";
        
        // Segments: [completed-symlinks, movies, MovieName] -> Count: 3
        var segments = parentPath.Trim('/').Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        var depth = segments.Length;
        
        var sb = new StringBuilder();

        // 2. Go Up to Mount Root using standard ".."
        // This lands us at "C:\nzbdav\mount"
        for (int i = 0; i < depth; i++)
        {
            sb.Append("../");
        }

        // 3. Append .ids folder and file ID
        // We do NOT inject "nzbdav/mount" here because we are already inside it.
        // Result: "../../../.ids/p/r/e/GUID"
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
