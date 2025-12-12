        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }
}
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
                // STRATEGY: Use Relative Paths with FORWARD SLASHES.
                // 1. Relative paths bypass the "Colons in absolute paths" corruption.
                // 2. Forward slashes bypass the "Backslash encoded as " corruption.
                // Rclone/Windows should resolve "../.ids/..." correctly.
                var target = GetRelativeTargetPath();
                
                Log.Error("[SYMLINK] Relative Target Generated (Forward Slash): '{Target}'", target);
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
        // 1. Calculate depth
        // We look at the parent path to decide how many "../" we need to get back to Root.
        var parentPath = davFile.Parent?.Path ?? System.IO.Path.GetDirectoryName(davFile.Path)?.Replace('\\', '/') ?? "";
        // Split by '/' since internal paths are stored with forward slashes
        var segments = parentPath.Trim('/').Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        var depth = segments.Length;
        
        var sb = new StringBuilder();

        // 2. Build "Go Up" string using FORWARD SLASH
        // Rclone treats '\' in symlinks as a special filename char and corrupts it to .
        // Forward slash is safe and standard for WebDAV.
        for (int i = 0; i < depth; i++)
        {
            sb.Append("../");
        }

        // 3. Append the target path (.ids/...) using FORWARD SLASH
        sb.Append(DavItem.IdsFolder.Name); // .ids
        
        foreach (var c in davFile.IdPrefix)
        {
            sb.Append('/'); // Forward slash
            sb.Append(c);
        }
        
        sb.Append('/'); // Forward slash
        sb.Append(davFile.Id);

        return sb.ToString();
    }

    // Unused but required for compilation compatibility
    public static string GetTargetPath(DavItem davFile, string mountDir) => ""; 

    // ==============================================================================
    // HELPER METHODS (Kept to prevent build errors in other files)
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
