namespace Reaparr.Data.Configurations;

public class PlexMovieConfiguration : IEntityTypeConfiguration<PlexMovie>
{
    public void Configure(EntityTypeBuilder<PlexMovie> builder)
    {
        builder.HasIndex(x => x.SortIndex);
        builder.HasIndex(x => x.Quality);
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.Quality });
        builder.HasIndex(x => x.SearchTitle);

        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        // The leading library equality and trailing feed order let each bounded library query use the index
        // directly instead of sorting every accessible movie.
        builder.HasIndex(x => new
        {
            x.PlexLibraryId,
            x.AddedAt,
            x.PlexServerId,
            x.PlexApiRatingKey,
        });

        builder
            .HasMany(x => x.MediaDataList)
            .WithOne(x => x.PlexMovie)
            .HasForeignKey(x => x.PlexMovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Actors)
            .WithMany(x => x.PlexMovieActors)
            .UsingEntity<PlexMovieActors>(
                l => l.HasOne<PlexActor>().WithMany().HasForeignKey(e => e.PlexActorId),
                r => r.HasOne<PlexMovie>().WithMany().HasForeignKey(e => e.PlexMovieId)
            );

        builder
            .HasMany(x => x.Genres)
            .WithMany(x => x.PlexMovieGenres)
            .UsingEntity<PlexMovieGenres>(
                l => l.HasOne<PlexGenre>().WithMany().HasForeignKey(e => e.GenresId),
                r => r.HasOne<PlexMovie>().WithMany().HasForeignKey(e => e.PlexMovieId)
            );

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexMovieCountries)
            .UsingEntity<PlexMovieCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexMovie>().WithMany().HasForeignKey(e => e.PlexMovieId)
            );
    }
}
