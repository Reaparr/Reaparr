namespace Reaparr.Data.UnitTests;

public class DbSetExtensionsPlexLibraryUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldApplyOverridesAndAccountLinks_WhenSelectingAndFilteringOwnership()
    {
        // Arrange
        await SetupDatabase(
            12400,
            cfg =>
            {
                cfg.PlexServerCount = 6;
                cfg.PlexMovieLibraryCount = 1;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(6);

        var overrideTrue = libraries[0];
        var overrideFalse = libraries[1];
        var libraryOwnedOnly = libraries[2];
        var serverOwnedOnly = libraries[3];
        var bothOwned = libraries[4];
        var noLinks = libraries[5];

        await dbContext.PlexServers
            .Where(x => x.Id != overrideTrue.PlexServerId && x.Id != overrideFalse.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, (bool?)null), CancellationToken);
        await dbContext.PlexServers
            .Where(x => x.Id == overrideTrue.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, true), CancellationToken);
        await dbContext.PlexServers
            .Where(x => x.Id == overrideFalse.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, false), CancellationToken);

        await dbContext.PlexAccountServers
            .Where(x => x.PlexServerId == libraryOwnedOnly.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsServerOwned, false), CancellationToken);
        await dbContext.PlexAccountLibraries
            .Where(x => x.PlexLibraryId == serverOwnedOnly.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsLibraryOwned, false), CancellationToken);
        await dbContext.PlexAccountLibraries
            .Where(x => x.PlexLibraryId == overrideTrue.Id || x.PlexLibraryId == noLinks.Id)
            .ExecuteDeleteAsync(CancellationToken);
        await dbContext.PlexAccountServers
            .Where(x => x.PlexServerId == overrideTrue.PlexServerId || x.PlexServerId == noLinks.PlexServerId)
            .ExecuteDeleteAsync(CancellationToken);

        var configuredOwnership = await dbContext
            .PlexLibraries.OrderBy(x => x.PlexServerId)
            .Select(x =>
                new
                {
                    x.Id,
                    x.PlexServerId,
                    OwnedOverride = x.PlexServer!.OwnedOverride,
                    LibraryOwned = x.PlexAccountLibraries.Any(y => y.IsLibraryOwned),
                    ServerOwned = x.PlexServer.PlexAccountServers.Any(y => y.IsServerOwned),
                }
            )
            .ToListAsync(CancellationToken);
        configuredOwnership.ShouldBe(
            [
                new
                {
                    Id = overrideTrue.Id,
                    PlexServerId = overrideTrue.PlexServerId,
                    OwnedOverride = (bool?)true,
                    LibraryOwned = false,
                    ServerOwned = false,
                },
                new
                {
                    Id = overrideFalse.Id,
                    PlexServerId = overrideFalse.PlexServerId,
                    OwnedOverride = (bool?)false,
                    LibraryOwned = true,
                    ServerOwned = true,
                },
                new
                {
                    Id = libraryOwnedOnly.Id,
                    PlexServerId = libraryOwnedOnly.PlexServerId,
                    OwnedOverride = (bool?)null,
                    LibraryOwned = true,
                    ServerOwned = false,
                },
                new
                {
                    Id = serverOwnedOnly.Id,
                    PlexServerId = serverOwnedOnly.PlexServerId,
                    OwnedOverride = (bool?)null,
                    LibraryOwned = false,
                    ServerOwned = true,
                },
                new
                {
                    Id = bothOwned.Id,
                    PlexServerId = bothOwned.PlexServerId,
                    OwnedOverride = (bool?)null,
                    LibraryOwned = true,
                    ServerOwned = true,
                },
                new
                {
                    Id = noLinks.Id,
                    PlexServerId = noLinks.PlexServerId,
                    OwnedOverride = (bool?)null,
                    LibraryOwned = false,
                    ServerOwned = false,
                },
            ]
        );
        (
            await dbContext.PlexAccountLibraries.CountAsync(
                x => x.PlexLibraryId == overrideTrue.Id || x.PlexLibraryId == noLinks.Id,
                CancellationToken
            )
        ).ShouldBe(0);
        (
            await dbContext.PlexAccountServers.CountAsync(
                x => x.PlexServerId == overrideTrue.PlexServerId || x.PlexServerId == noLinks.PlexServerId,
                CancellationToken
            )
        ).ShouldBe(0);

        // Act
        var ownershipProjection = await dbContext.PlexLibraries
            .OrderBy(x => x.Id)
            .SelectOwnership()
            .ToListAsync(CancellationToken);
        var ownedLibraryIds = await dbContext.PlexLibraries
            .WhereIsOwned()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var notOwnedLibraryIds = await dbContext.PlexLibraries
            .WhereIsNotOwned()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // Assert
        ownershipProjection.ShouldBe(
            new[]
            {
                new PlexLibraryOwnership(overrideTrue.Id, overrideTrue.Type, true),
                new PlexLibraryOwnership(overrideFalse.Id, overrideFalse.Type, false),
                new PlexLibraryOwnership(libraryOwnedOnly.Id, libraryOwnedOnly.Type, true),
                new PlexLibraryOwnership(serverOwnedOnly.Id, serverOwnedOnly.Type, true),
                new PlexLibraryOwnership(bothOwned.Id, bothOwned.Type, true),
                new PlexLibraryOwnership(noLinks.Id, noLinks.Type, false),
            }.OrderBy(x => x.Id)
        );
        ownedLibraryIds.ShouldBe(
            new[] { overrideTrue.Id, libraryOwnedOnly.Id, serverOwnedOnly.Id, bothOwned.Id }.OrderBy(x => x)
        );
        notOwnedLibraryIds.ShouldBe(new[] { overrideFalse.Id, noLinks.Id }.OrderBy(x => x));
    }
}
