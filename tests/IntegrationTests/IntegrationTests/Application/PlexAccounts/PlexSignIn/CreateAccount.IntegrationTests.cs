namespace Reaparr.IntegrationTests;

public class CreateAccountIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldCreateAndInspectAccessibleServers_WhenPlexAccountIsValid()
    {
        // Arrange
        var seed = new Seed(236234);
        var serverCount = 1;
        var libraryCount = 3;

        using var container = await CreateContainer(
            seed.Next(),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    // Start from an empty database
                    x.PlexServerCount = 0;
                    x.PlexMovieLibraryCount = 0;
                    x.PlexAccountCount = 0;
                    x.PlexServerConnectionPerServerCount = 1;
                };

                config.BaseMockHttpClientOptions = x =>
                {
                    x.PlexServerAccessCount = serverCount;
                    x.MovieLibraryCount = libraryCount;
                    x.MoviesPerLibraryCount = 25;
                };

                config.OverrideServices = builder =>
                    builder
                        .Register(_ =>
                            new FakeCommandExecutor()
                                .Intercept<QueueLibrarySyncJobCommand, Result>(
                                    (_, _) => Task.FromResult(Result.Ok())
                                )
                                .Intercept<QueueMediaOverviewRebuildCommand, Result>(
                                    (_, _) => Task.FromResult(Result.Ok())
                                )
                        )
                        .As<ICommandExecutor>()
                        .InstancePerDependency();
            }
        );

        var plexAccount = FakeData.GetPlexAccount(4347564).Generate();
        var plexAccountDTO = new CreatePlexAccountEndpointRequest
        {
            DisplayName = plexAccount.DisplayName,
            Username = plexAccount.Username,
            Password = plexAccount.Password,
            IsEnabled = plexAccount.IsEnabled,
            IsMain = plexAccount.IsMain,
            IsValidated = plexAccount.IsValidated,
            ValidatedAt = plexAccount.ValidatedAt,
            Uuid = plexAccount.Uuid,
            PlexId = plexAccount.PlexId,
            Email = plexAccount.Email,
            Title = plexAccount.Title,
            ClientId = plexAccount.ClientId,
            Is2Fa = plexAccount.Is2Fa,
            CustomAuthenticationToken = plexAccount.CustomAuthenticationToken,
            AuthenticationToken = plexAccount.AuthenticationToken,
        };

        // Act
        var client = container.GetApiClient();
        await client.SignIn();
        var response = await client.POSTAsync<
            CreatePlexAccountEndpoint,
            CreatePlexAccountEndpointRequest,
            ResultDTO<PlexAccountDTO>
        >(plexAccountDTO);
        response.Response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var resultDTO = response.Result;
        resultDTO.IsSuccess.ShouldBeTrue();

        await WaitForDatabaseConditionAsync(
            () =>
            {
                var account = container
                    .DbContext.PlexAccounts.AsNoTracking()
                    .Include(x => x.PlexAccountServers)
                    .Include(x => x.PlexAccountLibraries)
                    .FirstOrDefault();
                return account is not null
                    && account.PlexAccountServers.Count == serverCount
                    && account.PlexAccountLibraries.Count == libraryCount;
            },
            maxRetries: 30,
            delayMs: 500
        );

        await WaitForDatabaseConditionAsync(
            () =>
            {
                var updates = container.MockProgressHubService.JobStatusUpdateList.ToList();
                return updates.Any(x => x is { JobType: JobTypes.InspectPlexServerJob, Status: JobStatus.Started })
                    && updates.Any(x => x is { JobType: JobTypes.InspectPlexServerJob, Status: JobStatus.Completed });
            },
            maxRetries: 20,
            delayMs: 250
        );

        // Assert
        resultDTO.IsSuccess.ShouldBeTrue();
        container.DbContext.PlexAccounts.ToList().Count.ShouldBe(1);

        // Ensure an account has been created
        var plexAccountDb = container
            .DbContext.PlexAccounts.Include(x => x.PlexAccountLibraries)
                .ThenInclude(x => x.PlexLibrary)
            .Include(x => x.PlexAccountServers)
                .ThenInclude(x => x.PlexServer)
            .FirstOrDefault();

        plexAccountDb.ShouldNotBeNull();
        plexAccountDb.IsValidated.ShouldBeTrue();
        plexAccountDb.PlexServers.Count.ShouldBe(1);
        plexAccountDb.DisplayName.ShouldBe(plexAccountDTO.DisplayName);
        plexAccountDb.Username.ShouldBe(plexAccountDTO.Username);
        plexAccountDb.Password.ShouldBe(plexAccountDTO.Password);
        plexAccountDb.PlexAccountLibraries.Count.ShouldBe(libraryCount);

        // Ensure PlexServer has been created
        container.DbContext.PlexServers.ToList().Count.ShouldBe(serverCount);

        var serverCountDb = container.DbContext.PlexServers.Count();
        var jobStatusUpdateList = container.MockProgressHubService.JobStatusUpdateList.ToList();

        plexAccountDb.PlexServers.Count.ShouldBe(serverCount);
        serverCountDb.ShouldBe(serverCount);

        var plexServersDb = container
            .DbContext.PlexServers.Include(x => x.PlexLibraries)
            .IncludeLibrariesWithMedia()
            .FirstOrDefault();
        plexServersDb.ShouldNotBeNull();
        plexServersDb.MachineIdentifier.ShouldNotBeEmpty();

        // Libraries may or may not be created depending on job completion
        plexAccountDb.PlexAccountLibraries.Count.ShouldBe(libraryCount);
        plexServersDb.PlexLibraries.Count.ShouldBe(libraryCount);

        // Verify job notifications if jobs ran
        jobStatusUpdateList.Count.ShouldBeGreaterThanOrEqualTo(2);
        jobStatusUpdateList
            .Any(x => x is { JobType: JobTypes.InspectPlexServerJob, Status: JobStatus.Started })
            .ShouldBeTrue();
        jobStatusUpdateList
            .Any(x => x is { JobType: JobTypes.InspectPlexServerJob, Status: JobStatus.Completed })
            .ShouldBeTrue();
    }
}
