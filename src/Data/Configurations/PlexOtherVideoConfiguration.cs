namespace Reaparr.Data;

public class PlexOtherVideoConfiguration : IEntityTypeConfiguration<PlexOtherVideo>
{
    public void Configure(EntityTypeBuilder<PlexOtherVideo> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.HasOne(x => x.PlexLibrary).WithMany(x => x.OtherVideos).HasForeignKey(x => x.PlexLibraryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PlexServer).WithMany().HasForeignKey(x => x.PlexServerId).OnDelete(DeleteBehavior.Cascade);
    }
}
