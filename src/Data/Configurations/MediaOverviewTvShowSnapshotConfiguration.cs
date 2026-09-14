namespace Reaparr.Data.Configurations;

public class MediaOverviewTvShowSnapshotConfiguration : IEntityTypeConfiguration<MediaOverviewTvShowSnapshot>
{
    public void Configure(EntityTypeBuilder<MediaOverviewTvShowSnapshot> builder)
    {
        builder.HasIndex(x => x.PlexTvShowId).IsUnique();
        builder.HasIndex(x => x.TitleRank);
        builder.HasIndex(x => x.YearRank);
        builder.HasIndex(x => x.AddedAtRank);
        builder.HasIndex(x => x.UpdatedAtRank);
        builder.HasIndex(x => x.DurationRank);
        builder.HasIndex(x => x.MediaSizeRank);
        builder.HasIndex(x => x.QualityRank);

        builder
            .HasOne(x => x.PlexTvShow)
            .WithOne()
            .HasForeignKey<MediaOverviewTvShowSnapshot>(x => x.PlexTvShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
