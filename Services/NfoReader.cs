using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Services;

/// <summary>
/// Service to parse NFO files into Jellyfin BaseItem metadata.
/// </summary>
public class NfoReader
{
    private readonly ILogger<NfoReader> _logger;

    public NfoReader(ILogger<NfoReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads metadata from an NFO file and populates the given item and MetadataResult.
    /// </summary>
    public bool ReadNfo<T>(string nfoPath, MetadataResult<T> result, bool lockMetadata) where T : BaseItem
    {
        if (string.IsNullOrWhiteSpace(nfoPath) || !File.Exists(nfoPath))
        {
            return false;
        }

        try
        {
            var xmlText = File.ReadAllText(nfoPath);
            if (string.IsNullOrWhiteSpace(xmlText))
            {
                return false;
            }

            // Find start of XML element if there is leading text/garbage
            var xmlStart = xmlText.IndexOf('<');
            if (xmlStart > 0)
            {
                xmlText = xmlText.Substring(xmlStart);
            }

            var doc = XDocument.Parse(xmlText);
            var root = doc.Root;
            if (root == null)
            {
                return false;
            }

            var item = result.Item;
            var people = new List<PersonInfo>();

            // Title
            var title = GetString(root, "title");
            if (!string.IsNullOrWhiteSpace(title))
            {
                item.Name = title;
            }

            // Original Title
            var originalTitle = GetString(root, "originaltitle");
            if (!string.IsNullOrWhiteSpace(originalTitle))
            {
                item.OriginalTitle = originalTitle;
            }

            // Sort Title
            var sortTitle = GetString(root, "sorttitle");
            if (!string.IsNullOrWhiteSpace(sortTitle))
            {
                item.ForcedSortName = sortTitle;
            }

            // Plot / Overview
            var plot = GetString(root, "plot") ?? GetString(root, "outline");
            if (!string.IsNullOrWhiteSpace(plot))
            {
                item.Overview = plot;
            }

            // Tagline
            var tagline = GetString(root, "tagline");
            if (!string.IsNullOrWhiteSpace(tagline))
            {
                item.Tagline = tagline;
            }

            // Year & Premiere Date
            var yearStr = GetString(root, "year");
            if (int.TryParse(yearStr, out var year))
            {
                item.ProductionYear = year;
            }

            var premiered = GetString(root, "premiered")
                ?? GetString(root, "releasedate")
                ?? GetString(root, "aired")
                ?? GetString(root, "release");

            if (!string.IsNullOrWhiteSpace(premiered))
            {
                if (DateTime.TryParse(premiered, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    item.PremiereDate = date;
                    if (!item.ProductionYear.HasValue)
                    {
                        item.ProductionYear = date.Year;
                    }
                }
            }

            // Runtime (in minutes or ticks)
            var runtimeStr = GetString(root, "runtime");
            if (int.TryParse(runtimeStr, out var runtimeMinutes) && runtimeMinutes > 0)
            {
                item.RunTimeTicks = TimeSpan.FromMinutes(runtimeMinutes).Ticks;
            }

            // Ratings
            var ratingStr = GetString(root, "rating");
            if (float.TryParse(ratingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rating))
            {
                item.CommunityRating = rating;
            }

            var criticRatingStr = GetString(root, "criticrating");
            if (float.TryParse(criticRatingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var criticRating))
            {
                item.CriticRating = criticRating;
            }

            // MPAA / Official Rating
            var mpaa = GetString(root, "mpaa");
            if (!string.IsNullOrWhiteSpace(mpaa))
            {
                item.OfficialRating = mpaa;
            }

            var customRating = GetString(root, "customrating");
            if (!string.IsNullOrWhiteSpace(customRating))
            {
                item.CustomRating = customRating;
            }

            // Genres
            var genres = GetStrings(root, "genre");
            if (genres.Count > 0)
            {
                item.Genres = genres.ToArray();
            }

            // Tags
            var tags = GetStrings(root, "tag");
            if (tags.Count > 0)
            {
                item.Tags = tags.ToArray();
            }

            // Studios
            var studios = GetStrings(root, "studio");
            if (studios.Count > 0)
            {
                item.Studios = studios.ToArray();
            }

            // Production Locations / Countries
            var countries = GetStrings(root, "country");
            if (countries.Count > 0)
            {
                item.ProductionLocations = countries.ToArray();
            }

            // Episode / Season numbers
            var seasonStr = GetString(root, "season");
            if (int.TryParse(seasonStr, out var seasonNum))
            {
                item.ParentIndexNumber = seasonNum;
            }

            var episodeStr = GetString(root, "episode");
            if (int.TryParse(episodeStr, out var episodeNum))
            {
                item.IndexNumber = episodeNum;
            }

            // Provider IDs (e.g. IMDb, TMDb, TVDb, UniqueID)
            ParseProviderIds(root, item);

            // Cast / Crew
            ParsePeople(root, people);
            if (people.Count > 0)
            {
                result.People = people;
            }

            // Apply Lock to prevent subsequent providers from overwriting
            if (lockMetadata)
            {
                item.IsLocked = true;
                item.LockedFields = new[]
                {
                    MetadataField.Name,
                    MetadataField.Overview,
                    MetadataField.Genres,
                    MetadataField.Studios,
                    MetadataField.Tags,
                    MetadataField.OfficialRating,
                    MetadataField.Runtime,
                    MetadataField.ProductionLocations,
                    MetadataField.Cast
                };
            }

            result.HasMetadata = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JellyMeta Local: Failed to parse NFO file at {Path}", nfoPath);
            return false;
        }
    }

    private static void ParseProviderIds(XElement root, BaseItem item)
    {
        // Check <uniqueid type="..." default="true">...</uniqueid>
        foreach (var el in root.Elements("uniqueid"))
        {
            var type = el.Attribute("type")?.Value;
            var val = el.Value.Trim();
            if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(val))
            {
                item.SetProviderId(type, val);
            }
        }

        // Check explicit ID tags
        var imdb = GetString(root, "imdbid") ?? GetString(root, "id");
        if (!string.IsNullOrWhiteSpace(imdb) && imdb.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            item.SetProviderId(MetadataProvider.Imdb, imdb);
        }

        var tmdb = GetString(root, "tmdbid");
        if (!string.IsNullOrWhiteSpace(tmdb))
        {
            item.SetProviderId(MetadataProvider.Tmdb, tmdb);
        }

        var tvdb = GetString(root, "tvdbid");
        if (!string.IsNullOrWhiteSpace(tvdb))
        {
            item.SetProviderId(MetadataProvider.Tvdb, tvdb);
        }
    }

    private static void ParsePeople(XElement root, List<PersonInfo> people)
    {
        // Actors
        foreach (var actorEl in root.Elements("actor"))
        {
            var name = actorEl.Element("name")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var role = actorEl.Element("role")?.Value.Trim();
            var thumb = actorEl.Element("thumb")?.Value.Trim();

            people.Add(new PersonInfo
            {
                Name = name,
                Role = role ?? string.Empty,
                Type = PersonKind.Actor,
                ImageUrl = thumb
            });
        }

        // Directors
        foreach (var directorEl in root.Elements("director"))
        {
            var name = directorEl.Value.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                people.Add(new PersonInfo
                {
                    Name = name,
                    Type = PersonKind.Director
                });
            }
        }

        // Writers / Credits
        foreach (var writerEl in root.Elements("credits"))
        {
            var name = writerEl.Value.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                people.Add(new PersonInfo
                {
                    Name = name,
                    Type = PersonKind.Writer
                });
            }
        }

        foreach (var writerEl in root.Elements("writer"))
        {
            var name = writerEl.Value.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                people.Add(new PersonInfo
                {
                    Name = name,
                    Type = PersonKind.Writer
                });
            }
        }
    }

    private static string? GetString(XElement root, string elementName)
    {
        var el = root.Element(elementName);
        var val = el?.Value.Trim();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private static List<string> GetStrings(XElement root, string elementName)
    {
        var list = new List<string>();
        foreach (var el in root.Elements(elementName))
        {
            var val = el.Value.Trim();
            if (!string.IsNullOrWhiteSpace(val))
            {
                list.Add(val);
            }
        }
        return list;
    }
}
