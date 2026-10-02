using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.JellyMetaLocal.Tests;

public class NfoReaderTests : IDisposable
{
    private readonly string _tempFile;

    public NfoReaderTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N") + ".nfo");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void ReadNfo_ParsesXmlAndLocksFields_WhenLockEnabled()
    {
        var xml = """
        <?xml version="1.0" encoding="utf-8" standalone="yes"?>
        <movie>
          <title>Inception</title>
          <originaltitle>Inception Original</originaltitle>
          <sorttitle>Inception 01</sorttitle>
          <plot>A thief who steals corporate secrets through dream-sharing technology.</plot>
          <tagline>Your mind is the scene of the crime.</tagline>
          <year>2010</year>
          <premiered>2010-07-16</premiered>
          <runtime>148</runtime>
          <rating>8.8</rating>
          <criticrating>74</criticrating>
          <mpaa>PG-13</mpaa>
          <genre>Action</genre>
          <genre>Sci-Fi</genre>
          <studio>Warner Bros. Pictures</studio>
          <tag>Dream</tag>
          <country>United States</country>
          <actor>
            <name>Leonardo DiCaprio</name>
            <role>Cobb</role>
            <thumb>https://example.com/cobb.jpg</thumb>
          </actor>
          <director>Christopher Nolan</director>
          <uniqueid type="tmdb">27205</uniqueid>
        </movie>
        """;

        File.WriteAllText(_tempFile, xml);

        var reader = new NfoReader(NullLogger<NfoReader>.Instance);
        var result = new MetadataResult<Movie>
        {
            Item = new Movie()
        };

        var success = reader.ReadNfo(_tempFile, result, lockMetadata: true);

        Assert.True(success);
        Assert.True(result.HasMetadata);
        Assert.Equal("Inception", result.Item.Name);
        Assert.Equal("Inception Original", result.Item.OriginalTitle);
        Assert.Equal("Inception 01", result.Item.ForcedSortName);
        Assert.Equal("A thief who steals corporate secrets through dream-sharing technology.", result.Item.Overview);
        Assert.Equal("Your mind is the scene of the crime.", result.Item.Tagline);
        Assert.Equal(2010, result.Item.ProductionYear);
        Assert.Equal(new DateTime(2010, 7, 16), result.Item.PremiereDate);
        Assert.Equal(TimeSpan.FromMinutes(148).Ticks, result.Item.RunTimeTicks);
        Assert.Equal(8.8f, result.Item.CommunityRating);
        Assert.Equal(74f, result.Item.CriticRating);
        Assert.Equal("PG-13", result.Item.OfficialRating);
        Assert.Contains("Action", result.Item.Genres);
        Assert.Contains("Sci-Fi", result.Item.Genres);
        Assert.Contains("Warner Bros. Pictures", result.Item.Studios);
        Assert.Contains("Dream", result.Item.Tags);
        Assert.Contains("United States", result.Item.ProductionLocations);

        Assert.NotEmpty(result.People);
        var actor = result.People.FirstOrDefault(p => p.Name == "Leonardo DiCaprio");
        Assert.NotNull(actor);
        Assert.Equal("Cobb", actor.Role);
        Assert.Equal("https://example.com/cobb.jpg", actor.ImageUrl);

        // Verify overwrite lock:
        Assert.True(result.Item.IsLocked);
        Assert.NotNull(result.Item.LockedFields);
        Assert.Contains(MetadataField.Name, result.Item.LockedFields);
        Assert.Contains(MetadataField.Overview, result.Item.LockedFields);
        Assert.Contains(MetadataField.Genres, result.Item.LockedFields);
        Assert.Contains(MetadataField.Studios, result.Item.LockedFields);
        Assert.Contains(MetadataField.Tags, result.Item.LockedFields);
        Assert.Contains(MetadataField.OfficialRating, result.Item.LockedFields);
        Assert.Contains(MetadataField.Runtime, result.Item.LockedFields);
        Assert.Contains(MetadataField.Cast, result.Item.LockedFields);
    }

    [Fact]
    public void ReadNfo_DoesNotLock_WhenLockDisabled()
    {
        var xml = "<movie><title>Unlocked Movie</title></movie>";
        File.WriteAllText(_tempFile, xml);

        var reader = new NfoReader(NullLogger<NfoReader>.Instance);
        var result = new MetadataResult<Movie>
        {
            Item = new Movie()
        };

        var success = reader.ReadNfo(_tempFile, result, lockMetadata: false);

        Assert.True(success);
        Assert.Equal("Unlocked Movie", result.Item.Name);
        Assert.False(result.Item.IsLocked);
    }
}

