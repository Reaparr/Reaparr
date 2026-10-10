namespace Reaparr.Data;

public abstract class MediaOverviewSnapshotConfigurationBase<TSnapshot> : IEntityTypeConfiguration<TSnapshot>
    where TSnapshot : BaseMediaOverviewSnapshot
{
    public virtual void Configure(EntityTypeBuilder<TSnapshot> builder)
    {
        // Keep independent SQLite identities; TPC requires unique keys across the hierarchy.
        builder.HasBaseType((Type?)null);

        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany()
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PlexLibraryId, x.TitleRank });
        builder.HasIndex(x => new { x.PlexLibraryId, x.YearRank });
        builder.HasIndex(x => new { x.PlexLibraryId, x.AddedAtRank });
        builder.HasIndex(x => new { x.PlexLibraryId, x.UpdatedAtRank });
        builder.HasIndex(x => new { x.PlexLibraryId, x.DurationRank });
        builder.HasIndex(x => new { x.PlexLibraryId, x.MediaSizeRank });
    }
}
