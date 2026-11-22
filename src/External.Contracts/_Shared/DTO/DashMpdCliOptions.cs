namespace Reaparr.External.Contracts;

/// <summary>
/// Configuration options for dash-mpd-cli execution.
/// </summary>
public record DashMpdCliOptions
{
    /// <summary>
    ///  Gets or sets the URL of the MPD manifest to download.
    /// </summary>
    public required string MpdUrl { get; init; }

    /// <summary>
    /// Gets or sets the output file path where the downloaded media will be saved.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets or sets custom HTTP headers to include in requests.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the HTTP proxy to use.
    /// </summary>
    public string? Proxy { get; set; }

    /// <summary>
    /// Gets or sets whether to disable system proxy.
    /// </summary>
    public bool NoProxy { get; set; }

    /// <summary>
    /// Gets or sets the quality/resolution preference (e.g., "best", "worst", "1080p").
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// Gets or sets whether to suppress output (quiet mode).
    /// </summary>
    public bool Quiet { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable verbose output.
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the process.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets custom environment variables for the process.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Gets or sets additional command-line arguments to pass to dash-mpd-cli.
    /// </summary>
    public string? AdditionalArguments { get; set; }

    #region Bandwidth Control

    /// <summary>
    /// Gets or sets network bandwidth throttling (e.g., "500K", "2M").
    /// </summary>
    public string? LimitRate { get; set; }

    #endregion

    #region Authentication

    /// <summary>
    /// Gets or sets HTTP Basic authentication username.
    /// </summary>
    public string? AuthUsername { get; set; }

    /// <summary>
    /// Gets or sets HTTP Basic authentication password.
    /// </summary>
    public string? AuthPassword { get; set; }

    /// <summary>
    /// Gets or sets Bearer token for authentication.
    /// </summary>
    public string? AuthBearer { get; set; }

    #endregion

    #region Advanced Options

    /// <summary>
    /// Gets or sets whether to enable pseudo-live stream support.
    /// </summary>
    public bool EnableLiveStreams { get; set; }

    /// <summary>
    /// Gets or sets sleep duration in milliseconds between requests.
    /// </summary>
    public int? SleepRequests { get; set; }

    /// <summary>
    /// Gets or sets browser name to extract cookies from (e.g., "firefox", "chrome", "edge", "safari").
    /// </summary>
    public string? CookiesFromBrowser { get; set; }

    #endregion

    #region Decryption (DRM)

    /// <summary>
    /// Gets or sets decryption keys for DRM-protected content.
    /// </summary>
    public List<string>? DecryptionKeys { get; set; }

    /// <summary>
    /// Gets or sets the decryption application to use ("mp4decrypt" or "shaka-packager").
    /// </summary>
    public string? DecryptionApplication { get; set; }

    /// <summary>
    /// Gets or sets custom path to mp4decrypt binary.
    /// </summary>
    public string? Mp4DecryptLocation { get; set; }

    /// <summary>
    /// Gets or sets custom path to shaka-packager binary.
    /// </summary>
    public string? ShakaPackagerLocation { get; set; }

    #endregion

    #region Muxing

    /// <summary>
    /// Gets or sets muxer preference mapping for different container formats (e.g., "mp4:ffmpeg,vlc").
    /// </summary>
    public Dictionary<string, string>? MuxerPreference { get; set; }

    /// <summary>
    /// Gets or sets custom path to ffmpeg binary.
    /// </summary>
    public string? FfmpegLocation { get; set; }

    /// <summary>
    /// Gets or sets custom path to VLC binary.
    /// </summary>
    public string? VlcLocation { get; set; }

    /// <summary>
    /// Gets or sets custom path to mkvmerge binary.
    /// </summary>
    public string? MkvmergeLocation { get; set; }

    /// <summary>
    /// Gets or sets custom path to MP4Box binary.
    /// </summary>
    public string? Mp4BoxLocation { get; set; }

    #endregion

    #region Quality Selection

    /// <summary>
    /// Gets or sets preferred video height in pixels (e.g., 1080).
    /// </summary>
    public int? PreferVideoHeight { get; set; }

    /// <summary>
    /// Gets or sets preferred video width in pixels (e.g., 1920).
    /// </summary>
    public int? PreferVideoWidth { get; set; }

    /// <summary>
    /// Gets or sets preferred audio language code (e.g., "en", "fr").
    /// </summary>
    public string? AudioLanguage { get; set; }

    #endregion

    #region Other

    /// <summary>
    /// Gets or sets XPath expression to drop elements from MPD manifest.
    /// </summary>
    public string? DropElements { get; set; }

    /// <summary>
    /// Gets or sets path to XSLT stylesheet for MPD manifest rewriting.
    /// </summary>
    public string? XsltStylesheet { get; set; }

    /// <summary>
    /// Gets or sets whether to disable concatenation of multi-period content.
    /// </summary>
    public bool NoPeriodConcatenation { get; set; }

    #endregion
}
