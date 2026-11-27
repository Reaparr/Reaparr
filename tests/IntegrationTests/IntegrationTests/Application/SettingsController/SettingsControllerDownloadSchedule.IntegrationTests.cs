using System.Net.Http.Headers;
using Downloader;
using Reaparr.Settings;
using Reaparr.Settings.Contracts;

namespace Reaparr.IntegrationTests;

public class SettingsControllerDownloadScheduleIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPersistAndApplyScheduledLimitToRealDownload_WhenSettingsAreUpdatedOverHttp()
    {
        // Arrange
        using var container = await CreateContainer(new Seed(68501), config =>
        {
            config.DatabaseOptions = database =>
            {
                database.PlexAccountCount = 1;
                database.PlexServerCount = 1;
                database.MovieDownloadTasksCount = 1;
            };
        });
        var client = container.GetApiClient();
        await client.SignIn();
        // Integration boot intentionally skips recurring jobs; register the real job for this scenario.
        await container.BackgroundJobScheduler.AddJob(
            JobBuilder
                .Create<UpdateScheduledDownloadLimitsJob>()
                .WithIdentity(UpdateScheduledDownloadLimitsJob.GetJobKey())
                .StoreDurably()
                .Build(),
            new AddJobOptions { Replace = true },
            CancellationToken
        );
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var file = await context.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await context.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Downloading),
            CancellationToken
        );
        var machine = await context.PlexServers.Select(x => x.MachineIdentifier).SingleAsync(CancellationToken);
        var payload = Enumerable.Range(0, 256 * 1024).Select(x => (byte)(x % 251)).ToArray();
        using var handler = new GatedContentHandler(payload);
        var configuration = new DownloadConfiguration
        {
            ChunkCount = 1,
            CustomHttpMessageHandlerFactory = handler.CreateHandler,
        };
        using var subscription = container
            .Resolve<IDownloadSpeedLimitProvider>()
            .GetEffectiveDownloadSpeedLimitObservable(machine)
            .Subscribe(value => configuration.MaximumBytesPerSecond = value);
        await using var downloader = new DownloadService(configuration);
        var transfer = downloader.DownloadFileTaskAsync("https://schedule.test/hosted", CancellationToken);
        await handler.Requested.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        var response = await client.GETAsync<GetUserSettingsEndpoint, ResultDTO<SettingsModelDTO>>();
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        var settings = response.Result.Value;
        settings.ShouldNotBeNull();
        settings.DateTimeSettings.TimeZone = "UTC";
        settings.DownloadManagerSettings.DownloadSchedule.Enabled = true;
        settings.DownloadManagerSettings.DownloadSchedule.Days = Enum.GetNames<DayOfWeek>()
            .ToDictionary(day => day, _ => new Dictionary<string, int?> { ["00:00"] = 64 });
        settings.ServerSettings.Data.RemoveAll(x => x.MachineIdentifier == machine);

        // Act
        var updated = await client.PUTAsync<
            UpdateUserSettingsEndpoint,
            UpdateUserSettingsEndpointRequest,
            SettingsModelDTO
        >(new UpdateUserSettingsEndpointRequest { SettingsModelDto = settings });
        updated.Response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await updated.Response.Content.ReadAsStringAsync(CancellationToken)
        );
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);
        await WaitForDatabaseConditionAsync(() => Task.FromResult(configuration.MaximumBytesPerSecond == 64 * 1024L));
        handler.Release.TrySetResult();
        await using var downloaded = await transfer.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        using var contents = new MemoryStream();
        await downloaded.CopyToAsync(contents, CancellationToken);

        // Assert
        updated.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persistedResponse = await client.GETAsync<GetUserSettingsEndpoint, ResultDTO<SettingsModelDTO>>();
        persistedResponse.Result.IsSuccess.ShouldBeTrue();
        persistedResponse.Result.Errors.Count.ShouldBe(0);
        persistedResponse.Result.Value.ShouldNotBeNull();
        persistedResponse.Result.Value.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        persistedResponse
            .Result.Value.DownloadManagerSettings.DownloadSchedule.Days.SelectMany(day =>
                day.Value.Select(point => (day.Key, point.Key, point.Value))
            )
            .OrderBy(x => x.Item1)
            .ShouldBe(Enum.GetNames<DayOfWeek>().Order().Select(day => (day, "00:00", (int?)64)));
        configuration.MaximumBytesPerSecond.ShouldBe(64 * 1024L);
        contents.ToArray().ShouldBe(payload);
        var storedJson = await container
            .Resolve<IFile>()
            .ReadAllTextAsync(container.PathProvider.ConfigFileLocation, CancellationToken);
        var stored = UserSettingsSerializer.Deserialize(storedJson);
        stored.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        stored
            .DownloadManagerSettings.DownloadSchedule.Days.SelectMany(day =>
                day.Value.Select(point => (day.Key, point.Key, point.Value))
            )
            .OrderBy(x => x.Item1)
            .ShouldBe(Enum.GetNames<DayOfWeek>().Order().Select(day => (day, "00:00", (int?)64)));
        using var after = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (
            await after.DownloadTaskMovieFile.Select(x => new { x.Id, x.DownloadStatus }).ToListAsync(CancellationToken)
        ).ShouldBe([new { file.Id, DownloadStatus = Domain.DownloadStatus.Downloading }]);
    }

    private sealed class GatedContentHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedContentHandler(byte[] payload)
        {
            _payload = payload;
        }

        public HttpMessageHandler CreateHandler() => this;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requested.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            var range = request.Headers.Range?.Ranges.Single();
            var start = (int)(range?.From ?? 0);
            var end = (int)(range?.To ?? _payload.Length - 1);
            var response = new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(_payload, start, end - start + 1),
            };
            response.Headers.AcceptRanges.Add("bytes");
            if (range is not null)
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _payload.Length);
            return response;
        }
    }
}
