using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellyMetaLocal.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyMetaLocal.Services;

/// <summary>
/// Information about a matched local metadata directory.
/// </summary>
public class LocalFolderMatch
{
    public required string FolderPath { get; set; }
    public required string FolderName { get; set; }
    public string? NfoPath { get; set; }
    public string? PosterPath { get; set; }
    public string? BannerPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? ThumbPath { get; set; }
    public string? LogoPath { get; set; }
}

/// <summary>
/// Service that scans and matches media paths with local metadata directories.
/// </summary>
public class LocalMetadataMatcher
{
    private readonly ILogger<LocalMetadataMatcher> _logger;
    private static readonly Regex DatePrefixRegex = new(
        @"^(?:\[|\()?(\d{4}[-._]\d{2}[-._]\d{2})(?:\]|\))?\s*[-_.]?\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DateSuffixRegex = new(
        @"^(.*?)\s*[-_.]?\s*(?:\[|\()?(\d{4}[-._]\d{2}[-._]\d{2})(?:\]|\))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex YearSuffixRegex = new(
        @"^(.*?)\s*[\(\[](\d{4})[\)\]]$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DelimiterRegex = new(
        @"[\s\-_\.]+",
        RegexOptions.Compiled);

    // Cache metadata folder paths to prevent hammering the disk during library scans
    private string _cachedMetadataPath = string.Empty;
    private DateTime _cacheExpiration = DateTime.MinValue;
    private readonly List<string> _cachedDirectories = new();
    private readonly object _cacheLock = new();

    public LocalMetadataMatcher(ILogger<LocalMetadataMatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Strips date prefixes, suffixes, and year tags from a folder or file name.
    /// </summary>
    public static string StripDates(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string result = input.Trim();

        // Check date prefix (e.g. "2009-11-30 - FOLDER-1" -> "FOLDER-1")
        var matchPrefix = DatePrefixRegex.Match(result);
        if (matchPrefix.Success && matchPrefix.Groups.Count > 2)
        {
            var remainder = matchPrefix.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(remainder))
            {
                result = remainder;
            }
        }

        // Check date suffix (e.g. "FOLDER-1 - 2009-11-30" -> "FOLDER-1")
        var matchSuffix = DateSuffixRegex.Match(result);
        if (matchSuffix.Success && matchSuffix.Groups.Count > 1)
        {
            var remainder = matchSuffix.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(remainder))
            {
                result = remainder;
            }
        }

        // Check year suffix (e.g. "FOLDER-1 (2009)" -> "FOLDER-1")
        var matchYear = YearSuffixRegex.Match(result);
        if (matchYear.Success && matchYear.Groups.Count > 1)
        {
            var remainder = matchYear.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(remainder))
            {
                result = remainder;
            }
        }

        return result;
    }

    /// <summary>
    /// Normalizes a name for comparison by removing common punctuation and whitespace.
    /// </summary>
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return DelimiterRegex.Replace(name, "").ToLowerInvariant();
    }

    /// <summary>
    /// Finds a matching local metadata folder for a given media path and item name.
    /// </summary>
    public LocalFolderMatch? FindMatch(string? mediaPath, string? itemName = null)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || string.IsNullOrWhiteSpace(config.MetadataPath))
        {
            _logger.LogDebug("JellyMeta Local: Metadata path is not configured.");
            return null;
        }

        if (!Directory.Exists(config.MetadataPath))
        {
            _logger.LogWarning("JellyMeta Local: Configured metadata path does not exist: {Path}", config.MetadataPath);
            return null;
        }

        var candidateNames = ExtractCandidates(mediaPath, itemName, config.StripDatePrefix);
        if (candidateNames.Count == 0)
        {
            return null;
        }

        var directories = GetMetadataDirectories(config.MetadataPath, config.SearchSubdirectories);
        if (directories.Count == 0)
        {
            _logger.LogDebug("JellyMeta Local: No subdirectories found in metadata path {Path}", config.MetadataPath);
            return null;
        }

        string? matchedDir = EvaluateMatch(directories, candidateNames, config);
        if (matchedDir == null)
        {
            _logger.LogDebug("JellyMeta Local: No match found for candidates: {Candidates}", string.Join(", ", candidateNames));
            return null;
        }

        _logger.LogInformation("JellyMeta Local: Successfully matched media '{MediaPath}' to metadata directory '{Dir}'",
            mediaPath, matchedDir);

        return BuildFolderMatch(matchedDir);
    }

    /// <summary>
    /// Extracts candidate names from the media path, prioritizing the last folder name.
    /// </summary>
    public static List<string> ExtractCandidates(string? mediaPath, string? itemName, bool stripDates)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(mediaPath))
        {
            var cleanedPath = mediaPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Determine directory and file component
            string dirPath;
            string? fileNameWithoutExt = null;

            if (Directory.Exists(cleanedPath))
            {
                dirPath = cleanedPath;
            }
            else
            {
                dirPath = Path.GetDirectoryName(cleanedPath) ?? string.Empty;
                fileNameWithoutExt = Path.GetFileNameWithoutExtension(cleanedPath);
            }

            // 1. Immediate last folder name (e.g. "2009-11-30 - FOLDER-1")
            if (!string.IsNullOrWhiteSpace(dirPath))
            {
                var lastFolder = Path.GetFileName(dirPath);
                if (!string.IsNullOrWhiteSpace(lastFolder))
                {
                    candidates.Add(lastFolder);
                    if (stripDates)
                    {
                        var stripped = StripDates(lastFolder);
                        if (!string.Equals(stripped, lastFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(stripped);
                        }
                    }
                }

                // 2. Parent directory (multi-level support, e.g. "Site" or category folder)
                var parentDir = Path.GetDirectoryName(dirPath);
                if (!string.IsNullOrWhiteSpace(parentDir))
                {
                    var parentFolder = Path.GetFileName(parentDir);
                    if (!string.IsNullOrWhiteSpace(parentFolder))
                    {
                        candidates.Add(parentFolder);
                        if (stripDates)
                        {
                            var stripped = StripDates(parentFolder);
                            if (!string.Equals(stripped, parentFolder, StringComparison.OrdinalIgnoreCase))
                            {
                                candidates.Add(stripped);
                            }
                        }
                    }
                }
            }

            // 3. File name without extension
            if (!string.IsNullOrWhiteSpace(fileNameWithoutExt))
            {
                candidates.Add(fileNameWithoutExt);
                if (stripDates)
                {
                    var stripped = StripDates(fileNameWithoutExt);
                    if (!string.Equals(stripped, fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(stripped);
                    }
                }
            }
        }

        // 4. Item Name (if provided and not already in candidates)
        if (!string.IsNullOrWhiteSpace(itemName))
        {
            candidates.Add(itemName);
            if (stripDates)
            {
                var stripped = StripDates(itemName);
                if (!string.Equals(stripped, itemName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(stripped);
                }
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Evaluates candidates against metadata directories using exact, bidirectional, and normalized rules.
    /// </summary>
    private static string? EvaluateMatch(List<string> directories, List<string> candidates, PluginConfiguration config)
    {
        // Cache directory names and cleaned names
        var dirInfoList = directories.Select(d =>
        {
            var name = Path.GetFileName(d);
            var cleanName = config.StripDatePrefix ? StripDates(name) : name;
            var normName = config.NormalizeSeparators ? NormalizeName(cleanName) : cleanName.ToLowerInvariant();
            return new
            {
                FullPath = d,
                Name = name,
                CleanName = cleanName,
                NormName = normName
            };
        }).ToList();

        // Try exact match on clean names (Highest Priority)
        foreach (var candidate in candidates)
        {
            var candidateClean = config.StripDatePrefix ? StripDates(candidate) : candidate;

            var exactMatch = dirInfoList.FirstOrDefault(d =>
                string.Equals(d.CleanName, candidateClean, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, candidate, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                return exactMatch.FullPath;
            }
        }

        // Try bidirectional match if enabled
        if (config.MatchBothWays)
        {
            foreach (var candidate in candidates)
            {
                var candidateClean = config.StripDatePrefix ? StripDates(candidate) : candidate;

                // Candidate ends with directory name or directory ends with candidate
                var match = dirInfoList.FirstOrDefault(d =>
                    d.CleanName.Length > 2 && (
                        candidateClean.EndsWith(d.CleanName, StringComparison.OrdinalIgnoreCase) ||
                        d.CleanName.EndsWith(candidateClean, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    return match.FullPath;
                }

                // Candidate contains directory name or directory contains candidate
                match = dirInfoList.FirstOrDefault(d =>
                    d.CleanName.Length > 2 && (
                        candidateClean.Contains(d.CleanName, StringComparison.OrdinalIgnoreCase) ||
                        d.CleanName.Contains(candidateClean, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    return match.FullPath;
                }
            }
        }

        // Try normalized separator matching (removing -, _, ., spaces)
        if (config.NormalizeSeparators)
        {
            foreach (var candidate in candidates)
            {
                var normCandidate = NormalizeName(config.StripDatePrefix ? StripDates(candidate) : candidate);
                if (normCandidate.Length < 3)
                {
                    continue;
                }

                var match = dirInfoList.FirstOrDefault(d =>
                    d.NormName == normCandidate ||
                    (config.MatchBothWays && (normCandidate.Contains(d.NormName) || d.NormName.Contains(normCandidate))));

                if (match != null)
                {
                    return match.FullPath;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets subdirectories in the metadata path with a short-lived cache.
    /// </summary>
    private List<string> GetMetadataDirectories(string metadataPath, bool searchSubdirectories)
    {
        lock (_cacheLock)
        {
            if (_cachedMetadataPath == metadataPath &&
                DateTime.UtcNow < _cacheExpiration &&
                _cachedDirectories.Count > 0)
            {
                return _cachedDirectories;
            }

            _cachedDirectories.Clear();
            _cachedMetadataPath = metadataPath;

            try
            {
                var searchOption = searchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var dirs = Directory.GetDirectories(metadataPath, "*", searchOption);
                _cachedDirectories.AddRange(dirs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JellyMeta Local: Error enumerating directories in {Path}", metadataPath);
            }

            _cacheExpiration = DateTime.UtcNow.AddSeconds(60);
            return _cachedDirectories;
        }
    }

    /// <summary>
    /// Builds a LocalFolderMatch object by discovering NFO and image files in the matched directory.
    /// </summary>
    public static LocalFolderMatch BuildFolderMatch(string directoryPath)
    {
        var folderName = Path.GetFileName(directoryPath);
        var match = new LocalFolderMatch
        {
            FolderPath = directoryPath,
            FolderName = folderName
        };

        if (!Directory.Exists(directoryPath))
        {
            return match;
        }

        var files = Directory.GetFiles(directoryPath);

        // 1. Locate NFO
        // Priority: FolderName.nfo > movie.nfo/tvshow.nfo/series.nfo/episode.nfo > any *.nfo
        match.NfoPath = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), $"{folderName}.nfo", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(f =>
            {
                var name = Path.GetFileName(f).ToLowerInvariant();
                return name is "movie.nfo" or "tvshow.nfo" or "series.nfo" or "item.nfo" or "video.nfo";
            })
            ?? files.FirstOrDefault(f => f.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase));

        // 2. Locate Poster
        match.PosterPath = FindFile(files, "poster", "cover", "folder", "default");

        // 3. Locate Banner
        match.BannerPath = FindFile(files, "banner");

        // 4. Locate Backdrop / Fanart
        match.BackdropPath = FindFile(files, "backdrop", "fanart", "background", "art");

        // 5. Locate Thumb / Landscape
        match.ThumbPath = FindFile(files, "thumb", "landscape");

        // 6. Locate Logo / ClearArt
        match.LogoPath = FindFile(files, "logo", "clearart", "clearlogo");

        return match;
    }

    private static string? FindFile(string[] files, params string[] prefixes)
    {
        var extensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        foreach (var prefix in prefixes)
        {
            foreach (var ext in extensions)
            {
                var match = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), $"{prefix}{ext}", StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }
        }

        // Also check if any file starts with prefix and has valid extension
        foreach (var prefix in prefixes)
        {
            foreach (var ext in extensions)
            {
                var match = files.FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var fileExt = Path.GetExtension(f);
                    return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(fileExt, ext, StringComparison.OrdinalIgnoreCase);
                });
                if (match != null)
                {
                    return match;
                }
            }
        }

        return null;
    }
}

