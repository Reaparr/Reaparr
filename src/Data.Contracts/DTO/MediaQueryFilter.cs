using System.Security.Cryptography;
using System.Text;
using FlexQuery.NET.Models;

namespace Reaparr.Data.Contracts;

public record MediaQueryFilter
{
    public required PlexMediaType MediaType { get; init; }

    /// <summary>
    /// Is > 0 when a specific <see cref="PlexLibrary"/> is requested, and 0 when all are requested.
    /// </summary>
    public required int PlexLibraryId { get; init; }

    public required bool FilterOfflineMedia { get; init; }

    public required bool FilterOwnedMedia { get; init; }

    public PlexMediaComparisonState? ComparisonState { get; init; }

    public required FlexQueryParameters Parameters { get; init; }

    public string QueryHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(BuildHashInput())));

    public int Page => Parameters.Page ?? 1;
    public int PageSize => Parameters.PageSize ?? MaximumPageSize;

    public static readonly int MaximumPageSize = 100;

    private string BuildHashInput()
    {
        var parameters = Parameters;

        return string.Join(
            '\n',
            $"MediaType={MediaType}",
            $"PlexLibraryId={PlexLibraryId}",
            $"FilterOfflineMedia={FilterOfflineMedia}",
            $"FilterOwnedMedia={FilterOwnedMedia}",
            $"ComparisonState={ComparisonState?.ToString() ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Query)}={parameters.Query ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Filter)}={parameters.Filter ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Sort)}={parameters.Sort ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Select)}={parameters.Select ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Includes)}={parameters.Includes ?? string.Empty}",
            $"{nameof(FlexQueryParameters.GroupBy)}={parameters.GroupBy ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Having)}={parameters.Having ?? string.Empty}",
            $"{nameof(FlexQueryParameters.PageSize)}={parameters.PageSize?.ToString() ?? string.Empty}",
            $"{nameof(FlexQueryParameters.IncludeCount)}={parameters.IncludeCount?.ToString() ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Distinct)}={parameters.Distinct?.ToString() ?? string.Empty}",
            $"{nameof(FlexQueryParameters.Mode)}={parameters.Mode ?? string.Empty}"
        );
    }
}
