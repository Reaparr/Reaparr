using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Moq;
using Moq.Contrib.HttpClient;
using Moq.Protected;
using PostHog;


namespace Reaparr.IntegrationTests;

public class TorrentAddFailureIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldReturnRecognizedFailureWithoutTasksOrHashChanges_WhenUploadedMovieWasRemoved()
    {
        var telemetryBodies = new ConcurrentQueue<string>();
        PostHogClient? telemetryClient = null;
        var buildInfo = new MockAppBuildInfo
        {
            TelemetryHost = "https://telemetry.example.test",
            TelemetryProjectToken = "phc_test",
        };

        using var container = await CreateContainer(
            new Seed(8636),
            config =>
            {
                config.DatabaseOptions = database =>
                {
                    database.PlexServerCount = 1;
                    database.PlexAccountCount = 1;
                    database.PlexMovieLibraryCount = 1;
                    database.MovieCount = 1;
                    database.MovieDownloadTasksCount = 1;
                    database.RadarrIntegrationCount = 1;
                };
                config.OverrideAppBuildInfo = buildInfo;
                config.OverrideServices = builder =>
                {
                    var handler = new Mock<HttpMessageHandler>();
                    handler
                        .Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>()
                        )
                        .Returns(async (HttpRequestMessage request, CancellationToken token) =>
                        {
                            telemetryBodies.Enqueue(await request.Content!.ReadAsStringAsync(token));
                            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
                        });
                    var httpClient = new HttpClient(handler.Object);
                    var factory = new Mock<IHttpClientFactory>();
                    factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
                    telemetryClient = new PostHogClient(
                        new PostHogOptions
                        {
                            ProjectToken = buildInfo.TelemetryProjectToken,
                            HostUrl = new Uri(buildInfo.TelemetryHost),
                            EnableCompression = false,
                        },
                        httpClientFactory: factory.Object
                    );
                    builder.RegisterInstance(telemetryClient).As<IPostHogClient>();
                };
            }
        );
        var dbContext = container.DbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movie = await dbContext.PlexMovies.SingleAsync(CancellationToken);
        await dbContext.DownloadTaskMovie.ExecuteUpdateAsync(
            update => update.SetProperty(task => task.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            update => update.SetProperty(task => task.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(file => file.Id)
            .Select(file => new
            {
                file.Id,
                file.ParentId,
                file.HashId,
                file.DownloadStatus,
                file.RadarrIntegrationId,
            })
            .ToListAsync(CancellationToken);
        before.Count.ShouldBe(1);
        var parentIds = await dbContext
            .DownloadTaskMovie.OrderBy(task => task.Id)
            .Select(task => task.Id)
            .ToListAsync(CancellationToken);
        parentIds.ShouldBe([before.Single().ParentId]);
        var route = $"/api/public/integrations/{integration.Id}";
        using var client = container.GetApiClient();
        var rssUrl = $"{route}/indexer/api?t=search&cat=2000&limit=1&apikey={integration.TorznabApiKey}";
        using var rssResponse = await client.GetAsync(rssUrl, CancellationToken);
        rssResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var channel = XDocument
            .Parse(await rssResponse.Content.ReadAsStringAsync(CancellationToken))
            .Root!.Element("channel")!;
        var item = channel.Elements("item").Single();
        using var torrentResponse = await client.GetAsync(item.Element("link")!.Value, CancellationToken);
        torrentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        torrentResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-bittorrent");
        var torrentBytes = await torrentResponse.Content.ReadAsByteArrayAsync(CancellationToken);
        var torrentHash = new BencodeParser().Parse<Torrent>(new MemoryStream(torrentBytes)).GetInfoHash();
        (await dbContext.PlexMovies.Where(row => row.Id == movie.Id).ExecuteDeleteAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMovies.Select(row => row.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(torrentBytes), "torrents", "removed-movie.torrent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            integration.QBittorrentApiKey
        );

        // Act
        using var response = await client.PostAsync(
            $"{route}/download-client/api/v2/torrents/add",
            upload,
            CancellationToken
        );

        // Assert — exact text is the qBittorrent failure contract used by Arr clients.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Fails.");
        await telemetryClient!.FlushAsync();
        var nativeEvents = new List<JsonElement>();
        foreach (var body in telemetryBodies)
        {
            using var document = JsonDocument.Parse(body);
            nativeEvents.AddRange(document.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone()));
        }
        var submissions = nativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.ExternalDownloadSubmissionProcessed.ToString())
            .ToArray();
        submissions.Length.ShouldBe(1);
        var submissionProperties = submissions.Single().GetProperty("properties");
        submissionProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Rejected.ToString());
        submissionProperties
            .GetProperty(TelemetryProperty.Stage)
            .GetString()
            .ShouldBe(TelemetryStage.SourceValidation.ToString());
        submissionProperties.GetProperty(TelemetryProperty.HashId).GetString().ShouldBe(torrentHash);
        submissionProperties.GetProperty(TelemetryProperty.MediaId).GetInt32().ShouldBe(movie.Id);
        submissionProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(movie.PlexLibraryId);
        submissionProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(movie.PlexServerId);
        submissionProperties.GetProperty(TelemetryProperty.Integrations.AssociatedTaskCount).GetInt32().ShouldBe(0);
        submissionProperties.TryGetProperty(TelemetryProperty.Integrations.CreatedTotal, out _).ShouldBeFalse();
        submissionProperties.TryGetProperty(TelemetryProperty.Integrations.CreatedReport, out _).ShouldBeFalse();
        using var afterContext = container.DbContext;
        (
            await afterContext
                .DownloadTaskMovieFile.OrderBy(file => file.Id)
                .Select(file => new
                {
                    file.Id,
                    file.ParentId,
                    file.HashId,
                    file.DownloadStatus,
                    file.RadarrIntegrationId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        (
            await afterContext
                .DownloadTaskMovie.OrderBy(task => task.Id)
                .Select(task => task.Id)
                .ToListAsync(CancellationToken)
        ).ShouldBe(parentIds);
        (await container.DownloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(movie.PlexServerId)).ShouldBeEmpty();
        (
            await afterContext.DownloadTaskTvShowEpisodeFile.Select(file => file.Id).ToListAsync(CancellationToken)
        ).ShouldBeEmpty();
    }
}
