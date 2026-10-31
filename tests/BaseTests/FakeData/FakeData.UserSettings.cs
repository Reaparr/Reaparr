using Reaparr.Settings.Contracts;

namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly string[] _shortDateFormat =
    [
        "MMM dd yyyy",
        "dd MMM yyyy",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "yyyy-MM-dd",
    ];

    private static readonly string[] _longDateFormat = ["EEEE, MMMM dd, yyyy", "EEEE, dd MMMM yyyy"];

    private static readonly string[] _timeFormat = ["HH:mm:ss", "pp"];

    private static readonly Faker<UserSettings> _settingsModelFaker = new Faker<UserSettings>().StrictMode(true);

    private static readonly Faker<AuthenticationModule> _authenticationSettingsFaker = new Faker<AuthenticationModule>()
        .StrictMode(true)
        .RuleFor(x => x.HeaderAuthentication, _ => HeaderAuthenticationSettings.Create())
        .RuleFor(x => x.ResetCredentials, _ => false);

    private static readonly Faker<GeneralSettingsModule> _generalSettingsFaker = new Faker<GeneralSettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.FirstTimeSetup, f => f.Random.Bool())
        .RuleFor(x => x.HasAgreedToDisclaimer, f => f.Random.Bool())
        .RuleFor(x => x.DisableAnimatedBackground, f => f.Random.Bool())
        .RuleFor(x => x.HideMediaFromOwnedServers, f => f.Random.Bool())
        .RuleFor(x => x.HideMediaFromOfflineServers, f => f.Random.Bool())
        .RuleFor(x => x.UseLowQualityPosterImages, f => f.Random.Bool())
        .RuleFor(x => x.HasBeenInvitedToDiscord, f => f.Random.Bool())
        .RuleFor(x => x.ActiveAccountId, f => f.Random.Int(1, 10));

    private static readonly Faker<ConfirmationSettingsModule> _confirmationSettingsFaker =
        new Faker<ConfirmationSettingsModule>()
            .StrictMode(true)
            .RuleFor(x => x.AskDownloadMovieConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadTvShowConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadSeasonConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadEpisodeConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadMusicArtistConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadMusicAlbumConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadMusicTrackConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadPhotoAlbumConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadPhotoImageConfirmation, f => f.Random.Bool())
            .RuleFor(x => x.AskDownloadOtherVideosConfirmation, f => f.Random.Bool());

    private static readonly Faker<DateTimeSettingsModule> _dateTimeSettingsFaker = new Faker<DateTimeSettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.ShortDateFormat, f => f.PickRandom(_shortDateFormat))
        .RuleFor(x => x.LongDateFormat, f => f.PickRandom(_longDateFormat))
        .RuleFor(x => x.TimeFormat, f => f.PickRandom(_timeFormat))
        .RuleFor(x => x.TimeZone, f => f.PickRandom("UTC", "Europe/Amsterdam", "America/New_York", "Asia/Kathmandu"))
        .RuleFor(x => x.ShowRelativeDates, f => f.Random.Bool());

    private static readonly Faker<DisplaySettingsModule> _displaySettingsFaker = new Faker<DisplaySettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.MovieViewMode, f => f.Random.Enum<ViewMode>())
        .RuleFor(x => x.TvShowViewMode, f => f.Random.Enum<ViewMode>())
        .RuleFor(x => x.AllOverviewViewMode, f => f.PickRandom(PlexMediaType.Movie, PlexMediaType.TvShow))
        .RuleFor(x => x.MusicArtistViewMode, f => f.Random.Enum<ViewMode>())
        .RuleFor(x => x.PhotoAlbumViewMode, f => f.Random.Enum<ViewMode>())
        .RuleFor(x => x.OtherVideosViewMode, f => f.Random.Enum<ViewMode>());

    private static readonly Faker<DownloadManagerSettingsModule> _downloadManagerSettingsFaker =
        new Faker<DownloadManagerSettingsModule>()
            .StrictMode(true)
            .RuleFor(x => x.DownloadSegments, f => f.Random.Int(1, 3))
            .RuleFor(x => x.KeepCompletedInDownloadFolder, _ => false)
            .RuleFor(x => x.DownloadSchedule, _ => new DownloadSchedule());

    private static readonly Faker<LanguageSettingsModule> _languageSettingsFaker = new Faker<LanguageSettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.Language, f => f.Random.String(2));

    private static readonly Faker<DebugSettingsModule> _debugSettingsFaker = new Faker<DebugSettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.DebugModeEnabled, f => f.Random.Bool())
        .RuleFor(x => x.MaskServerNames, f => f.Random.Bool())
        .RuleFor(x => x.MaskLibraryNames, f => f.Random.Bool())
        .RuleFor(x => x.MaskAccountNames, f => f.Random.Bool());

    private static readonly Faker<PlexServerSettingsModule> _serverSettingsFaker =
        new Faker<PlexServerSettingsModule>().StrictMode(true);

    private static readonly Faker<PlexServerSettingItemModule> _plexServerSettingItemFaker =
        new Faker<PlexServerSettingItemModule>()
            .StrictMode(true)
            .RuleFor(x => x.MachineIdentifier, f => f.Finance.BitcoinAddress())
            .Ignore(x => x.PlexServerName)
            .RuleFor(x => x.DownloadSpeedLimit, _ => 0)
            .RuleFor(x => x.AllowStreamDownloader, f => f.Random.Bool());

    private static readonly Faker<NetworkSettingsModule> _networkSettingsFaker = new Faker<NetworkSettingsModule>()
        .StrictMode(true)
        .RuleFor(x => x.ReverseProxyUrl, _ => string.Empty)
        .RuleFor(x => x.BasePath, _ => string.Empty)
        .RuleFor(x => x.TrustProxyHeaders, f => f.Random.Bool())
        .RuleFor(x => x.AllowedProxyIps, _ => [])
        .RuleFor(x => x.ForwardedHostHeader, _ => string.Empty)
        .RuleFor(x => x.ForwardedPathHeader, _ => string.Empty);

    public static Faker<UserSettings> GetSettingsModel(Seed seed, Action<UnitTestDataConfig>? options = null)
    {
        return _settingsModelFaker
            .RuleFor(x => x.AuthenticationSettings, _ => GetAuthenticationSettings(seed).Generate())
            .RuleFor(x => x.GeneralSettings, _ => GetGeneralSettings(seed, options).Generate())
            .RuleFor(x => x.ConfirmationSettings, _ => GetConfirmationSettings(seed, options).Generate())
            .RuleFor(x => x.DateTimeSettings, _ => GetDateTimeSettings(seed, options).Generate())
            .RuleFor(x => x.DisplaySettings, _ => GetDisplaySettings(seed, options).Generate())
            .RuleFor(x => x.DownloadManagerSettings, _ => GetDownloadManagerSettings(seed, options).Generate())
            .RuleFor(x => x.LanguageSettings, _ => GetLanguageSettings(seed, options).Generate())
            .RuleFor(x => x.DebugSettings, _ => GetDebugSettings(seed, options).Generate())
            .RuleFor(x => x.ServerSettings, _ => GetServerSettings(seed, options).Generate())
            .RuleFor(x => x.NetworkSettings, _ => GetNetworkSettings(seed, options).Generate())
            .RuleFor(x => x.IntegrationsSettings, _ => IntegrationsSettings.Create())
            .UseSeed(seed.Next());
    }

    public static Faker<AuthenticationModule> GetAuthenticationSettings(Seed seed) =>
        _authenticationSettingsFaker.UseSeed(seed.Next());

    public static Faker<GeneralSettingsModule> GetGeneralSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _generalSettingsFaker.UseSeed(seed.Next());

    public static Faker<ConfirmationSettingsModule> GetConfirmationSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _confirmationSettingsFaker.UseSeed(seed.Next());

    public static Faker<DateTimeSettingsModule> GetDateTimeSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _dateTimeSettingsFaker.UseSeed(seed.Next());

    public static Faker<DisplaySettingsModule> GetDisplaySettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _displaySettingsFaker.UseSeed(seed.Next());

    public static Faker<DownloadManagerSettingsModule> GetDownloadManagerSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _downloadManagerSettingsFaker.UseSeed(seed.Next());

    public static Faker<LanguageSettingsModule> GetLanguageSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _languageSettingsFaker.UseSeed(seed.Next());

    public static Faker<DebugSettingsModule> GetDebugSettings(Seed seed, Action<UnitTestDataConfig>? options = null) =>
        _debugSettingsFaker.UseSeed(seed.Next());

    public static Faker<PlexServerSettingsModule> GetServerSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    )
    {
        var config = UnitTestDataConfig.FromOptions(options);

        return _serverSettingsFaker
            .RuleFor(
                x => x.Data,
                _ => GetPlexServerSettingsModel(seed, options).Generate(config.PlexServerSettingsCount)
            )
            .UseSeed(seed.Next());
    }

    public static Faker<PlexServerSettingItemModule> GetPlexServerSettingsModel(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    )
    {
        var config = UnitTestDataConfig.FromOptions(options);

        return _plexServerSettingItemFaker
            .RuleFor(x => x.DownloadSpeedLimit, _ => config.DownloadSpeedLimitInKib)
            .UseSeed(seed.Next());
    }

    public static Faker<NetworkSettingsModule> GetNetworkSettings(
        Seed seed,
        Action<UnitTestDataConfig>? options = null
    ) => _networkSettingsFaker.UseSeed(seed.Next());
}
