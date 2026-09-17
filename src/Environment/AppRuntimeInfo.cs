using System.Collections;
using Serilog.Events;

namespace Reaparr.Environment;

public class AppRuntimeInfo : IAppRuntimeInfo
{
    /// <inheritdoc/>
    public string AppRunId { get; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public bool IsAuthenticationDisabled => IsTrue(GetEnvironmentVariable(EnvKeys.DisableAuthentication));

    /// <inheritdoc/>
    public int AppPort =>
        int.TryParse(
            GetEnvironmentVariable(EnvKeys.DotNetHttpPorts)
                ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(),
            out var port
        )
            ? port
            : 5000;

    /// <summary>
    /// Gets the configured Serilog log level from <c>LOG_LEVEL</c>. Defaults to <see cref="LogEventLevel.Debug"/>.
    /// </summary>
    public LogEventLevel LogLevel =>
        Enum.TryParse<LogEventLevel>(GetEnvironmentVariable(EnvKeys.LogLevel), true, out var logLevel)
            ? logLevel
            : LogEventLevel.Debug;

    /// <inheritdoc/>
    public bool IsUnmasked => IsTrue(GetEnvironmentVariable(EnvKeys.Unmasked));

    /// <inheritdoc/>
    public bool IsDevelopmentEnvironment => GetEnvironmentVariable(EnvKeys.DotNetEnvironment) == "Development";

    /// <inheritdoc/>
    public bool IsProductionEnvironment => GetEnvironmentVariable(EnvKeys.DotNetEnvironment) == "Production";

    /// <inheritdoc/>
    public bool IsIntegrationTestMode => IsTrue(GetEnvironmentVariable(EnvKeys.IntegrationTestMode));

    /// <inheritdoc/>
    public bool IsDesktopEmbeddedDisabled => IsTrue(GetEnvironmentVariable(EnvKeys.DesktopEmbeddedDisabled));

    /// <inheritdoc/>
    public string SEQ_Url => GetEnvironmentVariable(EnvKeys.SeqUrl) ?? string.Empty;

    /// <inheritdoc/>
    public string SEQ_ApiKey => GetEnvironmentVariable(EnvKeys.SeqApiKey) ?? string.Empty;

    /// <inheritdoc/>
    public int PUID => int.TryParse(GetEnvironmentVariable(EnvKeys.Puid), out var puid) ? puid : -1;

    /// <inheritdoc/>
    public int PGID => int.TryParse(GetEnvironmentVariable(EnvKeys.Pgid), out var pgid) ? pgid : -1;

    /// <inheritdoc/>
    public string? GitHubToken => GetEnvironmentVariable(EnvKeys.GitHubToken);

    /// <inheritdoc/>
    public string? AppImage => GetEnvironmentVariable(EnvKeys.AppImage);

    /// <inheritdoc/>
    public bool ShouldLogEnvVars => IsTrue(GetEnvironmentVariable(EnvKeys.LogEnvironmentVariables));

    #region Authentication

    /// <inheritdoc/>
    public string HeaderAuthTokenName => GetEnvironmentVariable(EnvKeys.AuthHeaderTokenName) ?? "X-Auth-User";

    #endregion

    #region Paths

    /// <inheritdoc/>
    public string? DataPath => GetEnvironmentVariable(EnvKeys.ReaparrDataPath);

    /// <inheritdoc/>
    public string? ConfigPath => GetEnvironmentVariable(EnvKeys.ReaparrConfigPath);

    /// <inheritdoc/>
    public string? DownloadsPath => GetEnvironmentVariable(EnvKeys.ReaparrDownloadsPath);

    /// <inheritdoc/>
    public string? MoviesPath => GetEnvironmentVariable(EnvKeys.ReaparrMoviesPath);

    /// <inheritdoc/>
    public string? TvShowsPath => GetEnvironmentVariable(EnvKeys.ReaparrTvShowsPath);

    /// <inheritdoc/>
    public string? MusicPath => GetEnvironmentVariable(EnvKeys.ReaparrMusicPath);

    /// <inheritdoc/>
    public string? PhotosPath => GetEnvironmentVariable(EnvKeys.ReaparrPhotosPath);

    /// <inheritdoc/>
    public string? OtherPath => GetEnvironmentVariable(EnvKeys.ReaparrOtherPath);

    /// <inheritdoc/>
    public string? GamesPath => GetEnvironmentVariable(EnvKeys.ReaparrGamesPath);

    #endregion

    public static string? GetEnvironmentVariable(string key)
    {
        var value = System.Environment.GetEnvironmentVariable(key)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public Dictionary<string, string?> GetAllEnvironmentVariables =>
        System
            .Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => entry.Key.ToString()!, entry => entry.Value?.ToString());

    /// <summary>
    /// Determines if the value is true.
    /// </summary>
    /// <param name="value"></param>
    private static bool IsTrue(string? value) =>
        value is not null
        && (string.Equals(value, Convert.ToString(true), StringComparison.OrdinalIgnoreCase) || value == "1");
}
