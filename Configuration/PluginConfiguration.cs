using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyMetaLocal.Configuration;

/// <summary>
/// Plugin configuration for JellyMeta Local.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the local root directory path containing metadata folders.
    /// E.g. /metadata or D:\metadata
    /// </summary>
    public string MetadataPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether bidirectional matching is enabled.
    /// Matches whether media folder contains/ends with metadata folder or vice versa.
    /// </summary>
    public bool MatchBothWays { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to strip date prefixes (e.g., 2009-11-30 - )
    /// and date suffixes before matching.
    /// </summary>
    public bool StripDatePrefix { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to lock imported metadata and fields,
    /// preventing downstream online providers from overwriting it.
    /// </summary>
    public bool LockMetadataIfFound { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to normalize delimiters (hyphens, underscores, dots, spaces)
    /// during folder matching.
    /// </summary>
    public bool NormalizeSeparators { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to scan subdirectories within the metadata root path.
    /// </summary>
    public bool SearchSubdirectories { get; set; } = true;
}

