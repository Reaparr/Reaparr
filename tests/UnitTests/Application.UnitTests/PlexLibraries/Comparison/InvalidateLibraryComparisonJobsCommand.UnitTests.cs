using Quartz;

namespace Reaparr.Application.UnitTests;

public class InvalidateLibraryComparisonJobsCommandUnitTests
    : BaseUnitTest<InvalidateLibraryComparisonJobsCommandHandler>
{
    [Test]
    public async Task ShouldDeleteExactComparisonJobKeysForAffectedLibrary()
    {
        // Arrange
        await SetupDatabase(91114);

        var affectedLibraryId = 12;
        var ownedJobKey = PlexLibraryComparisonJob.GetJobKey(affectedLibraryId, 34);
        var remoteJobKey = PlexLibraryComparisonJob.GetJobKey(56, affectedLibraryId);
        var scheduler = Mock.Mock<IScheduler>();
        var ownedJobDetail = new Mock<IJobDetail>();
        ownedJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(affectedLibraryId, 34).ToJobDataMap());
        var remoteJobDetail = new Mock<IJobDetail>();
        remoteJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(56, affectedLibraryId).ToJobDataMap());
        var unaffectedJobKey = PlexLibraryComparisonJob.GetJobKey(7, 8);
        var unaffectedJobDetail = new Mock<IJobDetail>();
        unaffectedJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(7, 8).ToJobDataMap());

        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == JobTypes.LibraryComparisonJob.ToString()
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                new PagedResult<JobHeader>(
                    [
                        new JobHeader(
                            ownedJobKey,
                            null,
                            typeof(PlexLibraryComparisonJob).FullName!,
                            false,
                            true,
                            false,
                            true
                        ),
                        new JobHeader(
                            remoteJobKey,
                            null,
                            typeof(PlexLibraryComparisonJob).FullName!,
                            false,
                            true,
                            false,
                            true
                        ),
                        new JobHeader(
                            unaffectedJobKey,
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
            );
        scheduler.Setup(x => x.GetJobDetail(ownedJobKey, CancellationToken)).ReturnsAsync(ownedJobDetail.Object);
        scheduler.Setup(x => x.GetJobDetail(remoteJobKey, CancellationToken)).ReturnsAsync(remoteJobDetail.Object);
        scheduler
            .Setup(x => x.GetJobDetail(unaffectedJobKey, CancellationToken))
            .ReturnsAsync(unaffectedJobDetail.Object);
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == "test-scheduler"
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false));
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys =>
                        keys.Count == 2 && keys.Any(y => y == ownedJobKey) && keys.Any(y => y == remoteJobKey)
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync([ownedJobKey, remoteJobKey]);

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([affectedLibraryId]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        scheduler.VerifyAll();
    }

    [Test]
    public async Task ShouldInvalidateComparisonsAfterRunningJobIsRemoved_WhenBatchDeletionReportsMissingKey()
    {
        // Arrange
        await SetupDatabase(91115, config =>
        {
            config.PlexServerCount = 3;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var unrelatedLibrary = libraries[2];
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        foreach (var library in new[] { ownedLibrary, unrelatedLibrary })
        {
            var movie = await GetLibraryMovieAsync(library.Id);
            await AddCurrentScopeAsync(remoteLibrary, library, PlexMediaType.Movie);
            dbContext.PlexMovieComparisons.Add(
                CreateMovieComparison(
                    remoteLibrary.Id,
                    library.Id,
                    remoteMovie.Id,
                    movie.Id,
                    PlexMediaComparisonHitState.Matched
                )
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedBefore = new[] { ownedLibrary.Id, unrelatedLibrary.Id };
        (
            await dbContext
                .PlexMovieComparisons.OrderBy(x => x.OwnedPlexLibraryId)
                .Select(x => x.OwnedPlexLibraryId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(expectedBefore);
        (
            await dbContext
                .PlexComparisonScopes.OrderBy(x => x.OwnedPlexLibraryId)
                .Select(x => x.OwnedPlexLibraryId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(expectedBefore);
        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        var scheduler = Mock.Mock<IScheduler>();
        var isExecuting = true;
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => isExecuting,
            () => isExecuting = false,
            interruptSucceeds: true
        );
        scheduler.Setup(x => x.Exists(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => isExecuting);
        scheduler
            .Setup(x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), CancellationToken))
            .ReturnsAsync(new List<JobKey>());

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .PlexMovieComparisons.OrderBy(x => x.OwnedPlexLibraryId)
                .Select(x => x.OwnedPlexLibraryId)
                .ToListAsync(CancellationToken)
        ).ShouldBe([unrelatedLibrary.Id]);
        (
            await dbContext
                .PlexComparisonScopes.OrderBy(x => x.OwnedPlexLibraryId)
                .Select(x => x.OwnedPlexLibraryId)
                .ToListAsync(CancellationToken)
        ).ShouldBe([unrelatedLibrary.Id]);
        scheduler.Verify(x => x.Interrupt(jobKey, CancellationToken), Times.Once);
        scheduler.Verify(
            x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { jobKey })),
                    CancellationToken
                ),
            Times.Once
        );
        scheduler.Verify(x => x.Exists(jobKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task ShouldPreserveComparisonResults_WhenRunningJobCannotBeCancelled()
    {
        // Arrange
        await SetupDatabase(91116, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        var dbContext = IDbContext;
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

        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        var scheduler = Mock.Mock<IScheduler>();
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => true,
            static () => { },
            interruptSucceeds: false
        );

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        (await dbContext.PlexMovieComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(1);
        scheduler.Verify(
            x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveComparisonResults_WhenSchedulerDeletionFails(bool throws)
    {
        // Arrange
        await SetupDatabase(91117, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
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

        var scheduler = Mock.Mock<IScheduler>();
        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => false,
            static () => { },
            interruptSucceeds: true
        );
        var deletion = scheduler.Setup(x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), CancellationToken));
        if (throws)
            deletion.ThrowsAsync(new InvalidOperationException("Scheduler deletion unavailable"));
        else
            deletion.ReturnsAsync(new List<JobKey>());

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.PlexMovieComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(1);
        scheduler.Verify(
            x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 1 && keys.Contains(jobKey)),
                    CancellationToken
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ShouldDeleteOnlyAffectedComparisonResults_WhenCancellationAlreadyDeletedTheQuartzJob()
    {
        // Arrange
        await SetupDatabase(91118, config =>
        {
            config.PlexServerCount = 4;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var controlRemoteLibrary = libraries[2];
        var controlOwnedLibrary = libraries[3];
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        await AddCurrentScopeAsync(controlRemoteLibrary, controlOwnedLibrary, PlexMediaType.Movie);
        var affectedComparison = CreateMovieComparison(
            remoteLibrary.Id,
            ownedLibrary.Id,
            (await GetLibraryMovieAsync(remoteLibrary.Id)).Id,
            (await GetLibraryMovieAsync(ownedLibrary.Id)).Id,
            PlexMediaComparisonHitState.Matched
        );
        var controlComparison = CreateMovieComparison(
            controlRemoteLibrary.Id,
            controlOwnedLibrary.Id,
            (await GetLibraryMovieAsync(controlRemoteLibrary.Id)).Id,
            (await GetLibraryMovieAsync(controlOwnedLibrary.Id)).Id,
            PlexMediaComparisonHitState.Matched
        );
        dbContext.PlexMovieComparisons.AddRange(affectedComparison, controlComparison);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexMovieComparisons.CountAsync(CancellationToken)).ShouldBe(2);
        var retainedScopeIds = await dbContext
            .PlexComparisonScopes.Where(x => x.OwnedPlexLibraryId == controlOwnedLibrary.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        retainedScopeIds.Count.ShouldBe(1);

        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        var scheduler = Mock.Mock<IScheduler>();
        var isExecuting = true;
        var jobExists = true;
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => isExecuting,
            () => isExecuting = false,
            interruptSucceeds: true
        );
        scheduler
            .Setup(x => x.Exists(jobKey, CancellationToken))
            .ReturnsAsync(() => jobExists)
            .Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x => x.DeleteJob(jobKey, CancellationToken))
            .Callback(() => jobExists = false)
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Interrupt(jobKey, CancellationToken))
            .Callback(() => isExecuting = false)
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 1 && keys.Contains(jobKey)),
                    CancellationToken
                )
            )
            .ReturnsAsync(new List<JobKey>())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        isExecuting.ShouldBeFalse();
        jobExists.ShouldBeFalse();
        var retainedComparisons = await dbContext
            .PlexMovieComparisons.Select(x => new
            {
                x.Id,
                x.RemotePlexLibraryId,
                x.OwnedPlexLibraryId,
                x.RemotePlexMediaId,
                x.OwnedPlexMediaId,
            })
            .ToListAsync(CancellationToken);
        retainedComparisons.ShouldBe([
            new
            {
                controlComparison.Id,
                controlComparison.RemotePlexLibraryId,
                controlComparison.OwnedPlexLibraryId,
                controlComparison.RemotePlexMediaId,
                controlComparison.OwnedPlexMediaId,
            },
        ]);
        (await dbContext.PlexComparisonScopes.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            retainedScopeIds
        );
        scheduler.Verify();
    }

    private static void ConfigureAffectedJob(
        Mock<IScheduler> scheduler,
        JobKey jobKey,
        PlexLibraryComparisonJobPayload payload,
        Func<bool> isExecuting,
        Action onInterrupt,
        bool interruptSucceeds
    )
    {
        const string schedulerInstanceId = "test-scheduler";
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload.ToJobDataMap());
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(schedulerInstanceId);
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == JobTypes.LibraryComparisonJob.ToString()
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<JobHeader>(
                    [new JobHeader(jobKey, null, typeof(PlexLibraryComparisonJob).FullName!, false, true, false, true)],
                    false
                )
            );
        scheduler.Setup(x => x.GetJobDetail(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(jobDetail.Object);
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == schedulerInstanceId
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
                new PagedResult<FireInstance>(
                    isExecuting()
                        ?
                        [
                            new FireInstance(
                                $"fire-{jobKey.Name}",
                                new TriggerKey(jobKey.Name, jobKey.Group),
                                jobKey,
                                schedulerInstanceId,
                                FireInstanceState.Executing,
                                DateTimeOffset.UnixEpoch,
                                DateTimeOffset.UnixEpoch,
                                null
                            ),
                        ]
                        : [],
                    false
                )
            );
        scheduler.Setup(x => x.Exists(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(query =>
                        query.Job == jobKey && query.Skip == 0 && query.Take == PagedQuery.DefaultTake
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<TriggerHeader>(
                    [
                        new TriggerHeader(
                            new TriggerKey("comparison-trigger", "test"),
                            jobKey,
                            null,
                            "SIMPLE",
                            TriggerState.Normal,
                            DateTimeOffset.UnixEpoch,
                            null,
                            DateTimeOffset.UnixEpoch,
                            null,
                            null,
                            5,
                            null,
                            null,
                            0
                        ),
                    ],
                    false
                )
            );
        scheduler.Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scheduler
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .Callback<JobKey, CancellationToken>((_, _) => onInterrupt())
            .ReturnsAsync(interruptSucceeds);
    }
}
