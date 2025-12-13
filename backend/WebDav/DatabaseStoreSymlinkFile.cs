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
                // STRATEGY: Point to "content" folder.
                // 1. "completed-symlinks" mirrors the structure of "content".
                // 2. We simply calculate the relative path to jump over to the parallel "content" folder.
                // 3. This stays within the Rclone mount, avoiding absolute path/drive letter issues.
                //
                // Example: 
                // Source: /completed-symlinks/movies/Avatar/file.mkv
                // Target: ../../../content/movies/Avatar/file.mkv
                
                var target = GetContentRelativePath();
                
                // Log the generated target for verification
                Log.Information("[SYMLINK] Generated Content Target: '{Target}' for file '{Name}'", target, davFile.Name);
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

    private string GetContentRelativePath()
    {
        // 1. Get the full internal path of the current file
        // Example: /completed-symlinks/movies/MovieName/file.mkv
        var fullPath = davFile.Path?.Replace('\\', '/').Trim('/') ?? "";
        
        // 2. Split into segments: [completed-symlinks, movies, MovieName, file.mkv]
        var segments = fullPath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        
        // 3. Calculate depth to go back to Mount Root
        // We subtract 1 because we don't count the filename itself as a directory level
        var depth = segments.Length - 1; 
        
        var sb = new StringBuilder();
        
        // 4. Go up to Mount Root
        // "../../../"
        for (int i = 0; i < depth; i++)
        {
            sb.Append("../");
        }
        
        // 5. Descend into "content" folder
        // We start loop at index 1 to SKIP the first segment ("completed-symlinks")
        // We effectively replace "completed-symlinks" with "content"
        sb.Append("content");
        
        for (int i = 1; i < segments.Length; i++) 
        {
            sb.Append('/');
            sb.Append(segments[i]);
        }
        
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
