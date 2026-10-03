using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyMetaLocal.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Providers;

/// <summary>
/// Abstract base provider for JellyMeta Local metadata providers.
/// </summary>
public abstract class BaseLocalMetadataProvider<TItem, TLookup> :
    IRemoteMetadataProvider<TItem, TLookup>,
    IHasOrder
    where TItem : BaseItem, IHasLookupInfo<TLookup>, new()
    where TLookup : ItemLookupInfo, new()
{
    protected readonly LocalMetadataMatcher Matcher;
    protected readonly NfoReader NfoReader;
    protected readonly ILogger Logger;
    protected readonly IHttpClientFactory HttpClientFactory;

    protected BaseLocalMetadataProvider(
        LocalMetadataMatcher matcher,
        NfoReader nfoReader,
        ILogger logger,
        IHttpClientFactory httpClientFactory)
    {
        Matcher = matcher;
        NfoReader = nfoReader;
        Logger = logger;
        HttpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string Name => "JellyMeta Local";

    /// <inheritdoc />
    public int Order => -100; // Prioritize local metadata ahead of online scrapers

    /// <inheritdoc />
    public Task<MetadataResult<TItem>> GetMetadata(TLookup info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<TItem>
        {
            Item = new TItem(),
            HasMetadata = false
        };

        var match = Matcher.FindMatch(info.Path, info.Name);
        if (match == null)
        {
            // Yield to other metadata plugins
            return Task.FromResult(result);
        }

        var config = Plugin.Instance?.Configuration;
        bool lockMetadata = config?.LockMetadataIfFound ?? true;

        bool parsed = false;
        if (!string.IsNullOrWhiteSpace(match.NfoPath))
        {
            parsed = NfoReader.ReadNfo(match.NfoPath, result, lockMetadata);
        }

        if (!parsed)
        {
            // If NFO is missing, still import folder name and mark as matched
            result.Item.Name = match.FolderName;
            result.HasMetadata = true;

            if (lockMetadata)
            {
                result.Item.LockedFields = new[] { MetadataField.Name };
            }
        }

        result.Item.SetProviderId("JellyMetaLocal", match.FolderName);

        // Attach local artwork
        ArtworkHelper.PopulateImages(result, match);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TLookup searchInfo, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();
        var match = Matcher.FindMatch(searchInfo.Path, searchInfo.Name);
        if (match != null)
        {
            var searchResult = new RemoteSearchResult
            {
                Name = match.FolderName,
                SearchProviderName = Name,
                ImageUrl = match.PosterPath
            };
            searchResult.SetProviderId("JellyMetaLocal", match.FolderName);
            results.Add(searchResult);
        }

        return Task.FromResult<IEnumerable<RemoteSearchResult>>(results);
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

        var client = HttpClientFactory.CreateClient();
        return await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }
}
