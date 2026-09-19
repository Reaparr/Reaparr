import { route, headers } from '@fixtures';
import { generateResultDTO } from '@mock';
import { PlexMediaPaths } from '@api/api-paths';
import { PlexMediaComparisonState, PlexMediaType, VideoQuality } from '@dto';
import { getPlexMediaComparisonStateId } from '@composables';

const getVisibleCy = (selector: string) => cy.getCy(selector).filter(':visible').first();

function openComparisonFilterMenu() {
	getVisibleCy('media-overview-filter-btn').click();
	getVisibleCy('media-filter-menu-category-comparisonState').click();
}

describe('Comparison poster status UX', () => {
	it('Should show comparison badges in the filter menu for all-media movie overview', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 2,
			plexMovieLibraryCount: 2,
			movieCount: 2,
			isLoggedIn: true,
		})
			.then(({ mediaData }) => {
				mediaData[0]!.media[0]!.comparisonId = getPlexMediaComparisonStateId(PlexMediaComparisonState.HigherQuality);
				mediaData[1]!.media[0]!.comparisonId = getPlexMediaComparisonStateId(PlexMediaComparisonState.Missing);

				cy.visit(route('/'));
				openComparisonFilterMenu();

				getVisibleCy(`comparison-filter-option-${PlexMediaComparisonState.NotCompared}`).should('be.visible');
				getVisibleCy(`comparison-filter-option-${PlexMediaComparisonState.Owned}`).should('be.visible');
				getVisibleCy(`comparison-filter-option-${PlexMediaComparisonState.HigherQuality}`).should('be.visible');
				getVisibleCy(`comparison-filter-option-${PlexMediaComparisonState.Missing}`).should('be.visible');
			});
	});

	it('Should open comparison details dialog for a higher-quality badge', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 1,
			isLoggedIn: true,
		})
			.then(({ mediaData, plexLibraries }) => {
				const movieLibrary = plexLibraries.find((x) => x.type === PlexMediaType.Movie);
				if (!movieLibrary)
					throw new Error('Movie library not found');

				const movie = mediaData.find((x) => x.libraryId === movieLibrary.id)!.media[0]!;
				movie.comparisonId = getPlexMediaComparisonStateId(PlexMediaComparisonState.HigherQuality);

				cy.intercept({ method: 'GET', pathname: '/api/PlexMedia' }, {
					statusCode: 200,
					body: generateResultDTO({
						countries: [],
						episodeCount: 0,
						genres: [],
						mediaCount: 1,
						mediaList: [movie],
						mediaSize: movie.mediaSize,
						movieCount: 1,
						navigationIndexes: [{ label: movie.title[0]?.toUpperCase() ?? '#', index: 0 }],
						page: 1,
						pageSize: 100,
						qualities: [],
						queryHash: 'comparison-status-ux-movie',
						roles: [],
						seasonCount: 0,
						totalCount: 1,
						totalEpisodeCount: 0,
						totalMediaSize: movie.mediaSize,
						totalMovieCount: 1,
						totalSeasonCount: 0,
						totalTvShowCount: 0,
						tvShowCount: 0,
					}),
					...headers,
				});

				cy.intercept(
					'GET',
					PlexMediaPaths.getMediaComparisonDetailsEndpoint(movie.id, { type: PlexMediaType.Movie }),
					{
						statusCode: 200,
						body: generateResultDTO({
							plexMediaId: movie.id,
							type: PlexMediaType.Movie,
							state: PlexMediaComparisonState.HigherQuality,
							rows: [
								{
									children: [],
									plexLibraryId: movie.plexLibraryId,
									plexMediaId: movie.id,
									plexServerId: movie.plexServerId,
									type: PlexMediaType.Movie,
									title: movie.title,
									state: PlexMediaComparisonState.HigherQuality,
									remoteQuality: VideoQuality.FullHD,
									ownedQuality: VideoQuality.HD,
								},
							],
						}),
						...headers,
					},
				).as('comparisonDetails');

				cy.visit(route('/'));
				getVisibleCy(`comparison-chip-${PlexMediaComparisonState.HigherQuality}`).should('be.visible').click();
				cy.wait('@comparisonDetails');
				cy.getCy('media-comparison-details-dialog').should('be.visible');
				cy.getCy('media-comparison-details-table').should('contain', movie.title);
				cy.getCy('media-comparison-details-table').should('contain', '720p');
				cy.getCy('media-comparison-details-table').should('contain', '1080p');
			});
	});
});
