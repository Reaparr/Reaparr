namespace Reaparr.Data;

public class PlexMusicArtistComparisonConfiguration : IEntityTypeConfiguration<PlexMusicArtistComparison>
{
    public void Configure(EntityTypeBuilder<PlexMusicArtistComparison> builder)
    {
        builder.HasIndex(x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId });
        builder.HasIndex(x => new { x.RemotePlexLibraryId, x.OwnedPlexLibraryId });
        builder.HasIndex(x => new
        {
            x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.OwnedPlexMediaId,
        }).IsUnique();
        builder.HasOne<PlexLibrary>().WithMany().HasForeignKey(x => x.RemotePlexLibraryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlexLibrary>().WithMany().HasForeignKey(x => x.OwnedPlexLibraryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(x => x.RemotePlexMediaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(x => x.OwnedPlexMediaId).OnDelete(DeleteBehavior.Cascade);
    }
}
