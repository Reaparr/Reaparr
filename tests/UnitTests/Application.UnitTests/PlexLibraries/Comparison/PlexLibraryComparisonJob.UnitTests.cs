using System.Text.Json;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class PlexLibraryComparisonJobUnitTests : BaseUnitTest<PlexLibraryComparisonJob>
{
    [Test]
    public async Task ShouldRunMusicWriterAndPublishScope_WhenEligibleMusicPairExecutes()
    {
        // Arrange
        await SetupDatabase(
            963520,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 0;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.Outdated, false)
                    .SetProperty(y => y.SyncedAt, DateTime.UtcNow)
                    .SetProperty(y => y.SyncedContentChangedAt, y => y.ContentChangedAt),
            CancellationToken
        );
        await dbContext.PlexMusicArtists.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.PlexApiIdentityId, (string?)null)
                    .SetProperty(y => y.MusicBrainzArtistId, "00000000-0000-0000-0000-000000000201"),
            CancellationToken
        );
        var artists = await dbContext.PlexMusicArtists.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
        var context = new Mock<IJobExecutionContext>();
        context.SetupAllProperties();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken);
        context
            .SetupGet(x => x.MergedJobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(owned.Id, remote.Id).ToJobDataMap());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CompareMusicPlexLibraryCommand>(c =>
                        c.OwnedPlexLibraryId == owned.Id && c.RemotePlexLibraryId == remote.Id
                    ),
                    CancellationToken
                )
            )
            .Returns<ICommand<Result>, CancellationToken>(
                (cmd, ct) =>
                    new CompareMusicPlexLibraryCommandHandler(IDbContext, Log).ExecuteAsync(
                        (CompareMusicPlexLibraryCommand)cmd,
                        ct
                    )
            )
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(JobStatus.Completed);
        var scopes = await dbContext.PlexComparisonScopes.ToListAsync(CancellationToken);
        scopes
            .Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType))
            .ShouldBe([(remote.Id, owned.Id, PlexMediaType.MusicArtist)]);
        var hits = await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([(artists[0].Id, artists[1].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId)]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CompareMoviePlexLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CompareTvShowPlexLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    [Arguments(PlexMediaType.Movie)]
    public async Task ShouldRejectUnsupportedOrMixedPairWithoutDispatch_WhenMusicCannotBeCompared(
        PlexMediaType ownedType
    )
    {
        // Arrange
        await SetupDatabase(
            963521,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMusicLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await dbContext
            .PlexLibraries.Where(x => x.Id == libraries[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, ownedType), CancellationToken);
        if (ownedType != PlexMediaType.Movie)
            await dbContext
                .PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, ownedType), CancellationToken);
        var context = new Mock<IJobExecutionContext>();
        context.SetupAllProperties();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken);
        context
            .SetupGet(x => x.MergedJobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(libraries[1].Id, libraries[0].Id).ToJobDataMap());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CompareMusicPlexLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CompareMoviePlexLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CompareTvShowPlexLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.Movie, JobStatus.Completed)]
    [Arguments(PlexMediaType.TvShow, JobStatus.Completed)]
    [Arguments(PlexMediaType.MusicArtist, JobStatus.Completed)]
    [Arguments(PlexMediaType.Movie, JobStatus.Failed)]
    [Arguments(PlexMediaType.Movie, JobStatus.Cancelled)]
    public async Task ShouldPublishConsumableCompletionOnlyAfterSuccess_WhenComparisonLifecycleIsDelivered(
        PlexMediaType mediaType,
        JobStatus outcome
    )
    {
        // Arrange
        await SetupDatabase(
            963522,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = mediaType == PlexMediaType.Movie ? 1 : 0;
                config.PlexTvShowLibraryCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
                config.PlexMusicLibraryCount = mediaType == PlexMediaType.MusicArtist ? 1 : 0;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        var payload = new PlexLibraryComparisonJobPayload(owned.Id, remote.Id);
        var jobData = payload.ToJobDataMap();
        var context = new Mock<IJobExecutionContext>();
        context.SetupAllProperties();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken);
        context.SetupGet(x => x.MergedJobDataMap).Returns(jobData);
        context
            .SetupGet(x => x.JobDetail)
            .Returns(
                JobBuilder
                    .Create<PlexLibraryComparisonJob>()
                    .WithIdentity(PlexLibraryComparisonJob.GetJobKey(owned.Id, remote.Id))
                    .Build()
            );
        context.SetupGet(x => x.FireTimeUtc).Returns(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var executionData = new Dictionary<object, object>();
        context
            .Setup(x => x.Put(It.IsAny<object>(), It.IsAny<object>()))
            .Callback<object, object>((key, value) => executionData[key] = value);
        context.Setup(x => x.Get(It.IsAny<object>())).Returns<object>(key => executionData.GetValueOrDefault(key));
        var writerResult = outcome switch
        {
            JobStatus.Failed => Result.Fail("Comparison writer failed"),
            JobStatus.Cancelled => ResultExtensions.TaskIsCancelled("Comparison writer"),
            _ => Result.Ok(),
        };
        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x => x.Send(It.IsAny<CompareMoviePlexLibraryCommand>(), CancellationToken))
            .ReturnsAsync(writerResult);
        executor
            .Setup(x => x.Send(It.IsAny<CompareTvShowPlexLibraryCommand>(), CancellationToken))
            .ReturnsAsync(writerResult);
        executor
            .Setup(x => x.Send(It.IsAny<CompareMusicPlexLibraryCommand>(), CancellationToken))
            .ReturnsAsync(writerResult);
        var updates = new List<JobStatusUpdate<string>>();
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<string>>()))
            .Callback<JobStatusUpdate<string>>(updates.Add)
            .Returns(Task.CompletedTask);
        var listener = Mock.Create<AllJobListener>();

        // Act
        await listener.JobToBeExecuted(context.Object, CancellationToken);
        await Sut.Execute(context.Object);
        await listener.JobWasExecuted(context.Object, null, CancellationToken);

        // Assert
        updates.Select(x => x.Status).ShouldBe([JobStatus.Started, outcome]);
        var wire = updates[1].ToDTO();
        wire.JobType.ShouldBe(JobTypes.LibraryComparisonJob);
        if (outcome == JobStatus.Completed)
        {
            var completion = JsonSerializer.Deserialize<LibraryComparisonCompletedDTO>(
                wire.JsonString,
                DefaultJsonSerializerOptions.ConfigStandard
            )!;
            completion.AffectedLibraryIds.ShouldBe([owned.Id, remote.Id]);
            completion.MediaType.ShouldBe(mediaType);
        }
        else
        {
            using var json = JsonDocument.Parse(wire.JsonString);
            json.RootElement.TryGetProperty("affectedLibraryIds", out _).ShouldBeFalse();
            json.RootElement.GetProperty("ownedPlexLibraryId").GetInt32().ShouldBe(owned.Id);
            json.RootElement.GetProperty("remotePlexLibraryId").GetInt32().ShouldBe(remote.Id);
        }
        jobData.GetPayload<PlexLibraryComparisonJobPayload>().ShouldBe(payload);
    }
}
