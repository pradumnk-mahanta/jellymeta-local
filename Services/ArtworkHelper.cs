using System.Collections.Generic;
using System.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.JellyMetaLocal.Services;

/// <summary>
/// Helper class for resolving and populating local artwork.
/// </summary>
public static class ArtworkHelper
{
    /// <summary>
    /// Populates LocalImageInfo into the MetadataResult Images list.
    /// </summary>
    public static void PopulateImages<T>(MetadataResult<T> result, LocalFolderMatch match) where T : BaseItem
    {
        var images = GetLocalImages(match);
        foreach (var img in images)
        {
            result.Images.Add(img);
        }
    }

    /// <summary>
    /// Gets a list of LocalImageInfo from the matched local metadata directory.
    /// </summary>
    public static List<LocalImageInfo> GetLocalImages(LocalFolderMatch match)
    {
        var list = new List<LocalImageInfo>();

        if (!string.IsNullOrWhiteSpace(match.PosterPath) && File.Exists(match.PosterPath))
        {
            list.Add(CreateLocalImageInfo(match.PosterPath, ImageType.Primary));
        }

        if (!string.IsNullOrWhiteSpace(match.BannerPath) && File.Exists(match.BannerPath))
        {
            list.Add(CreateLocalImageInfo(match.BannerPath, ImageType.Banner));
        }

        if (!string.IsNullOrWhiteSpace(match.BackdropPath) && File.Exists(match.BackdropPath))
        {
            list.Add(CreateLocalImageInfo(match.BackdropPath, ImageType.Backdrop));
        }

        if (!string.IsNullOrWhiteSpace(match.ThumbPath) && File.Exists(match.ThumbPath))
        {
            list.Add(CreateLocalImageInfo(match.ThumbPath, ImageType.Thumb));
        }

        if (!string.IsNullOrWhiteSpace(match.LogoPath) && File.Exists(match.LogoPath))
        {
            list.Add(CreateLocalImageInfo(match.LogoPath, ImageType.Logo));
        }

        return list;
    }

    /// <summary>
    /// Gets remote image info representations of the local images.
    /// </summary>
    public static List<RemoteImageInfo> GetRemoteImages(LocalFolderMatch match, string providerName)
    {
        var list = new List<RemoteImageInfo>();

        void AddIfValid(string? path, ImageType type)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = providerName,
                    Url = path, // Jellyfin image fetchers accept local file paths
                    Type = type
                });
            }
        }

        AddIfValid(match.PosterPath, ImageType.Primary);
        AddIfValid(match.BannerPath, ImageType.Banner);
        AddIfValid(match.BackdropPath, ImageType.Backdrop);
        AddIfValid(match.ThumbPath, ImageType.Thumb);
        AddIfValid(match.LogoPath, ImageType.Logo);

        return list;
    }

    private static LocalImageInfo CreateLocalImageInfo(string path, ImageType type)
    {
        var fileInfo = new FileInfo(path);
        return new LocalImageInfo
        {
            FileInfo = new FileSystemMetadata
            {
                FullName = path,
                Exists = true,
                Length = fileInfo.Exists ? fileInfo.Length : 0,
                IsDirectory = false
            },
            Type = type
        };
    }
}

