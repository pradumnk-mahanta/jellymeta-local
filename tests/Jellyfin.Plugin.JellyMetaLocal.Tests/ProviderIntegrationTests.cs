using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyMetaLocal.Configuration;
using Jellyfin.Plugin.JellyMetaLocal.Providers;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.JellyMetaLocal.Tests;

public class ProviderIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _metadataDir;
    private readonly string _mediaDir;

    public ProviderIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ProviderTest_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task MovieMetadataProvider_ImportsMetadata_WhenMatchFound()
    {
        // 1. Setup metadata folder
        var folder1 = Path.Combine(_metadataDir, "FOLDER-1");
        Directory.CreateDirectory(folder1);
        File.WriteAllText(Path.Combine(folder1, "FOLDER-1.nfo"), """
        <movie>
            <title>My Matched Local Movie</title>
            <plot>A fascinating local movie plot.</plot>
            <year>2009</year>
            <genre>Drama</genre>
        </movie>
        """);
        File.WriteAllText(Path.Combine(folder1, "poster.png"), "dummy-poster");
        File.WriteAllText(Path.Combine(folder1, "banner.png"), "dummy-banner");

        // 2. Setup mock plugin
        var appPaths = new MockApplicationPaths();
        var serializer = new MockXmlSerializer();
        var plugin = new Plugin(appPaths, serializer);
        plugin.UpdateConfiguration(new PluginConfiguration
        {
            MetadataPath = _metadataDir,
            MatchBothWays = true,
            StripDatePrefix = true,
            LockMetadataIfFound = true
        });

        var matcher = new LocalMetadataMatcher(NullLogger<LocalMetadataMatcher>.Instance);
        var nfoReader = new NfoReader(NullLogger<NfoReader>.Instance);
        var httpClientFactory = new MockHttpClientFactory();

        var provider = new MovieMetadataProvider(
            matcher,
            nfoReader,
            NullLogger<MovieMetadataProvider>.Instance,
            httpClientFactory);

        // 3. Media info pointing to /media/Type/Site/2009-11-30 - FOLDER-1/FILE.mp4
        var mediaFolder = Path.Combine(_mediaDir, "Type", "Site", "2009-11-30 - FOLDER-1");
        Directory.CreateDirectory(mediaFolder);
        var mediaFile = Path.Combine(mediaFolder, "FILE.mp4");
        File.WriteAllText(mediaFile, "media-bytes");

        var movieInfo = new MovieInfo
        {
            Path = mediaFile
        };

        var result = await provider.GetMetadata(movieInfo, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Equal("My Matched Local Movie", result.Item.Name);
        Assert.Equal("A fascinating local movie plot.", result.Item.Overview);
        Assert.Equal(2009, result.Item.ProductionYear);
        Assert.Contains("Drama", result.Item.Genres);

        // Overwrite protection: item must NOT be locked so remote providers (TMDB/TVDB) can execute
        Assert.False(result.Item.IsLocked);
        Assert.Contains(MetadataField.Name, result.Item.LockedFields);
        Assert.Contains(MetadataField.Overview, result.Item.LockedFields);
        Assert.Contains(MetadataField.Genres, result.Item.LockedFields);
        Assert.DoesNotContain(MetadataField.Cast, result.Item.LockedFields);

        // Artwork
        Assert.NotEmpty(result.Images);
        Assert.Contains(result.Images, img => img.Type == ImageType.Primary);
        Assert.Contains(result.Images, img => img.Type == ImageType.Banner);
    }

    [Fact]
    public async Task MovieMetadataProvider_PassesThrough_WhenNoMatchFound()
    {
        var appPaths = new MockApplicationPaths();
        var serializer = new MockXmlSerializer();
        var plugin = new Plugin(appPaths, serializer);
        plugin.UpdateConfiguration(new PluginConfiguration
        {
            MetadataPath = _metadataDir
        });

        var matcher = new LocalMetadataMatcher(NullLogger<LocalMetadataMatcher>.Instance);
        var nfoReader = new NfoReader(NullLogger<NfoReader>.Instance);
        var httpClientFactory = new MockHttpClientFactory();

        var provider = new MovieMetadataProvider(
            matcher,
            nfoReader,
            NullLogger<MovieMetadataProvider>.Instance,
            httpClientFactory);

        var movieInfo = new MovieInfo
        {
            Path = Path.Combine(_mediaDir, "Type", "Site", "NonExistentFolder", "FILE.mp4")
        };

        var result = await provider.GetMetadata(movieInfo, CancellationToken.None);

        // Crucial requirement: HasMetadata must be false so other plugins can run!
        Assert.False(result.HasMetadata);
    }
}

public class MockHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient();
}

