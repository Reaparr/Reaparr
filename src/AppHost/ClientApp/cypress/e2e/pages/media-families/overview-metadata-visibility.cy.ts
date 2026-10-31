import { headers, route } from '@fixtures';
import { generatePlexMediaSlims, generatePlexMediaStatisticsDTO, generateResultDTO, generateSupportedSorts } from '@mock';
import { MediaMetaDataTypes } from '@enums';
import { PlexMediaType, ViewMode, type PlexMediaMetadataDTO } from '@dto';
import { SettingsPaths } from '@api-urls';

const genres = [
	{ id: 11, name: 'Owned-library genre' },
	{ id: 22, name: 'Offline-library genre' },
	{ id: 33, name: 'Visible-library genre' },
];

function visibleGenres(query: Record<string, unknown>) {
	return genres.filter((genre) => !(genre.id === 11 && query.filterOwnedMedia === 'true')
		&& !(genre.id === 22 && query.filterOfflineMedia === 'true'));
}

function setupVisibilityFixture() {
	cy.intercept('**/api/**', (request) => {
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});
	cy.basePageSetup({
		seed: 45045, plexServerCount: 3, plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0,
		movieCount: 1, tvShowCount: 0, firstTimeSetup: false,
		override: {
			settings: (settings) => {
				settings.generalSettings.hideMediaFromOwnedServers = false;
				settings.generalSettings.hideMediaFromOfflineServers = false;
				settings.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
				settings.displaySettings.movieViewMode = ViewMode.Table;
				return settings;
			},
		},
	}).then(({ config, plexServers, plexLibraries, settings }) => {
		let persistedSettings = settings;
		cy.intercept('PUT', SettingsPaths.updateUserSettingsEndpoint(), (request) => {
			persistedSettings = request.body;
			const general = persistedSettings.generalSettings;
			request.alias = `scopeSettings-${general.hideMediaFromOwnedServers}-${general.hideMediaFromOfflineServers}`;
			request.reply({ statusCode: 200, body: generateResultDTO(persistedSettings), ...headers });
		});
		cy.intercept('GET', SettingsPaths.getUserSettingsEndpoint(), (request) => {
			request.alias = 'readPersistedSettings';
			request.reply({ statusCode: 200, body: generateResultDTO(persistedSettings), ...headers });
		});
		const movies = generatePlexMediaSlims({
			config: { ...config, movieCount: 3 },
			partialData: { type: PlexMediaType.Movie, plexServerId: plexServers[0]!.id, plexLibraryId: plexLibraries[0]!.id },
		})
			.map((movie, index) => ({ ...movie, id: genres[index]!.id, title: `${genres[index]!.name} movie`,
				plexServerId: plexServers[index]!.id,
				plexLibraryId: plexLibraries.find((library) => library.plexServerId === plexServers[index]!.id)!.id,
				sortIndex: index + 1, hasThumb: false }));
		cy.intercept({ method: 'GET', pathname: '/api/PlexLibrary/0/metadata' }, (request) => {
			request.alias = `scopeMetadata-${request.query.filterOwnedMedia}-${request.query.filterOfflineMedia}`;
			const available = visibleGenres(request.query);
			const metadata: PlexMediaMetadataDTO = {
				mediaCount: available.length, genres: available, genreCount: available.length,
				countries: [], countryCount: 0, roles: [], roleCount: 0, qualities: [], qualityCount: 0,
				supportedSorts: generateSupportedSorts(request.query.mediaType as PlexMediaType),
			};
			request.reply({ statusCode: 200, body: generateResultDTO(metadata), ...headers });
		});
		cy.intercept({ method: 'GET', pathname: '/api/PlexLibrary/0/metadata-filter' }, (request) => {
			request.alias = `scopeFilter-${request.query.filterOwnedMedia}-${request.query.filterOfflineMedia}`;
			request.reply({ statusCode: 200,
				body: generateResultDTO({ genres: visibleGenres(request.query).map((genre) => genre.id), countries: [], roles: [], qualities: [] }),
				...headers });
		});
		cy.intercept({ method: 'GET', pathname: '/api/PlexMedia' }, (request) => {
			request.alias = `scopeMedia-${request.query.filterOwnedMedia}-${request.query.filterOfflineMedia}`;
			const availableIds = visibleGenres(request.query).map((genre) => genre.id);
			const statistics = generatePlexMediaStatisticsDTO(movies.filter((movie) => availableIds.includes(movie.id)));
			statistics.page = 1;
			statistics.pageSize = 100;
			statistics.queryHash = `visibility-${request.query.filterOwnedMedia}-${request.query.filterOfflineMedia}`;
			statistics.totalCount = availableIds.length;
			request.reply({ statusCode: 200, body: generateResultDTO(statistics), ...headers });
		});
	});
}

describe('All-media metadata visibility', () => {
	it('Keeps genre choices aligned with media after hiding and restoring owned and offline libraries', () => {
		// Arrange
		setupVisibilityFixture();

		// Act / Assert: initial choices include all three mock library scopes.
		cy.visit(route('/'));
		cy.wait('@readPersistedSettings');
		cy.wait('@scopeMetadata-false-false').its('request.query').should('include', { filterOwnedMedia: 'false', filterOfflineMedia: 'false' });
		cy.wait('@scopeFilter-false-false');
		cy.wait('@scopeMedia-false-false');
		cy.contains('Owned-library genre movie').should('be.visible');
		cy.getCy('media-overview-filter-btn').filter(':visible').click();
		cy.getCy(`media-filter-menu-category-${MediaMetaDataTypes.Genres}`).click();
		cy.get('.media-filter-menu-list').should('contain.text', genres[0]!.name)
			.and('contain.text', genres[1]!.name).and('contain.text', genres[2]!.name);
		cy.get('body').type('{esc}');

		// Each step is a real options-dialog edit followed by refreshed media and choices.
		for (const step of [
			{ toggles: ['owned'], owned: true, offline: false, ids: [22, 33] },
			{ toggles: ['offline'], owned: true, offline: true, ids: [33] },
			{ toggles: ['owned', 'offline'], owned: false, offline: false, ids: [11, 22, 33] },
		]) {
			cy.getCy('media-overview-options-btn').filter(':visible').click();
			for (const toggle of step.toggles) {
				const checked = toggle === 'owned' ? step.owned : step.offline;
				cy.getCy(`media-options-hide-${toggle}-toggle`).should('have.attr', 'aria-checked', String(!checked))
					.click().should('have.attr', 'aria-checked', String(checked));
			}
			cy.getCy('dialog-close-button').filter(':visible').click();
			cy.wait(`@scopeMedia-${step.owned}-${step.offline}`).its('request.query').should('include', {
				filterOwnedMedia: String(step.owned), filterOfflineMedia: String(step.offline),
			});
			cy.wait(`@scopeMetadata-${step.owned}-${step.offline}`).then(({ request, response }) => {
				expect(request.query).to.include({ filterOwnedMedia: String(step.owned), filterOfflineMedia: String(step.offline) });
				expect(response, 'Metadata HTTP response').not.to.equal(undefined);
				expect(response!.statusCode).to.equal(200);
				expect(response!.body.value.mediaCount).to.equal(step.ids.length);
			});
			cy.wait(`@scopeFilter-${step.owned}-${step.offline}`).its('response.body.value.genres').should('deep.equal', step.ids);
			for (const genre of genres) {
				cy.contains(`${genre.name} movie`).should(step.ids.includes(genre.id) ? 'be.visible' : 'not.exist');
			}
			cy.getCy('media-overview-filter-btn').filter(':visible').click();
			cy.getCy(`media-filter-menu-category-${MediaMetaDataTypes.Genres}`).click();
			for (const genre of genres) {
				cy.get('.media-filter-menu-list').should(step.ids.includes(genre.id) ? 'contain.text' : 'not.contain.text', genre.name);
			}
			cy.get('body').type('{esc}');
			// Prove each changed setting survives a fresh page load, not just restoration.
			cy.wait(`@scopeSettings-${step.owned}-${step.offline}`).its('request.body.generalSettings').should('include', {
				hideMediaFromOwnedServers: step.owned, hideMediaFromOfflineServers: step.offline,
			});
			cy.reload();
			cy.wait('@readPersistedSettings').its('response.body.value.generalSettings').should('include', {
				hideMediaFromOwnedServers: step.owned, hideMediaFromOfflineServers: step.offline,
			});
			cy.wait(`@scopeMetadata-${step.owned}-${step.offline}`).its('request.query').should('include', {
				filterOwnedMedia: String(step.owned), filterOfflineMedia: String(step.offline),
			});
			cy.wait(`@scopeFilter-${step.owned}-${step.offline}`).its('response.body.value.genres').should('deep.equal', step.ids);
			cy.wait(`@scopeMedia-${step.owned}-${step.offline}`);
		}
	});
});
