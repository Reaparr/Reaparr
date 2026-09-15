namespace Reaparr.Data.Configurations;

public class PlexTvShowEpisodeConfiguration : IEntityTypeConfiguration<PlexTvShowEpisode>
{
    public void Configure(EntityTypeBuilder<PlexTvShowEpisode> builder)
    {
        builder.Ignore(x => x.Quality);

        builder.HasIndex(x => x.SortIndex);
        builder.HasIndex(x => new { x.TvShowSeasonId, x.SortIndex });
        builder.HasIndex(x => new { x.TvShowId, x.SortIndex });

        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        // The leading library equality and trailing feed order let each bounded library query use the index
        // directly instead of sorting every accessible episode.
        builder.HasIndex(x => new
        {
            x.PlexLibraryId,
            x.AddedAt,
            x.PlexServerId,
            x.PlexApiRatingKey,
        });

        builder
            .HasMany(x => x.MediaDataList)
            .WithOne(x => x.PlexTvShowEpisode)
            .HasForeignKey(x => x.PlexTvShowEpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
