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
                var target = GetTargetPath();
                // We log this as an ERROR so it shows up regardless of log level settings
                Log.Error("Generated Symlink Target: {Target}", target);
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

    private string GetTargetPath()
    {
        // BYPASS: Ignore the potentially corrupted Config/Env variable entirely.
        // We hardcode the clean Windows path here.
        const string ForceCleanMountDir = @"C:\nzbdav\mount";
        
        return GetTargetPath(davFile, ForceCleanMountDir);
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        var idSegments = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Append(davFile.Id.ToString());

        // Since we are forcing a hardcoded path, we know it is Windows style
        // and doesn't need complex normalization.
        return JoinWithSeparator(mountDir, '\\', idSegments);
    }

    private static string JoinWithSeparator(string mountDir, char separator, IEnumerable<string> idSegments)
    {
        var builder = new StringBuilder(mountDir.TrimEnd('\\', '/'));
        foreach (var segment in idSegments)
        {
            builder.Append(separator);
            builder.Append(segment);
        }
        return builder.ToString();
    }
}
