using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Services.FileAggregators;
using NzbWebDAV.Services.FileProcessors;
using Usenet.Nzb;
using Xunit;

public class RarAggregatorTests
{
    [Theory]
    [InlineData("folder\\\\a34dfb6c9d9fd7094d99f82a7c2c7b9d.mkv")] // Windows-style
    [InlineData("folder/a34dfb6c9d9fd7094d99f82a7c2c7b9d.mkv")] // Unix-style
    public void ProcessArchive_RenamesSingleObfuscatedFile(string pathWithinArchive)
    {
        var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH");
        if (string.IsNullOrEmpty(configPath))
        {
            configPath = Path.Combine(Path.GetTempPath(), "nzbwebdav-tests");
            Directory.CreateDirectory(configPath);
            Environment.SetEnvironmentVariable("CONFIG_PATH", configPath);
        }

        using var ctx = new DavDatabaseContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        var mountDirName = "mountDir";
        var mountDirectory = new DavItem
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ParentId = null,
            Name = mountDirName,
            Type = DavItem.ItemType.Directory
        };
        ctx.Items.Add(mountDirectory);
        ctx.SaveChanges();

        var dbClient = new DavDatabaseClient(ctx);
        var aggregator = new RarAggregator(dbClient, mountDirectory);

        var segmentType = typeof(NzbSegment);
        var msgProp = segmentType.GetProperty("MessageId");
        var msgInstance = Activator.CreateInstance(msgProp!.PropertyType, "id");
        var segment = Activator.CreateInstance(segmentType, 1, 0L, 100L, msgInstance);
        var segmentsArray = Array.CreateInstance(segmentType, 1);
        segmentsArray.SetValue(segment, 0);
        var groups = Activator.CreateInstance(
            Type.GetType("Usenet.Nntp.Models.NntpGroups, Usenet")!,
            "alt.binaries"
        );
        var nzbFile = Activator.CreateInstance(typeof(NzbFile),
            "poster",
            "subject",
            "file.mkv",
            DateTimeOffset.Now,
            groups!,
            segmentsArray
        ) as NzbFile;

        var result = new RarProcessor.Result
        {
            NzbFile = nzbFile!,
            PartSize = 100,
            ArchiveName = "archive",
            PartNumber = 0,
            StoredFileSegments = new[]
            {
                new RarProcessor.StoredFileSegment
                {
                    PathWithinArchive = pathWithinArchive,
                    Offset = 0,
                    ByteCount = 100
                }
            }
        };

        var method = typeof(RarAggregator).GetMethod("ProcessArchive", BindingFlags.Instance | BindingFlags.NonPublic);
        method!.Invoke(aggregator, new object[] { new List<RarProcessor.Result> { result } });
        ctx.SaveChanges();

        var rarFileItem = Assert.Single(ctx.Items, i => i.Type == DavItem.ItemType.RarFile);
        var expectedName = Path.GetFileNameWithoutExtension(mountDirName) + Path.GetExtension(pathWithinArchive);
        Assert.Equal(expectedName, rarFileItem.Name);
    }
}
