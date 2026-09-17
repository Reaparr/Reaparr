namespace Reaparr.BaseTests;

public class MockAppRuntimeInfo : IAppRuntimeInfo
{
    private bool _isDevEnvironment = true;

    public int PUID { get; set; }
    public int PGID { get; set; }
    public string? GitHubToken { get; set; }

    public Dictionary<string, string?> GetAllEnvironmentVariables { get; set; } = new Dictionary<string, string?>();

    /// <inheritdoc/>
    public string? DataPath { get; set; }

    /// <inheritdoc/>
    public string? ConfigPath { get; set; }

    /// <inheritdoc/>
    public string? DownloadsPath { get; set; }

    /// <inheritdoc/>
    public string? MoviesPath { get; set; }

    /// <inheritdoc/>
    public string? TvShowsPath { get; set; }

    /// <inheritdoc/>
    public string? MusicPath { get; set; }

    /// <inheritdoc/>
    public string? PhotosPath { get; set; }

    /// <inheritdoc/>
    public string? OtherPath { get; set; }

    /// <inheritdoc/>
    public string? GamesPath { get; set; }

    public string? AppImage { get; set; }
    public bool ShouldLogEnvVars { get; set; }
    public string SEQ_Url { get; set; } = "http://localhost:5341";
    public string SEQ_ApiKey { get; set; } = Guid.NewGuid().ToString("N");

    public string AppRunId { get; } = Guid.NewGuid().ToString("N");

    public bool IsIntegrationTestMode { get; set; }

    public bool IsDevelopmentEnvironment
    {
        get => _isDevEnvironment;
        set => _isDevEnvironment = value;
    }

    public bool IsProductionEnvironment
    {
        get => !_isDevEnvironment;
        set => _isDevEnvironment = !value;
    }

    public string HeaderAuthTokenName { get; set; } = "X-Auth-User";
    public bool IsUnmasked { get; set; }

    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Debug;
    public bool IsAuthenticationDisabled { get; set; }
    public bool IsDesktopEmbeddedDisabled { get; set; }
    public int AppPort { get; set; } = 5000;
}
