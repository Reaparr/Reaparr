namespace Reaparr.Data.Configurations;

public class MediaOverviewMovieSnapshotConfiguration : IEntityTypeConfiguration<MediaOverviewMovieSnapshot>
{
    public void Configure(EntityTypeBuilder<MediaOverviewMovieSnapshot> builder)
    {
        builder.HasIndex(x => x.PlexMovieId).IsUnique();
        builder.HasIndex(x => x.TitleRank);
        builder.HasIndex(x => x.YearRank);
        builder.HasIndex(x => x.AddedAtRank);
        builder.HasIndex(x => x.UpdatedAtRank);
        builder.HasIndex(x => x.DurationRank);
        builder.HasIndex(x => x.MediaSizeRank);
        builder.HasIndex(x => x.QualityRank);

        builder
            .HasOne(x => x.PlexMovie)
            .WithOne()
            .HasForeignKey<MediaOverviewMovieSnapshot>(x => x.PlexMovieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
