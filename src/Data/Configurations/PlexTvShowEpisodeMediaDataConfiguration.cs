namespace Reaparr.Data.Configurations;

public class PlexTvShowEpisodeMediaDataConfiguration : IEntityTypeConfiguration<PlexTvShowEpisodeMediaData>
{
    public void Configure(EntityTypeBuilder<PlexTvShowEpisodeMediaData> builder)
    {
        builder.HasIndex(x => x.Quality);
        builder.HasIndex(x => new { x.PlexTvShowEpisodeId, x.Quality });
        builder.HasIndex(x => x.PlexApiRatingKey);

        builder
            .HasOne(x => x.PlexTvShowEpisode)
            .WithMany(x => x.MediaDataList)
            .HasForeignKey(x => x.PlexTvShowEpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
