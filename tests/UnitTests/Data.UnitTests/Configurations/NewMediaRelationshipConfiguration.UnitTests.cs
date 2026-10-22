using Microsoft.EntityFrameworkCore.Metadata;

namespace Reaparr.Data.UnitTests.Configurations;

public class NewMediaRelationshipConfigurationUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(typeof(PlexMusicArtist))]
    [Arguments(typeof(PlexMusicAlbum))]
    [Arguments(typeof(PlexMusicTrack))]
    [Arguments(typeof(PlexOtherVideo))]
    public async Task ShouldExposeScopedSourceKeyForSourceType(Type sourceType)
    {
        // Arrange
        await SetupRelationshipConfigurationDatabase();
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.Model;

        // Act
        var entityType = model.FindEntityType(sourceType).ShouldNotBeNull();

        // Assert
        entityType.GetKeys().ShouldContain(key =>
            !key.IsPrimaryKey()
            && key.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMedia.PlexLibraryId), nameof(BaseEntity.Id) })
        );
    }

    [Test]
    [Arguments(typeof(PlexMusicArtist))]
    [Arguments(typeof(PlexMusicAlbum))]
    [Arguments(typeof(PlexMusicTrack))]
    [Arguments(typeof(PlexPhotoAlbum))]
    [Arguments(typeof(PlexPhotoMediaData))]
    [Arguments(typeof(PlexOtherVideo))]
    public async Task ShouldExposeSourceLookupIndexesForSourceType(Type sourceType)
    {
        // Arrange
        await SetupRelationshipConfigurationDatabase();
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.Model;

        // Act
        var entityType = model.FindEntityType(sourceType).ShouldNotBeNull();

        // Assert
        entityType.GetIndexes().ShouldContain(index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMedia.PlexLibraryId), nameof(BasePlexMedia.PlexApiRatingKey) })
        );
        entityType.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMedia.PlexApiRatingKey), nameof(BasePlexMedia.PlexServerId) })
        );
        entityType.GetIndexes().ShouldNotContain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMedia.PlexApiRatingKey) })
        );
    }

    [Test]
    [Arguments(typeof(PlexMusicTrackMediaData), nameof(PlexMusicTrackMediaData.PlexTrackId))]
    [Arguments(typeof(PlexOtherVideoMediaData), nameof(PlexOtherVideoMediaData.PlexOtherVideoId))]
    public async Task ShouldExposeMediaDataLookupIndexWithoutRatingKeyIndex(Type mediaDataType, string ownerProperty)
    {
        // Arrange
        await SetupRelationshipConfigurationDatabase();
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.Model;

        // Act
        var entityType = model.FindEntityType(mediaDataType).ShouldNotBeNull();

        // Assert
        entityType.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMediaData.PlexLibraryId), ownerProperty })
        );
        entityType.GetIndexes().ShouldNotContain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(BasePlexMediaData.PlexApiRatingKey) })
        );
    }

    [Test]
    [Arguments(
        typeof(PlexMusicAlbum),
        typeof(PlexMusicArtist),
        nameof(PlexMusicAlbum.PlexLibraryId),
        nameof(PlexMusicAlbum.PlexArtistId)
    )]
    [Arguments(
        typeof(PlexMusicTrack),
        typeof(PlexMusicAlbum),
        nameof(PlexMusicTrack.PlexLibraryId),
        nameof(PlexMusicTrack.PlexAlbumId)
    )]
    [Arguments(
        typeof(PlexMusicTrackMediaData),
        typeof(PlexMusicTrack),
        nameof(BasePlexMediaData.PlexLibraryId),
        nameof(PlexMusicTrackMediaData.PlexTrackId)
    )]
    [Arguments(
        typeof(PlexOtherVideoMediaData),
        typeof(PlexOtherVideo),
        nameof(BasePlexMediaData.PlexLibraryId),
        nameof(PlexOtherVideoMediaData.PlexOtherVideoId)
    )]
    public async Task ShouldExposeOwnedHierarchyForeignKey(
        Type dependentType,
        Type principalType,
        string dependentLibraryProperty,
        string dependentOwnerProperty
    )
    {
        // Arrange
        await SetupRelationshipConfigurationDatabase();
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.Model;

        // Act
        var entityType = model.FindEntityType(dependentType).ShouldNotBeNull();

        // Assert
        entityType.GetForeignKeys().ShouldContain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == principalType
            && foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { dependentLibraryProperty, dependentOwnerProperty })
            && foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BasePlexMedia.PlexLibraryId), nameof(BaseEntity.Id) })
            && foreignKey.DeleteBehavior == DeleteBehavior.Cascade
        );
    }

    [Test]
    [MethodDataSource(nameof(ForeignKeyCases))]
    public async Task ShouldExposeConfiguredForeignKey(ForeignKeyCase expected)
    {
        // Arrange
        await SetupRelationshipConfigurationDatabase();
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.Model;

        // Act
        var entityType = model.FindEntityType(expected.DependentType).ShouldNotBeNull();

        // Assert
        AssertForeignKey(entityType, expected);
    }

    [Test]
    public async Task ShouldRejectCrossLibraryMusicTrack_WhenCompositeOwnerKeyDoesNotMatchAlbum()
    {
        // Arrange
        await SetupDatabase(
            626556,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;
        var albums = await context.PlexMusicAlbums.OrderBy(x => x.PlexLibraryId).ThenBy(x => x.Id).ToListAsync(CancellationToken);
        var sourceAlbum = albums[0];
        var sourceTrack = await context.PlexMusicTracks.SingleAsync(x => x.PlexAlbumId == sourceAlbum.Id, CancellationToken);
        var targetLibraryId = albums[1].PlexLibraryId;
        var invalidTrack = new PlexMusicTrack
        {
            PlexApiRatingKey = int.MaxValue - 626556,
            Title = "Cross-library track",
            SortIndex = int.MaxValue - 626556,
            SearchTitle = "cross-library track",
            Duration = sourceTrack.Duration,
            MediaSize = sourceTrack.MediaSize,
            PlexApiMetaDataKey = sourceTrack.PlexApiMetaDataKey,
            ChildCount = 0,
            AddedAt = sourceTrack.AddedAt,
            UpdatedAt = sourceTrack.UpdatedAt,
            HasThumb = sourceTrack.HasThumb,
            HasArt = sourceTrack.HasArt,
            PlexLibraryId = targetLibraryId,
            PlexServerId = sourceTrack.PlexServerId,
            ParentKey = sourceAlbum.PlexApiRatingKey,
            PlexAlbumId = sourceAlbum.Id,
            PlexApiIdentityId = null,
            MusicBrainzReleaseTrackId = null,
            DiscNumber = sourceTrack.DiscNumber,
            TrackNumber = sourceTrack.TrackNumber,
        };
        context.PlexMusicTracks.Add(invalidTrack);

        // Act
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));

        // Assert
        context.ChangeTracker.Clear();
        var tracks = await context.PlexMusicTracks
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .Select(x => new { x.PlexLibraryId, x.PlexAlbumId })
            .ToListAsync(CancellationToken);
        tracks.Count.ShouldBe(2);
        tracks.ShouldAllBe(x => albums.Any(album => album.PlexLibraryId == x.PlexLibraryId && album.Id == x.PlexAlbumId));
    }

    [Test]
    public async Task ShouldNullSelectedTrackDataReferences_WhenTrackDataIsDeleted()
    {
        // Arrange
        await SetupDatabase(
            626557,
            config =>
            {
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;
        var track = await context.PlexMusicTracks.SingleAsync(CancellationToken);
        var data = await context.PlexMusicTrackData.SingleAsync(CancellationToken);
        var comparison = new PlexMusicTrackComparison
        {
            RemotePlexLibraryId = track.PlexLibraryId,
            OwnedPlexLibraryId = track.PlexLibraryId,
            RemotePlexMediaId = track.Id,
            OwnedPlexMediaId = track.Id,
            RemotePlexMediaDataId = data.Id,
            OwnedPlexMediaDataId = data.Id,
            MatchType = PlexMediaComparisonMatchType.PlexTrackGuid,
            ComparedAt = DateTime.UtcNow,
        };
        context.PlexMusicTrackComparisons.Add(comparison);
        await context.SaveChangesAsync(CancellationToken);

        // Act
        context.PlexMusicTrackData.Remove(data);
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        var retained = await context.PlexMusicTrackComparisons.SingleAsync(CancellationToken);
        retained.RemotePlexLibraryId.ShouldBe(track.PlexLibraryId);
        retained.OwnedPlexLibraryId.ShouldBe(track.PlexLibraryId);
        retained.RemotePlexMediaId.ShouldBe(track.Id);
        retained.OwnedPlexMediaId.ShouldBe(track.Id);
        retained.RemotePlexMediaDataId.ShouldBeNull();
        retained.OwnedPlexMediaDataId.ShouldBeNull();
    }

    [Test]
    public async Task ShouldNullPhotoFolderReferenceWithoutChangingAssetOwnership_WhenFolderIsDeleted()
    {
        // Arrange
        await SetupDatabase(
            626558,
            config =>
            {
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;
        var asset = await context.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var libraryId = asset.PlexLibraryId;
        var serverId = asset.PlexServerId;
        var folder = await context.PlexPhotoFolders.SingleAsync(x => x.Id == asset.PlexPhotoFolderId, CancellationToken);

        // Act
        context.PlexPhotoFolders.Remove(folder);
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        var retained = await context.PlexPhotoMediaData.SingleAsync(CancellationToken);
        retained.PlexPhotoFolderId.ShouldBeNull();
        retained.PlexLibraryId.ShouldBe(libraryId);
        retained.PlexServerId.ShouldBe(serverId);
    }

    private static void AssertForeignKey(IEntityType entityType, ForeignKeyCase expected)
    {
        var foreignKeys = entityType
            .GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == expected.PrincipalType)
            .ToList();
        if (expected.ExpectedPrincipalForeignKeyCount is { } expectedCount)
            foreignKeys.Count.ShouldBe(expectedCount);

        foreignKeys.ShouldContain(foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(expected.DependentProperties)
            && (
                expected.PrincipalProperties == null
                || foreignKey.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(expected.PrincipalProperties)
            )
            && (expected.IsRequired == null || foreignKey.IsRequired == expected.IsRequired)
            && foreignKey.DeleteBehavior == expected.DeleteBehavior
        );
    }

    public static IEnumerable<ForeignKeyCase> ForeignKeyCases()
    {
        var sourceKey = new[] { nameof(BasePlexMedia.PlexLibraryId), nameof(BaseEntity.Id) };
        yield return new(
            typeof(PlexMusicAlbum),
            typeof(PlexMusicArtist),
            [nameof(PlexMusicAlbum.PlexLibraryId), nameof(PlexMusicAlbum.PlexArtistId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexMusicTrack),
            typeof(PlexMusicAlbum),
            [nameof(PlexMusicTrack.PlexLibraryId), nameof(PlexMusicTrack.PlexAlbumId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexMusicTrackMediaData),
            typeof(PlexMusicTrack),
            [nameof(BasePlexMediaData.PlexLibraryId), nameof(PlexMusicTrackMediaData.PlexTrackId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexOtherVideoMediaData),
            typeof(PlexOtherVideo),
            [nameof(BasePlexMediaData.PlexLibraryId), nameof(PlexOtherVideoMediaData.PlexOtherVideoId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexMusicArtistMediaQuality),
            typeof(PlexMusicArtist),
            [nameof(PlexMusicArtistMediaQuality.PlexLibraryId), nameof(PlexMusicArtistMediaQuality.PlexMusicArtistId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexMusicAlbumMediaQuality),
            typeof(PlexMusicAlbum),
            [nameof(PlexMusicAlbumMediaQuality.PlexLibraryId), nameof(PlexMusicAlbumMediaQuality.PlexMusicAlbumId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexMusicTrackComparison),
            typeof(PlexMusicTrackMediaData),
            [nameof(PlexMusicTrackComparison.RemotePlexMediaDataId)],
            null,
            DeleteBehavior.SetNull,
            IsRequired: false
        );
        yield return new(
            typeof(PlexMusicTrackComparison),
            typeof(PlexMusicTrackMediaData),
            [nameof(PlexMusicTrackComparison.OwnedPlexMediaDataId)],
            null,
            DeleteBehavior.SetNull,
            IsRequired: false
        );
        foreach (
            var comparisonCase in new[]
            {
                new ForeignKeyCase(
                    typeof(PlexMusicArtistComparison),
                    typeof(PlexMusicArtist),
                    [nameof(PlexMusicArtistComparison.RemotePlexLibraryId), nameof(PlexMusicArtistComparison.RemotePlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
                new ForeignKeyCase(
                    typeof(PlexMusicArtistComparison),
                    typeof(PlexMusicArtist),
                    [nameof(PlexMusicArtistComparison.OwnedPlexLibraryId), nameof(PlexMusicArtistComparison.OwnedPlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
                new ForeignKeyCase(
                    typeof(PlexMusicAlbumComparison),
                    typeof(PlexMusicAlbum),
                    [nameof(PlexMusicAlbumComparison.RemotePlexLibraryId), nameof(PlexMusicAlbumComparison.RemotePlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
                new ForeignKeyCase(
                    typeof(PlexMusicAlbumComparison),
                    typeof(PlexMusicAlbum),
                    [nameof(PlexMusicAlbumComparison.OwnedPlexLibraryId), nameof(PlexMusicAlbumComparison.OwnedPlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
                new ForeignKeyCase(
                    typeof(PlexMusicTrackComparison),
                    typeof(PlexMusicTrack),
                    [nameof(PlexMusicTrackComparison.RemotePlexLibraryId), nameof(PlexMusicTrackComparison.RemotePlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
                new ForeignKeyCase(
                    typeof(PlexMusicTrackComparison),
                    typeof(PlexMusicTrack),
                    [nameof(PlexMusicTrackComparison.OwnedPlexLibraryId), nameof(PlexMusicTrackComparison.OwnedPlexMediaId)],
                    sourceKey,
                    DeleteBehavior.Cascade,
                    ExpectedPrincipalForeignKeyCount: 2
                ),
            }
        )
            yield return comparisonCase;
        yield return new(
            typeof(PlexPhotoFolder),
            typeof(PlexPhotoFolder),
            [nameof(PlexPhotoFolder.PlexLibraryId), nameof(PlexPhotoFolder.ParentId)],
            sourceKey,
            DeleteBehavior.Cascade
        );
        yield return new(
            typeof(PlexPhotoMediaData),
            typeof(PlexPhotoFolder),
            [nameof(PlexPhotoMediaData.PlexPhotoFolderId)],
            null,
            DeleteBehavior.SetNull,
            IsRequired: false
        );
    }

    public sealed record ForeignKeyCase(
        Type DependentType,
        Type PrincipalType,
        IReadOnlyList<string> DependentProperties,
        IReadOnlyList<string>? PrincipalProperties,
        DeleteBehavior DeleteBehavior,
        bool? IsRequired = null,
        int? ExpectedPrincipalForeignKeyCount = null
    );

    private Task<Seed> SetupRelationshipConfigurationDatabase() =>
        SetupDatabase(
            626555,
            config =>
            {
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
                config.OtherVideoCount = 1;
            }
        );
}
