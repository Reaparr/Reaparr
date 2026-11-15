namespace Reaparr.Environment;

/// <summary>
/// Defines environment-variable names shared by runtime configuration, build inputs, and static frontend generation
/// so consumers use one spelling for each deployment convention.
/// </summary>
public static class EnvKeys
{
    /// <summary>
    /// Enables integration-test behavior so startup and persistence use test-safe paths and services.
    /// </summary>
    public const string IntegrationTestMode = "IntegrationTestMode";

    /// <summary>
    /// Shows otherwise masked sensitive values in logs so development diagnostics can inspect them; avoid in production.
    /// </summary>
    public const string Unmasked = "UNMASKED";

    /// <summary>
    /// Dumps all process environment variables at startup so deployment diagnostics can inspect configuration; it may expose secrets.
    /// </summary>
    public const string LogEnvironmentVariables = "LOG_ENV_VARS";

    /// <summary>
    /// Selects the minimum Serilog log level so deployments can tune logging verbosity without code changes.
    /// </summary>
    public const string LogLevel = "LOG_LEVEL";

    /// <summary>
    /// Overrides the root media-data directory so deployments can store media outside platform defaults.
    /// </summary>
    public const string ReaparrDataPath = "REAPARR_DATA_PATH";

    /// <summary>
    /// Overrides the Reaparr application-state directory so settings, databases, backups, and logs can live outside defaults.
    /// </summary>
    public const string ReaparrConfigPath = "REAPARR_CONFIG_PATH";

    /// <summary>
    /// Overrides the default downloads destination so downloaded files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrDownloadsPath = "REAPARR_DOWNLOADS_PATH";

    /// <summary>
    /// Overrides the default movies destination so movie files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrMoviesPath = "REAPARR_MOVIES_PATH";

    /// <summary>
    /// Overrides the default TV shows destination so TV files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrTvShowsPath = "REAPARR_TVSHOWS_PATH";

    /// <summary>
    /// Overrides the default music destination so music files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrMusicPath = "REAPARR_MUSIC_PATH";

    /// <summary>
    /// Overrides the default photos destination so photo files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrPhotosPath = "REAPARR_PHOTOS_PATH";

    /// <summary>
    /// Overrides the default miscellaneous-media destination so uncategorized files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrOtherPath = "REAPARR_OTHER_PATH";

    /// <summary>
    /// Overrides the default games destination so game files can target deployment-specific storage.
    /// </summary>
    public const string ReaparrGamesPath = "REAPARR_GAMES_PATH";

    /// <summary>
    /// Names the trusted-proxy HTTP header used for authentication so deployments can match their proxy header convention.
    /// </summary>
    public const string AuthHeaderTokenName = "AUTH_HEADER_TOKEN";

    /// <summary>
    /// Supplies an optional GitHub API bearer token so authenticated requests can avoid unauthenticated rate limits.
    /// </summary>
    public const string GitHubToken = "GITHUB_TOKEN";

    /// <summary>
    /// Selects an optional Seq sink URL so application logs can be forwarded to a separate log server.
    /// </summary>
    public const string SeqUrl = "SEQ_URL";

    /// <summary>
    /// Supplies the optional Seq API key so forwarded logs can authenticate when <see cref="SeqUrl"/> is configured.
    /// </summary>
    public const string SeqApiKey = "SEQ_API_KEY";

    /// <summary>
    /// Selects the .NET hosting environment so environment-specific startup behavior is applied.
    /// </summary>
    public const string DotNetEnvironment = "DOTNET_ENVIRONMENT";

    /// <summary>
    /// Selects the HTTP port exposed by ASP.NET Core so the process binds to the deployment's port.
    /// </summary>
    public const string DotNetHttpPorts = "DOTNET_HTTP_PORTS";

    /// <summary>
    /// Selects the Unix process user ID so container file ownership matches the deployment user.
    /// </summary>
    public const string Puid = "PUID";

    /// <summary>
    /// Selects the Unix process group ID so container file ownership matches the deployment group.
    /// </summary>
    public const string Pgid = "PGID";

    /// <summary>
    /// Enables the development-only authentication bypass so local runs can use a synthetic administrator identity.
    /// </summary>
    public const string DisableAuthentication = "I_AM_DUMB_SO_DISABLE_AUTHENTICATION";

    /// <summary>
    /// Supplies the AppImage package path so desktop single-instance identity can be scoped to the launched package.
    /// </summary>
    public const string AppImage = "APPIMAGE";

    /// <summary>
    /// Disables embedded desktop-window mode so the application launches browser-only mode when requested.
    /// </summary>
    public const string DesktopEmbeddedDisabled = "REAPARR_DESKTOP_EMBEDDED_DISABLED";

    /// <summary>
    /// Carries the telemetry ingestion origin into generated Nuxt output so the frontend has its public telemetry host.
    /// </summary>
    public const string NuxtPublicRpHost = "NUXT_PUBLIC_RP_HOST";

    /// <summary>
    /// Carries the public telemetry project key into generated Nuxt output so the frontend can initialize telemetry.
    /// </summary>
    public const string NuxtPublicRpKey = "NUXT_PUBLIC_RP_KEY";

    /// <summary>
    /// Supplies the build-time ingestion origin so release artifacts share one telemetry destination; it is not a runtime override.
    /// </summary>
    public const string TelemetryHost = "TELEMETRY_HOST";

    /// <summary>
    /// Supplies the application release version to Nuxt's PostHog uploader so injected errors and uploaded maps identify the same build.
    /// </summary>
    public const string PostHogCliReleaseVersion = "POSTHOG_CLI_RELEASE_VERSION";
}
