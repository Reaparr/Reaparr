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
        // Torznab RSS scans this index in reverse for its global AddedAt/server/rating-key order.
        // PlexLibraryId keeps the access filter covered without changing that required ordering.
        builder.HasIndex(x => new
        {
            x.AddedAt,
            x.PlexServerId,
            x.PlexApiRatingKey,
            x.PlexLibraryId,
        });

        builder
            .HasMany(x => x.MediaDataList)
            .WithOne(x => x.PlexTvShowEpisode)
            .HasForeignKey(x => x.PlexTvShowEpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
