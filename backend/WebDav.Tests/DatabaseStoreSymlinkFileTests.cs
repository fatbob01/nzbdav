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

    [Fact]
    public void GetTargetPath_NormalizesEscapedNewlinesInWindowsMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000444");
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

        var mountDirWithEscapes = "C:\n zbdav\\mount".Replace(" ", string.Empty);

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDirWithEscapes);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000444", targetPath);
    }

    [Fact]
    public void GetTargetPath_ReplacesPrivateUseGlyphsInWindowsMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000555");
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

        var mangledMountDir = "Cnzbdavmount";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mangledMountDir);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000555", targetPath);
    }

    [Fact]
    public void GetTargetPath_ReplacesAlternatePrivateUseGlyphsInWindowsMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000666");
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

        var mangledMountDir = "C\ue03a\ue05cnzbdav\ue05cmount";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mangledMountDir);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000666", targetPath);
    }

    [Fact]
    public void GetTargetPath_NormalizesExtendedPrivateUseGlyphsInWindowsMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000777");
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

        var mangledMountDir = "C\ue13a\ue15cnzbdav\ue12fmount";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mangledMountDir);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000777", targetPath);
    }

    [Fact]
    public void GetTargetPath_NormalizesFontOffsetPrivateUseGlyphsInWindowsMountDir()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000888");
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

        var mangledMountDir = "C\uf13a\uf07cnzbdav\uf06fmount";

        var targetPath = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mangledMountDir);

        Assert.Equal(@"C:\\nzbdav\\mount\\.ids\\0\\0\\0\\0\\0\\00000000-0000-0000-0000-000000000888", targetPath);
    }
}
