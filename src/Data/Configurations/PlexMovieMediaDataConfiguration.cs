namespace Reaparr.Data.Configurations;

public class PlexMovieMediaDataConfiguration : IEntityTypeConfiguration<PlexMovieMediaData>
{
    public void Configure(EntityTypeBuilder<PlexMovieMediaData> builder)
    {
        builder.HasIndex(x => x.Quality);
        builder.HasIndex(x => new { x.PlexMovieId, x.Quality });
        builder.HasIndex(x => x.PlexApiRatingKey);

        builder
            .HasOne(x => x.PlexMovie)
            .WithMany(x => x.MediaDataList)
            .HasForeignKey(x => x.PlexMovieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
