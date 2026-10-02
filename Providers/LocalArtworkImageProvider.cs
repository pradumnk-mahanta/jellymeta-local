using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Image provider that supplies posters, banners, backdrops, and logos from local metadata folders.
/// </summary>
public class LocalArtworkImageProvider : IRemoteImageProvider, ILocalImageProvider, IHasOrder
{
    private readonly LocalMetadataMatcher _matcher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LocalArtworkImageProvider> _logger;

    public LocalArtworkImageProvider(
        LocalMetadataMatcher matcher,
        IHttpClientFactory httpClientFactory,
        ILogger<LocalArtworkImageProvider> logger)
    {
        _matcher = matcher;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "JellyMeta Local";

    /// <inheritdoc />
    public int Order => -100;

    /// <inheritdoc />
    public bool Supports(BaseItem item)
    {
        return item is Movie or Series or Season or Episode or Video or MusicVideo;
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new[]
        {
            ImageType.Primary,
            ImageType.Banner,
            ImageType.Backdrop,
            ImageType.Thumb,
            ImageType.Logo
        };
    }

    /// <inheritdoc />
    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var match = _matcher.FindMatch(item.Path, item.Name);
        if (match == null)
        {
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(new List<RemoteImageInfo>());
        }

        var remoteImages = ArtworkHelper.GetRemoteImages(match, Name);
        return Task.FromResult<IEnumerable<RemoteImageInfo>>(remoteImages);
    }

    /// <inheritdoc />
    public IEnumerable<LocalImageInfo> GetImages(BaseItem item, IDirectoryService directoryService)
    {
        var match = _matcher.FindMatch(item.Path, item.Name);
        if (match == null)
        {
            return Enumerable.Empty<LocalImageInfo>();
        }

        return ArtworkHelper.GetLocalImages(match);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        if (File.Exists(url))
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(File.OpenRead(url))
            };
            var mime = MimeTypes.GetMimeType(url);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
            return response;
        }

        var client = _httpClientFactory.CreateClient();
        return await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }
}
