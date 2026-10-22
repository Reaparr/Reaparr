namespace Reaparr.Application;

public class UpdateUserSettingsEndpointRequest
{
    [FromBody]
    public SettingsModelDTO? SettingsModelDto { get; set; }
}

public class UpdateUserSettingsEndpointRequestValidator : Validator<UpdateUserSettingsEndpointRequest>
{
    public UpdateUserSettingsEndpointRequestValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.SettingsModelDto)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.SettingsModelDto!.ConfirmationSettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.DateTimeSettings)
                    .NotNull()
                    .DependentRules(() =>
                    {
                        RuleFor(x => x.SettingsModelDto!.DateTimeSettings.TimeZone)
                            .Cascade(CascadeMode.Stop)
                            .NotEmpty()
                            .Must(timeZone => TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _));
                    });
                RuleFor(x => x.SettingsModelDto!.DebugSettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.DisplaySettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.DownloadManagerSettings)
                    .NotNull()
                    .DependentRules(() =>
                    {
                        RuleFor(x => x.SettingsModelDto!.DownloadManagerSettings.DownloadSchedule)
                            .NotNull()
                            .DependentRules(() =>
                            {
                                RuleFor(x => x.SettingsModelDto!.DownloadManagerSettings.DownloadSchedule.Days)
                                    .Cascade(CascadeMode.Stop)
                                    .NotNull()
                                    .Must(DownloadSchedule.IsValidDays);
                            });
                    });
                RuleFor(x => x.SettingsModelDto!.GeneralSettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.LanguageSettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.ServerSettings).NotNull();
                RuleFor(x => x.SettingsModelDto!.NetworkSettings).NotNull();
            });
    }
}

public class UpdateUserSettingsEndpoint : Endpoint<UpdateUserSettingsEndpointRequest, SettingsModelDTO>
{
    private readonly IUserSettings _userSettings;
    private readonly IScheduler _scheduler;
    private static readonly SemaphoreSlim _updateGate = new(1, 1);

    public UpdateUserSettingsEndpoint(IUserSettings userSettings, IScheduler scheduler)
    {
        _userSettings = userSettings;
        _scheduler = scheduler;
    }

    public override void Configure()
    {
        Put(ApiRoutes.SettingsController + "/");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<SettingsModelDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdateUserSettingsEndpointRequest req, CancellationToken ct)
    {
        await _updateGate.WaitAsync(ct);
        try
        {
            var settings = req.SettingsModelDto!.ToModel(_userSettings);

            var timeZoneChanged = _userSettings.DateTimeSettings.TimeZone != settings.DateTimeSettings.TimeZone;
            var downloadLimitsChanged =
                _userSettings.DownloadManagerSettings.DownloadSchedule != settings.DownloadManagerSettings.DownloadSchedule
                || HaveManualLimitsChanged(_userSettings.ServerSettings, settings.ServerSettings);

            _userSettings.UpdateSettings(settings);

            if (timeZoneChanged || downloadLimitsChanged)
            {
                var result = await Result.Try(async Task () =>
                {
                    if (timeZoneChanged)
                        await _scheduler.RescheduleJob(
                            UpdateScheduledDownloadLimitsJob.GetTriggerKey(),
                            UpdateScheduledDownloadLimitsJob.CreateTrigger(_userSettings.DateTimeSettings.TimeZone),
                            CancellationToken.None
                        );

                    await _scheduler.TriggerJob(UpdateScheduledDownloadLimitsJob.GetJobKey(), CancellationToken.None);
                });
                result.LogIfFailed();
            }

            await Send.FluentResult(Result.Ok(_userSettings), x => x.ToDTO(), ct);
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private static bool HaveManualLimitsChanged(PlexServerSettingsModule previous, PlexServerSettingsModule updated)
    {
        var previousLimits = new Dictionary<string, int>(previous.Data.Count, StringComparer.Ordinal);
        foreach (var server in previous.Data)
            previousLimits.TryAdd(server.MachineIdentifier, Math.Max(0, server.DownloadSpeedLimit));

        foreach (var server in updated.Data)
        {
            if (previousLimits.GetValueOrDefault(server.MachineIdentifier) != Math.Max(0, server.DownloadSpeedLimit))
                return true;

            previousLimits.Remove(server.MachineIdentifier);
        }

        // Missing settings and a zero cap both mean Unlimited.
        return previousLimits.Values.Any(limit => limit > 0);
    }
}
