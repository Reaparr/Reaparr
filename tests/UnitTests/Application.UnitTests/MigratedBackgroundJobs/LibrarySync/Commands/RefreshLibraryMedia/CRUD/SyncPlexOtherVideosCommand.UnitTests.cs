namespace Reaparr.Application.UnitTests;

public class SyncPlexOtherVideosCommandUnitTests : BaseCommandUnitTest<SyncPlexOtherVideosCommand>
{
    [Test]
    [Arguments("initial")]
    [Arguments("unchanged")]
    [Arguments("update")]
    [Arguments("force")]
    [Arguments("empty")]
    public async Task ShouldReconcileOnlySelectedLibrary_WithAccurateReportsAndOriginals(string scenario)
    {
        // Arrange
        await SetupDatabase(
            625201,
            x =>
            {
                x.PlexServerCount = 2;
                x.PlexOtherVideoLibraryCount = 2;
                x.OtherVideoCount = 2;
            }
        );
        var db = IDbContext;
        var library = await db.PlexLibraries.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var storedVideos = await db
            .PlexOtherVideos.AsNoTracking()
            .Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var videos = storedVideos.Select(video => ToIncomingVideo(db, video)).ToList();
        var originalIds = storedVideos.Select(x => x.Id).ToArray();
        var beforeData = await db
            .PlexOtherVideoData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiPartId,
                x.Size,
                x.OriginalFilename,
            })
            .ToListAsync(CancellationToken);
        var control = await db
            .PlexOtherVideos.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.MediaSize,
            })
            .ToListAsync(CancellationToken);
        var controlData = await db
            .PlexOtherVideoData.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        videos.Count.ShouldBe(2);
        beforeData.Count.ShouldBe(2);
        long addedSize = 0;
        if (scenario is "initial" or "force")
        {
            var originals = FakeData.GetPlexOtherVideoMediaData(new Seed(625211)).Generate(2);
            originals[0]
                .UpdateInitProperty(
                    nameof(BasePlexMediaData.PlexApiMediaId),
                    videos[0].MediaDataList.Single().PlexApiMediaId
                );
            originals[0].UpdateInitProperty(nameof(PlexOtherVideoMediaData.PartIndex), 1);
            videos[0].MediaDataList.AddRange(originals);
            addedSize = originals.Sum(x => x.Size);
            videos[0].MediaSize += addedSize;
        }
        if (scenario == "initial")
        {
            videos[0]
                .MediaDataList.First()
                .Streams.Add(
                    new PlexOtherVideoDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = 6252011,
                        StreamType = StreamType.Audio,
                        LanguageId = 0,
                        IsExternal = false,
                        SidecarFormat = "aac",
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "ja",
                            LanguageTag = "jpn",
                        },
                    }
                );
            await db.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(CancellationToken);
        }
        if (scenario == "unchanged")
        {
            videos[0].MediaSize = 1;
            videos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 1L);
        }
        if (scenario == "update")
        {
            videos[0].UpdatedAt = videos[0].UpdatedAt?.AddSeconds(1) ?? new DateTime(2026, 1, 1);
            videos[0].Title = "Updated original";
            videos[0].MediaSize = 123;
            videos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 123L);
            videos.RemoveAt(1);
        }
        if (scenario == "empty")
            videos.Clear();
        library.OtherVideos.Clear();
        library.OtherVideos.AddRange(videos);
        var expectedSize =
            scenario == "empty" ? 0
            : scenario == "update" ? 123
            : beforeData.Sum(x => x.Size) + addedSize;

        // Act
        var result = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library), scenario == "force")
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var expectedReport = scenario switch
        {
            "initial" => new CrudOtherVideosReport { CreatedOtherVideos = 2 },
            "force" => new CrudOtherVideosReport { CreatedOtherVideos = 2, DeletedOtherVideos = 2 },
            "update" => new CrudOtherVideosReport { UpdatedOtherVideos = 1, DeletedOtherVideos = 1 },
            "empty" => new CrudOtherVideosReport { DeletedOtherVideos = 2 },
            _ => new CrudOtherVideosReport { UnchangedOtherVideos = 2 },
        };
        result.Value.ShouldBe(expectedReport);
        var after = await db
            .PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToListAsync(CancellationToken);
        after.Select(x => x.PlexApiRatingKey).ShouldBe(videos.Select(x => x.PlexApiRatingKey).Order());
        after.ShouldAllBe(x => x.PlexLibraryId == library.Id && x.PlexServerId == library.PlexServerId);
        if (scenario is "unchanged" or "update")
            after.Select(x => x.Id).Order().ShouldBe(originalIds.Take(videos.Count).Order());
        if (scenario == "update")
            after.Single().Title.ShouldBe("Updated original");
        var afterData = await db
            .PlexOtherVideoData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiPartId,
                x.Size,
                x.OriginalFilename,
            })
            .ToListAsync(CancellationToken);
        if (scenario == "unchanged")
            afterData.ShouldBe(beforeData);
        else
            afterData
                .Select(x => (x.PlexOtherVideoId, x.PlexApiPartId, x.Size, x.OriginalFilename))
                .OrderBy(x => x.PlexApiPartId)
                .ShouldBe(
                    videos
                        .SelectMany(v =>
                            v.MediaDataList.Select(d => (v.Id, d.PlexApiPartId, d.Size, d.OriginalFilename))
                        )
                        .OrderBy(x => x.PlexApiPartId)
                );
        if (scenario == "initial")
        {
            videos.ShouldAllBe(x => x.Id > 0);
            videos.SelectMany(x => x.MediaDataList).ShouldAllBe(x => x.Id > 0);
            var createdData = videos[0].MediaDataList.First();
            var language = db
                .StreamLanguages.AsNoTracking()
                .Single(x => x.LanguageCode == "ja" && x.LanguageTag == "jpn");
            var persistedStream = await db
                .PlexOtherVideoDataStreams.AsNoTracking()
                .Where(x => x.MediaDataId == createdData.Id)
                .Select(x => new
                {
                    x.MediaDataId,
                    x.PlexApiStreamId,
                    x.StreamType,
                    x.LanguageId,
                    LanguageCode = x.Language.LanguageCode,
                    LanguageTag = x.Language.LanguageTag,
                    x.IsExternal,
                    x.SidecarFormat,
                })
                .SingleAsync(CancellationToken);
            persistedStream.ShouldBe(
                new
                {
                    MediaDataId = createdData.Id,
                    PlexApiStreamId = 6252011,
                    StreamType = StreamType.Audio,
                    LanguageId = language.Id,
                    LanguageCode = "ja",
                    LanguageTag = "jpn",
                    IsExternal = (bool?)false,
                    SidecarFormat = (string?)"aac",
                }
            );
        }
        var metrics = await db.PlexLibraries.SingleAsync(x => x.Id == library.Id, CancellationToken);
        metrics.OtherVideoCount.ShouldBe(videos.Count);
        metrics.MediaSize.ShouldBe(expectedSize);
        (
            await db
                .PlexOtherVideos.Where(x => x.PlexLibraryId != library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    x.MediaSize,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(control);
        (
            await db
                .PlexOtherVideoData.Where(x => x.PlexLibraryId != library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexOtherVideoId,
                    x.PlexApiPartId,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(controlData);
    }

    [Test]
    public async Task ShouldRefreshOtherVideoMetrics_WhenStoredAndIncomingLibrariesAreEmpty()
    {
        // Arrange
        await SetupDatabase(
            625292,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var db = IDbContext;
        var library = await db.PlexLibraries.SingleAsync(CancellationToken);
        const long staleMediaSize = 43_413_438_912;
        await db
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MediaSize, staleMediaSize), CancellationToken);
        (
            await db
                .PlexLibraries.Where(x => x.Id == library.Id)
                .Select(x => new { x.OtherVideoCount, x.MediaSize })
                .SingleAsync(CancellationToken)
        ).ShouldBe(new { OtherVideoCount = 0, MediaSize = staleMediaSize });
        library.OtherVideos.ShouldBeEmpty();

        // Act
        var result = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new CrudOtherVideosReport());
        (
            await db
                .PlexLibraries.Where(x => x.Id == library.Id)
                .Select(x => new { x.OtherVideoCount, x.MediaSize })
                .SingleAsync(CancellationToken)
        ).ShouldBe(new { OtherVideoCount = 0, MediaSize = 0L });
    }

    [Test]
    public async Task ShouldNotRefreshStreamsForUnchangedVideoParts()
    {
        // Arrange
        await SetupDatabase(
            713463,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedVideo = db.PlexOtherVideos.AsNoTracking().Include(x => x.MediaDataList).Single();
        var ownerId = storedVideo.MediaDataList.First().Id;
        var video = ToIncomingVideo(db, storedVideo);
        var mediaData = video.MediaDataList.First();
        var beforeData = db
            .PlexOtherVideoData.AsNoTracking()
            .Where(x => x.Id == ownerId)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .Single();
        mediaData.Streams.Add(
            new PlexOtherVideoDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134631,
                StreamType = StreamType.Audio,
                LanguageId = 0,
                IsExternal = true,
                SidecarFormat = null,
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "ja",
                    LanguageTag = "jpn",
                },
            }
        );
        library.OtherVideos.Add(video);

        // Act
        var result = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        db.PlexOtherVideoDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexOtherVideoData.AsNoTracking()
            .Where(x => x.Id == ownerId)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .Single()
            .ShouldBe(beforeData);
    }

    [Test]
    public async Task ShouldIgnoreVideoTopologyAndStreamsWhenTimestampIsUnchanged()
    {
        // Arrange
        await SetupDatabase(
            713473,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedVideo = db.PlexOtherVideos.AsNoTracking().Include(x => x.MediaDataList).Single();
        var video = ToIncomingVideo(db, storedVideo);
        var original = video.MediaDataList.First();
        var beforeTitle = storedVideo.Title;
        var beforeData = db
            .PlexOtherVideoData.AsNoTracking()
            .Where(x => x.PlexOtherVideoId == storedVideo.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList();
        var added = FakeData.GetPlexOtherVideoMediaData(new Seed(713474)).Generate();
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), video.PlexApiRatingKey);
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), original.PlexApiMediaId);
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiPartId), original.PlexApiPartId + 1);
        added.PartIndex = original.PartIndex + 1;
        added.Streams.Add(
            new PlexOtherVideoDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134731,
                StreamType = StreamType.Audio,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = null,
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "ja",
                    LanguageTag = "jpn",
                },
            }
        );
        video.MediaDataList.Add(added);
        video.Title = "Incoming video title";
        library.OtherVideos.Add(video);

        // Act
        var result = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new CrudOtherVideosReport { UnchangedOtherVideos = 1 });
        db.PlexOtherVideos.AsNoTracking()
            .Where(x => x.Id == storedVideo.Id)
            .Select(x => x.Title)
            .Single()
            .ShouldBe(beforeTitle);
        db.PlexOtherVideoDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexOtherVideoData.AsNoTracking()
            .Where(x => x.PlexOtherVideoId == storedVideo.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList()
            .ShouldBe(beforeData);
    }

    [Test]
    public async Task ShouldReconcileVideoStreamLifecycleWithoutChurn()
    {
        // Arrange
        await SetupDatabase(
            713467,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();

        async Task<PlexOtherVideo> LoadVideo() =>
            ToIncomingVideo(
                db,
                await db.PlexOtherVideos.AsNoTracking().Include(x => x.MediaDataList).SingleAsync(CancellationToken)
            );

        void SetIncoming(PlexOtherVideo video, bool includeStream, bool changed = false, bool updateTimestamp = false)
        {
            if (updateTimestamp)
                video.UpdatedAt = video.UpdatedAt.GetValueOrDefault().AddSeconds(1);

            var mediaData = video.MediaDataList.First();
            mediaData.Streams.Clear();
            if (includeStream)
                mediaData.Streams.Add(
                    new PlexOtherVideoDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = 7134671,
                        StreamType = StreamType.Audio,
                        LanguageId = 0,
                        IsExternal = changed,
                        SidecarFormat = null,
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "ja",
                            LanguageTag = "jpn",
                        },
                    }
                );
            library.OtherVideos.Clear();
            library.OtherVideos.Add(video);
        }

        var initial = await LoadVideo();
        SetIncoming(initial, includeStream: true, updateTimestamp: true);

        // Act
        var initialResult = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        initialResult.IsSuccess.ShouldBeTrue(initialResult.ToString());
        initialResult.Errors.Count.ShouldBe(0);
        var persistedVideoId = db
            .PlexOtherVideos.AsNoTracking()
            .Single(x => x.PlexApiRatingKey == initial.PlexApiRatingKey)
            .Id;
        var originalOwnerId = db
            .PlexOtherVideoData.AsNoTracking()
            .Single(x => x.PlexOtherVideoId == persistedVideoId)
            .Id;
        var language = db.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "ja");
        var expectedInitial = new
        {
            OwnerId = originalOwnerId,
            SourceId = 7134671,
            Type = StreamType.Audio,
            LanguageId = language.Id,
            LanguageCode = "ja",
            LanguageTag = "jpn",
            External = (bool?)false,
            Format = (string?)null,
        };
        var firstId = db.PlexOtherVideoDataStreams.AsNoTracking().Select(x => x.Id).Single();
        db.PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single()
            .ShouldBe(expectedInitial);

        var unchanged = await LoadVideo();
        SetIncoming(unchanged, includeStream: true, changed: true);

        // Act
        var unchangedResult = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedResult.IsSuccess.ShouldBeTrue(unchangedResult.ToString());
        unchangedResult.Errors.Count.ShouldBe(0);
        var unchangedStream = db
            .PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        unchangedStream.Id.ShouldBe(firstId);
        new
        {
            OwnerId = unchangedStream.OwnerId,
            SourceId = unchangedStream.SourceId,
            Type = unchangedStream.Type,
            unchangedStream.LanguageId,
            unchangedStream.LanguageCode,
            unchangedStream.LanguageTag,
            unchangedStream.External,
            unchangedStream.Format,
        }.ShouldBe(expectedInitial);

        var changed = await LoadVideo();
        SetIncoming(changed, includeStream: true, changed: true, updateTimestamp: true);

        // Act
        var changedResult = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        changedResult.IsSuccess.ShouldBeTrue(changedResult.ToString());
        changedResult.Errors.Count.ShouldBe(0);
        var changedOwnerId = db.PlexOtherVideoData.AsNoTracking().Single(x => x.PlexOtherVideoId == changed.Id).Id;
        var expectedChanged = new
        {
            OwnerId = changedOwnerId,
            SourceId = 7134671,
            Type = StreamType.Audio,
            LanguageId = language.Id,
            LanguageCode = "ja",
            LanguageTag = "jpn",
            External = (bool?)true,
            Format = (string?)null,
        };
        var changedStream = db
            .PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        changedStream.Id.ShouldBeGreaterThan(0);
        new
        {
            OwnerId = changedStream.OwnerId,
            SourceId = changedStream.SourceId,
            Type = changedStream.Type,
            changedStream.LanguageId,
            changedStream.LanguageCode,
            changedStream.LanguageTag,
            changedStream.External,
            changedStream.Format,
        }.ShouldBe(expectedChanged);

        // Arrange
        var empty = await LoadVideo();
        var replacementMediaData = empty.MediaDataList.Single();
        var persistedVideoIdBeforeEmpty = db
            .PlexOtherVideos.AsNoTracking()
            .Single(x => x.PlexApiRatingKey == empty.PlexApiRatingKey)
            .Id;
        empty.UpdatedAt = empty.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        empty.MediaDataList.Clear();
        library.OtherVideos.Clear();
        library.OtherVideos.Add(empty);

        // Act
        var emptyResult = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        emptyResult.IsSuccess.ShouldBeTrue(emptyResult.ToString());
        emptyResult.Errors.Count.ShouldBe(0);
        db.PlexOtherVideoDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexOtherVideoData.AsNoTracking()
            .Where(x => x.PlexOtherVideoId == persistedVideoIdBeforeEmpty)
            .ToList()
            .ShouldBeEmpty();

        var forced = await LoadVideo();
        forced.MediaDataList.Add(replacementMediaData);
        SetIncoming(forced, includeStream: true);

        var forcedMediaData = forced.MediaDataList.First();

        // Act
        var forcedResult = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library), ForceMediaRefresh: true)
        );

        // Assert
        forcedResult.IsSuccess.ShouldBeTrue(forcedResult.ToString());
        forcedResult.Errors.Count.ShouldBe(0);
        var forcedData = db
            .PlexOtherVideoData.AsNoTracking()
            .Single(x =>
                x.PlexApiMediaId == forcedMediaData.PlexApiMediaId && x.PlexApiPartId == forcedMediaData.PlexApiPartId
            );
        var forcedStream = db
            .PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        new
        {
            OwnerId = forcedStream.OwnerId,
            SourceId = forcedStream.SourceId,
            Type = forcedStream.Type,
            forcedStream.LanguageId,
            forcedStream.LanguageCode,
            forcedStream.LanguageTag,
            forcedStream.External,
            forcedStream.Format,
        }.ShouldBe(
            new
            {
                OwnerId = forcedData.Id,
                SourceId = 7134671,
                Type = StreamType.Audio,
                LanguageId = language.Id,
                LanguageCode = "ja",
                LanguageTag = "jpn",
                External = (bool?)false,
                Format = (string?)null,
            }
        );
    }

    [Test]
    public async Task ShouldPreserveOtherVideoStateWhenSynchronizationIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            713476,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedVideo = db.PlexOtherVideos.AsNoTracking().Include(x => x.MediaDataList).Single();
        var video = ToIncomingVideo(db, storedVideo);
        video
            .MediaDataList.First()
            .Streams.Add(
                new PlexOtherVideoDataStream
                {
                    Id = 0,
                    MediaDataId = 0,
                    PlexApiStreamId = 7134761,
                    StreamType = StreamType.Audio,
                    LanguageId = 0,
                    IsExternal = null,
                    SidecarFormat = "aac",
                    Language = new StreamLanguage
                    {
                        Id = 0,
                        LanguageCode = "fr",
                        LanguageTag = "fra",
                    },
                }
            );
        library.OtherVideos.Add(video);

        // Act
        var seeded = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());
        seeded.Errors.Count.ShouldBe(0);
        var beforeVideo = db
            .PlexOtherVideos.AsNoTracking()
            .Where(x => x.Id == video.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeData = db
            .PlexOtherVideoData.AsNoTracking()
            .Where(x => x.PlexOtherVideoId == video.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList();
        var beforeStreams = db
            .PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList();
        var beforeMetrics = db
            .PlexLibraries.AsNoTracking()
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.MediaCount,
                x.OtherVideoCount,
                x.MediaSize,
            })
            .Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancellationVideo = ToIncomingVideo(
            db,
            await db.PlexOtherVideos.AsNoTracking().Include(x => x.MediaDataList).SingleAsync(CancellationToken)
        );
        library.OtherVideos.Clear();
        library.OtherVideos.Add(cancellationVideo);
        var command = new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library));
        (
            await new SyncPlexOtherVideosCommandValidator().ValidateAsync(command, CancellationToken)
        ).IsValid.ShouldBeTrue();

        // Act
        var result = await Mock.Create<SyncPlexOtherVideosCommandHandler>().ExecuteAsync(command, cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        db.PlexOtherVideos.AsNoTracking()
            .Where(x => x.Id == beforeVideo.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeVideo);
        db.PlexOtherVideoData.AsNoTracking()
            .Where(x => x.PlexOtherVideoId == beforeVideo.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexOtherVideoId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList()
            .ShouldBe(beforeData);
        db.PlexOtherVideoDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList()
            .ShouldBe(beforeStreams);
        db.PlexLibraries.AsNoTracking()
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.MediaCount,
                x.OtherVideoCount,
                x.MediaSize,
            })
            .Single()
            .ShouldBe(beforeMetrics);
    }

    private static PlexOtherVideo ToIncomingVideo(IReaparrDbContext db, PlexOtherVideo source)
    {
        var video = (PlexOtherVideo)db.Entry(source).CurrentValues.ToObject();
        video.Id = 0;
        foreach (var mediaData in source.MediaDataList)
        {
            var incomingData = (PlexOtherVideoMediaData)db.Entry(mediaData).CurrentValues.ToObject();
            incomingData.Id = 0;
            incomingData.PlexOtherVideoId = 0;
            foreach (var stream in mediaData.Streams)
                incomingData.Streams.Add(
                    new PlexOtherVideoDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = stream.PlexApiStreamId,
                        StreamType = stream.StreamType,
                        LanguageId = 0,
                        IsExternal = stream.IsExternal,
                        SidecarFormat = stream.SidecarFormat,
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = stream.Language.LanguageCode,
                            LanguageTag = stream.Language.LanguageTag,
                        },
                    }
                );
            video.MediaDataList.Add(incomingData);
        }
        foreach (var actor in source.Actors)
            video.Actors.Add(new PlexActor { Name = actor.Name, Key = actor.Key });
        foreach (var genre in source.Genres)
            video.Genres.Add(new PlexGenreVideo { Name = genre.Name, Key = genre.Key });
        foreach (var country in source.Countries)
            video.Countries.Add(new PlexCountry { Name = country.Name, Key = country.Key });
        return video;
    }
}
