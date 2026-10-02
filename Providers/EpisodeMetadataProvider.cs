using System.Net.Http;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Episode metadata provider for JellyMeta Local.
/// </summary>
public class EpisodeMetadataProvider : BaseLocalMetadataProvider<Episode, EpisodeInfo>
{
    public EpisodeMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger<EpisodeMetadataProvider> logger,
        IHttpClientFactory httpClientFactory)
        : base(matcher, nfoReader, logger, httpClientFactory)
    {
    }
}

