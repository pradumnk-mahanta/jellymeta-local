using System.Net.Http;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Video (generic / home videos / other) metadata provider for JellyMeta Local.
/// </summary>
public class VideoMetadataProvider : BaseLocalMetadataProvider<Video, ItemLookupInfo>
{
    public VideoMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger<VideoMetadataProvider> logger,
        IHttpClientFactory httpClientFactory)
        : base(matcher, nfoReader, logger, httpClientFactory)
    {
    }
}

