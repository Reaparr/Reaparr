using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    public static GetMetadataItemResponse GetLibrarySectionsAllResponse(
        HttpStatusCode statusCode,
        Seed seed,
        LibrarySection library,
        MediaContainerWithMetadata? responseBody = null,
        HttpRequestMessage? request = null
    )
    {
        return new Faker<GetMetadataItemResponse>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .Ignore(x => x.Headers)
            .RuleFor(
                x => x.MediaContainerWithMetadata,
                _ => responseBody ?? GetLibrarySectionsAllResponseBody(seed, library, 100)
            )
            .RuleFor(
                x => x.RawResponse,
                (_, res) => GetHttpResponseMessage(statusCode, res.MediaContainerWithMetadata, request)
            )
            .Generate();
    }

    public static MediaContainerWithMetadata GetLibrarySectionsAllResponseBody(
        Seed seed,
        LibrarySection library,
        int mediaCount = 0
    )
    {
        var type = library.Type.ToPlexMediaType();

        return new MediaContainerWithMetadata
        {
            MediaContainer = _getLibrarySectionsAllMediaContainer
                .UseSeed(seed.Next())
                .FinishWith(
                    (_, x) =>
                    {
                        x.Metadata = GetLibrarySectionsAllMetadata(seed, type).Generate(mediaCount);
                        x.Size = x.Metadata!.Count;
                        x.TotalSize = x.Metadata!.Count;
                    }
                )
                .Generate(),
        };
    }

    public static Faker<Metadata> GetLibrarySectionsAllMetadata(Seed seed, PlexMediaType type)
    {
        return _getLibrarySectionsAllMetadata
            .UseSeed(seed.Next())
            .FinishWith(
                (f, x) =>
                {
                    x.Type = type.ToPlexApiString();
                    x.Media = [GetLibrarySectionsAllMedia(seed).Generate()];
                    x.Guid = f.PlexMedia().Guid(type);
                }
            );
    }

    public static Faker<Media> GetLibrarySectionsAllMedia(Seed seed)
    {
        return _getLibrarySectionsAllMedia
            .UseSeed(seed.Next())
            .FinishWith(
                (_, x) =>
                {
                    x.Part = [GetLibrarySectionsAllPart(seed).Generate()];
                }
            );
    }

    public static Faker<Part> GetLibrarySectionsAllPart(Seed seed) =>
        _getLibrarySectionsAllPartFaker
            .UseSeed(seed.Next())
            .FinishWith(
                (_, x) =>
                {
                    x.Stream = [_getLibrarySectionsAllStreamFaker.Generate()];
                }
            );
}
