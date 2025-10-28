using System.Collections.Generic;
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

    private string TargetPath
    {
        get
        {
            var pathSegments = new List<string>();
            var levelsUp = parentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length + 1;
            pathSegments.AddRange(Enumerable.Repeat("..", levelsUp));
            pathSegments.Add(DavItem.ContentFolder.Name);
            if (!string.IsNullOrEmpty(parentPath))
            {
                pathSegments.AddRange(parentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
            }
            pathSegments.Add(davFile.Name);
            return Path.Combine(pathSegments.ToArray()).Replace('\', '/');
        }
    }

    private byte[] ContentBytes => Encoding.UTF8.GetBytes(TargetPath);

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }
}
