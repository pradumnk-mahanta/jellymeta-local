using System.Net.Http;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Series metadata provider for JellyMeta Local.
/// </summary>
public class SeriesMetadataProvider : BaseLocalMetadataProvider<Series, SeriesInfo>
{
    public SeriesMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger<SeriesMetadataProvider> logger,
        IHttpClientFactory httpClientFactory)
        : base(matcher, nfoReader, logger, httpClientFactory)
    {
    }
}

