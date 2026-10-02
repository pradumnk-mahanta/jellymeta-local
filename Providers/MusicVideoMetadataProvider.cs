using System.Net.Http;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Music video metadata provider for JellyMeta Local.
/// </summary>
public class MusicVideoMetadataProvider : BaseLocalMetadataProvider<MusicVideo, MusicVideoInfo>
{
    public MusicVideoMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger<MusicVideoMetadataProvider> logger,
        IHttpClientFactory httpClientFactory)
        : base(matcher, nfoReader, logger, httpClientFactory)
    {
    }
}

