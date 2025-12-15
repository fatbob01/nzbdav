using System.Text;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, ConfigManager configManager) : BaseStoreReadonlyItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;
    public override DateTime CreatedAt => davFile.CreatedAt;
    private byte[] ContentBytes => Encoding.UTF8.GetBytes(GetTargetPath());

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    private string GetTargetPath()
    {
        return GetTargetPath(davFile, configManager.GetRcloneMountDir());
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        // Strip drive letter and ensure leading slash for drive-relative path
        var normalizedMount = RemoveDriveLetter(mountDir?.Replace('\\', '/') ?? "").TrimStart('/');
        
        // Build path: /nzbdav/mount/.ids/7/d/9/e/b/guid
        var pathParts = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Prepend(normalizedMount)
            .Append(davFile.Id.ToString())
            .ToArray();
        
        // Ensure it starts with / for drive-relative absolute path
        return "/" + string.Join('/', pathParts);
    }
    
    private static string RemoveDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        
        // Remove C: or C:/ or C:\ from the start
        if (path.Length >= 2 && path[1] == ':')
        {
            return path.Substring(2).TrimStart('/', '\\');
        }
        
        return path;
    }
}
