using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Downloader;

namespace Reaparr.Application.UnitTests;

public class DownloadScheduleThrottleUnitTests : BaseCommandUnitTest<UpdateScheduledDownloadLimitsCommand>
{
    [Test]
    [Arguments(1)]
    [Arguments(4)]
    public async Task ShouldThrottleAndReleaseAnActiveTransfer_WhenScheduledIntervalsChange(int segments)
    {
        // Arrange
        await SetupDatabase(85360, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Downloading),
            CancellationToken
        );
        var machineIdentifier = await dbContext
            .PlexServers.Select(x => x.MachineIdentifier)
            .SingleAsync(CancellationToken);
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Monday"] = new()
                {
                    ["00:00"] = null,
                    ["00:30"] = 64,
                    ["01:00"] = 256,
                    ["01:30"] = null,
                },
            },
        };
        var payload = Enumerable.Range(0, 8 * 1024 * 1024).Select(x => (byte)(x % 251)).ToArray();
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterType<DownloadSpeedLimitProvider>().As<IDownloadSpeedLimitProvider>().SingleInstance();
            builder.RegisterInstance(new PayloadHandler(payload)).AsSelf();
        });
        var speedLimits = Mock.Container.Resolve<IDownloadSpeedLimitProvider>();
        var configuration = new DownloadConfiguration
        {
            ChunkCount = segments,
            ParallelCount = segments,
            ParallelDownload = segments > 1,
            BufferBlockSize = 1024,
            CustomHttpMessageHandlerFactory = Mock.Container.Resolve<PayloadHandler>,
        };
        using var subscription = speedLimits
            .GetEffectiveDownloadSpeedLimitObservable(machineIdentifier)
            .Subscribe(value => configuration.MaximumBytesPerSecond = value);
        await using var downloader = new DownloadService(configuration);
        var progressSamples = Channel.CreateUnbounded<(long Bytes, long Tick)>();
        downloader.DownloadProgressChanged += (_, progress) =>
            progressSamples.Writer.TryWrite((progress.ReceivedBytesSize, Stopwatch.GetTimestamp()));
        var localTime = DateTimeOffset.Parse("2026-10-05T00:00:00Z");

        // Act
        var initial = await TestHandlerExecuteAsync(new UpdateScheduledDownloadLimitsCommand(localTime));
        var transfer = downloader.DownloadFileTaskAsync("https://schedule.test/payload", CancellationToken);
        var unlimitedRate = await MeasureRateAsync(progressSamples.Reader, 256 * 1024, CancellationToken);
        var limited = await TestHandlerExecuteAsync(new UpdateScheduledDownloadLimitsCommand(localTime.AddMinutes(30)));
        var limitedRate = await MeasureRateAsync(progressSamples.Reader, 128 * 1024, CancellationToken);
        var increased = await TestHandlerExecuteAsync(new UpdateScheduledDownloadLimitsCommand(localTime.AddHours(1)));
        var increasedRate = await MeasureRateAsync(progressSamples.Reader, 256 * 1024, CancellationToken);
        var released = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(localTime.AddMinutes(90))
        );
        var releasedRate = await MeasureRateAsync(progressSamples.Reader, 256 * 1024, CancellationToken);
        await using var downloaded = await transfer.WaitAsync(TimeSpan.FromSeconds(20), CancellationToken);
        using var contents = new MemoryStream();
        await downloaded.CopyToAsync(contents, CancellationToken);

        // Assert
        initial.IsSuccess.ShouldBeTrue();
        initial.Errors.Count.ShouldBe(0);
        limited.IsSuccess.ShouldBeTrue();
        limited.Errors.Count.ShouldBe(0);
        increased.IsSuccess.ShouldBeTrue();
        increased.Errors.Count.ShouldBe(0);
        released.IsSuccess.ShouldBeTrue();
        released.Errors.Count.ShouldBe(0);
        unlimitedRate.ShouldBeGreaterThan(192 * 1024);
        limitedRate.ShouldBeInRange(16 * 1024, 96 * 1024);
        increasedRate.ShouldBeInRange(96 * 1024, 384 * 1024);
        releasedRate.ShouldBeGreaterThan(192 * 1024);
        configuration.MaximumBytesPerSecond.ShouldBe(long.MaxValue);
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(0);
        contents.ToArray().ShouldBe(payload);
        settings.ServerSettings.Data.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRedistributeMeasuredBandwidth_WhenAnotherServerJoinsAndPauses()
    {
        // Arrange
        await SetupDatabase(85361, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieDownloadTasksCount = 0;
        });
        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        foreach (var server in servers)
        {
            var libraryId = await dbContext
                .PlexLibraries.Where(x => x.PlexServerId == server.Id && x.Type == PlexMediaType.Movie)
                .Select(x => x.Id)
                .SingleAsync(CancellationToken);
            var tasks = FakeData.GetMovieDownloadTask(new Seed(85370 + server.Id)).Generate(1);
            tasks.SetRelationshipIds(server.Id, libraryId);
            dbContext.DownloadTaskMovie.AddRange(tasks);
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Paused),
            CancellationToken
        );
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.PlexServerId == servers[0].Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Downloading),
                CancellationToken
            );
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 128 } },
        };
        var payload = Enumerable.Range(0, 8 * 1024 * 1024).Select(x => (byte)(x % 251)).ToArray();
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterType<DownloadSpeedLimitProvider>().As<IDownloadSpeedLimitProvider>().SingleInstance();
            builder.RegisterInstance(new PayloadHandler(payload)).AsSelf();
        });
        var limits = Mock.Container.Resolve<IDownloadSpeedLimitProvider>();
        var firstConfig = new DownloadConfiguration
        {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            CustomHttpMessageHandlerFactory = Mock.Container.Resolve<PayloadHandler>,
        };
        var secondConfig = new DownloadConfiguration
        {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            CustomHttpMessageHandlerFactory = Mock.Container.Resolve<PayloadHandler>,
        };
        using var firstSubscription = limits
            .GetEffectiveDownloadSpeedLimitObservable(servers[0].MachineIdentifier)
            .Subscribe(value => firstConfig.MaximumBytesPerSecond = value);
        using var secondSubscription = limits
            .GetEffectiveDownloadSpeedLimitObservable(servers[1].MachineIdentifier)
            .Subscribe(value => secondConfig.MaximumBytesPerSecond = value);
        await using var firstDownloader = new DownloadService(firstConfig);
        await using var secondDownloader = new DownloadService(secondConfig);
        var firstSamples = Channel.CreateUnbounded<(long Bytes, long Tick)>();
        var secondSamples = Channel.CreateUnbounded<(long Bytes, long Tick)>();
        firstDownloader.DownloadProgressChanged += (_, progress) =>
            firstSamples.Writer.TryWrite((progress.ReceivedBytesSize, Stopwatch.GetTimestamp()));
        secondDownloader.DownloadProgressChanged += (_, progress) =>
            secondSamples.Writer.TryWrite((progress.ReceivedBytesSize, Stopwatch.GetTimestamp()));
        var command = new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"));

        // Act
        var initial = await TestHandlerExecuteAsync(command);
        var firstTransfer = firstDownloader.DownloadFileTaskAsync("https://schedule.test/first", CancellationToken);
        var soloRate = await MeasureRateAsync(firstSamples.Reader, 128 * 1024, CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.PlexServerId == servers[1].Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Downloading),
                CancellationToken
            );
        var joined = await TestHandlerExecuteAsync(command);
        var secondTransfer = secondDownloader.DownloadFileTaskAsync("https://schedule.test/second", CancellationToken);
        var sharedRates = await Task.WhenAll(
            MeasureRateAsync(firstSamples.Reader, 128 * 1024, CancellationToken),
            MeasureRateAsync(secondSamples.Reader, 128 * 1024, CancellationToken)
        );
        await firstDownloader.CancelTaskAsync();
        await using var partial = await firstTransfer;
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.PlexServerId == servers[0].Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DownloadStatus, Domain.DownloadStatus.Paused),
                CancellationToken
            );
        var paused = await TestHandlerExecuteAsync(command);
        var remainingRate = await MeasureRateAsync(secondSamples.Reader, 128 * 1024, CancellationToken);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule();
        var released = await TestHandlerExecuteAsync(command);
        await using var downloaded = await secondTransfer.WaitAsync(TimeSpan.FromSeconds(20), CancellationToken);
        using var contents = new MemoryStream();
        await downloaded.CopyToAsync(contents, CancellationToken);

        // Assert
        initial.IsSuccess.ShouldBeTrue();
        initial.Errors.Count.ShouldBe(0);
        joined.IsSuccess.ShouldBeTrue();
        joined.Errors.Count.ShouldBe(0);
        paused.IsSuccess.ShouldBeTrue();
        paused.Errors.Count.ShouldBe(0);
        released.IsSuccess.ShouldBeTrue();
        released.Errors.Count.ShouldBe(0);
        soloRate.ShouldBeInRange(48 * 1024, 192 * 1024);
        sharedRates[0].ShouldBeInRange(16 * 1024, 96 * 1024);
        sharedRates[1].ShouldBeInRange(16 * 1024, 96 * 1024);
        sharedRates.Sum().ShouldBeLessThan(192 * 1024);
        remainingRate.ShouldBeInRange(48 * 1024, 192 * 1024);
        remainingRate.ShouldBeGreaterThan(sharedRates[1] * 1.4);
        contents.ToArray().ShouldBe(payload);
        (
            await dbContext
                .DownloadTaskMovieFile.OrderBy(x => x.PlexServerId)
                .Select(x => x.DownloadStatus)
                .ToListAsync(CancellationToken)
        ).ShouldBe([Domain.DownloadStatus.Paused, Domain.DownloadStatus.Downloading]);
        settings.ServerSettings.Data.ShouldBeEmpty();
    }

    private static async Task<double> MeasureRateAsync(
        ChannelReader<(long Bytes, long Tick)> samples,
        long bytes,
        CancellationToken token
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        while (samples.TryRead(out _)) { }
        var start = await samples.ReadAsync(timeout.Token);
        (long Bytes, long Tick) point;
        // Discard in-flight reads from the preceding limit before measuring the new interval.
        do
        {
            point = await samples.ReadAsync(timeout.Token);
        } while (point.Bytes - start.Bytes < 32 * 1024);
        start = point;
        do
        {
            point = await samples.ReadAsync(timeout.Token);
        } while (point.Bytes - start.Bytes < bytes);
        return (point.Bytes - start.Bytes) / Stopwatch.GetElapsedTime(start.Tick, point.Tick).TotalSeconds;
    }

    private sealed class PayloadHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;

        public PayloadHandler(byte[] payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var range = request.Headers.Range?.Ranges.Single();
            var start = (int)(range?.From ?? 0);
            var end = (int)(range?.To ?? _payload.Length - 1);
            var response = new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(new PacedPayloadStream(_payload, start, end - start + 1)),
                RequestMessage = request,
            };
            response.Content.Headers.ContentLength = end - start + 1;
            response.Headers.AcceptRanges.Add("bytes");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            if (range is not null)
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _payload.Length);
            return Task.FromResult(response);
        }
    }

    private sealed class PacedPayloadStream : MemoryStream
    {
        public PacedPayloadStream(byte[] payload, int start, int length)
            : base(payload, start, length, false) { }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            // A finite-speed upstream leaves time to apply limits to an initially unlimited transfer.
            await Task.Delay(1, cancellationToken);
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 4096)], cancellationToken);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
