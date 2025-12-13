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
                // STRATEGY: Universal Content Mirror (Hybrid).
                // 1. DYNAMIC PATHS: Calculate path segments to handle nested folders.
                // 2. ROOT WALKING: Use 20x ".." to force resolution to Drive Root (C:\).
                // 3. TARGET: Point to "content" folder (requires Junction).
                
                var target = GetUniversalContentPath();
                
                Log.Information("[SYMLINK] Generated Hybrid Target: '{Target}'", target);
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

    private string GetUniversalContentPath()
    {
        // 1. Get the full internal path
        var fullPath = davFile.Path?.Replace('\\', '/').Trim('/') ?? "";
        
        // Split segments: [completed-symlinks, movies, MovieName, file.mkv]
        var segments = fullPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        
        var sb = new StringBuilder();

        // 2. ROOT WALKING: Go up to the "Ceiling" (Drive Root)
        // We use 20 levels to force resolution to C:\ regardless of where Radarr moves the file.
        for (int i = 0; i < 20; i++)
        {
            sb.Append("../");
        }

        // 3. Point to "content" folder
        // This relies on the junction "C:\content" -> "C:\nzbdav\mount\content"
        sb.Append("content");
        
        // 4. Reconstruct the dynamic path
        // We skip index 0 ("completed-symlinks") and append the rest.
        for (int i = 1; i < segments.Length; i++) 
        {
            sb.Append('/');
            sb.Append(segments[i]);
        }
        
        return sb.ToString();
    }

    // Helper methods required for compilation
    public static string GetTargetPath(DavItem davFile, string mountDir) => ""; 

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
