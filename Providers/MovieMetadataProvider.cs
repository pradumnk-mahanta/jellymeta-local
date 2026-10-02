using System.Net.Http;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Movie metadata provider for JellyMeta Local.
/// </summary>
public class MovieMetadataProvider : BaseLocalMetadataProvider<Movie, MovieInfo>
{
    public MovieMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger<MovieMetadataProvider> logger,
        IHttpClientFactory httpClientFactory)
        : base(matcher, nfoReader, logger, httpClientFactory)
    {
    }
}

