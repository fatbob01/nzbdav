using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav;
using Xunit;

namespace NzbWebDAV.WebDav.Tests;

public class DatabaseStoreSymlinkFileTests
{
    [Fact]
    public void GetTargetPath_PreservesWindowsDriveMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000111");
        var davItem = new DavItem
        {
            Id = id,
            IdPrefix = id.ToString()[..5],
            CreatedAt = new DateTime(2024, 1, 1),
            ParentId = Guid.Empty,
            Name = "test.nzb",
            FileSize = null,
            Type = DavItem.ItemType.NzbFile,
            Path = "/test.nzb",
        };

        var mountDir = @"C:\\nzbdav\\mount";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000111", targetPath);
    }

    [Fact]
    public void GetTargetPath_PreservesUncMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000222");
        var davItem = new DavItem
        {
            Id = id,
            IdPrefix = id.ToString()[..5],
            CreatedAt = new DateTime(2024, 1, 1),
            ParentId = Guid.Empty,
            Name = "test.nzb",
            FileSize = null,
            Type = DavItem.ItemType.NzbFile,
            Path = "/test.nzb",
        };

        var mountDir = @"\\\\server\\share\\nzbdav";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal(@"\\\\server\\share\\nzbdav\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000222", targetPath);
    }

    [Fact]
    public void GetTargetPath_PreservesNormalizedUncMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000333");
        var davItem = new DavItem
        {
            Id = id,
            IdPrefix = id.ToString()[..5],
            CreatedAt = new DateTime(2024, 1, 1),
            ParentId = Guid.Empty,
            Name = "test.nzb",
            FileSize = null,
            Type = DavItem.ItemType.NzbFile,
            Path = "/test.nzb",
        };

        var mountDir = "//server/share/nzbdav";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal(@"\\\\server\\share\\nzbdav\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000333", targetPath);
    }
}
