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

        Assert.Equal("/nzbdav/content/movies/Inception.mkv", target);
        Assert.Equal(target, Path.GetFullPath(target, "/srv/media/movies"));
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
}
