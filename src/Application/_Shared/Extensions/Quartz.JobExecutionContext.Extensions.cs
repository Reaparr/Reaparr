namespace Reaparr.Application;

public static partial class QuartzExtensions
{
    public static void SetResult(this IJobExecutionContext context, JobStatus status, ResultBase result) =>
        context.SetResult(status, result.Errors.FirstOrDefault()?.Message);

    public static void SetResult(this IJobExecutionContext context, JobStatus status, string? errorSummary = null) =>
        context.Result = new BackgroundJobResult(status, errorSummary);

    public static JobStatusUpdate<TPayload> SetCronJobPayload<TPayload>(
        this IJobExecutionContext context,
        JobStatus status,
        TPayload payload
    )
        where TPayload : class
    {
        var update = new JobStatusUpdate<TPayload>(context.GetJobType(), status, payload);
        context.Result = update;
        return update;
    }

    public static TPayload? GetCronJobPayload<TPayload>(this IJobExecutionContext context)
        where TPayload : class => (context.Result as JobStatusUpdate<TPayload>)?.Data;

    public static JobTypes GetJobType(this IJobExecutionContext context) =>
        JobStatusUpdateMapper.ToJobType(context.JobDetail.Key.Group);

    public static Result<TPayload> GetRequiredPayload<TPayload>(this IJobExecutionContext context) =>
        Result.Try(() =>
        {
            var payload = context.MergedJobDataMap.GetPayload<TPayload>();
            return payload is null
                ? Result.Fail<TPayload>($"Missing required {typeof(TPayload).Name}")
                : Result.Ok(payload);
        });

    public static string GetPayloadAsJson(this IJobExecutionContext context)
    {
        var defaultJson = "{}";
        switch (context.GetJobType())
        {
            case JobTypes.DownloadJob:
                return JsonSerializer.Serialize(
                    context.MergedJobDataMap.GetPayload<DownloadJobPayload>(),
                    DefaultJsonSerializerOptions.ConfigStandard
                );
            case JobTypes.MoveDownloadFileJob:
                return JsonSerializer.Serialize(
                    context.MergedJobDataMap.GetPayload<MoveDownloadFileJobPayload>(),
                    DefaultJsonSerializerOptions.ConfigStandard
                );
            case JobTypes.InspectPlexServerJob:
                return JsonSerializer.Serialize(
                    context.MergedJobDataMap.GetPayload<InspectPlexServerJobPayload>(),
                    DefaultJsonSerializerOptions.ConfigStandard
                );
            case JobTypes.LibrarySyncJob:
                return JsonSerializer.Serialize(
                    context.MergedJobDataMap.GetPayload<LibrarySyncJobPayload>(),
                    DefaultJsonSerializerOptions.ConfigStandard
                );
            case JobTypes.LibraryComparisonJob:
                return JsonSerializer.Serialize(
                    context.MergedJobDataMap.GetPayload<PlexLibraryComparisonJobPayload>(),
                    DefaultJsonSerializerOptions.ConfigStandard
                );
            case JobTypes.CheckAllConnectionsStatusByPlexServerJob:
                var payload = context.GetCronJobPayload<CheckAllConnectionStatusUpdateDTO>();
                return payload is null
                    ? defaultJson
                    : JsonSerializer.Serialize(payload, DefaultJsonSerializerOptions.ConfigStandard);
            case JobTypes.MetadataSyncJob:
            case JobTypes.CheckForUpdateJob:
            case JobTypes.CheckPlexLibrariesForUpdatesJob:
            case JobTypes.RefreshPlexAccountAccessJob:
            case JobTypes.MediaOverviewSnapshotJob:
            case JobTypes.OptimizeDatabaseJob:
                return defaultJson;
            case JobTypes.Unknown:
            case JobTypes.None:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(context),
                    context.GetJobType(),
                    $"Unknown job type {context.GetJobType()}"
                );
        }
    }
}
