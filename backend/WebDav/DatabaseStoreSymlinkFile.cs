using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Text;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, string parentPath) : BaseStoreReadonlyItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;
    public override DateTime CreatedAt => davFile.CreatedAt;

    private string TargetPath => BuildRelativeTargetPath(parentPath, davFile);

    private static string BuildRelativeTargetPath(string parentPath, DavItem davFile)
    {
        var levelsUp = GetParentSegments(parentPath).Length + 1;
        var segments = Enumerable.Repeat("..", levelsUp)
            .Concat(GetContentSegments(davFile));

        return Path.Combine(segments.ToArray())
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string[] GetParentSegments(string parentPath)
    {
        return string.IsNullOrWhiteSpace(parentPath)
            ? Array.Empty<string>()
            : parentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] GetContentSegments(DavItem davFile)
    {
        return davFile.Path.TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        if (string.IsNullOrWhiteSpace(mountDir))
            throw new ArgumentException("The rclone mount directory must be configured.", nameof(mountDir));

        var sanitizedMount = mountDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var segments = new[] { sanitizedMount }
            .Concat(GetContentSegments(davFile));

        return Path.Combine(segments.ToArray());
    }

    private byte[] ContentBytes => Encoding.UTF8.GetBytes(TargetPath);

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }
}

