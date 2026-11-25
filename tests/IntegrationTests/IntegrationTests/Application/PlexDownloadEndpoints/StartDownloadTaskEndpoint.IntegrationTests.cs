using System.ComponentModel;
using Reaparr.Settings.Contracts;
using DownloadConfiguration = Downloader.DownloadConfiguration;
using DownloadPackage = Downloader.DownloadPackage;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using DownloadService = Downloader.DownloadService;
using IDownloadService = Downloader.IDownloadService;

namespace Reaparr.IntegrationTests;

public class StartDownloadTaskEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldSetServerUnreachable_WhenDirectDownloadFailsMidDownload()
    {
        // Arrange
        var seed = new Seed(8933);
        using var container = await CreateContainer(seed, config =>
        {
            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
                x.MovieCount = 1;
                x.MovieDownloadTasksCount = 1;
            };

            config.OverrideServices = builder =>
            {
                builder
                    .Register(_ =>
                        new FakeCommandExecutor().Intercept<GetDirectDownloadUrlCommand, Result<string>>(
                            (_, _) => Task.FromResult(Result.Ok("http://mock/direct-file.mkv"))
                        )
                    )
                    .As<ICommandExecutor>()
                    .InstancePerDependency();

                builder
                    .Register(_ =>
                        (Func<DownloadConfiguration, IDownloadService>)(
                            _ => BuildMidDownloadTimeoutFailureDownloadService()
                        )
                    )
                    .SingleInstance();
            };
        });

        var downloadTasks = await container.DbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        downloadTasks.Count.ShouldBe(1);
        var downloadTask = downloadTasks[0].Children.FirstOrDefault();
        downloadTask.ShouldNotBeNull();

        // Act
        var client = container.GetApiClient();
        await client.SignIn();

        var testResult = await client.PUTAsync<
            StartDownloadTaskEndpoint,
            StartDownloadTaskEndpointRequest,
            BaseResultDTO
        >(new StartDownloadTaskEndpointRequest { DownloadTaskGuid = downloadTask.Id });

        // Assert
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue(
            await testResult.Response.Content.ReadAsStringAsync(CancellationToken)
        );
        testResult.Result.IsSuccess.ShouldBeTrue();

        await WaitForDatabaseConditionAsync(
            async () =>
            {
                using var dbContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
                return await dbContext.DownloadTaskMovieFileLogs.AnyAsync(
                    x =>
                        x.DownloadTaskFileId == downloadTask.Id
                        && x.Status == DownloadStatus.ServerUnreachable
                        && x.LogLevel == NotificationLevel.Error
                        && x.Message.Contains("timed out while downloading"),
                    CancellationToken
                );
            },
            maxRetries: 60,
            delayMs: 250
        );

        using var dbContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var downloadTaskDb = await dbContext.GetDownloadTaskAsync(
            downloadTask.Id,
            cancellationToken: CancellationToken
        );
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadStatus.ShouldBeOneOf(DownloadStatus.ServerUnreachable, DownloadStatus.Downloading);

        var logs = await dbContext
            .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskFileId == downloadTask.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(CancellationToken);

        logs.Any(x => x.Status == DownloadStatus.Downloading).ShouldBeTrue();
        logs.Any(x => x.Status == DownloadStatus.ServerUnreachable).ShouldBeTrue();

        var serverUnreachableLogs = logs.Where(x => x.Status == DownloadStatus.ServerUnreachable).ToList();
        serverUnreachableLogs.ShouldContain(x =>
            x.LogLevel == NotificationLevel.Error && x.Message.Contains("timed out while downloading")
        );
    }

    [Test]
    public async Task ShouldResumePartialHttpDownloadAndFinish_WhenConnectionDropsOnce()
    {
        // Arrange
        var payload = Enumerable.Range(0, 1024 * 1024).Select(x => (byte)(x % 251)).ToArray();
        await using var server = new DropOnceRangeServer(payload);
        var downloadDirectory = Path.Combine(Path.GetTempPath(), $"reaparr-range-{Guid.NewGuid():N}");
        Directory.CreateDirectory(downloadDirectory);
        var downloadPath = Path.Combine(downloadDirectory, "range-download.bin");
        var configuration = new DownloadConfiguration { ChunkCount = 4, ParallelCount = 4 };
        await using var downloader = new DownloadService(configuration);

        // Act
        await downloader.DownloadFileTaskAsync(server.Url, downloadPath, CancellationToken);

        // Assert
        try
        {
            server.RequestCount.ShouldBeGreaterThanOrEqualTo(2);
            server.RangeRequestCount.ShouldBeGreaterThanOrEqualTo(1);
            File.Exists(downloadPath).ShouldBeTrue();
            (await File.ReadAllBytesAsync(downloadPath, CancellationToken)).ShouldBe(payload);
        }
        finally
        {
            Directory.Delete(downloadDirectory, true);
        }
    }

    [Test]
    public async Task ShouldWriteExactFileAndRemovePartialArtifact_WhenResumeRangeIsIgnored()
    {
        // Arrange
        var payload = Enumerable.Range(0, 1024 * 1024).Select(x => (byte)(x % 251)).ToArray();
        await using var server = new IgnoreResumeRangeServer(payload);
        var serverUrl = server.Url;
        var seed = new Seed(8934);
        using var container = await CreateContainer(seed, config =>
        {
            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.MovieDownloadTasksCount = 1;
                x.DownloadFileSizeInMb = 1;
            };
            config.MockDownloadServiceFactory = _ =>
                downloadConfiguration =>
                {
                    // Keep the two-chunk plan (which enables fallback) but run one request at a time.
                    downloadConfiguration.ParallelDownload = false;
                    return new DownloadService(downloadConfiguration);
                };
            config.OverrideServices = builder =>
                builder
                    .Register(_ =>
                        new FakeCommandExecutor().Intercept<GetDirectDownloadUrlCommand, Result<string>>(
                            (_, _) => Task.FromResult(Result.Ok(serverUrl))
                        )
                    )
                    .As<ICommandExecutor>()
                    .InstancePerDependency();
        });

        using var arrangeContext = container.DbContext;
        var task = await arrangeContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        var fileSystem = container.Resolve<IFileSystem>();
        var completedPath = Path.Combine(task.DownloadDirectory, task.FileName);
        task.DataTotal = payload.Length;
        task.DataReceived = 17;
        task.DownloadStatus = DownloadStatus.Paused;
        task.DirectDownloadSnapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 17 * 100.0 / payload.Length,
            Status = (int)Downloader.DownloadStatus.Stopped,
            Urls = [server.Url],
            TotalFileSize = payload.Length,
            FileName = completedPath,
            DownloadingFileExtension = FilePathExtensions.TempDownloadFileSuffix,
            Chunks =
            [
                new DirectDownloadSnapshotChunk
                {
                    Id = "resume",
                    Start = 0,
                    End = payload.Length - 1,
                    Position = 17,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                },
            ],
            IsSupportDownloadInRange = true,
            ETag = server.ETag,
            LastModified = server.LastModified,
        };
        fileSystem.Directory.CreateDirectory(task.DownloadDirectory);
        await using (var partialFile = fileSystem.File.Create(task.DownloadFilePath))
        {
            partialFile.SetLength(payload.Length);
            partialFile.Write(payload.AsSpan(0, 17));
        }
        (await arrangeContext.SaveChangesAsync(CancellationToken)).ShouldBe(1);

        var key = task.ToKey();
        container.Resolve<IDownloadManagerSettings>().DownloadSegments = 2;
        var dispatcher = new Mock<IDownloadTaskUpdateDispatcher>(MockBehavior.Strict);
        dispatcher
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Downloading, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        dispatcher
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.DownloadFinished, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        dispatcher
            .Setup(x => x.OnProgressUpdated(key, It.IsAny<DownloadTaskProgress>(), It.IsAny<DirectDownloadSnapshot?>()))
            .Verifiable(Times.AtLeastOnce());
        await using var sut = new DirectPlexDownloadClient(
            container.Resolve<ILogger>(),
            container.Resolve<IReaparrDbContextFactory>(),
            container.Resolve<ICommandExecutor>(),
            dispatcher.Object,
            container.Resolve<IDownloadManagerSettings>(),
            container.Resolve<IDownloadSpeedLimitProvider>(),
            container.Resolve<IFile>(),
            container.Resolve<IFileInfoFactory>(),
            container.Resolve<Func<DownloadConfiguration, IDownloadService>>(),
            container.Resolve<ITelemetry>()
        );

        // Act
        var result = await sut.Start(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        server.ProbeRequestCount.ShouldBe(1);
        server.IgnoredRanges.ToArray().ShouldBe([$"bytes=17-{payload.Length - 1}"]);
        server.FullRequestCount.ShouldBe(1);
        fileSystem.File.Exists(task.DownloadFilePath).ShouldBeFalse();
        (await fileSystem.File.ReadAllBytesAsync(completedPath, CancellationToken)).ShouldBe(payload);
        dispatcher.Verify();
    }

    [Test]
    public async Task ShouldStartQueuedMovieDownloadTaskOnStartCommand_WhenNoTasksAreDownloading()
    {
        // Arrange
        var seed = new Seed(8932);
        using var container = await CreateContainer(seed, config =>
        {
            config.HttpClientOptions = (x, _) =>
            {
                x.SetupIdentityRequest(seed);
            };

            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 2;
                x.MovieCount = 10;
                x.MovieDownloadTasksCount = 1;
                x.DownloadFileSizeInMb = 1;
            };
        });

        var downloadTasks = await container.DbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        downloadTasks.Count.ShouldBe(1);
        var downloadTask = downloadTasks.First().Children.FirstOrDefault();
        downloadTask.ShouldNotBeNull();

        // Act
        var client = container.GetApiClient();
        await client.SignIn();

        var testResult = await client.PUTAsync<
            StartDownloadTaskEndpoint,
            StartDownloadTaskEndpointRequest,
            BaseResultDTO
        >(new StartDownloadTaskEndpointRequest { DownloadTaskGuid = downloadTask.Id });
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue(
            await testResult.Response.Content.ReadAsStringAsync(CancellationToken)
        );

        var finalDownload = await container.WaitForDownloadStatusAsync(
            downloadTask.Id,
            [DownloadStatus.Completed],
            TimeSpan.FromSeconds(20),
            CancellationToken
        );

        // Assert
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        var finalDownloadFailureMessage = string.Format(
            "WaitForDownloadStatusAsync timed out waiting for download '{0}' to reach status '{1}'.",
            downloadTask.Id,
            DownloadStatus.Completed
        );
        finalDownload.ShouldNotBeNull(finalDownloadFailureMessage);

        var downloadTaskDb = await container.DbContext.GetDownloadTaskAsync(
            downloadTask.Id,
            cancellationToken: CancellationToken
        );
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadStatus.ShouldBe(DownloadStatus.Completed);
    }

    private sealed class IgnoreResumeRangeServer : IAsyncDisposable
    {
        private readonly byte[] _payload;
        private readonly System.Net.Sockets.TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _worker;
        private int _probeRequestCount;
        public System.Collections.Concurrent.ConcurrentQueue<string> IgnoredRanges { get; } = new();
        private int _fullRequestCount;

        public IgnoreResumeRangeServer(byte[] payload)
        {
            _payload = payload;
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/file.bin";
            _worker = ServeAsync(_cancellationTokenSource.Token);
        }

        public string Url { get; }
        public string ETag => "\"resume-test-etag\"";
        public string LastModified => "Wed, 21 Oct 2015 07:28:00 GMT";
        public int ProbeRequestCount => Volatile.Read(ref _probeRequestCount);
        public int FullRequestCount => Volatile.Read(ref _fullRequestCount);

        private async Task ServeAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                System.Net.Sockets.TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                using (client)
                await using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, leaveOpen: true))
                {
                    var requestLines = new List<string>();
                    while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
                        requestLines.Add(line);

                    var requestLine = requestLines.FirstOrDefault() ?? string.Empty;
                    var rangeHeader = requestLines.FirstOrDefault(x =>
                        x.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)
                    );
                    var hasRange = rangeHeader is not null;
                    var isProbe = hasRange && rangeHeader!.Contains("bytes=0-0", StringComparison.OrdinalIgnoreCase);
                    if (isProbe)
                        Interlocked.Increment(ref _probeRequestCount);
                    else if (hasRange)
                        IgnoredRanges.Enqueue(rangeHeader!["Range:".Length..].Trim());
                    else if (requestLine.StartsWith("GET ", StringComparison.Ordinal))
                        Interlocked.Increment(ref _fullRequestCount);

                    var status = isProbe ? "206 Partial Content" : "200 OK";
                    var bodyLength = isProbe ? 1 : _payload.Length;
                    var contentRange = isProbe ? $"Content-Range: bytes 0-0/{_payload.Length}\r\n" : string.Empty;
                    var headers =
                        $"HTTP/1.1 {status}\r\nContent-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\nContent-Length: {bodyLength}\r\n{contentRange}ETag: {ETag}\r\nLast-Modified: {LastModified}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(headers), cancellationToken);
                    await stream.WriteAsync(_payload.AsMemory(0, bodyLength), cancellationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            _listener.Stop();
            await _worker;
            _cancellationTokenSource.Dispose();
        }
    }

    private sealed class DropOnceRangeServer : IAsyncDisposable
    {
        private readonly byte[] _payload;
        private readonly System.Net.Sockets.TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _worker;
        private int _requestCount;
        private int _rangeRequestCount;
        private int _droppedGetRequest;

        public DropOnceRangeServer(byte[] payload)
        {
            _payload = payload;
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/file.bin";
            _worker = ServeAsync(_cancellationTokenSource.Token);
        }

        public string Url { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);
        public int RangeRequestCount => Volatile.Read(ref _rangeRequestCount);

        private async Task ServeAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                System.Net.Sockets.TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);
                var request = new List<string>();
                while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
                    request.Add(line);

                Interlocked.Increment(ref _requestCount);
                var isGetRequest = request.FirstOrDefault()?.StartsWith("GET ", StringComparison.Ordinal) == true;
                var isProbe = request.Contains("Range: bytes=0-0", StringComparer.OrdinalIgnoreCase);
                var shouldDrop =
                    isGetRequest && !isProbe && Interlocked.CompareExchange(ref _droppedGetRequest, 1, 0) == 0;
                var rangeHeader = request.FirstOrDefault(x =>
                    x.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)
                );
                var offset = ParseRangeOffset(rangeHeader?.Split(':', 2)[1].Trim());
                if (rangeHeader is not null)
                    Interlocked.Increment(ref _rangeRequestCount);

                var end = isProbe ? 0 : ParseRangeEnd(rangeHeader, _payload.Length - 1);
                var status = rangeHeader is not null ? "206 Partial Content" : "200 OK";
                var contentRange = rangeHeader is not null
                    ? $"Content-Range: bytes {offset}-{end}/{_payload.Length}\r\n"
                    : string.Empty;
                var responseLength = end - offset + 1;
                var headers =
                    $"HTTP/1.1 {status}\r\nContent-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\nContent-Length: {responseLength}\r\n{contentRange}Connection: close\r\n\r\n";
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(headers), cancellationToken);
                var bytesToWrite = shouldDrop ? responseLength / 2 : responseLength;
                await stream.WriteAsync(_payload.AsMemory(offset, bytesToWrite), cancellationToken);
                client.Close();
            }
        }

        private static int ParseRangeOffset(string? rangeHeader)
        {
            if (string.IsNullOrWhiteSpace(rangeHeader))
                return 0;

            var value = rangeHeader["bytes=".Length..].Split('-', 2)[0];
            return int.TryParse(value, out var offset) ? offset : 0;
        }

        private static int ParseRangeEnd(string? rangeHeader, int lastByte)
        {
            if (rangeHeader is null)
                return lastByte;

            var value = rangeHeader[(rangeHeader.IndexOf('-') + 1)..];
            return int.TryParse(value, out var end) ? end : lastByte;
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            _listener.Stop();
            await _worker;
            _cancellationTokenSource.Dispose();
        }
    }

    private static IDownloadService BuildMidDownloadTimeoutFailureDownloadService()
    {
        var package = new DownloadPackage
        {
            FileName = "mid-download-timeout.mkv",
            TotalFileSize = 5 * 1024 * 1024,
            Urls = ["http://mock/mid-download-timeout.mkv"],
        };

        var mock = new Mock<IDownloadService>();
        mock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        mock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        mock.Setup(x => x.Package).Returns(package);

        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, _, cancellationToken) =>
                {
                    mock.Raise(
                        x => x.DownloadProgressChanged += null,
                        mock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = package.TotalFileSize,
                            ReceivedBytesSize = 2 * 1024 * 1024,
                            BytesPerSecondSpeed = 512 * 1024,
                        }
                    );

                    await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);

                    mock.Raise(
                        x => x.DownloadFileCompleted += null,
                        mock.Object,
                        new AsyncCompletedEventArgs(new TimeoutException("timed out while downloading"), false, package)
                    );
                }
            );

        return mock.Object;
    }
}
