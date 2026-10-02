using System;
using System.IO;
using Jellyfin.Plugin.JellyMetaLocal.Configuration;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.JellyMetaLocal.Tests;

public class LocalMetadataMatcherTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _metadataDir;
    private readonly string _mediaDir;

    public LocalMetadataMatcherTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "JellyMetaTest_" + Guid.NewGuid().ToString("N"));
        _metadataDir = Path.Combine(_testRoot, "metadata");
        _mediaDir = Path.Combine(_testRoot, "media");

        Directory.CreateDirectory(_metadataDir);
        Directory.CreateDirectory(_mediaDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try { Directory.Delete(_testRoot, true); } catch { }
        }
    }

    [Theory]
    [InlineData("2009-11-30 - FOLDER-1", "FOLDER-1")]
    [InlineData("2015.08.20.FOLDER-2", "FOLDER-2")]
    [InlineData("[2022-01-15] FOLDER-3", "FOLDER-3")]
    [InlineData("(2018-05-10) - FOLDER-4", "FOLDER-4")]
    [InlineData("FOLDER-5 - 2009-11-30", "FOLDER-5")]
    [InlineData("FOLDER-6 (2021)", "FOLDER-6")]
    [InlineData("FOLDER-7", "FOLDER-7")]
    public void StripDates_HandlesVariousFormats(string input, string expected)
    {
        var result = LocalMetadataMatcher.StripDates(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractCandidates_PrioritizesLastFolder()
    {
        var mediaPath = @"/media/Type/Site/2009-11-30 - FOLDER-1/FILE.mp4";
        var candidates = LocalMetadataMatcher.ExtractCandidates(mediaPath, null, true);

        Assert.NotEmpty(candidates);
        // Primary candidate must be the last folder
        Assert.Equal("2009-11-30 - FOLDER-1", candidates[0]);
        // Second candidate must be the date-stripped last folder
        Assert.Equal("FOLDER-1", candidates[1]);
        // Also contains parent directory "Site"
        Assert.Contains("Site", candidates);
    }

    [Fact]
    public void FindMatch_MatchesBidirectionalDateStrippedFolder()
    {
        // Setup metadata folder: /metadata/FOLDER-1/
        var metaFolder = Path.Combine(_metadataDir, "FOLDER-1");
        Directory.CreateDirectory(metaFolder);
        File.WriteAllText(Path.Combine(metaFolder, "FOLDER-1.nfo"), "<movie><title>Test Movie</title></movie>");
        File.WriteAllText(Path.Combine(metaFolder, "poster.png"), "dummy");
        File.WriteAllText(Path.Combine(metaFolder, "banner.png"), "dummy");

        // Setup mock plugin configuration
        var config = new PluginConfiguration
        {
            MetadataPath = _metadataDir,
            MatchBothWays = true,
            StripDatePrefix = true
        };

        var matcher = new LocalMetadataMatcher(NullLogger<LocalMetadataMatcher>.Instance);

        // Media path: /media/Type/Site/2009-11-30 - FOLDER-1/FILE.mp4
        var mediaSubdir = Path.Combine(_mediaDir, "Type", "Site", "2009-11-30 - FOLDER-1");
        Directory.CreateDirectory(mediaSubdir);
        var mediaFilePath = Path.Combine(mediaSubdir, "FILE.mp4");
        File.WriteAllText(mediaFilePath, "video");

        // Initialize plugin instance for test
        var appPaths = new MockApplicationPaths();
        var serializer = new MockXmlSerializer();
        var plugin = new Plugin(appPaths, serializer);
        plugin.UpdateConfiguration(config);

        var match = matcher.FindMatch(mediaFilePath);

        Assert.NotNull(match);
        Assert.Equal("FOLDER-1", match.FolderName);
        Assert.NotNull(match.NfoPath);
        Assert.True(File.Exists(match.NfoPath));
        Assert.NotNull(match.PosterPath);
        Assert.NotNull(match.BannerPath);
    }

    [Fact]
    public void FindMatch_ReverseMatch_WhenMetadataHasDateAndMediaDoesNot()
    {
        // Setup metadata folder with date: /metadata/2009-11-30 - FOLDER-A/
        var metaFolder = Path.Combine(_metadataDir, "2009-11-30 - FOLDER-A");
        Directory.CreateDirectory(metaFolder);
        File.WriteAllText(Path.Combine(metaFolder, "item.nfo"), "<movie><title>Movie A</title></movie>");

        var config = new PluginConfiguration
        {
            MetadataPath = _metadataDir,
            MatchBothWays = true,
            StripDatePrefix = true
        };

        var matcher = new LocalMetadataMatcher(NullLogger<LocalMetadataMatcher>.Instance);

        var appPaths = new MockApplicationPaths();
        var serializer = new MockXmlSerializer();
        var plugin = new Plugin(appPaths, serializer);
        plugin.UpdateConfiguration(config);

        // Media path with clean folder: /media/Movies/FOLDER-A/movie.mkv
        var mediaFile = Path.Combine(_mediaDir, "Movies", "FOLDER-A", "movie.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(mediaFile)!);
        File.WriteAllText(mediaFile, "dummy");

        var match = matcher.FindMatch(mediaFile);

        Assert.NotNull(match);
        Assert.Equal("2009-11-30 - FOLDER-A", match.FolderName);
    }

    [Fact]
    public void FindMatch_ReturnsNull_WhenNoMatchFound()
    {
        var metaFolder = Path.Combine(_metadataDir, "OTHER-FOLDER");
        Directory.CreateDirectory(metaFolder);

        var config = new PluginConfiguration
        {
            MetadataPath = _metadataDir,
            MatchBothWays = true,
            StripDatePrefix = true
        };

        var matcher = new LocalMetadataMatcher(NullLogger<LocalMetadataMatcher>.Instance);

        var appPaths = new MockApplicationPaths();
        var serializer = new MockXmlSerializer();
        var plugin = new Plugin(appPaths, serializer);
        plugin.UpdateConfiguration(config);

        var mediaFile = Path.Combine(_mediaDir, "Type", "Site", "2009-11-30 - FOLDER-1", "FILE.mp4");
        var match = matcher.FindMatch(mediaFile);

        // Crucial requirement: return null so downstream providers can run!
        Assert.Null(match);
    }
}

