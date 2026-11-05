using System.Net.Http.Json;
using Semver;

namespace Reaparr.Application;

/// <summary>
/// Command to fetch the latest releases from GitHub for the Reaparr repository. This is used to check for updates and notify users of new versions.
/// This also filters to only return new releases based on the current version and if this Reaparr instance is running on the dev or stable channel, so that users only see relevant releases in update notifications.
/// </summary>
public record GetGitHubReleasesCommand : ICommand<Result<IReadOnlyList<ReleaseNote>>>;

public class GetGitHubReleasesCommandValidator : AbstractValidator<GetGitHubReleasesCommand>
{
    public GetGitHubReleasesCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class GetGitHubReleasesCommandHandler
    : ICommandHandler<GetGitHubReleasesCommand, Result<IReadOnlyList<ReleaseNote>>>
{
    private readonly ILogger _log;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppBuildInfo _appBuildInfo;

    public GetGitHubReleasesCommandHandler(
        ILogger log,
        IHttpClientFactory httpClientFactory,
        IAppBuildInfo appBuildInfo
    )
    {
        _log = log.ForContext<GetGitHubReleasesCommandHandler>();
        _httpClientFactory = httpClientFactory;
        _appBuildInfo = appBuildInfo;
    }

    public async Task<Result<IReadOnlyList<ReleaseNote>>> ExecuteAsync(
        GetGitHubReleasesCommand command,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var client = _httpClientFactory.CreateGitHubHttpClient();

            var currentVersionString = _appBuildInfo.InformationalVersion;
            if (!TryParseSemVersion(currentVersionString, out var currentVersion))
            {
                _log.Here()
                    .Warning(
                        "Failed to parse current informational version {CurrentVersion}; falling back to 0.0.0 for release filtering",
                        currentVersionString
                    );
                currentVersion = new SemVersion(0);
            }

            var isDevBuild = _appBuildInfo.IsDevRelease;
            const int pageSize = 100;
            var filteredReleases = new List<GitHubReleaseDTO>();
            for (var page = 1; ; page++)
            {
                var releases = await client.GetFromJsonAsync<IReadOnlyList<GitHubReleaseDTO>>(
                    $"repos/Reaparr/Reaparr/releases?per_page={pageSize}&page={page}",
                    cancellationToken
                );
                if (releases is null or { Count: 0 })
                    break;

                filteredReleases.AddRange(
                    releases
                        .Where(x => x.Prerelease == isDevBuild)
                        .Where(x =>
                            TryParseReleaseVersion(x.TagName, out var parsedVersion)
                            && parsedVersion.ComparePrecedenceTo(currentVersion) > 0
                        )
                );

                if (releases.Count < pageSize)
                    break;
            }

            return Result.Ok(filteredReleases.ToReleaseNotes());
        }
        catch (Exception ex)
        {
            _log.Here().Warning(ex, "Failed to fetch GitHub releases");
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    private bool TryParseReleaseVersion(string version, out SemVersion semVersion)
    {
        if (TryParseSemVersion(version, out semVersion))
            return true;

        _log.Here().Warning("Skipping GitHub release with malformed tag {TagName}", version);
        return false;
    }

    private static bool TryParseSemVersion(string version, out SemVersion semVersion)
    {
        var success = SemVersion.TryParse(version.TrimStart('v'), SemVersionStyles.Any, out var parsedVersion);
        semVersion = parsedVersion ?? new SemVersion(0);
        return success;
    }
}
