using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Threading.Tasks;
using PostHog;
using Quartz;
using Reaparr.Identity.Contracts;
using Serilog.Core;
using Serilog.Events;

namespace Reaparr.Application.UnitTests;

public class TelemetryUnitTests : BaseUnitTest
{
    private const string IDENTITY = "5e2c5c48-aa0f-4f21-8c65-94e72b2bd389";
    private const string IDENTITY_NAME = "Amber Meadow Marill · 5e2c5c48";
    private const string HOST = "https://telemetry.example.test";

    [Test]
    public async Task ShouldKeepCreatedNodeCountSeparateFromCorrelatedRoots_WhenCreationIsReported()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var rootTaskIds = new[] { Guid.Parse("2179daa8-5226-448e-b132-f407d3568b41") };
        var report = new DownloadTaskCreationReport
        {
            TvShows = 1,
            Seasons = 2,
            Episodes = 5,
            RootTaskIds = rootTaskIds,
        };
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.DownloadCreationProcessed(new CreateDownloadTasksRequest([]), Result.Ok(report));

        // Assert
        var captured = events.Single();
        captured.Name.ShouldBe(nameof(TelemetryEvent.DownloadCreationProcessed));
        captured.Properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        captured.Properties[TelemetryProperty.Downloads.CreatedTotal].ShouldBe(8);
        ((IEnumerable<Guid>)captured.Properties[TelemetryProperty.Downloads.RootTaskIds]).ShouldBe(rootTaskIds);
        captured.Properties.ShouldNotContainKey(TelemetryProperty.Downloads.CompletionElapsedMilliseconds);
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments(DownloadStatus.Completed, true)]
    [Arguments(DownloadStatus.Downloading, false)]
    [Arguments(DownloadStatus.DownloadFinished, true)]
    [Arguments(DownloadStatus.ServerUnreachable, false)]
    [Arguments(DownloadStatus.Stopped, true)]
    public async Task ShouldDescribePersistedRootWithoutInventingTerminalFailures_WhenLifecycleChanges(
        DownloadStatus status,
        bool attributed
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var key = new DownloadTaskKey
        {
            Id = Guid.Parse("2179daa8-5226-448e-b132-f407d3568b41"),
            Type = DownloadTaskType.Movie,
            PlexServerId = 11,
            PlexLibraryId = 22,
        };
        var createdAt = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var observedAt = createdAt.AddMinutes(12);
        var integration = attributed
            ? new IntegrationIdentity(IntegrationType.Radarr, Guid.Parse("06fdf6ef-e3fc-457d-b35f-e3619e498d47"))
            : null;
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.DownloadLifecycleChanged(key, status, createdAt, integration, observedAt);

        // Assert
        var captured = events.Single();
        captured.Name.ShouldBe(nameof(TelemetryEvent.DownloadLifecycleChanged));
        captured.Properties[TelemetryProperty.TaskId].ShouldBe(key.Id);
        captured.Properties[TelemetryProperty.Downloads.TaskType].ShouldBe(nameof(DownloadTaskType.Movie));
        captured.Properties[TelemetryProperty.ServerId].ShouldBe(11);
        captured.Properties[TelemetryProperty.LibraryId].ShouldBe(22);
        captured.Properties[TelemetryProperty.Status].ShouldBe(status.ToString());
        captured.Properties[TelemetryProperty.CreatedAt].ShouldBe(createdAt);
        if (attributed)
        {
            captured.Properties[TelemetryProperty.IntegrationId].ShouldBe(integration!.Id);
            captured.Properties[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Radarr));
        }
        else
        {
            captured.Properties.ShouldNotContainKey(TelemetryProperty.IntegrationId);
            captured.Properties.ShouldNotContainKey(TelemetryProperty.Provider);
        }
        if (status == DownloadStatus.Completed)
        {
            captured.Properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
            captured.Properties[TelemetryProperty.Downloads.CompletionElapsedMilliseconds].ShouldBe(720000d);
        }
        else
        {
            captured.Properties.ShouldNotContainKey(TelemetryProperty.Outcome);
            captured.Properties.ShouldNotContainKey(TelemetryProperty.Downloads.CompletionElapsedMilliseconds);
        }
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments(TelemetryDiagnosticOrigin.Backend, TelemetryProtocol.BackendOrigin)]
    [Arguments(TelemetryDiagnosticOrigin.Frontend, TelemetryProtocol.FrontendOrigin)]
    public async Task ShouldUseExistingIdentityAndNativeContext_WhenEventsAreCaptured(
        TelemetryDiagnosticOrigin origin,
        string expectedOrigin
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
            build.InformationalVersion = "0.40.0-dev.1";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new ConcurrentQueue<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    It.Is<string>(name =>
                        name == TelemetryProtocol.DiagnosticEvent || name == nameof(TelemetryEvent.NotificationsCleared)
                    ),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Enqueue((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(3));
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        using var request = new Activity(nameof(ShouldUseExistingIdentityAndNativeContext_WhenEventsAreCaptured))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        const string session = "0197af2c-8533-71b3-a351-80da30db084a";
        ITelemetry facade = telemetry;
        var requestContext = facade.BeginRequest(session);

        // Act
        var diagnostic = telemetry.SendDiagnostic(origin);
        telemetry.NotificationsCleared(3);

        // Assert
        diagnostic.IsSuccess.ShouldBeTrue();
        diagnostic.Errors.Count.ShouldBe(0);
        diagnostic.Successes.Single().Message.ShouldBe("Diagnostic event accepted for sending");
        var properties = events.Single(x => x.Name == TelemetryProtocol.DiagnosticEvent).Properties;
        properties[TelemetryProperty.Origin].ShouldBe(expectedOrigin);
        properties[TelemetryProtocol.ReleaseChannel].ShouldBe(TelemetryProtocol.DevelopmentEnvironment);
        properties[TelemetryProtocol.RuntimeEnvironment]
            .ShouldBe(Mock.Container.Resolve<IAppRuntimeInfo>().DotnetEnvironment);
        properties.ShouldNotContainKey("environment");
        properties.ShouldNotContainKey("$internal_or_test_user");
        properties[TelemetryProperty.ReleaseVersion].ShouldBe("0.40.0-dev.1");
        properties[TelemetryProperty.RuntimeMode].ShouldBe("docker");
        properties[TelemetryProperty.Platform].ShouldBe("Linux");
        properties[TelemetryProperty.RuntimeIdentifier].ShouldBe("linux-x64");
        properties[TelemetryProtocol.ProcessPersonProfile].ShouldBe(true);
        properties[TelemetryProperty.TelemetryName].ShouldBe(IDENTITY_NAME);
        request.GetTagItem(TelemetryProperty.TelemetryName).ShouldBe(IDENTITY_NAME);
        properties.ShouldNotContainKey("$geoip_disable");
        properties[TelemetryProtocol.TraceId].ShouldBe(request.TraceId.ToHexString());

        properties[TelemetryProtocol.SpanId].ShouldBe(request.SpanId.ToHexString());
        properties[TelemetryProtocol.SessionId].ShouldBe(session);
        var businessEvent = events.Single(x => x.Name == nameof(TelemetryEvent.NotificationsCleared));
        businessEvent.Properties[TelemetryProperty.DeletedCount].ShouldBe(3);
        // Completed request scopes must not attribute later background work to the browser session.
        requestContext?.Dispose();
        request.GetTagItem(TelemetryProtocol.DistinctIdTag).ShouldBeNull();
        request.GetTagItem(TelemetryProperty.TelemetryName).ShouldBeNull();
        request.GetTagItem(TelemetryProtocol.SessionIdTag).ShouldBeNull();
        events.Clear();
        telemetry.NotificationsCleared(1);
        events.Single().Properties.ShouldNotContainKey(TelemetryProtocol.SessionId);
        new { Queue = "scheduled" }.ToJobDataMap().ContainsKey(TelemetryProtocol.SessionIdMetadata).ShouldBeFalse();
        user.Verify();
        client.Verify();
        client.Verify(x => x.FlushAsync(), Times.Never());
        client.Verify(x => x.Dispose(), Times.Never());
    }

    [Test]
    public void ShouldProvideCompleteOfflineSpeciesVocabulary()
    {
        // Arrange / Act
        var names = TelemetrySpecies.Names.Value;
        var ordered = TelemetryNicknameDataset.OrderedSpecies.Value;
        // Assert
        names.Count.ShouldBe(1313);
        names.ShouldContain("Bulbasaur");
        names.ShouldContain("Pecharunt");
        names.ShouldContain("Lamball");
        names.ShouldContain("Chillet");
        names.ShouldContain("Depresso");
        names.ShouldNotContain("Eye of Cthulhu");
        ordered.ShouldBe(names.Order(StringComparer.Ordinal).ToArray());
    }

    [Test]
    [Arguments(IDENTITY, IDENTITY_NAME)]
    [Arguments("5E2C5C48-AA0F-4F21-8C65-94E72B2BD389", IDENTITY_NAME)]
    [Arguments("5e2c5c48-aa0f-4f21-8c65-94e72b2bd388", "Swift Cloud Goomy · 5e2c5c48")]
    public async Task ShouldReturnBrowserBootstrap_WhenTelemetryIsConfiguredAndStartupIdentityIsCached(
        string identity,
        string expectedName
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
            build.InformationalVersion = "0.40.0-dev.1";
            build.RuntimeMode = "desktop";
            build.RuntimeIdentifier = "linux-x64";
            build.CurrentOS = OperatingSystemPlatform.Linux;
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    TelemetryProtocol.ProfileEvent,
                    It.Is<Dictionary<string, object>>(properties =>
                        (string)properties[TelemetryProfileProperty.Name] == expectedName
                    ),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();

        // Act
        var bootstrap = telemetry.GetBrowserBootstrap();

        // Assert
        bootstrap.ShouldNotBeNull();
        bootstrap.ApiHost.ShouldBe(HOST);
        bootstrap.ProjectToken.ShouldBe("phc_test");
        bootstrap.DistinctId.ShouldBe(identity);
        bootstrap.ReleaseVersion.ShouldBe("0.40.0-dev.1");
        bootstrap.ReleaseChannel.ShouldBe(TelemetryProtocol.DevelopmentEnvironment);
        bootstrap.RuntimeEnvironment.ShouldBe(Mock.Container.Resolve<IAppRuntimeInfo>().DotnetEnvironment);
        bootstrap.RuntimeMode.ShouldBe("desktop");
        bootstrap.RuntimeIdentifier.ShouldBe("linux-x64");
        bootstrap.Platform.ShouldBe(nameof(OperatingSystemPlatform.Linux));
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldPreserveTorrentIdentityAndOutcomes_WhenUsingMetadataObjects()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var integration = new IntegrationIdentity(
            IntegrationType.Radarr,
            Guid.Parse("06fdf6ef-e3fc-457d-b35f-e3619e498d47")
        );
        var metadata = new TorrentMetadataTelemetryDTO
        {
            Type = PlexMediaType.Movie,
            ServerId = 11,
            LibraryId = 22,
            MediaId = 33,
            DataId = 44,
            PartId = 55,
            PlexApiPartId = 66,
            VideoQuality = VideoQuality.HD,
        };
        var rootTaskId = Guid.Parse("2ed7cf50-6983-45f4-9d91-bca1c401b292");
        var report = new DownloadTaskCreationReport
        {
            Movies = 2,
            Episodes = 3,
            RootTaskIds = [rootTaskId],
        };
        var associationFailed = Result.Fail<int>("association failed");
        var associationCancelled = Result.Fail<int>(new ExceptionalError(new OperationCanceledException()));
        var associationFailedWithoutCreation = Result.Fail<int>(
            new ExceptionalError(new InvalidOperationException("association failed"))
        );
        var associationCancelledWithoutCreation = Result.Fail<int>(
            new ExceptionalError(new OperationCanceledException())
        );
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(8));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.TorrentMetadataGenerated(integration, metadata, "movie.mkv", 1048576, 262144, 4);
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "rejected-hash",
            null,
            null,
            TelemetryStage.SourceValidation
        );
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "accepted-hash",
            Result.Ok(report),
            Result.Ok(5),
            TelemetryStage.HashAssociation
        );
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "failed-association-hash",
            Result.Ok(report),
            associationFailed,
            TelemetryStage.HashAssociation
        );
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "cancelled-association-hash",
            Result.Ok(report),
            associationCancelled,
            TelemetryStage.HashAssociation
        );
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "failed-no-creation-hash",
            Result.Ok(new DownloadTaskCreationReport()),
            associationFailedWithoutCreation,
            TelemetryStage.HashAssociation
        );
        telemetry.ExternalDownloadSubmissionProcessed(
            integration,
            metadata,
            "cancelled-no-creation-hash",
            Result.Ok(new DownloadTaskCreationReport()),
            associationCancelledWithoutCreation,
            TelemetryStage.HashAssociation
        );

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.TorrentMetadataGenerated),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
                nameof(TelemetryEvent.ExternalDownloadSubmissionProcessed),
            ]);
        foreach (var captured in events)
        {
            captured.Properties[TelemetryProperty.IntegrationId].ShouldBe(integration.Id);
            captured.Properties[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Radarr));
            captured.Properties[TelemetryProperty.MediaType].ShouldBe(nameof(PlexMediaType.Movie));
            captured.Properties[TelemetryProperty.ServerId].ShouldBe(11);
            captured.Properties[TelemetryProperty.LibraryId].ShouldBe(22);
            captured.Properties[TelemetryProperty.MediaId].ShouldBe(33);
            captured.Properties[TelemetryProperty.DataId].ShouldBe(44);
            captured.Properties[TelemetryProperty.PartId].ShouldBe(55);
            captured.Properties[TelemetryProperty.PlexApiPartId].ShouldBe(66);
            captured.Properties[TelemetryProperty.Integrations.VideoQuality].ShouldBe(nameof(VideoQuality.HD));
        }
        var generated = events[0].Properties;
        generated[TelemetryProperty.FileName].ShouldBe("movie.mkv");
        generated[TelemetryProperty.FileSize].ShouldBe(1048576L);
        generated[TelemetryProperty.Integrations.PieceLength].ShouldBe(262144);
        generated[TelemetryProperty.Integrations.PieceCount].ShouldBe(4);
        var rejected = events[1].Properties;
        rejected[TelemetryProperty.HashId].ShouldBe("rejected-hash");
        rejected[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Rejected));
        rejected[TelemetryProperty.Stage].ShouldBe(nameof(TelemetryStage.SourceValidation));
        rejected[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(0);
        rejected.ContainsKey(TelemetryProperty.Integrations.CreatedTotal).ShouldBeFalse();
        rejected.ContainsKey(TelemetryProperty.Integrations.CreatedReport).ShouldBeFalse();
        var accepted = events[2].Properties;
        accepted[TelemetryProperty.HashId].ShouldBe("accepted-hash");
        accepted[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        accepted[TelemetryProperty.Stage].ShouldBe(nameof(TelemetryStage.HashAssociation));
        accepted[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(5);
        accepted[TelemetryProperty.Integrations.CreatedTotal].ShouldBe(5);
        ((IEnumerable<Guid>)accepted[TelemetryProperty.Downloads.RootTaskIds]).ShouldBe([rootTaskId]);
        var created = (Dictionary<string, object>)accepted[TelemetryProperty.Integrations.CreatedReport];
        created[TelemetryProperty.Integrations.CreatedMovies].ShouldBe(2);
        created[TelemetryProperty.Integrations.CreatedEpisodes].ShouldBe(3);
        var failedAssociation = events[3].Properties;
        failedAssociation[TelemetryProperty.HashId].ShouldBe("failed-association-hash");
        failedAssociation[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Partial));
        failedAssociation[TelemetryProperty.Stage].ShouldBe(nameof(TelemetryStage.HashAssociation));
        failedAssociation[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(0);
        failedAssociation[TelemetryProperty.Integrations.CreatedTotal].ShouldBe(5);
        var failedCreated = (Dictionary<string, object>)failedAssociation[TelemetryProperty.Integrations.CreatedReport];
        failedCreated[TelemetryProperty.Integrations.CreatedMovies].ShouldBe(2);
        failedCreated[TelemetryProperty.Integrations.CreatedEpisodes].ShouldBe(3);
        var cancelledAssociation = events[4].Properties;
        cancelledAssociation[TelemetryProperty.HashId].ShouldBe("cancelled-association-hash");
        cancelledAssociation[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Partial));
        cancelledAssociation[TelemetryProperty.Stage].ShouldBe(nameof(TelemetryStage.HashAssociation));
        cancelledAssociation[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(0);
        cancelledAssociation[TelemetryProperty.Integrations.CreatedTotal].ShouldBe(5);
        var cancelledCreated =
            (Dictionary<string, object>)cancelledAssociation[TelemetryProperty.Integrations.CreatedReport];
        cancelledCreated[TelemetryProperty.Integrations.CreatedMovies].ShouldBe(2);
        cancelledCreated[TelemetryProperty.Integrations.CreatedEpisodes].ShouldBe(3);
        var failedWithoutCreation = events[5].Properties;
        failedWithoutCreation[TelemetryProperty.HashId].ShouldBe("failed-no-creation-hash");
        failedWithoutCreation[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Failed));
        failedWithoutCreation[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(0);
        failedWithoutCreation[TelemetryProperty.Integrations.CreatedTotal].ShouldBe(0);
        var cancelledWithoutCreation = events[6].Properties;
        cancelledWithoutCreation[TelemetryProperty.HashId].ShouldBe("cancelled-no-creation-hash");
        cancelledWithoutCreation[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Cancelled));
        cancelledWithoutCreation[TelemetryProperty.Integrations.AssociatedTaskCount].ShouldBe(0);
        cancelledWithoutCreation[TelemetryProperty.Integrations.CreatedTotal].ShouldBe(0);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldAvoidIdentityAndSdkWork_WhenBuildIsUnconfigured()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = string.Empty;
            build.TelemetryProjectToken = string.Empty;
        });
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );

        // Act
        await telemetry.InitializeAsync();
        var bootstrap = telemetry.GetBrowserBootstrap();
        telemetry.NotificationsCleared(3);
        telemetry.CaptureException(new IOException("ordinary transport failure"), TelemetryActivity.Download);
        var diagnostic = telemetry.SendDiagnostic(TelemetryDiagnosticOrigin.Backend);
        using var jobScope = telemetry.BeginJob(
            TelemetryActivity.PlexAccessRefresh,
            (default, null, "unconfigured-refresh", "unconfigured-fire")
        );

        // Assert
        bootstrap.ShouldBeNull();
        jobScope.ShouldBeNull();
        diagnostic.IsFailed.ShouldBeTrue();
        diagnostic.Errors.Count.ShouldBe(1);
        diagnostic.FindStatusCode().ShouldBe(503);
        diagnostic.Errors.Single().Message.ShouldBe("Telemetry is unconfigured or disabled");
        user.Verify(x => x.GetTelemetryId(), Times.Never());
        client.Invocations.ShouldBeEmpty();
        telemetry.BeginRequest("session-data").ShouldBeNull();
        telemetry.StartActivity(TelemetryActivity.Download).ShouldBeNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepSdkFailureInsideTelemetry_WhenSdkCaptureFails(bool throws)
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    nameof(TelemetryEvent.NotificationsCleared),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(() => throws ? throw new IOException("private SDK transport details") : false)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.DiagnosticEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(() => throws ? throw new IOException("private SDK transport details") : false)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        await telemetry.InitializeAsync();

        // Act
        telemetry.NotificationsCleared(3);
        var diagnostic = telemetry.SendDiagnostic(TelemetryDiagnosticOrigin.Backend);

        // Assert
        diagnostic.IsFailed.ShouldBeTrue();
        diagnostic.Errors.Count.ShouldBe(1);
        diagnostic.FindStatusCode().ShouldBe(503);
        diagnostic.Errors.Single().Message.ShouldBe("Telemetry could not accept the event");
        diagnostic.Errors.OfType<ExceptionalError>().ShouldBeEmpty();
        client.Verify(
            x =>
                x.Capture(
                    IDENTITY,
                    nameof(TelemetryEvent.NotificationsCleared),
                    It.Is<Dictionary<string, object>>(p => (int)p[TelemetryProperty.DeletedCount] == 3),
                    null,
                    null,
                    null
                ),
            Times.Once()
        );
        client.Verify(
            x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.DiagnosticEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                ),
            Times.Once()
        );
        client.Verify();
        user.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldDisableAllTelemetry_WhenStartupIdentityLookupFailsOrReturnsEmpty(bool returnsEmpty)
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        const string requestLogMessage = "Request telemetry probe";
        const string jobLogMessage = "Job telemetry probe";
        var settings = new UserSettings();
        var settingsNotification = settings.SettingsUpdated.Take(1).ToTask();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        if (returnsEmpty)
            user.Setup(x => x.GetTelemetryId()).ReturnsAsync(string.Empty).Verifiable(Times.Once());
        else
            user.Setup(x => x.GetTelemetryId())
                .ThrowsAsync(new IOException("identity database unavailable"))
                .Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var sourceReference = source;
        var started = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = candidate => ReferenceEquals(candidate, sourceReference);
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = started.Add;
        ActivitySource.AddActivityListener(listener);
        var logs = new List<LogEvent>();
        var sink = new Mock<ILogEventSink>(MockBehavior.Strict);
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(logs.Add).Verifiable(Times.Exactly(2));
        await using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Filter.ByIncludingOnly(entry => entry.MessageTemplate.Text is requestLogMessage or jobLogMessage)
            .WriteTo.Sink(sink.Object)
            .CreateLogger();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            settings
        );
        await telemetry.InitializeAsync();
        using var request = new Activity(nameof(ShouldDisableAllTelemetry_WhenStartupIdentityLookupFailsOrReturnsEmpty))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // Act
        var diagnostic = telemetry.SendDiagnostic(TelemetryDiagnosticOrigin.Backend);
        telemetry.NotificationsCleared(3);
        telemetry.CaptureException(new IOException("native telemetry probe"), TelemetryActivity.Download);
        telemetry.CaptureErrors(
            Result.Fail(new ExceptionalError(new InvalidOperationException("nested native telemetry probe"))),
            TelemetryActivity.Download
        );
        using var requestScope = telemetry.BeginRequest("request-session");
        logger.Information(requestLogMessage);
        using var jobScope = telemetry.BeginJob(
            TelemetryActivity.BackgroundJob,
            (default, "job-session", "job-key", "fire-instance")
        );
        logger.Information(jobLogMessage);
        using var manualSpan = telemetry.StartActivity(TelemetryActivity.Download);
        settings.LanguageSettings.Language = "fr-FR";
        await settingsNotification.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        diagnostic.IsFailed.ShouldBeTrue();
        diagnostic.Errors.Count.ShouldBe(1);
        diagnostic.FindStatusCode().ShouldBe(503);
        diagnostic.Errors.Single().Message.ShouldBe("Telemetry could not accept the event");
        telemetry.GetBrowserBootstrap().ShouldBeNull();
        requestScope.ShouldBeNull();
        jobScope.ShouldBeNull();
        manualSpan.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(request);
        request.GetTagItem(TelemetryProtocol.DistinctIdTag).ShouldBeNull();
        request.GetTagItem(TelemetryProtocol.SessionIdTag).ShouldBeNull();
        request.Status.ShouldBe(ActivityStatusCode.Unset);
        started.ShouldBeEmpty();
        logs.Count.ShouldBe(2);
        logs.ShouldAllBe(log => !log.Properties.ContainsKey(TelemetryProtocol.DistinctIdTag));
        logs.ShouldAllBe(log => !log.Properties.ContainsKey(TelemetryProtocol.SessionIdTag));
        logs.ShouldAllBe(log => !log.Properties.ContainsKey(TelemetryProperty.JobName));
        logs.ShouldAllBe(log => !log.Properties.ContainsKey(TelemetryProperty.FireInstanceId));
        client.Invocations.ShouldBeEmpty();
        user.Verify();
        sink.Verify();
    }

    [Test]
    public async Task ShouldPublishDetachedCurrentSettings_WhenNotificationsChangeAndTelemetryIsDisposed()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        const string keepPath = "settings.downloadManagerSettings.keepCompletedInDownloadFolder";
        const string schedulePath = "settings.downloadManagerSettings.downloadSchedule";
        var settings = new UserSettings
        {
            DownloadManagerSettings =
            {
                KeepCompletedInDownloadFolder = true,
                DownloadSchedule = new DownloadSchedule
                {
                    Enabled = true,
                    Days = new Dictionary<string, Dictionary<string, int?>>
                    {
                        ["Monday"] = new() { ["10:00"] = 123, ["18:00"] = null },
                    },
                },
            },
            NetworkSettings = { ReverseProxyUrl = "http://192.168.4.20:5000/reaparr" },
        };
        var first = new TaskCompletionSource<Dictionary<string, object>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var second = new TaskCompletionSource<Dictionary<string, object>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var changes = new ConcurrentQueue<Dictionary<string, object>>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    nameof(TelemetryEvent.SettingsChanged),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, _, properties, _, _, _) =>
                {
                    changes.Enqueue(properties!);
                    if (!((JsonElement)properties![keepPath]).GetBoolean())
                        first.TrySetResult(properties);
                    else if (
                        ((JsonElement)properties[schedulePath])
                            .GetProperty("days")
                            .GetProperty("Monday")
                            .GetProperty("10:00")
                            .GetInt32() == 456
                    )
                        second.TrySetResult(properties);
                }
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            settings
        );
        await telemetry.InitializeAsync();

        // Act
        settings.DownloadManagerSettings.KeepCompletedInDownloadFolder = false;
        var firstCapture = await first.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        settings.DownloadManagerSettings.DownloadSchedule.Days["Monday"]["10:00"] = 456;
        settings.DownloadManagerSettings.KeepCompletedInDownloadFolder = true;
        var secondCapture = await second.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        telemetry.Dispose();
        var nextNotification = settings.SettingsUpdated.Take(1).ToTask();
        var countAtDisposal = changes.Count;
        settings.LanguageSettings.Language = "fr-FR";
        await nextNotification.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        var firstProfile = (Dictionary<string, object>)firstCapture[TelemetryProtocol.PersonProperties];
        ((JsonElement)firstCapture[keepPath]).GetBoolean().ShouldBeFalse();
        ((JsonElement)firstProfile[keepPath]).GetBoolean().ShouldBeFalse();
        ((JsonElement)firstProfile[schedulePath])
            .GetProperty("days")
            .GetProperty("Monday")
            .GetProperty("10:00")
            .GetInt32()
            .ShouldBe(123);
        ((JsonElement)firstProfile[schedulePath])
            .GetProperty("days")
            .GetProperty("Monday")
            .GetProperty("18:00")
            .ValueKind.ShouldBe(JsonValueKind.Null);
        ((JsonElement)firstProfile["settings.networkSettings.reverseProxyUrl"])
            .GetString()
            .ShouldBe("http://192.168.4.20:5000/reaparr");
        ((JsonElement)secondCapture[keepPath]).GetBoolean().ShouldBeTrue();
        ((JsonElement)secondCapture[schedulePath])
            .GetProperty("days")
            .GetProperty("Monday")
            .GetProperty("10:00")
            .GetInt32()
            .ShouldBe(456);
        firstProfile[TelemetryProfileProperty.Name].ShouldBe(IDENTITY_NAME);
        firstProfile
            .Keys.Where(key => key != TelemetryProfileProperty.Name)
            .ShouldAllBe(key => key.StartsWith(TelemetryProfileProperty.SettingsPrefix, StringComparison.Ordinal));
        changes.Count.ShouldBe(countAtDisposal);
        user.Verify();
        client.Verify();
        client.Verify(
            x =>
                x.IdentifyAsync(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRestoreAmbientContext_WhenWorkerParentIsSupplied(bool hasParent)
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var sourceReference = source;
        using var listener = new ActivityListener();
        listener.ShouldListenTo = candidate => ReferenceEquals(candidate, sourceReference);
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        using var ambient = new Activity(nameof(ShouldRestoreAmbientContext_WhenWorkerParentIsSupplied))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var parent = hasParent
            ? new ActivityContext(
                ActivityTraceId.CreateFromString("0123456789abcdef0123456789abcdef"),
                ActivitySpanId.CreateFromString("0123456789abcdef"),
                ActivityTraceFlags.Recorded
            )
            : default;
        const string session = "session-supplied-by-native-client";

        // Act
        using (telemetry.BeginJob(TelemetryActivity.LibrarySync, (parent, session, "sync.17.42", "fire-1")))
        {
            var worker = Activity.Current;
            await Task.Yield();

            // Assert
            worker.ShouldNotBeNull();
            Activity.Current.ShouldBeSameAs(worker);
            worker.Kind.ShouldBe(ActivityKind.Consumer);
            worker.TraceId.ShouldNotBe(ambient.TraceId);
            worker.ParentSpanId.ShouldBe(parent.SpanId);
            if (hasParent)
                worker.TraceId.ShouldBe(parent.TraceId);
            worker.GetTagItem(TelemetryProtocol.DistinctIdTag).ShouldBe(IDENTITY);
            worker.GetTagItem(TelemetryProperty.TelemetryName).ShouldBe(IDENTITY_NAME);
            worker.GetTagItem(TelemetryProtocol.SessionIdTag).ShouldBe(session);
            worker.GetTagItem(TelemetryProperty.JobName).ShouldBe("sync.17.42");
            worker.GetTagItem(TelemetryProperty.FireInstanceId).ShouldBe("fire-1");
        }
        Activity.Current.ShouldBeSameAs(ambient);
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCaptureTerminalFailureInJobContext_WhenRefreshFailsAfterAwait(bool throws)
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        const string capturedLogMessage = "Captured job exception";
        const string restoredLogMessage = "After job";
        var logs = new List<LogEvent>();
        var sink = new Mock<ILogEventSink>(MockBehavior.Strict);
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(logs.Add).Verifiable(Times.Exactly(2));
        await using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Filter.ByIncludingOnly(entry => entry.MessageTemplate.Text is capturedLogMessage or restoredLogMessage)
            .WriteTo.Sink(sink.Object)
            .CreateLogger();
        var loggerForCallback = logger;
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var sourceReference = source;
        var stopped = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = candidate => ReferenceEquals(candidate, sourceReference);
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = stopped.Add;
        ActivitySource.AddActivityListener(listener);
        var failure = new IOException("Account refresh failed after await");
        var captures = new List<(Exception Exception, Activity? Activity, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    IDENTITY,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x => x.CaptureException(failure, IDENTITY, It.IsAny<Dictionary<string, object>>(), null, null, null))
            .Callback<Exception, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (exception, _, properties, _, _, _) =>
                {
                    captures.Add((exception, Activity.Current, properties!));
                    loggerForCallback.Information(capturedLogMessage);
                }
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        using var ambient = new Activity(nameof(ShouldCaptureTerminalFailureInJobContext_WhenRefreshFailsAfterAwait))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var jobPropertyName = TelemetryProperty.JobName;
        using var ambientLogContext = Serilog.Context.LogContext.PushProperty(jobPropertyName, "scheduler");
        const string session = "native-refresh-session";
        var data = new JobDataMap
        {
            [TelemetryProtocol.TraceParentMetadata] = ambient.Id!,
            [TelemetryProtocol.SessionIdMetadata] = session,
        };
        var context = new Mock<IJobExecutionContext>();
        context
            .SetupGet(x => x.JobDetail)
            .Returns(
                JobBuilder
                    .Create<RefreshPlexAccountAccessJob>()
                    .WithIdentity(RefreshPlexAccountAccessJob.GetJobKey())
                    .Build()
            );
        context.SetupGet(x => x.MergedJobDataMap).Returns(data);
        context.SetupGet(x => x.FireInstanceId).Returns("refresh-fire");
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(x => x.Result);
        var executor = new Mock<ICommandExecutor>(MockBehavior.Strict);
        executor
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .Returns(async () =>
            {
                await Task.Yield();
                return throws
                    ? throw failure
                    : Result.Fail<List<RefreshPlexAccountAccessRapportDTO>>(new ExceptionalError(failure));
            })
            .Verifiable(Times.Once());
        var job = new RefreshPlexAccountAccessJob(logger, executor.Object, telemetry);

        // Act
        await job.Execute(context.Object);
        logger.Information(restoredLogMessage);

        // Assert
        var terminal = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        terminal.Status.ShouldBe(JobStatus.Failed);
        terminal.ErrorSummary.ShouldBe(failure.Message);
        var captured = captures.Single();
        captured.Exception.ShouldBeSameAs(failure);
        var worker = captured.Activity.ShouldNotBeNull();
        worker.OperationName.ShouldBe(nameof(TelemetryActivity.PlexAccessRefresh));
        worker.Kind.ShouldBe(ActivityKind.Consumer);
        worker.TraceId.ShouldBe(ambient.TraceId);
        worker.ParentSpanId.ShouldBe(ambient.SpanId);
        worker.SpanId.ShouldNotBe(ambient.SpanId);
        worker.Status.ShouldBe(ActivityStatusCode.Error);
        captured.Properties[TelemetryProtocol.TraceId].ShouldBe(worker.TraceId.ToHexString());
        captured.Properties[TelemetryProtocol.SpanId].ShouldBe(worker.SpanId.ToHexString());
        captured.Properties[TelemetryProtocol.SessionId].ShouldBe(session);
        stopped.ShouldBe([worker]);
        Activity.Current.ShouldBeSameAs(ambient);
        ambient.Status.ShouldBe(ActivityStatusCode.Unset);
        var terminalLog = logs.Single(x => x.MessageTemplate.Text == capturedLogMessage);
        terminalLog.Properties[TelemetryProtocol.DistinctIdTag].ShouldBeOfType<ScalarValue>().Value.ShouldBe(IDENTITY);
        terminalLog
            .Properties[TelemetryProperty.TelemetryName]
            .ShouldBeOfType<ScalarValue>()
            .Value.ShouldBe(IDENTITY_NAME);
        terminalLog.Properties[TelemetryProtocol.SessionIdTag].ShouldBeOfType<ScalarValue>().Value.ShouldBe(session);
        terminalLog
            .Properties[TelemetryProperty.JobName]
            .ShouldBeOfType<ScalarValue>()
            .Value.ShouldBe(RefreshPlexAccountAccessJob.GetJobKey().ToString());
        terminalLog
            .Properties[TelemetryProperty.FireInstanceId]
            .ShouldBeOfType<ScalarValue>()
            .Value.ShouldBe("refresh-fire");
        var restoredLog = logs.Single(x => x.MessageTemplate.Text == restoredLogMessage);
        restoredLog.Properties[TelemetryProperty.JobName].ShouldBeOfType<ScalarValue>().Value.ShouldBe("scheduler");
        restoredLog.Properties.ContainsKey(TelemetryProtocol.SessionIdTag).ShouldBeFalse();
        restoredLog.Properties.ContainsKey(TelemetryProtocol.DistinctIdTag).ShouldBeFalse();
        restoredLog.Properties.ContainsKey(TelemetryProperty.TelemetryName).ShouldBeFalse();
        user.Verify();
        client.Verify();
        executor.Verify();
        sink.Verify();
    }

    [Test]
    public async Task ShouldCaptureOriginalUnexpectedErrors_WhenResultsContainUnexpectedExceptions()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var captured = new List<Exception>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    IDENTITY,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<Exception, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (exception, _, _, _, _, _) => captured.Add(exception)
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        var failure = new IOException("Password=original-native-exception");
        var transportTimeout = new OperationCanceledException("transport timeout, not caller cancellation");
        using var operation = new Activity(
            nameof(ShouldCaptureOriginalUnexpectedErrors_WhenResultsContainUnexpectedExceptions)
        ).Start();

        // Act
        telemetry.CaptureErrors(Result.Fail("Expected rejection"), TelemetryActivity.Download);
        telemetry.CaptureErrors(
            ResultExtensions.TaskIsCancelled(
                nameof(ShouldCaptureOriginalUnexpectedErrors_WhenResultsContainUnexpectedExceptions)
            ),
            TelemetryActivity.Download
        );
        telemetry.CaptureErrors(
            Result.Fail(new Error("Terminal worker failure").CausedBy(new ExceptionalError(failure))),
            TelemetryActivity.Download
        );
        telemetry.CaptureException(transportTimeout, TelemetryActivity.Download);

        // Assert
        captured.ShouldBe(new Exception[] { failure, transportTimeout });
        captured[0].Message.ShouldBe("Password=original-native-exception");
        operation.Status.ShouldBe(ActivityStatusCode.Error);
        client.Verify(
            x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    IDENTITY,
                    It.Is<Dictionary<string, object>>(p =>
                        (string)p[TelemetryProperty.Stage] == nameof(TelemetryActivity.Download)
                    ),
                    null,
                    null,
                    null
                ),
            Times.Exactly(2)
        );
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectConnectionTestResult_WhenResultsHaveDifferentOutcomes()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var integrationId = Guid.Parse("f27bcb59-5b95-46a1-bf2a-8b8ad8ee5c26");
        var success = new ConnectionTestTelemetryDTO
        {
            Status = TestConnectionStatus.Success,
            HttpStatusCode = StatusCodes.Status200OK,
        };
        ResultBase successful = Result.Ok();
        ResultBase cancelled = ResultExtensions.TaskIsCancelled(
            nameof(ShouldProjectConnectionTestResult_WhenResultsHaveDifferentOutcomes)
        );
        ResultBase failed = Result.Fail("connection failed");
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(4));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.IntegrationConnectionTestFinished(IntegrationType.Sonarr, integrationId, success, successful);
        telemetry.IntegrationConnectionTestFinished(IntegrationType.Radarr, null, null, failed);
        telemetry.IntegrationConnectionTestFinished(IntegrationType.Radarr, null, null, cancelled);

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.IntegrationConnectionTestFinished),
                nameof(TelemetryEvent.IntegrationConnectionTestFinished),
                nameof(TelemetryEvent.IntegrationConnectionTestFinished),
            ]);
        var stored = events[0].Properties;
        stored[TelemetryProperty.Mode].ShouldBe(nameof(TelemetryMode.Stored));
        stored[TelemetryProperty.IntegrationId].ShouldBe(integrationId);
        stored[TelemetryProperty.Status].ShouldBe(nameof(TestConnectionStatus.Success));
        stored[TelemetryProperty.HttpStatusCode].ShouldBe(StatusCodes.Status200OK);
        stored[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        var explicitFailed = events[1].Properties;
        explicitFailed[TelemetryProperty.Mode].ShouldBe(nameof(TelemetryMode.Explicit));
        explicitFailed.ContainsKey(TelemetryProperty.IntegrationId).ShouldBeFalse();
        explicitFailed.ContainsKey(TelemetryProperty.Status).ShouldBeFalse();
        explicitFailed.ContainsKey(TelemetryProperty.HttpStatusCode).ShouldBeFalse();
        explicitFailed[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Rejected));
        var explicitCancelled = events[2].Properties;
        explicitCancelled[TelemetryProperty.Mode].ShouldBe(nameof(TelemetryMode.Explicit));
        explicitCancelled.ContainsKey(TelemetryProperty.IntegrationId).ShouldBeFalse();
        explicitCancelled.ContainsKey(TelemetryProperty.Status).ShouldBeFalse();
        explicitCancelled.ContainsKey(TelemetryProperty.HttpStatusCode).ShouldBeFalse();
        explicitCancelled[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Cancelled));
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments(TestConnectionStatus.InvalidApiKey, TelemetryOutcome.Rejected)]
    [Arguments(TestConnectionStatus.UrlIsInvalid, TelemetryOutcome.Rejected)]
    [Arguments(TestConnectionStatus.ConnectionFailed, TelemetryOutcome.Failed)]
    public async Task ShouldReportUnhealthyConnectionOutcome_WhenResultEnvelopeIsSuccessful(
        TestConnectionStatus status,
        TelemetryOutcome expectedOutcome
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var integrationId = Guid.Parse("0c0e9678-6a2e-4599-af78-056d1d8eb8e3");
        var report = new ConnectionTestTelemetryDTO
        {
            Status = status,
            HttpStatusCode = status == TestConnectionStatus.InvalidApiKey ? StatusCodes.Status401Unauthorized : null,
        };
        var result = Result.Ok(
            new TestConnectionResult
            {
                Status = status,
                HttpStatusCode = report.HttpStatusCode,
                TestedAt = new DateTime(2026, 11, 5, 13, 0, 0, DateTimeKind.Utc),
                ErrorMessage = "private upstream response",
            }
        );
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.IntegrationConnectionTestFinished(IntegrationType.Radarr, integrationId, report, result);

        // Assert
        var captured = events.Single();
        captured.Name.ShouldBe(nameof(TelemetryEvent.IntegrationConnectionTestFinished));
        captured.Properties[TelemetryProperty.IntegrationId].ShouldBe(integrationId);
        captured.Properties[TelemetryProperty.Status].ShouldBe(status.ToString());
        captured.Properties[TelemetryProperty.Outcome].ShouldBe(expectedOutcome.ToString());
        captured.Properties.ContainsValue("private upstream response").ShouldBeFalse();
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments(ExternalIndexerQueryType.Search)]
    [Arguments(ExternalIndexerQueryType.TvSearch)]
    [Arguments(ExternalIndexerQueryType.Movie)]
    public async Task ShouldProjectNormalizedIndexerRequestAndResponse_WhenRssAndSearchResultsAreCaptured(
        ExternalIndexerQueryType queryType
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var integration = new IntegrationIdentity(
            IntegrationType.Radarr,
            Guid.Parse("473cf359-8c22-4f5d-8641-2a6384b48c65")
        );
        const string indexerApiKey = "super-secret-indexer-key";
        var rssReport = new ExternalIndexerQueryReport
        {
            Integration = integration,
            Type = queryType,
            Mode = TelemetryMode.Rss,
            Query = string.Empty,
            ImdbId = string.Empty,
            TmdbId = 0,
            TvdbId = 0,
            Season = 0,
            Episode = 0,
            Categories = [2000],
            ReturnedItemCount = 2,
            TotalCount = 7,
        };
        var searchReport = new ExternalIndexerQueryReport
        {
            Integration = integration,
            Type = queryType,
            Mode = TelemetryMode.Search,
            Query = "The Matrix",
            ImdbId = "tt0133093",
            TmdbId = 603,
            TvdbId = 456,
            Season = 2,
            Episode = 3,
            Categories = [2000],
            ReturnedItemCount = 0,
            TotalCount = 0,
        };
        ResultBase successful = Result.Ok();
        ResultBase cancelled = Result.Fail(
            new ExceptionalError(new OperationCanceledException($"api_key={indexerApiKey}"))
        );
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(3));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.ExternalIndexerQueryFinished(rssReport, successful);
        telemetry.ExternalIndexerQueryFinished(searchReport, cancelled);

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.ExternalIndexerQueryFinished),
                nameof(TelemetryEvent.ExternalIndexerQueryFinished),
            ]);
        var rss = events[0].Properties;
        rss[TelemetryProperty.Mode].ShouldBe(nameof(TelemetryMode.Rss));
        rss[TelemetryProperty.Integrations.QueryType].ShouldBe(queryType.ToString());
        rss[TelemetryProperty.ReturnedItemCount].ShouldBe(2);
        rss[TelemetryProperty.TotalCount].ShouldBe(7);
        rss[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Radarr));
        rss[TelemetryProperty.IntegrationId].ShouldBe(integration.Id);
        rss[TelemetryProperty.Integrations.Categories].ShouldBeOfType<int[]>().ShouldBe([2000]);
        rss.ContainsKey("api_key").ShouldBeFalse();
        rss.ContainsKey("ApiKey").ShouldBeFalse();
        rss.ContainsValue(indexerApiKey).ShouldBeFalse();
        var failedSearch = events[1].Properties;
        failedSearch[TelemetryProperty.Mode].ShouldBe(nameof(TelemetryMode.Search));
        failedSearch[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Cancelled));
        failedSearch[TelemetryProperty.Integrations.QueryType].ShouldBe(queryType.ToString());
        failedSearch[TelemetryProperty.Query].ShouldBe("The Matrix");
        failedSearch[TelemetryProperty.Integrations.ImdbId].ShouldBe("tt0133093");
        failedSearch[TelemetryProperty.Integrations.TmdbId].ShouldBe(603);
        failedSearch[TelemetryProperty.Integrations.TvdbId].ShouldBe(456);
        failedSearch[TelemetryProperty.Integrations.Season].ShouldBe(2);
        failedSearch[TelemetryProperty.Integrations.Episode].ShouldBe(3);
        failedSearch.ContainsKey(TelemetryProperty.ReturnedItemCount).ShouldBeFalse();
        failedSearch.ContainsKey(TelemetryProperty.TotalCount).ShouldBeFalse();
        failedSearch.ContainsValue(indexerApiKey).ShouldBeFalse();
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectTypedIntegrationLifecycleInputs_WhenIntegrationEventsAreCaptured()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var sonarr = new SonarrIntegration
        {
            Id = Guid.Parse("fba8a1ef-7867-4d44-b2b6-6c35a7c0bd21"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.test",
            SonarrApiKey = "sonarr-secret",
            QBittorrentApiKey = "qbittorrent-secret",
            TorznabApiKey = "torznab-secret",
            Category = "tv",
            ExternalDownloadClientId = 11,
            ExternalIndexerId = 12,
            ProvisioningState = IntegrationProvisioningState.Configured,
            DownloadFolderId = 13,
        };
        var radarr = new RadarrIntegration
        {
            Id = Guid.Parse("a40c722e-2ba0-4e19-8acf-7afbd8994c8d"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.test",
            RadarrApiKey = "radarr-secret",
            QBittorrentApiKey = "qbittorrent-secret",
            TorznabApiKey = "torznab-secret",
            Category = "movies",
            ExternalDownloadClientId = 21,
            ExternalIndexerId = 22,
            ProvisioningState = IntegrationProvisioningState.ChangesPending,
            DownloadFolderId = 23,
        };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(4));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.IntegrationChanged(sonarr, TelemetryAction.Update, connectionIdentityChanged: true);
        telemetry.IntegrationDeleted(radarr, Result.Fail("remote cleanup failed"), localRemoved: true, forced: true);
        telemetry.IntegrationSetupFinished(
            sonarr,
            Result.Fail("setup rejected"),
            IntegrationSetupProgressStage.Validation
        );

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.IntegrationChanged),
                nameof(TelemetryEvent.IntegrationChanged),
                nameof(TelemetryEvent.IntegrationSetupFinished),
            ]);
        var changed = events[0].Properties;
        changed[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Sonarr));
        changed[TelemetryProperty.Configuration.Name].ShouldBe("Sonarr");
        changed[TelemetryProperty.Configuration.Url].ShouldBe("https://sonarr.example.test");
        changed[TelemetryProperty.Configuration.Category].ShouldBe("tv");
        changed[TelemetryProperty.Configuration.DownloadFolderId].ShouldBe(13);
        changed[TelemetryProperty.Configuration.ProvisioningState]
            .ShouldBe(nameof(IntegrationProvisioningState.Configured));
        changed[TelemetryProperty.Configuration.ExternalDownloadClientId].ShouldBe(11);
        changed[TelemetryProperty.Configuration.ExternalIndexerId].ShouldBe(12);
        changed[TelemetryProperty.Action].ShouldBe(nameof(TelemetryAction.Update));
        changed[TelemetryProperty.Integrations.ConnectionIdentityChanged].ShouldBe(true);
        changed.ContainsValue("sonarr-secret").ShouldBeFalse();
        changed.ContainsValue("qbittorrent-secret").ShouldBeFalse();
        changed.ContainsValue("torznab-secret").ShouldBeFalse();
        var deleted = events[1].Properties;
        deleted[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Radarr));
        deleted[TelemetryProperty.Action].ShouldBe(nameof(TelemetryAction.Delete));
        deleted[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Partial));
        deleted[TelemetryProperty.Integrations.RemoteCleanupOutcome].ShouldBe(nameof(TelemetryOutcome.Failed));
        deleted[TelemetryProperty.Integrations.LocalRemoved].ShouldBe(true);
        deleted[TelemetryProperty.Integrations.Forced].ShouldBe(true);
        deleted.ContainsValue("radarr-secret").ShouldBeFalse();
        var setup = events[2].Properties;
        setup[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Sonarr));
        setup[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Failed));
        setup[TelemetryProperty.Stage].ShouldBe(nameof(IntegrationSetupProgressStage.Validation));
        setup.ContainsValue("sonarr-secret").ShouldBeFalse();
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectTypedLibraryRequestsAndSyncLifecycle_WhenResultsIncludeFailures()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var createdAt = new DateTime(2026, 11, 5, 13, 0, 0, DateTimeKind.Utc);
        var queue = new LibrarySyncJobQueue
        {
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = createdAt,
            PlexLibraryId = 12,
            PlexServerId = 34,
            ForceMediaRefresh = false,
        };
        var mediaRequest = new MediaQueryTelemetryDTO
        {
            Search = "  alpha   beta ",
            MediaType = PlexMediaType.Movie,
            Page = 2,
            PageSize = 30,
            PlexLibraryId = 12,
            Sort = "title:asc",
        };
        var photoRequest = new PhotoSearchTelemetryDTO
        {
            Search = "  \"Café au lait\"   café  résumé 😀 ",
            Page = 3,
            PageSize = 40,
            Sort = SortType.Title,
            Direction = Domain.SortDirection.Desc,
        };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(9));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.MediaQueryFinished(mediaRequest, "owned", Result.Fail<PlexMediaStatisticsDTO>("media query failed"));
        telemetry.PhotoSearchFinished(photoRequest, Result.Fail<PhotoAssetPageDTO>("photo search failed"));
        telemetry.LibraryRefreshRequested(
            new LibraryRefreshTelemetryDTO
            {
                PlexLibraryId = 12,
                ForceLibrarySync = true,
                ForceMediaRefresh = false,
            },
            Result.Ok(),
            LibrarySyncJobStatus.Queued
        );
        telemetry.ServerMediaSyncRequested(
            new ServerMediaSyncTelemetryDTO
            {
                PlexServerId = 34,
                ForceLibrarySync = false,
                ForceMediaRefresh = true,
            },
            Result.Fail("server sync failed")
        );
        telemetry.LibrarySyncQueued(
            (PlexLibraryId: 12, PlexServerId: 34, CreatedAt: createdAt, ForceMediaRefresh: false),
            PlexMediaType.Movie,
            forceLibrarySync: true,
            trigger: TelemetryTrigger.Manual
        );
        telemetry.LibrarySyncFinished(
            queue,
            PlexMediaType.Movie,
            forceMediaRefresh: true,
            status: LibrarySyncJobStatus.Completed,
            offline: true
        );
        telemetry.LibrarySyncCancellationProcessed(42, Result.Ok(), interrupted: false, noOp: true);
        telemetry.PhotoSearchFinished(
            photoRequest with
            {
                Search = " \"quoted phrase\"  repeated   spaces résumé 😀 ",
            },
            ResultExtensions
                .Create400BadRequestResult("Search requires an effective title term")
                .ToResult<PhotoAssetPageDTO>()
        );

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.MediaQueryFinished),
                nameof(TelemetryEvent.PhotoSearchFinished),
                nameof(TelemetryEvent.LibraryRefreshRequested),
                nameof(TelemetryEvent.ServerMediaSyncRequested),
                nameof(TelemetryEvent.LibrarySyncQueued),
                nameof(TelemetryEvent.LibrarySyncFinished),
                nameof(TelemetryEvent.LibrarySyncCancellationProcessed),
                nameof(TelemetryEvent.PhotoSearchFinished),
            ]);
        var media = events[0].Properties;
        media[TelemetryProperty.MediaType].ShouldBe(nameof(PlexMediaType.Movie));
        media[TelemetryProperty.Libraries.Filter].ShouldBe("owned");
        media[TelemetryProperty.Page].ShouldBe(2);
        media[TelemetryProperty.PageSize].ShouldBe(30);
        media[TelemetryProperty.Libraries.Search].ShouldBe("  alpha   beta ");
        media.ContainsKey(TelemetryProperty.TotalCount).ShouldBeFalse();
        media.ContainsKey(TelemetryProperty.Libraries.MediaCount).ShouldBeFalse();
        media.ContainsKey(TelemetryProperty.Libraries.TotalMediaSize).ShouldBeFalse();
        var photo = events[1].Properties;
        photo[TelemetryProperty.Query].ShouldBe("  \"Café au lait\"   café  résumé 😀 ");
        photo[TelemetryProperty.Page].ShouldBe(3);
        photo[TelemetryProperty.PageSize].ShouldBe(40);
        photo[TelemetryProperty.Libraries.Sort].ShouldBe(nameof(SortType.Title));
        photo[TelemetryProperty.Libraries.Direction].ShouldBe(nameof(Domain.SortDirection.Desc));
        photo[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Rejected));
        photo.ContainsKey(TelemetryProperty.ReturnedItemCount).ShouldBeFalse();
        photo.ContainsKey(TelemetryProperty.TotalCount).ShouldBeFalse();
        photo.ContainsKey(TelemetryProperty.TotalBytes).ShouldBeFalse();
        var refresh = events[2].Properties;
        refresh[TelemetryProperty.LibraryId].ShouldBe(12);
        refresh[TelemetryProperty.Libraries.ForceLibrarySync].ShouldBe(true);
        refresh[TelemetryProperty.Libraries.ForceMediaRefresh].ShouldBe(false);
        refresh[TelemetryProperty.Libraries.QueueStatus].ShouldBe(nameof(LibrarySyncJobStatus.Queued));
        refresh[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        var server = events[3].Properties;
        server[TelemetryProperty.ServerId].ShouldBe(34);
        server[TelemetryProperty.Libraries.ForceLibrarySync].ShouldBe(false);
        server[TelemetryProperty.Libraries.ForceMediaRefresh].ShouldBe(true);
        server[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Rejected));
        var queued = events[4].Properties;
        queued[TelemetryProperty.ServerId].ShouldBe(34);
        queued[TelemetryProperty.LibraryId].ShouldBe(12);
        queued[TelemetryProperty.MediaType].ShouldBe(nameof(PlexMediaType.Movie));
        queued[TelemetryProperty.CreatedAt].ShouldBe(createdAt);
        queued[TelemetryProperty.Libraries.ForceLibrarySync].ShouldBe(true);
        queued[TelemetryProperty.Libraries.ForceMediaRefresh].ShouldBe(false);
        queued[TelemetryProperty.Trigger].ShouldBe(nameof(TelemetryTrigger.Manual));
        var finished = events[5].Properties;
        finished[TelemetryProperty.ServerId].ShouldBe(34);
        finished[TelemetryProperty.LibraryId].ShouldBe(12);
        finished[TelemetryProperty.CreatedAt].ShouldBe(createdAt);
        finished[TelemetryProperty.MediaType].ShouldBe(nameof(PlexMediaType.Movie));
        finished[TelemetryProperty.Libraries.ForceMediaRefresh].ShouldBe(true);
        finished[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        finished[TelemetryProperty.Libraries.Offline].ShouldBe(true);
        var cancellation = events[6].Properties;
        cancellation[TelemetryProperty.LibraryId].ShouldBe(42);
        cancellation[TelemetryProperty.ServerId].ShouldBe(0);
        cancellation[TelemetryProperty.Libraries.Interrupted].ShouldBe(false);
        cancellation[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.NoOp));
        cancellation.ContainsKey(TelemetryProperty.CreatedAt).ShouldBeFalse();
        var rejectedPhoto = events[7].Properties;
        rejectedPhoto[TelemetryProperty.Query].ShouldBe(" \"quoted phrase\"  repeated   spaces résumé 😀 ");
        rejectedPhoto[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Rejected));
        rejectedPhoto.ContainsKey(TelemetryProperty.ReturnedItemCount).ShouldBeFalse();
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldCaptureOnlyGrantedAndRevokedLibraryAccess_WhenRapportsContainMeaningfulChanges()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var refreshRunId = Guid.Parse("36791bc4-08eb-49b2-966e-85cd61ce2189");
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.LibraryAccessChanged(7, refreshRunId, []);
        telemetry.LibraryAccessChanged(
            7,
            refreshRunId,
            [new PlexLibraryAccessRapport("account", 31, "Home").AddUpdated(12, "Series")]
        );

        // Assert
        events.ShouldBeEmpty();

        // Act
        telemetry.LibraryAccessChanged(
            7,
            refreshRunId,
            [
                new PlexLibraryAccessRapport("account", 31, "Home").AddGranted(11, "Movies").AddUpdated(12, "Series"),
                new PlexLibraryAccessRapport("account", 32, "Remote").AddRevoked(13, "Photos"),
            ]
        );

        var change = events.ShouldHaveSingleItem();
        change.Name.ShouldBe(nameof(TelemetryEvent.LibraryAccessChanged));
        change.Properties[TelemetryProperty.AccountId].ShouldBe(7);
        change.Properties[TelemetryProperty.RefreshRunId].ShouldBe(refreshRunId);
        change.Properties[TelemetryProperty.Libraries.Granted].ShouldBe(1);
        change.Properties[TelemetryProperty.Libraries.Revoked].ShouldBe(1);
        change.Properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        var access = change
            .Properties[TelemetryProperty.Libraries.Access]
            .ShouldBeOfType<List<Dictionary<string, object>>>();
        access
            .Select(x => x[TelemetryProperty.Libraries.State])
            .ShouldBe([nameof(PlexAccessState.Granted), nameof(PlexAccessState.Revoked)]);
        access.Select(x => x[TelemetryProperty.LibraryId]).ShouldBe([11, 13]);
        access.Select(x => x[TelemetryProperty.ServerId]).ShouldBe([31, 32]);
        access.Select(x => x[TelemetryProperty.Libraries.LibraryName]).ShouldBe(["Movies", "Photos"]);
        access.Select(x => x[TelemetryProperty.Libraries.ServerName]).ShouldBe(["Home", "Remote"]);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectExternalDownloadRemovalReport_WhenCleanupIsPartial()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var integration = new IntegrationIdentity(
            IntegrationType.Radarr,
            Guid.Parse("cccd37e7-2e2b-46ac-a14d-4fae89b937be")
        );
        var report = new ExternalDownloadRemovalReport(3, allRequested: false, deleteFiles: false)
        {
            Result = Result.Fail("cleanup failed"),
            Stage = TelemetryStage.Cleanup,
            MatchedCount = 8,
            ActiveCount = 5,
            NonActiveCount = 3,
            EligibleCount = 6,
            DeletedCount = 6,
            StopFailureCount = 1,
            StopCancellationCount = 2,
            FileFailureCount = 3,
            FileCancellationCount = 4,
            CleanupFailureCount = 5,
        };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.ExternalDownloadRemovalProcessed(integration, report);

        // Assert
        events.Select(x => x.Name).ShouldBe([nameof(TelemetryEvent.ExternalDownloadRemovalProcessed)]);
        var properties = events[0].Properties;
        properties[TelemetryProperty.IntegrationId].ShouldBe(integration.Id);
        properties[TelemetryProperty.Provider].ShouldBe(nameof(IntegrationType.Radarr));
        properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Partial));
        properties[TelemetryProperty.Stage].ShouldBe(nameof(TelemetryStage.Cleanup));
        properties[TelemetryProperty.Integrations.RequestedHashCount].ShouldBe(3);
        properties[TelemetryProperty.Integrations.AllRequested].ShouldBe(false);
        properties[TelemetryProperty.MatchedCount].ShouldBe(8);
        properties[TelemetryProperty.Integrations.ActiveCount].ShouldBe(5);
        properties[TelemetryProperty.Integrations.NonActiveCount].ShouldBe(3);
        properties[TelemetryProperty.Integrations.EligibleCount].ShouldBe(6);
        properties[TelemetryProperty.DeletedCount].ShouldBe(6);
        properties[TelemetryProperty.DeleteFiles].ShouldBe(false);
        properties[TelemetryProperty.Integrations.StopFailureCount].ShouldBe(1);
        properties[TelemetryProperty.Integrations.StopCancellationCount].ShouldBe(2);
        properties[TelemetryProperty.Integrations.FileFailureCount].ShouldBe(3);
        properties[TelemetryProperty.Integrations.FileCancellationCount].ShouldBe(4);
        properties[TelemetryProperty.Integrations.CleanupFailureCount].ShouldBe(5);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldDerivePlexAccessRefreshRequestOutcomes_WhenResultsCoverEachScope()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var emptyReports = new List<RefreshPlexAccountAccessRapportDTO>();
        var reports = new List<RefreshPlexAccountAccessRapportDTO> { new(2, "Account") };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(9));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.PlexAccessRefreshRequested(TelemetryScope.Account, Result.Ok(emptyReports), accountId: 7);
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Account,
            Result.Ok(emptyReports),
            accountId: 7,
            serverId: 13
        );
        telemetry.PlexAccessRefreshRequested(TelemetryScope.Server, Result.Ok(emptyReports), serverId: 13);
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            Result.Fail<List<RefreshPlexAccountAccessRapportDTO>>("missing").Add404NotFoundError(),
            serverId: 13
        );
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            Result.Ok(emptyReports).WithError("server failed with no reports"),
            serverId: 13
        );
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            Result
                .Fail<List<RefreshPlexAccountAccessRapportDTO>>("disabled")
                .AddStatusCode(System.Net.HttpStatusCode.Conflict),
            serverId: 13
        );
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            Result.Ok(reports).WithError("one account failed"),
            serverId: 13,
            accountCount: 2
        );
        telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            Result.Ok(reports).WithError(new ExceptionalError(new OperationCanceledException())),
            serverId: 13,
            accountCount: 2
        );

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.PlexAccessRefreshRequested));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.NoOp),
                nameof(TelemetryOutcome.Accepted),
                nameof(TelemetryOutcome.NoOp),
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Cancelled),
            ]);
        events
            .Select(x => x.Properties[TelemetryProperty.Scope])
            .ShouldBe([
                nameof(TelemetryScope.Account),
                nameof(TelemetryScope.Account),
                nameof(TelemetryScope.Server),
                nameof(TelemetryScope.Server),
                nameof(TelemetryScope.Server),
                nameof(TelemetryScope.Server),
                nameof(TelemetryScope.Server),
                nameof(TelemetryScope.Server),
            ]);
        events.ShouldAllBe(x => (string)x.Properties[TelemetryProperty.Trigger] == nameof(TelemetryTrigger.Manual));
        events[0].Properties[TelemetryProperty.AccountId].ShouldBe(7);
        events[1].Properties[TelemetryProperty.ServerId].ShouldBe(13);
        events[0].Properties.ContainsKey(TelemetryProperty.Accounts.AccountCount).ShouldBeFalse();
        events[6].Properties[TelemetryProperty.Accounts.AccountCount].ShouldBe(2);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldDerivePlexAccessRefreshFinishedOutcomes_WhenResultsIncludeRapportData()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var rapport = new RefreshPlexAccountAccessRapportDTO(42, "Account")
        {
            Access =
            [
                new PlexServerAccessRapportDTO
                {
                    PlexServerId = 1,
                    PlexServerName = "One",
                    State = PlexAccessState.Granted,
                    IsServerOffline = false,
                    LibraryAccess = [],
                },
                new PlexServerAccessRapportDTO
                {
                    PlexServerId = 2,
                    PlexServerName = "Two",
                    State = PlexAccessState.Updated,
                    IsServerOffline = false,
                    LibraryAccess = [],
                },
                new PlexServerAccessRapportDTO
                {
                    PlexServerId = 3,
                    PlexServerName = "Three",
                    State = PlexAccessState.Revoked,
                    IsServerOffline = true,
                    LibraryAccess = [],
                },
            ],
        };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(9));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.PlexAccessRefreshValidationFinished(42, TelemetryTrigger.Manual, Result.Fail("token invalid"));
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Fail<RefreshPlexServerAccessRapport>("server unavailable")
        );
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Fail<PlexLibraryAccessRefreshResponse>("library unavailable")
        );
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Fail<RefreshPlexAccountAccessRapportDTO>("revoke unavailable")
        );
        telemetry.PlexAccessRefreshValidationFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Try((Action)(() => throw new OperationCanceledException()))
        );
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [13], Reports = [] })
        );
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Manual,
            Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [], Reports = [] })
        );
        telemetry.PlexAccessRefreshFinished(
            42,
            TelemetryTrigger.Scheduled,
            Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [], Reports = [] }),
            rapport
        );

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.PlexAccessRefreshFinished));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Succeeded),
            ]);
        events.ShouldAllBe(x => (int)x.Properties[TelemetryProperty.AccountId] == 42);
        events[0].Properties[TelemetryProperty.Trigger].ShouldBe(nameof(TelemetryTrigger.Manual));
        events[7].Properties[TelemetryProperty.Trigger].ShouldBe(nameof(TelemetryTrigger.Scheduled));
        var reportProperties = events[7].Properties;
        reportProperties[TelemetryProperty.Accounts.GrantedCount].ShouldBe(1);
        reportProperties[TelemetryProperty.Accounts.UpdatedCount].ShouldBe(1);
        reportProperties[TelemetryProperty.Accounts.RevokedCount].ShouldBe(1);
        reportProperties[TelemetryProperty.Accounts.AccessCount].ShouldBe(3);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectAggregateConnectionCheckResults_WhenResultsContainHealthStates()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var healthy = Result.Ok(new List<PlexServerStatus> { Status(successful: true) });
        var unhealthy = Result
            .Ok(new List<PlexServerStatus> { Status(successful: false), Status(successful: false) })
            .WithError("all connections failed");
        var unexpected = Result.Fail<List<PlexServerStatus>>("probe failed");
        var cancelled = Result.Fail<List<PlexServerStatus>>(new ExceptionalError(new OperationCanceledException()));
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(5));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.ConnectionCheckFinished(77, healthy);
        telemetry.ConnectionCheckFinished(77, unhealthy);
        telemetry.ConnectionCheckFinished(77, unexpected);
        telemetry.ConnectionCheckFinished(77, cancelled);

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.PlexConnectionCheckFinished));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Cancelled),
            ]);
        events
            .Select(x => x.Properties[TelemetryProperty.Health])
            .ShouldBe([
                nameof(TelemetryHealth.Healthy),
                nameof(TelemetryHealth.Unhealthy),
                nameof(TelemetryHealth.Unknown),
                nameof(TelemetryHealth.Unknown),
            ]);
        events.ShouldAllBe(x => (int)x.Properties[TelemetryProperty.ServerId] == 77);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectConnectionCheckHealth_WhenResultsUseTypedStatuses()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var healthy = Result.Ok(Status(successful: true));
        var unhealthy = Result.Ok(Status(successful: false));
        var failed = Result.Fail<PlexServerStatus>("probe failed");
        var cancelled = Result.Fail<PlexServerStatus>(new ExceptionalError(new OperationCanceledException()));
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(7));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.ConnectionCheckFinished(88, healthy);
        telemetry.ConnectionCheckFinished(88, unhealthy);
        telemetry.ConnectionCheckFinished(88, failed);
        telemetry.ConnectionCheckFinished(88, cancelled);
        telemetry.ConnectionCheckFinished(Result.Ok());
        telemetry.ConnectionCheckFinished(Result.Fail("url rejected"));

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.PlexConnectionCheckFinished));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Rejected),
            ]);
        events
            .Select(x => x.Properties[TelemetryProperty.Health])
            .ShouldBe([
                nameof(TelemetryHealth.Healthy),
                nameof(TelemetryHealth.Unhealthy),
                nameof(TelemetryHealth.Unknown),
                nameof(TelemetryHealth.Unknown),
                nameof(TelemetryHealth.Healthy),
                nameof(TelemetryHealth.Unknown),
            ]);
        events.Take(4).ShouldAllBe(x => (int)x.Properties[TelemetryProperty.Accounts.PlexServerConnectionId] == 88);
        events[0].Properties[TelemetryProperty.ServerId].ShouldBe(77);
        events[2].Properties[TelemetryProperty.ServerId].ShouldBeNull();
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectServerInspectionRequestedAndFinishedOutcomes_WhenRequestsAndFinishesAreCaptured()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(7));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.ServerInspectionRequested(31, Result.Ok());
        telemetry.ServerInspectionRequested(31, Result.Fail("already queued"));
        telemetry.ServerInspectionRequested(31, Result.Try((Action)(() => throw new OperationCanceledException())));
        telemetry.ServerInspectionFinished(31, Result.Ok());
        telemetry.ServerInspectionFinished(31, Result.Fail("inspection failed"));
        telemetry.ServerInspectionFinished(31, Result.Try((Action)(() => throw new OperationCanceledException())));

        // Assert
        events
            .Select(x => x.Name)
            .ShouldBe([
                nameof(TelemetryEvent.ServerInspectionRequested),
                nameof(TelemetryEvent.ServerInspectionRequested),
                nameof(TelemetryEvent.ServerInspectionRequested),
                nameof(TelemetryEvent.ServerInspectionFinished),
                nameof(TelemetryEvent.ServerInspectionFinished),
                nameof(TelemetryEvent.ServerInspectionFinished),
            ]);
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Accepted),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.Cancelled),
            ]);
        events.ShouldAllBe(x => (int)x.Properties[TelemetryProperty.ServerId] == 31);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldDeriveCredentialsUpdateOutcome_WhenResultAndChangedFieldsVary()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(6));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.CredentialsUpdateProcessed(Result.Fail("password update failed"), true, false);
        telemetry.CredentialsUpdateProcessed(Result.Fail("update rejected"), false, false);
        telemetry.CredentialsUpdateProcessed(Result.Ok(), false, false);
        telemetry.CredentialsUpdateProcessed(Result.Ok(), false, true);
        telemetry.CredentialsUpdateProcessed(
            Result.Try((Action)(() => throw new OperationCanceledException())),
            false,
            false
        );

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.CredentialsUpdateProcessed));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Partial),
                nameof(TelemetryOutcome.Failed),
                nameof(TelemetryOutcome.NoOp),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Failed),
            ]);
        events[0].Properties[TelemetryProperty.Actions.UsernameChanged].ShouldBe(true);
        events[0].Properties[TelemetryProperty.Actions.PasswordChanged].ShouldBe(false);
        events[3].Properties[TelemetryProperty.Actions.UsernameChanged].ShouldBe(false);
        events[3].Properties[TelemetryProperty.Actions.PasswordChanged].ShouldBe(true);
        user.Verify();
        client.Verify();
    }

    [Test]
    [Arguments("docker", true, false, false, false, nameof(TelemetryOutcome.Unsupported))]
    [Arguments("desktop", false, false, false, false, nameof(TelemetryOutcome.NoUpdate))]
    [Arguments("desktop", true, false, false, false, nameof(TelemetryOutcome.Failed))]
    [Arguments("desktop", false, false, true, true, nameof(TelemetryOutcome.Succeeded))]
    [Arguments("desktop", false, true, false, false, nameof(TelemetryOutcome.Cancelled))]
    public async Task ShouldDeriveUpdateDownloadOutcome_WhenBuildModeAndResultVary(
        string runtimeMode,
        bool failed,
        bool cancelled,
        bool hasVersion,
        bool pendingRestart,
        string expectedOutcome
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
            build.RuntimeMode = runtimeMode;
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        ResultBase result =
            cancelled ? Result.Try((Action)(() => throw new OperationCanceledException()))
            : failed ? Result.Try((Action)(() => throw new InvalidOperationException("download failed")))
            : Result.Ok();
        var targetVersion = hasVersion ? "9.9.9" : null;
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(2));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.UpdateDownloadFinished(result, targetVersion, hasVersion ? pendingRestart : null);

        // Assert
        var properties = events.Single(x => x.Name == nameof(TelemetryEvent.UpdateDownloadFinished)).Properties;
        properties[TelemetryProperty.Outcome].ShouldBe(expectedOutcome);
        properties.ContainsKey(TelemetryProperty.Version).ShouldBe(hasVersion);
        properties.ContainsKey(TelemetryProperty.Actions.PendingRestart).ShouldBe(hasVersion);
        if (hasVersion)
        {
            properties[TelemetryProperty.Version].ShouldBe("9.9.9");
            properties[TelemetryProperty.Actions.PendingRestart].ShouldBe(pendingRestart);
        }
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectPlexAuthenticationResult_WhenAuthenticationResultsContainCredentials()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        var signIn = new PlexSignInCommandResult
        {
            ClientId = "client",
            Username = "user@example.test",
            Password = "secret-password",
            Email = "user@example.test",
            Title = "User",
            PlexId = 12,
            Uuid = "uuid",
            AuthenticationToken = "secret-token",
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            Is2Fa = false,
        };
        var validation = new ValidatePlexTokenCommandResult
        {
            ClientId = signIn.ClientId,
            Username = signIn.Username,
            Email = signIn.Email,
            Title = signIn.Title,
            PlexId = signIn.PlexId,
            Uuid = signIn.Uuid,
            AuthenticationToken = signIn.AuthenticationToken,
            IsValidated = true,
            ValidatedAt = signIn.ValidatedAt,
            Is2Fa = false,
        };
        var validatedReport = new PlexAuthenticationTelemetryDTO { IsValidated = true };
        var rejectedReport = new PlexAuthenticationTelemetryDTO { IsValidated = false };
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(12));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.Credentials,
            null,
            Result.Ok(signIn),
            validatedReport
        );
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.Credentials,
            null,
            Result
                .Ok(signIn)
                .WithError(new PlexError("verification required") { Code = PlexErrorCodes.EnterVerificationCode }),
            validatedReport
        );
        telemetry.PlexAuthenticationProcessed(
            12,
            Result.Fail<ValidatePlexTokenCommandResult>("unauthorized").AddPlex401UnauthorizedError()
        );
        telemetry.PlexAuthenticationProcessed(12, Result.Try((Action)(() => throw new OperationCanceledException())));
        telemetry.PlexAuthenticationProcessed(TelemetryAuthenticationMethod.GenerateToken, 12, Result.Ok());
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.GenerateToken,
            12,
            Result
                .Fail("verification required")
                .WithError(new PlexError("verification required") { Code = PlexErrorCodes.EnterVerificationCode })
        );
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.GenerateToken,
            12,
            Result.Fail("unauthorized").AddStatusCode(System.Net.HttpStatusCode.Unauthorized)
        );
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.GenerateToken,
            12,
            Result.Try((Action)(() => throw new OperationCanceledException()))
        );
        telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.Credentials,
            null,
            Result.Try((Action)(() => throw new OperationCanceledException()))
        );
        telemetry.PlexAuthenticationProcessed(12, Result.Ok(validation), rejectedReport);
        telemetry.PlexAuthenticationProcessed(12, Result.Ok(validation), validatedReport);

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.PlexAuthenticationProcessed));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.NotRequired),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.NotRequired),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Cancelled),
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.Succeeded),
            ]);
        events[0].Properties[TelemetryProperty.Method].ShouldBe(nameof(TelemetryAuthenticationMethod.Credentials));
        events.ShouldAllBe(x => !x.Properties.ContainsKey(nameof(PlexSignInCommandResult.Password)));
        events.ShouldAllBe(x => !x.Properties.ContainsKey(nameof(ValidatePlexTokenCommandResult.AuthenticationToken)));
        events.ShouldAllBe(x => !x.Properties.ContainsKey(nameof(ValidatePlexTokenCommandResult.Username)));
        var payload = JsonSerializer.Serialize(events.Select(x => x.Properties));
        payload.ShouldNotContain(signIn.Password);
        payload.ShouldNotContain(signIn.AuthenticationToken);
        events[2].Properties[TelemetryProperty.AccountId].ShouldBe(12);
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldProjectAuthenticationResult_WhenOutcomeAndRememberMeVary()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = HOST;
            build.TelemetryProjectToken = "phc_test";
        });
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(IDENTITY, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null)
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Exactly(5));
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        await telemetry.InitializeAsync();
        events.Clear();

        // Act
        telemetry.AuthenticationProcessed(
            TelemetryAction.Login,
            Result.Fail("invalid credentials").AddStatusCode(System.Net.HttpStatusCode.Unauthorized)
        );
        telemetry.AuthenticationProcessed(
            TelemetryAction.Login,
            Result.Fail("account locked").AddStatusCode(System.Net.HttpStatusCode.Forbidden)
        );
        telemetry.AuthenticationProcessed(TelemetryAction.Login, Result.Ok(), rememberMe: true);
        telemetry.AuthenticationProcessed(TelemetryAction.Login, Result.Ok());

        // Assert
        events.Select(x => x.Name).ShouldAllBe(x => x == nameof(TelemetryEvent.AuthenticationProcessed));
        events
            .Select(x => x.Properties[TelemetryProperty.Outcome])
            .ShouldBe([
                nameof(TelemetryOutcome.Rejected),
                nameof(TelemetryOutcome.LockedOut),
                nameof(TelemetryOutcome.Succeeded),
                nameof(TelemetryOutcome.Succeeded),
            ]);
        events.ShouldAllBe(x => (string)x.Properties[TelemetryProperty.Action] == nameof(TelemetryAction.Login));
        events[2].Properties[TelemetryProperty.Actions.RememberMe].ShouldBe(true);
        events[3].Properties.ContainsKey(TelemetryProperty.Actions.RememberMe).ShouldBeFalse();
        user.Verify();
        client.Verify();
    }

    private static PlexServerStatus Status(bool successful) =>
        new()
        {
            IsSuccessful = successful,
            StatusCode = successful ? 200 : 503,
            StatusMessage = successful ? "OK" : "Unavailable",
            LastChecked = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PlexServerId = 77,
            PlexServerConnectionId = 88,
        };
}
