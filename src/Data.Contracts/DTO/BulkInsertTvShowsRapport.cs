namespace Reaparr.Data.Contracts;

public record BulkInsertTvShowsRapport
{
    public int CreatedTvShows { get; set; }

    public int UpdatedTvShows { get; set; }

    public int DeletedTvShows { get; set; }

    public int UnchangedTvShows { get; set; }

    public int CreatedSeasons { get; set; }

    public int UpdatedSeasons { get; set; }

    public int DeletedSeasons { get; set; }

    public int UnchangedSeasons { get; set; }

    public int CreatedEpisodes { get; set; }

    public int UpdatedEpisodes { get; set; }

    public int DeletedEpisodes { get; set; }

    public int UnchangedEpisodes { get; set; }

    public override string ToString() => $"""
                CreatedTvShows: {CreatedTvShows}
                UpdatedTvShows: {UpdatedTvShows}
                DeletedTvShows: {DeletedTvShows}
                UnchangedTvShows: {UnchangedTvShows}
                CreatedSeasons: {CreatedSeasons}
                UpdatedSeasons: {UpdatedSeasons}
                DeletedSeasons: {DeletedSeasons}
                UnchangedSeasons: {UnchangedSeasons}
                CreatedEpisodes: {CreatedEpisodes}
                UpdatedEpisodes: {UpdatedEpisodes}
                DeletedEpisodes: {DeletedEpisodes}
                UnchangedEpisodes: {UnchangedEpisodes}
        """;
}
