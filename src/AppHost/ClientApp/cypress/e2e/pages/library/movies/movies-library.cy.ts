import { route } from '@fixtures';
import { PlexMediaType } from '@dto';

describe('Display media collection on the Library detail page', () => {
	it('Should successfully scroll to the bottom when scrolling the page', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 1000,
			isLoggedIn: true,
		})
			.then(({ mediaData, plexLibraries }) => {
				const movieLibrary = plexLibraries.find((x) => x.type === PlexMediaType.Movie);
				if (!movieLibrary) {
					throw new Error('Movie library not found');
				}
				const movieList = mediaData.find((x) => x.libraryId === movieLibrary.id)?.media ?? [];
				const lastPage = Math.ceil(movieList.length / 100);
				cy.intercept('GET', '**/api/PlexMedia*', (req) => {
					if (Number(req.query.plexLibraryId) === movieLibrary.id && Number(req.query.page) === lastPage) {
						req.alias = 'lastMoviePage';
					}
				});

				// Visit the page
				cy.visit(route(`/movies/${movieLibrary.id}`));
				cy.url({ timeout: 20000 }).should('include', `/movies/${movieLibrary.id}`);

				cy.get('[data-cy="poster-table"] #poster-table', { timeout: 20000 })
					.should('exist')
					.scrollTo('bottom', { duration: 10000 });
				cy.wait('@lastMoviePage', { timeout: 20000 });
				cy.get(`[data-scroll-index="${movieList.length - 1}"]`, { timeout: 20000 })
					.should('exist')
					.and('be.visible');
			});
	});

	it('Should display and click on all the letters in the alphabet navigation when movies are available', function () {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 1000,
			isLoggedIn: true,
		})
			.then((data) => {
				const movieLibrary = data.plexLibraries.find((x) => x.type === PlexMediaType.Movie);
				if (!movieLibrary) {
					throw new Error('Movie library not found');
				}
				// Visit the page
				cy.visit(route(`/movies/${movieLibrary.id}`));
				cy.url({ timeout: 20000 }).should('include', `/movies/${movieLibrary.id}`);

				cy.get('[data-cy="poster-table"]', { timeout: 20000 }).should('exist');

				const movies = data.mediaData.find((x) => x.libraryId === movieLibrary.id)?.media ?? [];
				const sortTitles = movies.map((x) => x.title[0]?.toLowerCase() ?? '#');
				cy.get('[data-cy$="alphabet-navigation-btn"]').each(($btn) => {
					const dataCy = $btn.attr('data-cy') ?? '';
					const letter = dataCy.replace('letter-', '').replace('-alphabet-navigation-btn', '');
					const index = sortTitles.indexOf(letter);
					if (index > -1) {
						cy.log(`Navigating to letter: ${letter} at index: ${index}`);
						cy.wrap($btn).should('be.visible').click();
						cy.get(`[data-scroll-index="${index}"]`, { timeout: 10000 }).should('be.visible');
					}
				});
			});
	});
});
