using Quartz;

namespace Reaparr.Application.UnitTests;

public class ApplyOwnedTvShowComparisonStateCommandUnitTests
    : BaseCommandUnitTest<ApplyOwnedTvShowComparisonStateCommand>
{
    [Test]
    public async Task ShouldMarkPartial_WhenOwnedTvShowHasMatchedRemoteShowWithMoreEpisodes()
    {
        // Arrange
        await SetupDatabase(63, config =>
        {
            config.PlexServerCount = 2;
            config.PlexTvShowLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 3;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 9, 5, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var ownedEpisodes = await GetLibraryEpisodesAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteTvShow.Id,
                ownedTvShow.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateTvShowItem(ownedTvShow) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedTvShowComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Partial.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkHigherQuality_WhenOwnedTvShowHasShowUpgradeAndAllEpisodesMatched()
    {
        // Arrange
        await SetupDatabase(68, config =>
        {
            config.PlexServerCount = 2;
            config.PlexTvShowLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 2;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 9, 5, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var ownedEpisodes = await GetLibraryEpisodesAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteTvShow.Id,
                ownedTvShow.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateTvShowItem(ownedTvShow) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedTvShowComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
    }

    [Test]
    public async Task ShouldUseStoredRemoteEpisodeCount_WhenEpisodeTableContainsAdditionalRows()
    {
        // Arrange
        await SetupDatabase(70, config =>
        {
            config.PlexServerCount = 2;
            config.PlexTvShowLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 3;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var ownedEpisodes = await GetLibraryEpisodesAsync(ownedLibrary.Id);
        await dbContext
            .PlexTvShows.Where(x => x.Id == remoteTvShow.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.GrandChildCount, 2), CancellationToken);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteTvShow.Id,
                ownedTvShow.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.AddRange(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            ),
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateTvShowItem(ownedTvShow) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedTvShowComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Owned.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkPending_WhenNoCurrentTvScopeAndComparisonIsProcessing()
    {
        // Arrange
        await SetupDatabase(69, config =>
        {
            config.PlexServerCount = 2;
            config.PlexTvShowLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.TvShowCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);

        var triggerKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(
                    new PagedResult<JobHeader>(
                        [
                            new JobHeader(
                                triggerKey,
                                null,
                                typeof(PlexLibraryComparisonJob).FullName!,
                                false,
                                true,
                                false,
                                true
                            ),
                        ],
                        false
                    )
                )
            );

        var items = new List<PlexMediaSlimDTO> { CreateTvShowItem(ownedTvShow) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedTvShowComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Pending.ToComparisonId());
    }
}
