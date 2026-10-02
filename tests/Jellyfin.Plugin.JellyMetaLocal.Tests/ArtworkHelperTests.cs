using System;
using System.IO;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.JellyMetaLocal.Tests;

public class ArtworkHelperTests : IDisposable
{
    private readonly string _testDir;

    public ArtworkHelperTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ArtworkTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public void PopulateImages_FindsAllArtworkTypes()
    {
        var poster = Path.Combine(_testDir, "poster.png");
        var banner = Path.Combine(_testDir, "banner.png");
        var backdrop = Path.Combine(_testDir, "backdrop.jpg");
        var thumb = Path.Combine(_testDir, "thumb.webp");
        var logo = Path.Combine(_testDir, "logo.png");

        File.WriteAllText(poster, "dummy");
        File.WriteAllText(banner, "dummy");
        File.WriteAllText(backdrop, "dummy");
        File.WriteAllText(thumb, "dummy");
        File.WriteAllText(logo, "dummy");

        var match = new LocalFolderMatch
        {
            FolderPath = _testDir,
            FolderName = "TestFolder",
            PosterPath = poster,
            BannerPath = banner,
            BackdropPath = backdrop,
            ThumbPath = thumb,
            LogoPath = logo
        };

        var result = new MetadataResult<Movie>
        {
            Item = new Movie()
        };

        ArtworkHelper.PopulateImages(result, match);

        Assert.Equal(5, result.Images.Count);
        Assert.Contains(result.Images, img => img.Type == ImageType.Primary && img.FileInfo.FullName == poster);
        Assert.Contains(result.Images, img => img.Type == ImageType.Banner && img.FileInfo.FullName == banner);
        Assert.Contains(result.Images, img => img.Type == ImageType.Backdrop && img.FileInfo.FullName == backdrop);
        Assert.Contains(result.Images, img => img.Type == ImageType.Thumb && img.FileInfo.FullName == thumb);
        Assert.Contains(result.Images, img => img.Type == ImageType.Logo && img.FileInfo.FullName == logo);
    }
}

