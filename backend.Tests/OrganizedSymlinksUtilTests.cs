using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Utils;
using Xunit;

public class OrganizedSymlinksUtilTests
{
    [Fact]
    public void GetDavItemSymlinks_MatchesMountDirWithoutDriveLetter()
    {
        var mountDir = "C:/nzbdav";
        var targetId = Guid.NewGuid();
        var configManager = new ConfigManager();
        configManager.UpdateValues(
            new List<ConfigItem>
            {
                new() { ConfigName = "rclone.mount-dir", ConfigValue = mountDir },
            }
        );

        var symlinkInfos = new[]
        {
            new SymlinkUtil.SymlinkInfo
            {
                SymlinkPath = "/library/movies/Inception.mkv",
                TargetPath = $"/nzbdav/.ids/{targetId}.rclonelink"
            }
        };

        var method = typeof(OrganizedSymlinksUtil)
            .GetMethod("GetDavItemSymlinks", BindingFlags.NonPublic | BindingFlags.Static)!;

        var results = (IEnumerable<OrganizedSymlinksUtil.DavItemSymlink>)method.Invoke(
            null,
            new object[] { symlinkInfos, configManager }
        );

        var symlink = Assert.Single(results);
        Assert.Equal(targetId, symlink.DavItemId);
        Assert.Equal("/library/movies/Inception.mkv", symlink.SymlinkPath);
    }
}
