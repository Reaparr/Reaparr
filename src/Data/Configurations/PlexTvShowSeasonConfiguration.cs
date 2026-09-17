namespace Reaparr.Data.Configurations;

public class PlexTvShowSeasonConfiguration : IEntityTypeConfiguration<PlexTvShowSeason>
{
    public void Configure(EntityTypeBuilder<PlexTvShowSeason> builder)
    {
        builder.Ignore(x => x.Quality);

        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        // GetMediaDetailByIdEndpoint.GetPlexTvShow loads every season with Where(x => x.TvShowId == id);
        // keeping the foreign-key index explicit prevents that show-detail lookup from scanning all seasons.
        builder.HasIndex(x => x.TvShowId);

        // SearchTvShowCommandHandler combines a matched show with command.Season;
        // this index resolves the requested season before its episode candidates are evaluated.
        builder.HasIndex(x => new { x.TvShowId, x.SeasonNumber });

        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });

        builder
            .HasMany(x => x.Qualities)
            .WithOne(x => x.PlexTvShowSeason)
            .HasForeignKey(x => x.PlexTvShowSeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
