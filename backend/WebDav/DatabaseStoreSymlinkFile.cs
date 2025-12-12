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
                // STRATEGY: Calculated Relative Path with FORWARD SLASHES.
                // 1. Absolute paths fail due to Rclone colon corruption (C: -> C).
                // 2. Backslashes fail due to Rclone corruption (\ -> ).
                // 3. We use calculated relative paths ("../") to avoid colons.
                // 4. We use forward slashes ("/") to avoid backslash corruption.
                // 5. This path must be valid for the SOURCE location (where Radarr scans it).
                
                var target = GetCalculatedRelativePath();
                
                // Log strictly as error to ensure visibility
                Log.Error("[SYMLINK] Generated Relative Target: '{Target}'", target);
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

    private string GetCalculatedRelativePath()
    {
        // 1. Calculate depth from the file's perspective
        // Docker Path example: /completed-symlinks/movies/MovieName/file.mkv
        // Parent Path: /completed-symlinks/movies/MovieName
        var parentPath = davFile.Parent?.Path ?? System.IO.Path.GetDirectoryName(davFile.Path)?.Replace('\\', '/') ?? "";
        
        // Segments: [completed-symlinks, movies, MovieName] -> Count: 3
        var segments = parentPath.Trim('/').Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        var depth = segments.Length;
        
        var sb = new StringBuilder();

        // 2. Go Up to Root using standard ".."
        for (int i = 0; i < depth; i++)
        {
            sb.Append("../");
        }

        // 3. Go Down to .ids
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

    // Unused but required for compilation compatibility with OrganizedSymlinksUtil
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
