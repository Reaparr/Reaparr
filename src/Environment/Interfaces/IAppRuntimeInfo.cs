using Serilog.Events;

namespace Reaparr.Environment;

/// <summary>
/// Provides resolved runtime environment values used by Reaparr, including storage paths,
/// process identity, GitHub authentication, logging configuration, authentication settings,
/// application port, environment selectors, and an environment-variable dump mapped to
/// <see cref="EnvKeys"/> where applicable.
/// </summary>
public interface IAppRuntimeInfo
{
    /// <summary>
    /// Gets the process user ID (PUID) from the environment. Returns -1 when not set or invalid.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    int PUID { get; }

    /// <summary>
    /// Gets the process group ID (PGID) from the environment. Returns -1 when not set or invalid.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    int PGID { get; }

    /// <summary>
    /// Gets the GitHub token from <c>GITHUB_TOKEN</c> for authenticated GitHub API requests.
    /// </summary>
    string? GitHubToken { get; }

    /// <summary>
    /// Gets all defined System and user level Environment keys and their values
    /// </summary>
    Dictionary<string, string?> GetAllEnvironmentVariables { get; }

    #region Paths

    /// <summary>
    /// Gets the base data path from <see cref="EnvKeys.ReaparrDataPath"/>.
    /// </summary>
    string? DataPath { get; }

    /// <summary>
    /// Gets the default configuration path from <see cref="EnvKeys.ReaparrConfigPath"/>.
    /// </summary>
    string? ConfigPath { get; }

    /// <summary>
    /// Gets the default downloads path from <see cref="EnvKeys.ReaparrDownloadsPath"/>.
    /// </summary>
    string? DownloadsPath { get; }

    /// <summary>
    /// Gets the default movies library path from <see cref="EnvKeys.ReaparrMoviesPath"/>.
    /// </summary>
    string? MoviesPath { get; }

    /// <summary>
    /// Gets the default TV shows library path from <see cref="EnvKeys.ReaparrTvShowsPath"/>.
    /// </summary>
    string? TvShowsPath { get; }

    /// <summary>
    /// Gets the default music library path from <see cref="EnvKeys.ReaparrMusicPath"/>.
    /// </summary>
    string? MusicPath { get; }

    /// <summary>
    /// Gets the default photos library path from <see cref="EnvKeys.ReaparrPhotosPath"/>.
    /// </summary>
    string? PhotosPath { get; }

    /// <summary>
    /// Gets the miscellaneous library path from <see cref="EnvKeys.ReaparrOtherPath"/>.
    /// </summary>
    string? OtherPath { get; }

    /// <summary>
    /// Gets the default games library path from <see cref="EnvKeys.ReaparrGamesPath"/>.
    /// </summary>
    string? GamesPath { get; }

    /// <summary>
    /// Gets the AppImage path when running from an AppImage package.
    /// </summary>
    string? AppImage { get; }

    #endregion

    #region Logging

    /// <summary>
    /// When set to true, the application will log all environment variables set on startup
    /// </summary>
    bool ShouldLogEnvVars { get; }

    /// <summary>
    /// Sets the SEQ_URL environment variable to the specified URL.
    /// Note: This is used for development and testing purposes to redirect logs to a hosted Docker instance of Seq.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    string SEQ_Url { get; }

    /// <summary>
    /// Sets the SEQ_API_KEY environment variable to the specified API key.
    /// Note: This is used for development and testing purposes to redirect logs to a hosted Docker instance of Seq.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    string SEQ_ApiKey { get; }

    /// <summary>
    /// Gets the unique identifier for this application process lifetime. It is attached to every
    /// structured log event so Seq queries can isolate the current or a specific application run.
    /// </summary>
    string AppRunId { get; }

    /// <summary>
    /// Gets the configured Serilog log level from <c>LOG_LEVEL</c>. Defaults to <see cref="LogEventLevel.Debug"/>.
    /// </summary>
    LogEventLevel LogLevel { get; }

    #endregion

    #region Environment

    /// <summary>
    /// Returns true when <c>IntegrationTestMode</c> environment variable is set to a truthy value.
    /// </summary>
    bool IsIntegrationTestMode { get; }

    /// <summary>
    /// Returns true when desktop embedded window mode is disabled and browser-only mode should be used.
    /// </summary>
    bool IsDesktopEmbeddedDisabled { get; }

    /// <summary>
    /// Returns true if the DOTNET_ENVIRONMENT is set to Development.
    /// </summary>
    bool IsDevelopmentEnvironment { get; }

    /// <summary>
    /// Returns true if the DOTNET_ENVIRONMENT is set to Production.
    /// </summary>
    bool IsProductionEnvironment { get; }

    /// <summary>
    /// Gets the port number from the DOTNET_HTTP_PORTS environment variable.
    /// </summary>
    /// <returns>The port number or 5000 if not configured.</returns>
    int AppPort { get; }

    #endregion

    #region Auth

    /// <summary>
    /// Gets the name of the HTTP header used for bearer/auth token passing. Defaults to <c>X-Auth-User</c>.
    /// </summary>
    string HeaderAuthTokenName { get; }

    /// <summary>
    /// When set to true, the application will not mask/censor sensitive data in the logs.
    /// </summary>
    bool IsUnmasked { get; }

    /// <summary>
    /// When set to a truthy value, disables all authentication. FOR DEVELOPMENT USE ONLY.
    /// </summary>
    bool IsAuthenticationDisabled { get; }

    #endregion
}
