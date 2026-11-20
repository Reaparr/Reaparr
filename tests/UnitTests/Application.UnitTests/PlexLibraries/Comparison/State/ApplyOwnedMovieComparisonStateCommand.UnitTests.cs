using Quartz;

namespace Reaparr.Application.UnitTests;

public class ApplyOwnedMovieComparisonStateCommandUnitTests : BaseCommandUnitTest<ApplyOwnedMovieComparisonStateCommand>
{
    [Test]
    public async Task ShouldMarkOwned_WhenCurrentRemoteScopeHasNoUpgradeHit()
    {
        // Arrange
        await SetupDatabase(64, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 10, 5, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(ownedMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMovieComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Owned.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkPending_WhenNoCurrentRemoteScopeAndComparisonIsQueued()
    {
        // Arrange
        await SetupDatabase(65, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 10, 5, 0, DateTimeKind.Utc));

        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
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
                                PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id),
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

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(ownedMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMovieComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Pending.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkHigherQuality_WhenCurrentRemoteScopeHasUpgradeHit()
    {
        // Arrange
        await SetupDatabase(66, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 10, 5, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(ownedMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMovieComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
    }

    [Test]
    public async Task ShouldLeaveNotCompared_WhenOnlyCompletedQueueExistsWithoutCurrentScope()
    {
        // Arrange
        await SetupDatabase(67, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([], false)));

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(ownedMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMovieComparisonStateCommand(items, ownedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
    }
}
