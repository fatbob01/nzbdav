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
        // Use the full mount path including drive letter for Windows symlinks
        // (Windows NTFS symlinks handle colons fine, unlike rclone text links)
        var pathParts = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Prepend(NormalizeMountDir(mountDir))
            .Append(davFile.Id.ToString())
            .ToArray();
        
        return string.Join('/', pathParts);
    }
    
    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        return NormalizePathSeparators(mountDir).TrimEnd('/');
    }
    
    public static string NormalizePathSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.Replace('\\', '/');
    }
    
    public static string RemoveDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        
        if (path.Length >= 2 && path[1] == ':')
        {
            return path.Substring(2).TrimStart('/', '\\');
        }
        
        return path;
    }
    
    public static string EnsureLeadingSlash(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        return path.StartsWith('/') ? path : "/" + path;
    }
}
