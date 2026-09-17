namespace Reaparr.Data.Configurations;

public class PlexMovieConfiguration : IEntityTypeConfiguration<PlexMovie>
{
    public void Configure(EntityTypeBuilder<PlexMovie> builder)
    {
        builder.HasIndex(x => x.SortIndex);
        builder.HasIndex(x => x.Quality);
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.Quality });
        // SearchMovieCommandHandler and SearchGenericCommandHandler use LIKE '<normalized-title>%';
        // NOCASE lets SQLite turn that case-insensitive prefix predicate into an index range.
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => x.SearchTitle);

        // SearchMovieCommandHandler and SearchGenericCommandHandler first restrict movies by accessible library,
        // then match Guid_TMDB; this index resolves those parent candidates before PlexMovieData is read.
        builder.HasIndex(x => new { x.PlexLibraryId, x.Guid_TMDB });

        // SearchMovieCommandHandler and SearchGenericCommandHandler first restrict movies by accessible library,
        // then match Guid_IMDB; this index resolves those parent candidates before PlexMovieData is read.
        builder.HasIndex(x => new { x.PlexLibraryId, x.Guid_IMDB });

        // SearchMovieCommandHandler and SearchGenericCommandHandler first restrict movies by accessible library,
        // then run a normalized SearchTitle prefix match; this index avoids scanning every movie in those libraries.
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });

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
