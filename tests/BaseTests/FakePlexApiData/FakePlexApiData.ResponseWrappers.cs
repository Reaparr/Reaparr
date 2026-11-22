using System.Text;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    /// <summary>
    /// Allows you to generate a fake response for the PostUsersSignInData operation
    /// Works with HttpStatusCode.Created and HttpStatusCode.Unauthorized
    /// </summary>
    /// <param name="statusCode"> The status code to return </param>
    /// <param name="seed"> The seed to use for the faker data </param>
    /// <param name="request"> The request message that was sent </param>
    /// <returns></returns>
    public static PostUsersSignInDataResponse PostUsersSignInDataResponse(
        HttpStatusCode statusCode,
        Seed seed,
        HttpRequestMessage? request = null
    )
    {
        return new Faker<PostUsersSignInDataResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(
                x => x.UserPlexAccount,
                _ => statusCode == HttpStatusCode.Created ? GetPlexSignInResponse(seed).Generate() : null
            )
            .RuleFor(
                x => x.RawResponse,
                (_, res) =>
                    statusCode == HttpStatusCode.Created
                        ? GetHttpResponseMessage(statusCode, res.UserPlexAccount, request)
                        : GetHttpResponseMessage(statusCode, GetFailedPlexSignInResponse(), request)
            )
            .Generate();
    }

    public static GetServerResourcesResponse GetServerResourcesResponse(
        HttpStatusCode statusCode,
        Seed seed,
        List<PlexDevice>? devices = null,
        HttpRequestMessage? request = null,
        Action<PlexApiDataConfig>? options = null
    )
    {
        var config = PlexApiDataConfig.FromOptions(options);

        return new Faker<GetServerResourcesResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(
                x => x.PlexDevices,
                _ => devices ?? GetServerResource(seed, options).Generate(config.PlexServerAccessCount)
            )
            .RuleFor(
                x => x.RawResponse,
                (_, res) =>
                {
                    switch ((HttpStatusCode)res.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return GetHttpResponseMessage(statusCode, res.PlexDevices, request);
                        case HttpStatusCode.Unauthorized:
                            return GetPlexUnauthorizedResponseMessage(request);
                        default:
                            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, null);
                    }
                }
            )
            .Generate();
    }

    public static GetSectionsResponse GetAllLibrariesResponse(
        HttpStatusCode statusCode,
        Seed seed,
        HttpRequestMessage? request = null,
        Action<PlexApiDataConfig>? options = null
    )
    {
        var config = PlexApiDataConfig.FromOptions(options);

        var mediaContainer = new Faker<GetSectionsMediaContainer>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(
                x => x.AllowSync,
                f => LukeHagar.PlexAPI.SDK.Models.Components.AllowSync.CreateBoolean(f.Random.Bool())
            )
            .RuleFor(x => x.Title1, f => f.Company.CompanyName())
            .RuleFor(
                x => x.Directory,
                (f, _) =>
                    GetLibrariesResponseDirectory(seed, f.PlexApi().LibraryType.ToPlexMediaType())
                        .Generate(config.LibraryCount())
            )
            .RuleFor(x => x.Size, (_, x) => x.Directory!.Count)
            .RuleFor(x => x.TotalSize, _ => config.LibraryCount())
            .Ignore(x => x.Offset)
            .Ignore(x => x.Identifier)
            .FinishWith(
                (_, x) =>
                {
                    // Directory might take a while to generate
                    x.Size = x.Directory!.Count;
                }
            );

        var body = new Faker<GetSectionsResponseBody>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.MediaContainer, _ => mediaContainer.Generate())
            .Generate();

        return new Faker<GetSectionsResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(x => x.Object, _ => body)
            .RuleFor(x => x.RawResponse, (_, res) => GetHttpResponseMessage(statusCode, res.Object, request))
            .Ignore(x => x.Headers)
            .Generate();
    }

    /// <summary>
    /// Generates a fake response for the GetLibraryItemsResponse operation
    /// URL: /library/sections/{sectionKey}/{tag}
    /// </summary>
    public static GetLibraryItemsResponse GetLibraryMediaItemsResponse(
        HttpStatusCode statusCode,
        Seed seed,
        LibrarySection library,
        MediaContainerWithMetadata? responseBody = null,
        HttpRequestMessage? request = null,
        Action<PlexApiDataConfig>? options = null
    )
    {
        return new Faker<GetLibraryItemsResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(
                x => x.MediaContainerWithMetadata,
                _ => responseBody ?? GetPlexLibrarySectionAllResponse(seed, library, 100, options)
            )
            .RuleFor(
                x => x.RawResponse,
                (_, res) => GetHttpResponseMessage(statusCode, res.MediaContainerWithMetadata, request)
            )
            .Ignore(x => x.Headers)
            .Generate();
    }

    public static GetIdentityResponse GetPlexServerIdentityResponse(
        HttpStatusCode statusCode,
        Seed seed,
        HttpRequestMessage? request = null
    )
    {
        var container = new Faker<GetIdentityMediaContainer>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.Size, _ => 0L)
            .RuleFor(x => x.Claimed, f => f.Random.Bool())
            .RuleFor(x => x.MachineIdentifier, f => f.PlexApi().MachineIdentifier)
            .RuleFor(x => x.Version, f => f.PlexApi().PlexVersion);

        var body = new Faker<GetIdentityResponseBody>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.MediaContainer, _ => container.Generate())
            .Generate();

        return new Faker<GetIdentityResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(x => x.Object, _ => body)
            .RuleFor(x => x.RawResponse, (_, res) => GetHttpResponseMessage(statusCode, res.Object, request))
            .Generate();
    }

    public static HttpResponseMessage GetPlexUnauthorizedResponseMessage(HttpRequestMessage? request)
    {
        var html401 = "<html><head><title>Unauthorized</title></head><body><h1>401 Unauthorized</h1></body></html>";

        return new HttpResponseMessage
        {
            Content = new StringContent(html401, Encoding.UTF8, "text/html"),
            ReasonPhrase = "Unauthorized",
            RequestMessage = request,
            StatusCode = HttpStatusCode.Unauthorized,
            Version = new Version(1, 1),
        };
    }
}
