using System;
using System.IO;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav;
using Xunit;

public class DatabaseStoreSymlinkFileTests
{
    [Fact]
    public void GetTargetPath_UsesMountRootAndContentLayout()
    {
        var mountDir = "X:/nzbdav";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/completed-symlinks/movies/Inception.mkv",
            Name = "Inception.mkv",
            ParentId = DavItem.SymlinkFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("X:/nzbdav/content/movies/Inception.mkv", target);
    }

    [Fact]
    public void GetTargetPath_IncludesDriveLetterWhenMountIsDriveRoot()
    {
        var mountDir = "X:";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/completed-symlinks/movies/Inception.mkv",
            Name = "Inception.mkv",
            ParentId = DavItem.SymlinkFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("X:/content/movies/Inception.mkv", target);
    }

    [Fact]
    public void GetTargetPath_DoesNotDuplicateContentWhenMountAlreadyIncludesContent()
    {
        var mountDir = "C:/nzbdav/mount/content";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/completed-symlinks/movies/Inception.mkv",
            Name = "Inception.mkv",
            ParentId = DavItem.SymlinkFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("C:/nzbdav/mount/content/movies/Inception.mkv", target);
    }

    [Fact]
    public void GetTargetPath_NormalizesSymlinkMountToContent()
    {
        var mountDir = "C:/nzbdav/mount/completed-symlinks";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/completed-symlinks/movies/Inception.mkv",
            Name = "Inception.mkv",
            ParentId = DavItem.SymlinkFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("C:/nzbdav/mount/content/movies/Inception.mkv", target);
    }

    [Fact]
    public void GetTargetPath_NormalizesSymlinkMountBeforeDeterminingRoot()
    {
        var mountDir = "C:/nzbdav/mount/completed-symlinks/";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/content/series/Andor/S01E01.mkv",
            Name = "S01E01.mkv",
            ParentId = DavItem.ContentFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("C:/nzbdav/mount/content/series/Andor/S01E01.mkv", target);
    }

    [Fact]
    public void GetTargetPath_AnchorsSymlinkMountUnderContentRoot()
    {
        var mountDir = "C:/nzbdav/mount/completed-symlinks";
        var davItem = new DavItem
        {
            Id = Guid.NewGuid(),
            Path = "/content/movies/Inception.mkv",
            Name = "Inception.mkv",
            ParentId = DavItem.ContentFolder.Id,
            Type = DavItem.ItemType.NzbFile,
            IdPrefix = "abcde",
            CreatedAt = DateTime.UtcNow,
        };

        var target = DatabaseStoreSymlinkFile.GetTargetPath(davItem, mountDir);

        Assert.Equal("C:/nzbdav/mount/content/movies/Inception.mkv", target);
    }
}
