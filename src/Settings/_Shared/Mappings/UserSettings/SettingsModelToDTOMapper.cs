namespace Reaparr.Settings;

public static class SettingsModelToDTOMapper
{
    #region ToModel

    public static UserSettings ToModel(this SettingsModelDTO dto, IUserSettings? currentSettings = null) =>
        new()
        {
            GeneralSettings = dto.GeneralSettings.ToModel(),
            ConfirmationSettings = dto.ConfirmationSettings.ToModel(),
            DateTimeSettings = dto.DateTimeSettings.ToModel(),
            DisplaySettings = dto.DisplaySettings.ToModel(),
            DownloadManagerSettings = dto.DownloadManagerSettings.ToModel(),
            LanguageSettings = dto.LanguageSettings.ToModel(),
            DebugSettings = dto.DebugSettings.ToModel(),
            ServerSettings = dto.ServerSettings.ToModel(),
            NetworkSettings = dto.NetworkSettings.ToModel(),
            AuthenticationSettings = currentSettings?.AuthenticationSettings ?? AuthenticationModule.Create(),
            IntegrationsSettings = currentSettings?.IntegrationsSettings ?? IntegrationsSettings.Create(),
        };

    public static GeneralSettingsModule ToModel(this GeneralSettingsDTO dto) =>
        new()
        {
            FirstTimeSetup = dto.FirstTimeSetup,
            ActiveAccountId = dto.ActiveAccountId,
            DisableAnimatedBackground = dto.DisableAnimatedBackground,
            HideMediaFromOfflineServers = dto.HideMediaFromOfflineServers,
            HideMediaFromOwnedServers = dto.HideMediaFromOwnedServers,
            UseLowQualityPosterImages = dto.UseLowQualityPosterImages,
            HasBeenInvitedToDiscord = dto.HasBeenInvitedToDiscord,
        };

    public static ConfirmationSettingsModule ToModel(this ConfirmationSettingsDTO dto) =>
        new()
        {
            AskDownloadMovieConfirmation = dto.AskDownloadMovieConfirmation,
            AskDownloadTvShowConfirmation = dto.AskDownloadTvShowConfirmation,
            AskDownloadSeasonConfirmation = dto.AskDownloadSeasonConfirmation,
            AskDownloadEpisodeConfirmation = dto.AskDownloadEpisodeConfirmation,
        };

    public static DateTimeSettingsModule ToModel(this DateTimeSettingsDTO dto) =>
        new()
        {
            ShortDateFormat = dto.ShortDateFormat,
            LongDateFormat = dto.LongDateFormat,
            TimeFormat = dto.TimeFormat,
            TimeZone = dto.TimeZone,
            ShowRelativeDates = dto.ShowRelativeDates,
        };

    public static DisplaySettingsModule ToModel(this DisplaySettingsDTO dto) =>
        new()
        {
            TvShowViewMode = dto.TvShowViewMode,
            MovieViewMode = dto.MovieViewMode,
            AllOverviewViewMode = dto.AllOverviewViewMode,
        };

    public static LanguageSettingsModule ToModel(this LanguageSettingsDTO dto) => new() { Language = dto.Language };

    public static DownloadManagerSettingsModule ToModel(this DownloadManagerSettingsDTO dto) =>
        new()
        {
            DownloadSegments = dto.DownloadSegments,
            KeepCompletedInDownloadFolder = dto.KeepCompletedInDownloadFolder,
            DownloadSchedule = new DownloadSchedule
            {
                Enabled = dto.DownloadSchedule.Enabled,
                Days = dto.DownloadSchedule.Days,
            },
        };

    public static PlexServerSettingsModule ToModel(this ServerSettingsDTO dto) => new() { Data = dto.Data };

    public static NetworkSettingsModule ToModel(this NetworkSettingsDTO dto) =>
        new()
        {
            ReverseProxyUrl = dto.ReverseProxyUrl,
            BasePath = dto.BasePath,
            TrustProxyHeaders = dto.TrustProxyHeaders,
            AllowedProxyIps = dto.AllowedProxyIps,
            ForwardedHostHeader = dto.ForwardedHostHeader,
            ForwardedPathHeader = dto.ForwardedPathHeader,
        };

    public static DebugSettingsModule ToModel(this DebugSettingsDTO dto) =>
        new()
        {
            DebugModeEnabled = dto.DebugModeEnabled,
            MaskServerNames = dto.MaskServerNames,
            MaskLibraryNames = dto.MaskLibraryNames,
            MaskAccountNames = dto.MaskAccountNames,
        };

    #endregion

    #region ToDTO

    public static SettingsModelDTO ToDTO(this IUserSettings model) =>
        new()
        {
            GeneralSettings = model.GeneralSettings.ToDTO(),
            DebugSettings = model.DebugSettings.ToDTO(),
            ConfirmationSettings = model.ConfirmationSettings.ToDTO(),
            DateTimeSettings = model.DateTimeSettings.ToDTO(),
            DisplaySettings = model.DisplaySettings.ToDTO(),
            DownloadManagerSettings = model.DownloadManagerSettings.ToDTO(),
            LanguageSettings = model.LanguageSettings.ToDTO(),
            ServerSettings = model.ServerSettings.ToDTO(),
            NetworkSettings = model.NetworkSettings.ToDTO(),
        };

    public static GeneralSettingsDTO ToDTO(this GeneralSettingsModule module) =>
        new()
        {
            FirstTimeSetup = module.FirstTimeSetup,
            ActiveAccountId = module.ActiveAccountId,
            DisableAnimatedBackground = module.DisableAnimatedBackground,
            HideMediaFromOfflineServers = module.HideMediaFromOfflineServers,
            HideMediaFromOwnedServers = module.HideMediaFromOwnedServers,
            UseLowQualityPosterImages = module.UseLowQualityPosterImages,
            HasBeenInvitedToDiscord = module.HasBeenInvitedToDiscord,
            HasAgreedToDisclaimer = module.HasAgreedToDisclaimer,
        };

    public static ConfirmationSettingsDTO ToDTO(this ConfirmationSettingsModule module) =>
        new()
        {
            AskDownloadMovieConfirmation = module.AskDownloadMovieConfirmation,
            AskDownloadTvShowConfirmation = module.AskDownloadTvShowConfirmation,
            AskDownloadSeasonConfirmation = module.AskDownloadSeasonConfirmation,
            AskDownloadEpisodeConfirmation = module.AskDownloadEpisodeConfirmation,
        };

    public static DateTimeSettingsDTO ToDTO(this DateTimeSettingsModule module) =>
        new()
        {
            ShortDateFormat = module.ShortDateFormat,
            LongDateFormat = module.LongDateFormat,
            TimeFormat = module.TimeFormat,
            ShowRelativeDates = module.ShowRelativeDates,
            TimeZone = module.TimeZone,
        };

    public static DisplaySettingsDTO ToDTO(this DisplaySettingsModule module) =>
        new()
        {
            TvShowViewMode = module.TvShowViewMode,
            MovieViewMode = module.MovieViewMode,
            AllOverviewViewMode = module.AllOverviewViewMode,
        };

    public static LanguageSettingsDTO ToDTO(this LanguageSettingsModule module) => new() { Language = module.Language };

    public static DownloadManagerSettingsDTO ToDTO(this DownloadManagerSettingsModule module) =>
        new()
        {
            DownloadSegments = module.DownloadSegments,
            KeepCompletedInDownloadFolder = module.KeepCompletedInDownloadFolder,
            DownloadSchedule = new DownloadScheduleDTO
            {
                Enabled = module.DownloadSchedule.Enabled,
                Days = module.DownloadSchedule.Days,
            },
        };

    public static ServerSettingsDTO ToDTO(this PlexServerSettingsModule module) => new() { Data = module.Data };

    public static NetworkSettingsDTO ToDTO(this NetworkSettingsModule module) =>
        new()
        {
            ReverseProxyUrl = module.ReverseProxyUrl,
            BasePath = module.BasePath,
            TrustProxyHeaders = module.TrustProxyHeaders,
            AllowedProxyIps = module.AllowedProxyIps,
            ForwardedHostHeader = module.ForwardedHostHeader,
            ForwardedPathHeader = module.ForwardedPathHeader,
        };

    public static DebugSettingsDTO ToDTO(this DebugSettingsModule module) =>
        new()
        {
            DebugModeEnabled = module.DebugModeEnabled,
            MaskServerNames = module.MaskServerNames,
            MaskLibraryNames = module.MaskLibraryNames,
            MaskAccountNames = module.MaskAccountNames,
        };

    #endregion
}
