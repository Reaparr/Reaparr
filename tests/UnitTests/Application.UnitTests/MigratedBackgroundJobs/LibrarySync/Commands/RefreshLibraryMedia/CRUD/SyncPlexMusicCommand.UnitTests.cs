namespace Reaparr.Application.UnitTests;

public class SyncPlexMusicCommandUnitTests : BaseCommandUnitTest<SyncPlexMusicCommand>
{
    [Test]
    [Arguments("initial")]
    [Arguments("unchanged")]
    [Arguments("update")]
    [Arguments("force")]
    [Arguments("empty")]
    public async Task ShouldReconcileMusicHierarchy_WithScopedOriginalsCountsAndReports(string scenario)
    {
        // Arrange
        await SetupDatabase(
            625203,
            x =>
            {
                x.PlexServerCount = 2;
                x.PlexMusicLibraryCount = 2;
                x.MusicArtistCount = 2;
                x.MusicAlbumCount = 1;
                x.MusicTrackCount = 2;
            }
        );
        var db = IDbContext;
        var library = await db.PlexLibraries.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var storedArtists = await db
            .PlexMusicArtists.AsNoTracking()
            .Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var artists = storedArtists.Select(artist => ToIncomingArtist(db, artist)).ToList();
        var originalAlbums = artists.SelectMany(x => x.Albums).OrderBy(x => x.PlexApiRatingKey).ToList();
        var originalTracks = originalAlbums.SelectMany(x => x.Tracks).OrderBy(x => x.PlexApiRatingKey).ToList();
        var originalArtistIds = storedArtists.Select(x => x.Id).ToArray();
        var originalAlbumIds = storedArtists.SelectMany(x => x.Albums).Select(x => x.Id).ToArray();
        var originalTrackIds = storedArtists
            .SelectMany(x => x.Albums)
            .SelectMany(x => x.Tracks)
            .Select(x => x.Id)
            .ToArray();
        var beforeData = await db
            .PlexMusicTrackData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiPartId,
                x.Size,
                x.OriginalFilename,
            })
            .ToListAsync(CancellationToken);
        var control = await db
            .PlexMusicTracks.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexAlbumId,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.MediaSize,
            })
            .ToListAsync(CancellationToken);
        var controlData = await db
            .PlexMusicTrackData.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        artists.Count.ShouldBe(2);
        originalAlbums.Count.ShouldBe(2);
        originalTracks.Count.ShouldBe(4);
        beforeData.Count.ShouldBe(4);
        long addedSize = 0;
        if (scenario is "initial" or "force")
        {
            var originals = FakeData.GetPlexMusicTrackMediaData(new Seed(625213)).Generate(2);
            originals[0]
                .UpdateInitProperty(
                    nameof(BasePlexMediaData.PlexApiMediaId),
                    originalTracks[0].MediaDataList.Single().PlexApiMediaId
                );
            originals[0].UpdateInitProperty(nameof(PlexMusicTrackMediaData.PartIndex), 1);
            originalTracks[0].MediaDataList.AddRange(originals);
            addedSize = originals.Sum(x => x.Size);
            originalTracks[0].MediaSize += addedSize;
        }
        if (scenario == "initial")
        {
            originalTracks[0]
                .MediaDataList.First()
                .Streams.Add(
                    new PlexMusicTrackDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = 6252031,
                        StreamType = StreamType.Lyrics,
                        LanguageId = 0,
                        IsExternal = false,
                        SidecarFormat = "lrc",
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "de",
                            LanguageTag = "deu",
                        },
                    }
                );
            await db.PlexMusicArtists.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(CancellationToken);
        }
        if (scenario == "unchanged")
        {
            originalTracks[0].MediaSize = 1;
            originalTracks[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 1L);
        }
        if (scenario == "update")
        {
            artists.RemoveAt(1);
            artists[0].UpdatedAt = artists[0].UpdatedAt?.AddDays(1) ?? DateTime.UnixEpoch;
            originalAlbums[0].UpdatedAt = originalAlbums[0].UpdatedAt?.AddDays(1) ?? DateTime.UnixEpoch;
            originalAlbums[0].Tracks.Remove(originalTracks[1]);
            originalTracks[0].UpdatedAt = originalTracks[0].UpdatedAt?.AddDays(1) ?? DateTime.UnixEpoch;
            originalTracks[0].Title = "Updated track";
            originalTracks[0].MediaSize = 123;
            originalTracks[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 123L);
        }
        if (scenario == "empty")
            artists.Clear();
        library.Music.AddRange(artists);

        // Act
        var result = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library), scenario == "force")
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var expectedReport = scenario switch
        {
            "initial" => new CrudMusicReport
            {
                CreatedArtists = 2,
                CreatedAlbums = 2,
                CreatedTracks = 4,
            },
            "force" => new CrudMusicReport
            {
                CreatedArtists = 2,
                CreatedAlbums = 2,
                CreatedTracks = 4,
                DeletedArtists = 2,
                DeletedAlbums = 2,
                DeletedTracks = 4,
            },
            "update" => new CrudMusicReport
            {
                UpdatedArtists = 1,
                DeletedArtists = 1,
                UpdatedAlbums = 1,
                DeletedAlbums = 1,
                UpdatedTracks = 1,
                DeletedTracks = 3,
            },
            "empty" => new CrudMusicReport
            {
                DeletedArtists = 2,
                DeletedAlbums = 2,
                DeletedTracks = 4,
            },
            _ => new CrudMusicReport
            {
                UnchangedArtists = 2,
                UnchangedAlbums = 2,
                UnchangedTracks = 4,
            },
        };
        result.Value.ShouldBe(expectedReport);
        var albums = artists.SelectMany(x => x.Albums).ToList();
        var tracks = albums.SelectMany(x => x.Tracks).ToList();
        var afterArtists = await db
            .PlexMusicArtists.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToListAsync(CancellationToken);
        var afterAlbums = await db
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToListAsync(CancellationToken);
        var afterTracks = await db
            .PlexMusicTracks.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToListAsync(CancellationToken);
        afterArtists.Select(x => x.PlexApiRatingKey).ShouldBe(artists.Select(x => x.PlexApiRatingKey).Order());
        afterAlbums
            .Select(x => (x.PlexApiRatingKey, x.PlexArtistId))
            .ShouldBe(
                albums
                    .OrderBy(x => x.PlexApiRatingKey)
                    .Select(x =>
                        (
                            x.PlexApiRatingKey,
                            afterArtists.Single(artist => artist.PlexApiRatingKey == x.ParentKey).Id
                        )
                    )
            );
        afterTracks
            .Select(x => (x.PlexApiRatingKey, x.PlexAlbumId))
            .ShouldBe(
                tracks
                    .OrderBy(x => x.PlexApiRatingKey)
                    .Select(x =>
                        (
                            x.PlexApiRatingKey,
                            afterAlbums.Single(album => album.PlexApiRatingKey == x.ParentKey).Id
                        )
                    )
            );
        afterTracks.ShouldAllBe(x => x.PlexLibraryId == library.Id && x.PlexServerId == library.PlexServerId);
        if (scenario == "unchanged")
        {
            afterTracks.Select(x => x.Id).Order().ShouldBe(originalTrackIds.Order());
            afterAlbums.Select(x => x.Id).Order().ShouldBe(originalAlbumIds.Order());
            afterArtists.Select(x => x.Id).Order().ShouldBe(originalArtistIds.Order());
        }
        if (scenario == "update")
        {
            afterArtists.Single().Id.ShouldBe(originalArtistIds[0]);
            afterAlbums.Single().Id.ShouldBe(originalAlbumIds[0]);
            afterTracks.Single().Id.ShouldBe(originalTrackIds[0]);
            afterTracks.Single().Title.ShouldBe("Updated track");
        }
        var afterData = await db
            .PlexMusicTrackData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiPartId,
                x.Size,
                x.OriginalFilename,
            })
            .ToListAsync(CancellationToken);
        if (scenario == "unchanged")
            afterData.ShouldBe(beforeData);
        else
            afterData
                .Select(x => (x.PlexTrackId, x.PlexApiPartId, x.Size, x.OriginalFilename))
                .OrderBy(x => x.PlexApiPartId)
                .ShouldBe(
                    tracks
                        .SelectMany(t =>
                            t.MediaDataList.Select(d => (t.Id, d.PlexApiPartId, d.Size, d.OriginalFilename))
                        )
                        .OrderBy(x => x.PlexApiPartId)
                );
        if (scenario == "initial")
        {
            artists.ShouldAllBe(x => x.Id > 0);
            artists.SelectMany(x => x.Albums).ShouldAllBe(x => x.Id > 0);
            artists.SelectMany(x => x.Albums).SelectMany(x => x.Tracks).ShouldAllBe(x => x.Id > 0);
            var createdData = originalTracks[0].MediaDataList.First();
            createdData.Id.ShouldBeGreaterThan(0);
            var language = db
                .StreamLanguages.AsNoTracking()
                .Single(x => x.LanguageCode == "de" && x.LanguageTag == "deu");
            var persistedStream = await db
                .PlexMusicTrackDataStreams.AsNoTracking()
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
                    PlexApiStreamId = 6252031,
                    StreamType = StreamType.Lyrics,
                    LanguageId = language.Id,
                    LanguageCode = "de",
                    LanguageTag = "deu",
                    IsExternal = (bool?)false,
                    SidecarFormat = (string?)"lrc",
                }
            );
        }
        var metrics = await db.PlexLibraries.SingleAsync(x => x.Id == library.Id, CancellationToken);
        metrics.MusicArtistCount.ShouldBe(artists.Count);
        metrics.MusicAlbumCount.ShouldBe(albums.Count);
        metrics.MusicTrackCount.ShouldBe(tracks.Count);
        metrics.MediaSize.ShouldBe(
            scenario switch
            {
                "empty" => 0,
                "update" => tracks.Sum(x => x.MediaSize),
                _ => beforeData.Sum(x => x.Size) + addedSize,
            }
        );
        (
            await db
                .PlexMusicTracks.Where(x => x.PlexLibraryId != library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexAlbumId,
                    x.PlexApiRatingKey,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    x.MediaSize,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(control);
        (
            await db
                .PlexMusicTrackData.Where(x => x.PlexLibraryId != library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexTrackId,
                    x.PlexApiPartId,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(controlData);
    }

    [Test]
    public async Task ShouldRefreshMusicMetrics_WhenStoredAndIncomingLibrariesAreEmpty()
    {
        // Arrange
        await SetupDatabase(
            625291,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
            }
        );
        var db = IDbContext;
        var library = await db.PlexLibraries.SingleAsync(CancellationToken);
        const long staleMediaSize = 66_419_253_194;
        await db
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MediaSize, staleMediaSize), CancellationToken);
        (
            await db
                .PlexLibraries.Where(x => x.Id == library.Id)
                .Select(x => new
                {
                    x.MusicArtistCount,
                    x.MusicAlbumCount,
                    x.MusicTrackCount,
                    x.MediaSize,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                MusicArtistCount = 0,
                MusicAlbumCount = 0,
                MusicTrackCount = 0,
                MediaSize = staleMediaSize,
            }
        );
        library.Music.ShouldBeEmpty();

        // Act
        var result = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new CrudMusicReport());
        (
            await db
                .PlexLibraries.Where(x => x.Id == library.Id)
                .Select(x => new
                {
                    x.MusicArtistCount,
                    x.MusicAlbumCount,
                    x.MusicTrackCount,
                    x.MediaSize,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                MusicArtistCount = 0,
                MusicAlbumCount = 0,
                MusicTrackCount = 0,
                MediaSize = 0L,
            }
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReplaceOrClearAlbumCountriesWithoutChangingUnrefreshedAlbumsOrOtherLibraries_WhenAlbumTimestampChanges(
        bool clearCountries
    )
    {
        // Arrange
        await SetupDatabase(
            625261,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 2;
                config.MusicTrackCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var storedArtist = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var artist = ToIncomingArtist(dbContext, storedArtist);
        var allAlbums = await dbContext.PlexMusicAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var allArtists = await dbContext.PlexMusicArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        allAlbums.Count.ShouldBe(4);
        allArtists.Count.ShouldBe(2);
        var oldCountry = new PlexCountry { Name = "Old Album Country", Key = "Old Album Country".ToMd5Hash() };
        var firstCountry = new PlexCountry { Name = "First Album Country", Key = "First Album Country".ToMd5Hash() };
        var secondCountry = new PlexCountry { Name = "Second Album Country", Key = "Second Album Country".ToMd5Hash() };
        dbContext.PlexCountries.AddRange(oldCountry, firstCountry, secondCountry);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexMusicAlbumCountries.AddRange(
            allAlbums.Select(x => new PlexMusicAlbumCountries(oldCountry.Id, x.Id))
        );
        dbContext.PlexMusicArtistCountries.AddRange(
            allArtists.Select(x => new PlexMusicArtistCountries(oldCountry.Id, x.Id))
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexMusicAlbumCountries.OrderBy(x => x.PlexMusicAlbumId).ToListAsync(CancellationToken))
            .Select(x => (x.PlexMusicAlbumId, x.CountryId))
            .ShouldBe(allAlbums.Select(x => (x.Id, oldCountry.Id)));
        var artistLinks = await dbContext
            .PlexMusicArtistCountries.OrderBy(x => x.PlexMusicArtistId)
            .Select(x => new
            {
                x.PlexMusicArtistId,
                x.CountryId,
            })
            .ToListAsync(CancellationToken);
        artistLinks
            .Select(x => (x.PlexMusicArtistId, x.CountryId))
            .ShouldBe(allArtists.Select(x => (x.Id, oldCountry.Id)));

        var targetAlbum = artist.Albums.OrderBy(x => x.PlexApiRatingKey).First();
        var unchangedAlbum = artist.Albums.OrderBy(x => x.PlexApiRatingKey).Last();
        artist.UpdatedAt = artist.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        targetAlbum.UpdatedAt = targetAlbum.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        artist.Countries.Add(oldCountry);
        unchangedAlbum.Countries.Add(firstCountry);
        if (!clearCountries)
        {
            targetAlbum.Countries.Add(firstCountry);
            targetAlbum.Countries.Add(secondCountry);
            targetAlbum.Countries.Add(firstCountry);
        }
        library.Music.Add(artist);
        var metadata = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexCountries = new[] { oldCountry, firstCountry, secondCountry }.ToDictionary(x => x.Key),
        };

        // Act
        var result = await TestHandlerExecuteAsync<CrudMusicReport>(new SyncPlexMusicCommand(metadata));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new CrudMusicReport
            {
                UpdatedArtists = 1,
                UpdatedAlbums = 1,
                UnchangedAlbums = 1,
                UnchangedTracks = 2,
            }
        );
        var expectedLinks = allAlbums
            .Where(x => x.Id != targetAlbum.Id)
            .Select(x => (PlexMusicAlbumId: x.Id, CountryId: oldCountry.Id))
            .ToList();
        if (!clearCountries)
        {
            expectedLinks.Add((targetAlbum.Id, firstCountry.Id));
            expectedLinks.Add((targetAlbum.Id, secondCountry.Id));
        }
        (
            await dbContext
                .PlexMusicAlbumCountries.OrderBy(x => x.PlexMusicAlbumId)
                .ThenBy(x => x.CountryId)
                .ToListAsync(CancellationToken)
        )
            .Select(x => (x.PlexMusicAlbumId, x.CountryId))
            .ShouldBe(expectedLinks.OrderBy(x => x.PlexMusicAlbumId).ThenBy(x => x.CountryId));
        (
            await dbContext
                .PlexMusicArtistCountries.OrderBy(x => x.PlexMusicArtistId)
                .Select(x => new
                {
                    x.PlexMusicArtistId,
                    x.CountryId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(artistLinks);
        (
            await dbContext
                .PlexCountries.Where(x => x.Id == oldCountry.Id || x.Id == firstCountry.Id || x.Id == secondCountry.Id)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Key })
                .ToListAsync(CancellationToken)
        )
            .Select(x => (x.Id, x.Key))
            .ShouldBe(new[] { oldCountry, firstCountry, secondCountry }.Select(x => (x.Id, x.Key)));
    }

    [Test]
    public async Task ShouldNotRefreshStreamsForUnchangedMusicTrackParts()
    {
        // Arrange
        await SetupDatabase(
            713462,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedArtist = db
            .PlexMusicArtists.AsNoTracking()
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var ownerId = storedArtist.Albums.Single().Tracks.Single().MediaDataList.First().Id;
        var artist = ToIncomingArtist(db, storedArtist);
        var mediaData = artist.Albums.Single().Tracks.Single().MediaDataList.First();
        var beforeData = db
            .PlexMusicTrackData.AsNoTracking()
            .Where(x => x.Id == ownerId)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .Single();
        mediaData.Streams.Add(
            new PlexMusicTrackDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134621,
                StreamType = StreamType.Lyrics,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = "lrc",
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "de",
                    LanguageTag = "deu",
                },
            }
        );
        library.Music.Add(artist);

        // Act
        var result = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new CrudMusicReport
            {
                UnchangedArtists = 1,
                UnchangedAlbums = 1,
                UnchangedTracks = 1,
            }
        );
        db.PlexMusicTrackDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexMusicTrackData.AsNoTracking()
            .Where(x => x.Id == ownerId)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .Single()
            .ShouldBe(beforeData);
    }

    [Test]
    public async Task ShouldReconcileMusicStreamLifecycleWithoutChurn()
    {
        // Arrange
        await SetupDatabase(
            713466,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();

        async Task<PlexMusicArtist> LoadArtist() =>
            ToIncomingArtist(
                db,
                await db
                    .PlexMusicArtists.AsNoTracking()
                    .Include(x => x.Albums)
                        .ThenInclude(x => x.Tracks)
                            .ThenInclude(x => x.MediaDataList)
                    .SingleAsync(CancellationToken)
            );

        void SetIncoming(PlexMusicArtist artist, bool includeStream, bool changed = false, bool updateTimestamp = false)
        {
            var album = artist.Albums.Single();
            var track = album.Tracks.Single();
            if (updateTimestamp)
            {
                artist.UpdatedAt = artist.UpdatedAt.GetValueOrDefault().AddSeconds(1);
                album.UpdatedAt = album.UpdatedAt.GetValueOrDefault().AddSeconds(1);
                track.UpdatedAt = track.UpdatedAt.GetValueOrDefault().AddSeconds(1);
            }

            var mediaData = track.MediaDataList.First();
            mediaData.Streams.Clear();
            if (includeStream)
                mediaData.Streams.Add(
                    new PlexMusicTrackDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = 7134661,
                        StreamType = StreamType.Lyrics,
                        LanguageId = 0,
                        IsExternal = changed,
                        SidecarFormat = changed ? "lrc" : null,
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "de",
                            LanguageTag = "deu",
                        },
                    }
                );
            library.Music.Clear();
            library.Music.Add(artist);
        }

        var initial = await LoadArtist();
        SetIncoming(initial, includeStream: true, updateTimestamp: true);

        // Act
        var initialResult = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        initialResult.IsSuccess.ShouldBeTrue(initialResult.ToString());
        initialResult.Errors.Count.ShouldBe(0);
        var initialTrackKey = initial.Albums.Single().Tracks.Single().PlexApiRatingKey;
        var persistedTrackId = db.PlexMusicTracks.AsNoTracking().Single(x => x.PlexApiRatingKey == initialTrackKey).Id;
        var originalOwnerId = db.PlexMusicTrackData.AsNoTracking().Single(x => x.PlexTrackId == persistedTrackId).Id;
        var language = db.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "de");
        var expectedInitial = new
        {
            OwnerId = originalOwnerId,
            SourceId = 7134661,
            Type = StreamType.Lyrics,
            LanguageId = language.Id,
            LanguageCode = "de",
            LanguageTag = "deu",
            External = (bool?)false,
            Format = (string?)null,
        };
        var firstId = db.PlexMusicTrackDataStreams.AsNoTracking().Select(x => x.Id).Single();
        db.PlexMusicTrackDataStreams.AsNoTracking()
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

        var unchanged = await LoadArtist();
        SetIncoming(unchanged, includeStream: true, changed: true);

        // Act
        var unchangedResult = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedResult.IsSuccess.ShouldBeTrue(unchangedResult.ToString());
        unchangedResult.Errors.Count.ShouldBe(0);
        var unchangedStream = db
            .PlexMusicTrackDataStreams.AsNoTracking()
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

        var changed = await LoadArtist();
        SetIncoming(changed, includeStream: true, changed: true, updateTimestamp: true);

        // Act
        var changedResult = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        changedResult.IsSuccess.ShouldBeTrue(changedResult.ToString());
        changedResult.Errors.Count.ShouldBe(0);
        var expectedChanged = new
        {
            OwnerId = originalOwnerId,
            SourceId = 7134661,
            Type = StreamType.Lyrics,
            LanguageId = language.Id,
            LanguageCode = "de",
            LanguageTag = "deu",
            External = (bool?)true,
            Format = (string?)"lrc",
        };
        var changedStream = db
            .PlexMusicTrackDataStreams.AsNoTracking()
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
        changedStream.Id.ShouldBe(firstId);
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
        var empty = await LoadArtist();
        var emptyAlbum = empty.Albums.Single();
        var emptyTrack = emptyAlbum.Tracks.Single();
        var replacementMediaData = emptyTrack.MediaDataList.Single();
        var persistedTrackIdBeforeEmpty = db.PlexMusicTracks.AsNoTracking()
            .Single(x => x.PlexApiRatingKey == emptyTrack.PlexApiRatingKey)
            .Id;
        empty.UpdatedAt = empty.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        emptyAlbum.UpdatedAt = emptyAlbum.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        emptyTrack.UpdatedAt = emptyTrack.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        emptyTrack.MediaDataList.Clear();
        library.Music.Clear();
        library.Music.Add(empty);

        // Act
        var emptyResult = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        emptyResult.IsSuccess.ShouldBeTrue(emptyResult.ToString());
        emptyResult.Errors.Count.ShouldBe(0);
        db.PlexMusicTrackDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexMusicTrackData.AsNoTracking()
            .Where(x => x.PlexTrackId == persistedTrackIdBeforeEmpty)
            .ToList()
            .ShouldBeEmpty();

        var forced = await LoadArtist();
        var forcedTrack = forced.Albums.Single().Tracks.Single();
        forcedTrack.MediaDataList.Add(replacementMediaData);
        SetIncoming(forced, includeStream: true);
        var forcedMediaData = forced.Albums.Single().Tracks.Single().MediaDataList.First();

        // Act
        var forcedResult = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library), ForceMediaRefresh: true)
        );

        // Assert
        forcedResult.IsSuccess.ShouldBeTrue(forcedResult.ToString());
        forcedResult.Errors.Count.ShouldBe(0);
        var forcedData = db
            .PlexMusicTrackData.AsNoTracking()
            .Single(x =>
                x.PlexApiMediaId == forcedMediaData.PlexApiMediaId && x.PlexApiPartId == forcedMediaData.PlexApiPartId
            );
        var forcedStream = db
            .PlexMusicTrackDataStreams.AsNoTracking()
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
                SourceId = 7134661,
                Type = StreamType.Lyrics,
                LanguageId = language.Id,
                LanguageCode = "de",
                LanguageTag = "deu",
                External = (bool?)false,
                Format = (string?)null,
            }
        );
    }

    [Test]
    public async Task ShouldPreserveMusicStateWhenSynchronizationIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            713476,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedArtist = db
            .PlexMusicArtists.AsNoTracking()
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var artist = ToIncomingArtist(db, storedArtist);
        var track = artist.Albums.Single().Tracks.Single();
        track
            .MediaDataList.First()
            .Streams.Add(
                new PlexMusicTrackDataStream
                {
                    Id = 0,
                    MediaDataId = 0,
                    PlexApiStreamId = 7134761,
                    StreamType = StreamType.Lyrics,
                    LanguageId = 0,
                    IsExternal = null,
                    SidecarFormat = "lrc",
                    Language = new StreamLanguage
                    {
                        Id = 0,
                        LanguageCode = "de",
                        LanguageTag = "deu",
                    },
                }
            );
        library.Music.Add(artist);

        // Act
        var seeded = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());
        seeded.Errors.Count.ShouldBe(0);
        var beforeArtist = db
            .PlexMusicArtists.AsNoTracking()
            .Where(x => x.Id == artist.Id)
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
        var beforeAlbum = db
            .PlexMusicAlbums.AsNoTracking()
            .Where(x => x.PlexArtistId == artist.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexArtistId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeTrack = db
            .PlexMusicTracks.AsNoTracking()
            .Where(x => x.PlexAlbumId == beforeAlbum.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexAlbumId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeData = db
            .PlexMusicTrackData.AsNoTracking()
            .Where(x => x.PlexTrackId == beforeTrack.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList();
        var beforeStreams = db
            .PlexMusicTrackDataStreams.AsNoTracking()
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
                x.MusicArtistCount,
                x.MusicAlbumCount,
                x.MusicTrackCount,
                x.MediaSize,
            })
            .Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancellationArtist = ToIncomingArtist(
            db,
            await db
                .PlexMusicArtists.AsNoTracking()
                .Include(x => x.Albums)
                    .ThenInclude(x => x.Tracks)
                        .ThenInclude(x => x.MediaDataList)
                .SingleAsync(CancellationToken)
        );
        library.Music.Clear();
        library.Music.Add(cancellationArtist);
        var command = new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library));
        (await new SyncPlexMusicCommandValidator().ValidateAsync(command, CancellationToken)).IsValid.ShouldBeTrue();

        // Act
        var result = await Mock.Create<SyncPlexMusicCommandHandler>().ExecuteAsync(command, cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        db.PlexMusicArtists.AsNoTracking()
            .Where(x => x.Id == beforeArtist.Id)
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
            .ShouldBe(beforeArtist);
        db.PlexMusicAlbums.AsNoTracking()
            .Where(x => x.Id == beforeAlbum.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexArtistId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeAlbum);
        db.PlexMusicTracks.AsNoTracking()
            .Where(x => x.Id == beforeTrack.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexAlbumId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeTrack);
        db.PlexMusicTrackData.AsNoTracking()
            .Where(x => x.PlexTrackId == beforeTrack.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList()
            .ShouldBe(beforeData);
        db.PlexMusicTrackDataStreams.AsNoTracking()
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
                x.MusicArtistCount,
                x.MusicAlbumCount,
                x.MusicTrackCount,
                x.MediaSize,
            })
            .Single()
            .ShouldBe(beforeMetrics);
    }

    [Test]
    public async Task ShouldIgnoreMusicTopologyAndStreamsWhenTimestampsAreUnchanged()
    {
        // Arrange
        await SetupDatabase(
            713468,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var storedArtist = await db
            .PlexMusicArtists.AsNoTracking()
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var storedAlbum = storedArtist.Albums.Single();
        var storedTrack = storedAlbum.Tracks.Single();
        var artist = ToIncomingArtist(db, storedArtist);
        var track = artist.Albums.Single().Tracks.Single();
        var beforeArtistTitle = storedArtist.Title;
        var beforeTrackTitle = storedTrack.Title;
        var beforeData = db
            .PlexMusicTrackData.AsNoTracking()
            .Where(x => x.PlexTrackId == storedTrack.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList();
        var added = FakeData.GetPlexMusicTrackMediaData(new Seed(713469)).Generate();
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), track.PlexApiRatingKey);
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), track.MediaDataList.Single().PlexApiMediaId);
        added.UpdateInitProperty(
            nameof(BasePlexMediaData.PlexApiPartId),
            track.MediaDataList.Single().PlexApiPartId + 1
        );
        added.UpdateInitProperty(nameof(PlexMusicTrackMediaData.PartIndex), 1);
        added.Streams.Add(
            new PlexMusicTrackDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134681,
                StreamType = StreamType.Subtitle,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = "srt",
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "en",
                    LanguageTag = "eng",
                },
            }
        );
        track.MediaDataList.Add(added);
        artist.Title = "Incoming artist title";
        track.Title = "Incoming track title";
        library.Music.Add(artist);

        // Act
        var result = await TestHandlerExecuteAsync<CrudMusicReport>(
            new SyncPlexMusicCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new CrudMusicReport
            {
                UnchangedArtists = 1,
                UnchangedAlbums = 1,
                UnchangedTracks = 1,
            }
        );
        db.PlexMusicArtists.AsNoTracking()
            .Where(x => x.Id == storedArtist.Id)
            .Select(x => new { x.Id, x.Title })
            .Single()
            .ShouldBe(new { Id = storedArtist.Id, Title = beforeArtistTitle });
        db.PlexMusicAlbums.AsNoTracking()
            .Where(x => x.Id == storedAlbum.Id)
            .Select(x => new { x.Id, x.PlexArtistId })
            .Single()
            .ShouldBe(new { Id = storedAlbum.Id, PlexArtistId = storedAlbum.PlexArtistId });
        db.PlexMusicTracks.AsNoTracking()
            .Where(x => x.Id == storedTrack.Id)
            .Select(x => new { x.Id, x.PlexAlbumId, x.Title })
            .Single()
            .ShouldBe(
                new
                {
                    Id = storedTrack.Id,
                    PlexAlbumId = storedTrack.PlexAlbumId,
                    Title = beforeTrackTitle,
                }
            );
        db.PlexMusicTrackDataStreams.AsNoTracking().Count().ShouldBe(0);
        db.PlexMusicTrackData.AsNoTracking()
            .Where(x => x.PlexTrackId == storedTrack.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTrackId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PartIndex,
                x.Size,
            })
            .ToList()
            .ShouldBe(beforeData);
    }

    private static PlexMusicArtist ToIncomingArtist(IReaparrDbContext db, PlexMusicArtist source)
    {
        var artist = (PlexMusicArtist)db.Entry(source).CurrentValues.ToObject();
        artist.Id = 0;
        artist.Albums = source
            .Albums.Select(album =>
            {
                var incomingAlbum = (PlexMusicAlbum)db.Entry(album).CurrentValues.ToObject();
                incomingAlbum.Id = 0;
                incomingAlbum.PlexArtistId = 0;
                incomingAlbum.PlexArtist = null;
                foreach (var country in album.Countries)
                    incomingAlbum.Countries.Add(new PlexCountry { Name = country.Name, Key = country.Key });
                incomingAlbum.Tracks = album
                    .Tracks.Select(track =>
                    {
                        var incomingTrack = (PlexMusicTrack)db.Entry(track).CurrentValues.ToObject();
                        incomingTrack.Id = 0;
                        incomingTrack.PlexAlbumId = 0;
                        incomingTrack.PlexAlbum = null;
                        foreach (var mediaData in track.MediaDataList)
                        {
                            var incomingData = (PlexMusicTrackMediaData)db.Entry(mediaData).CurrentValues.ToObject();
                            incomingData.Id = 0;
                            incomingData.PlexTrackId = 0;
                            foreach (var stream in mediaData.Streams)
                                incomingData.Streams.Add(
                                    new PlexMusicTrackDataStream
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
                            incomingTrack.MediaDataList.Add(incomingData);
                        }
                        return incomingTrack;
                    })
                    .ToList();
                return incomingAlbum;
            })
            .ToList();
        foreach (var actor in source.Actors)
            artist.Actors.Add(new PlexActor { Name = actor.Name, Key = actor.Key });
        foreach (var genre in source.Genres)
            artist.Genres.Add(new PlexGenreAudio { Name = genre.Name, Key = genre.Key });
        foreach (var country in source.Countries)
            artist.Countries.Add(new PlexCountry { Name = country.Name, Key = country.Key });
        return artist;
    }
}
