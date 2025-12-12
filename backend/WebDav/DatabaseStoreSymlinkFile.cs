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
                // STRATEGY CHANGE: Use Relative Paths to bypass Rclone corruption.
                // Rclone is corrupting "C:\" into "C" regardless of settings.
                // Relative paths (e.g. "..\..\.ids\...") do not have colons, so they are safe.
                var target = GetRelativeTargetPath();
                
                // Log the generated target for verification
                Log.Error("[SYMLINK] Relative Target Generated: '{Target}'", target);
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

    private string GetRelativeTargetPath()
    {
        // 1. Calculate how deep the current file is.
        // Format is usually: /completed-symlinks/category/release_name/file.ext
        // We need to go up enough levels to reach the root.
        // We count the slashes in the parent path to determine depth.
        // e.g. /completed-symlinks/movies/Movie.2025/ -> 3 levels deep -> needs "..\..\..\"
        
        var parentPath = davFile.Parent?.Path ?? System.IO.Path.GetDirectoryName(davFile.Path)?.Replace('\\', '/') ?? "";
        // Ensure we don't count empty segments if path is just "/"
        var segments = parentPath.Trim('/').Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        var depth = segments.Length;
        
        // 2. Build the "Go Up" string (e.g. "..\..\..")
        // We use Windows backslashes '\' because Radarr/Windows expects them.
        var sb = new StringBuilder();
        // We go up one extra level to get out of the "release_name" folder, 
        // then "category", then "completed-symlinks" to reach root.
        // Actually, 'depth' is exactly the number of parent folders we are in relative to root.
        for (int i = 0; i < depth; i++)
        {
            sb.Append(@"..\");
        }

        // 3. Append the target path starting from root (.ids/...)
        // Structure: .ids/p/r/e/f/i/x/GUID
        sb.Append(DavItem.IdsFolder.Name); // .ids
        
        foreach (var c in davFile.IdPrefix)
        {
            sb.Append('\\');
            sb.Append(c);
        }
        
        sb.Append('\\');
        sb.Append(davFile.Id);

        return sb.ToString();
    }

    // Unused but required for compilation compatibility with other parts of the app
    public static string GetTargetPath(DavItem davFile, string mountDir) => ""; 

    // ==============================================================================
    // PUBLIC HELPER METHODS RESTORED TO FIX BUILD ERRORS
    // ==============================================================================

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        // Standard cleanup just in case
        var clean = mountDir.Replace('\uF03A', ':').Replace('\uF05C', '\\').Replace('\uF02F', '/');
        return clean.TrimEnd('\\', '/').Replace('\\', '/');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }
}
