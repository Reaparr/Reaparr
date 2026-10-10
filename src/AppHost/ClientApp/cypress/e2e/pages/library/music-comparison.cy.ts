import { headers, route } from '@fixtures';
import { generatePlexMedia, generateResultDTO } from '@mock';
import { PlexLibraryPaths, PlexMediaPaths } from '@api/api-paths';
import { getPlexMediaComparisonState, getPlexMediaComparisonStateId } from '@composables';
import {
	FolderType, JobStatus, JobTypes, PlexMediaComparisonState, PlexMediaType, VideoQuality,
	type DownloadMediaDTO, type LibraryComparisonCompletedDTO, type PlexMediaComparisonDetailsDTO,
	type PlexMediaComparisonDetailsRowDTO, type PlexMediaDTO, type PlexMediaStatisticsDTO,
} from '@dto';

const visibleCy = (selector: string) => cy.getCy(selector).filter(':visible').first();
const comparisonRow = (title: string) => cy.contains('[data-cy^="media-comparison-details-title-"]', title).closest('tr');

function setupMusic(owned = false) {
	// A missing fixture must fail rather than contact a backend or Plex.
	cy.intercept('**/api/**', (request) => {
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});
	return cy.basePageSetup({
		seed: 623, isLoggedIn: true, firstTimeSetup: false, plexServerCount: 2,
		plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0, movieCount: 0, tvShowCount: 0,
		movieDownloadTask: 0, tvShowDownloadTask: 0,
		override: {
			plexServer: (servers) => servers.map((server, index) => ({ ...server, owned: index === 0 ? owned : !owned })),
			plexLibraries: (libraries) => libraries.map((library, index) => ({ ...library, id: 45 + index, title: `Music library ${index + 1}`, count: 1 })),
			settings: (settings) => ({ ...settings, generalSettings: { ...settings.generalSettings, hideMediaFromOwnedServers: false } }),
		},
	}).then(({ config, plexLibraries }) => {
		// The shared startup factory supports Movie/TV only; replace its empty browse data after it completes.
		plexLibraries.forEach((library) => {
			library.type = PlexMediaType.MusicArtist;
		});
		expect(plexLibraries).to.have.length(2);
		const library = plexLibraries[0]!;
		const source = owned ? plexLibraries[1]! : library;
		const media = (id: number, type: PlexMediaType, title: string, state: PlexMediaComparisonState, parentId?: number): PlexMediaDTO => generatePlexMedia({
			config,
			partialData: {
				id, type, title, searchTitle: title.toLowerCase(), parentId,
				plexLibraryId: library.id, plexServerId: library.plexServerId,
				comparisonId: getPlexMediaComparisonStateId(state), children: [], mediaData: [], qualities: [],
				hasThumb: false, hasArt: false, childCount: 0, grandChildCount: 0, sortIndex: id,
				duration: 180000, mediaSize: 2048,
			},
		});
		const artist = media(100, PlexMediaType.MusicArtist, 'Synth comparison artist', PlexMediaComparisonState.Partial);
		const partialAlbum = media(110, PlexMediaType.MusicAlbum, 'Partial album', PlexMediaComparisonState.Partial, artist.id);
		partialAlbum.children = [
			media(111, PlexMediaType.MusicTrack, 'Owned track', PlexMediaComparisonState.Owned, partialAlbum.id),
			media(112, PlexMediaType.MusicTrack, 'Missing track', PlexMediaComparisonState.Missing, partialAlbum.id),
		];
		const missingAlbum = media(120, PlexMediaType.MusicAlbum, 'Missing album', PlexMediaComparisonState.Missing, artist.id);
		missingAlbum.children = [media(121, PlexMediaType.MusicTrack, 'Missing album track', PlexMediaComparisonState.Missing, missingAlbum.id)];
		const ownedAlbum = media(130, PlexMediaType.MusicAlbum, 'Owned album', PlexMediaComparisonState.Owned, artist.id);
		ownedAlbum.children = [media(131, PlexMediaType.MusicTrack, 'Other owned track', PlexMediaComparisonState.Owned, ownedAlbum.id)];
		artist.children = [partialAlbum, missingAlbum, ownedAlbum];
		artist.childCount = 3;
		artist.grandChildCount = 4;
		const toRow = (item: PlexMediaDTO): PlexMediaComparisonDetailsRowDTO => ({
			plexMediaId: item.id, plexLibraryId: source.id, plexServerId: source.plexServerId,
			type: item.type, title: item.title, state: getPlexMediaComparisonState(item),
			ownedQuality: VideoQuality.None, remoteQuality: VideoQuality.None, children: item.children.map(toRow),
		});
		const details: PlexMediaComparisonDetailsDTO = {
			plexMediaId: artist.id, type: PlexMediaType.MusicArtist, state: PlexMediaComparisonState.Partial,
			rows: artist.children.map(toRow),
		};
		const state = { artist, library, source, details, detailsFailures: 0, holdDetails: false, releaseDetails: undefined as (() => void) | undefined, detailsRequests: 0, browseRequests: 0 };
		cy.intercept('GET', '**/api/Integration', { statusCode: 200, body: generateResultDTO([]), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getAllPlexLibrariesEndpoint(), { statusCode: 200, body: generateResultDTO(plexLibraries), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getPlexLibraryByIdEndpoint(library.id), { statusCode: 200, body: generateResultDTO(library), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getLibraryMediaMetadata(library.id, { mediaType: library.type }), { statusCode: 200, body: generateResultDTO({ countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0, qualities: [], qualityCount: 0, mediaCount: 1 }), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getMetadataFilter(library.id, { mediaType: library.type }), { statusCode: 200, body: generateResultDTO({ countries: [], genres: [], roles: [], qualities: [] }), ...headers });
		cy.intercept('GET', '**/api/FolderPath', { statusCode: 200, body: generateResultDTO([
			{ id: 81, displayName: 'Music default', directory: '/owned/music/default', mediaType: PlexMediaType.MusicArtist, folderType: FolderType.MusicFolder, isDefault: true, isValid: true },
		]), ...headers });
		cy.intercept({ method: 'GET', pathname: '/api/PlexMedia' }, (request) => {
			state.browseRequests++;
			expect(request.query.mediaType).to.equal(PlexMediaType.MusicArtist);
			expect(request.query).not.to.have.property('qualityId');
			const filter = request.query.comparisonState;
			if (filter) expect([PlexMediaComparisonState.NotCompared, PlexMediaComparisonState.Owned, PlexMediaComparisonState.Pending, PlexMediaComparisonState.Missing, PlexMediaComparisonState.Partial]).to.include(filter);
			const items = !filter || filter === getPlexMediaComparisonState(state.artist) ? [state.artist] : [];
			const stats: PlexMediaStatisticsDTO = {
				countries: [], genres: [], roles: [], qualities: [], mediaList: items.map((item, index) => ({ ...item, sortIndex: index + 1 })), mediaCount: items.length, totalCount: items.length,
				mediaSize: 8192, totalMediaSize: 8192, page: 1, pageSize: 100, queryHash: `music-comparison-${state.browseRequests}`,
				navigationIndexes: [], movieCount: 0, tvShowCount: 0, seasonCount: 0, episodeCount: 0,
				totalMovieCount: 0, totalTvShowCount: 0, totalSeasonCount: 0, totalEpisodeCount: 0,
			};
			request.reply({ statusCode: 200, body: generateResultDTO(stats), ...headers });
		}).as('musicBrowse');
		cy.intercept('GET', PlexMediaPaths.getMediaDetailByIdEndpoint(artist.id, { type: artist.type }), { statusCode: 200, body: generateResultDTO(artist), ...headers }).as('musicDetail');
		cy.intercept('GET', PlexMediaPaths.getMediaComparisonDetailsEndpoint(artist.id, { type: artist.type }), (request) => {
			state.detailsRequests++;
			if (state.detailsFailures > 0) {
				state.detailsFailures--;
				request.reply({ statusCode: 500, body: { ...generateResultDTO(null), isSuccess: false, statusCode: 500, errors: [{ message: 'Comparison unavailable', reasons: [] }] }, ...headers });
				return;
			}
			const response = { statusCode: 200, body: generateResultDTO(Cypress._.cloneDeep(state.details)), ...headers };
			if (state.holdDetails) {
				state.holdDetails = false;
				return new Promise<void>((resolve) => {
					state.releaseDetails = () => {
						request.reply(response);
						resolve();
					};
				});
			}
			request.reply(response);
		}).as('musicComparison');
		cy.intercept('POST', '**/api/Download/preview', (request) => {
			const commands = request.body as DownloadMediaDTO[];
			const previews = commands.flatMap((command) => command.mediaIds.map((id) => ({ key: `music-${id}`, title: id === 112 ? 'Missing track' : 'Missing album track', type: command.type, children: [], size: 2048, qualities: [] })));
			request.reply({ statusCode: 200, body: generateResultDTO({ previews, totalSize: previews.length * 2048, expanded: {} }), ...headers });
		}).as('musicPreview');
		return cy.wrap(state);
	});
}

function openComparison() {
	visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).click();
	cy.wait('@musicComparison');
	cy.getCy('media-comparison-details-dialog').should('be.visible');
}

function expandAlbum(title: string) {
	comparisonRow(title).find('button[data-pc-group-section="rowactionbutton"]').click();
}

function assertMissingPreview(libraryId: number, serverId: number, ids: number[]) {
	cy.wait('@musicPreview').its('request.body').should((commands: DownloadMediaDTO[]) => {
		expect(commands.flatMap((command) => command.mediaIds).sort()).to.deep.equal([...ids].sort());
		for (const command of commands) expect(command).to.deep.equal({ mediaIds: command.mediaIds, type: PlexMediaType.MusicTrack, plexLibraryId: libraryId, plexServerId: serverId, qualities: [], keepCompletedInDownloadFolder: false });
	});
	cy.getCy('download-confirmation-dialog').should('be.visible').and('contain.text', '/owned/music/default');
	cy.getCy('download-confirmation-dialog').find('[data-cy^="column-qualities-"]').should('not.exist');
}

describe('Actual Music comparison frontend integration', () => {
	it('offers only applicable remote Music filters and downloads missing leaves from a partial album', () => {
		cy.viewport(1280, 800);
		setupMusic().then((state) => {
			cy.visit(route(`/music/${state.library.id}`));
			cy.getPageData();
			cy.wait('@musicBrowse');
			visibleCy('media-overview-filter-btn').click();
			visibleCy('media-filter-menu-category-comparisonState').click();
			for (const value of [PlexMediaComparisonState.NotCompared, PlexMediaComparisonState.Owned, PlexMediaComparisonState.Pending, PlexMediaComparisonState.Partial, PlexMediaComparisonState.Missing]) visibleCy(`comparison-filter-option-${value}`).should('be.visible');
			cy.getCy(`comparison-filter-option-${PlexMediaComparisonState.HigherQuality}`).should('not.exist');
			cy.getCy(`comparison-filter-option-${PlexMediaComparisonState.PartialAndHigherQuality}`).should('not.exist');
			visibleCy(`comparison-filter-option-${PlexMediaComparisonState.Partial}`).click();
			cy.wait('@musicBrowse').its('request.query.comparisonState').should('eq', PlexMediaComparisonState.Partial);
			cy.get('body').type('{esc}');
			openComparison();
			cy.getCy('media-comparison-details-table').should('contain.text', 'Partial album').and('contain.text', 'Owned album');
			cy.getCy('media-comparison-details-dialog').should('not.contain.text', 'Current quality').and('not.contain.text', 'Available quality');
			expandAlbum('Partial album');
			comparisonRow('Owned track').find('[data-cy^="media-comparison-details-download-"]').should('not.exist');
			comparisonRow('Partial album').find('[data-cy^="media-comparison-details-download-"]').click();
			assertMissingPreview(state.source.id, state.source.plexServerId, [112]);
		});
	});

	it('keeps owned Music filters accurate and uses remote row identity from an owned artist on a phone', () => {
		cy.viewport(320, 568);
		setupMusic(true).then((state) => {
			cy.visit(route(`/music/${state.library.id}`));
			cy.getPageData();
			visibleCy('media-overview-filter-btn').click();
			visibleCy('media-filter-menu-category-comparisonState').click();
			visibleCy(`comparison-filter-option-${PlexMediaComparisonState.Partial}`).should('be.visible');
			cy.getCy(`comparison-filter-option-${PlexMediaComparisonState.Missing}`).should('not.exist');
			cy.getCy(`comparison-filter-option-${PlexMediaComparisonState.HigherQuality}`).should('not.exist');
			cy.get('body').type('{esc}');
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).should(($button) => {
				const rect = $button[0]!.getBoundingClientRect();
				expect(rect.width).to.be.at.least(44);
				expect(rect.height).to.be.at.least(44);
			});
			openComparison();
			expandAlbum('Partial album');
			comparisonRow('Missing track').find('[data-cy^="media-comparison-details-download-"]').should('be.visible').click();
			assertMissingPreview(state.source.id, state.source.plexServerId, [112]);
			cy.get('.q-layout').should(($layout) => expect($layout[0]!.scrollWidth).to.be.at.most($layout[0]!.clientWidth));
		});
	});

	it('renders artist, album and track projected states without audio quality or child comparison requests', () => {
		cy.viewport(320, 568);
		setupMusic().then((state) => {
			cy.visit(route(`/music/${state.library.id}/details/${state.artist.id}`));
			cy.getPageData();
			cy.get('h1').should('be.visible').and('have.text', state.artist.title);
			cy.getCy(`music-comparison-artist-${state.artist.id}`).scrollIntoView().should('be.visible');
			cy.getCy('music-comparison-album-110').scrollIntoView().should('be.visible');
			cy.getCy('music-album-110').find('.q-expansion-item__container > .q-item').first().click();
			cy.getCy('music-comparison-track-110-111').scrollIntoView().should('be.visible');
			cy.getCy('music-comparison-track-110-112').scrollIntoView().should('be.visible').click();
			cy.then(() => expect(state.detailsRequests).to.equal(0));
			cy.getCy('music-media-list').should('not.contain.text', '1080p').and('not.contain.text', 'Higher quality');
			cy.getCy(`music-comparison-artist-${state.artist.id}`).scrollIntoView().click();
			cy.wait('@musicComparison').its('request.url').should('include', 'type=MusicArtist');
			cy.getCy('media-comparison-details-dialog').should('be.visible');
		});
	});

	it('deduplicates album and descendant selection while excluding owned leaves', () => {
		cy.viewport(1280, 800);
		setupMusic().then((state) => {
			cy.visit(route(`/music/${state.library.id}`));
			cy.getPageData();
			openComparison();
			comparisonRow('Partial album').find('[role="checkbox"]').click();
			expandAlbum('Partial album');
			comparisonRow('Owned track').find('[role="checkbox"]').should('have.attr', 'aria-disabled', 'true');
			visibleCy('media-comparison-details-dialog-download-button').should('not.be.disabled').click();
			assertMissingPreview(state.source.id, state.source.plexServerId, [112]);
		});
	});

	it('recovers failed comparison requests and discards a cancelled stale response on reopen', () => {
		cy.viewport(1280, 800);
		setupMusic().then((state) => {
			state.detailsFailures = 1;
			cy.visit(route(`/music/${state.library.id}`));
			cy.getPageData();
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).click();
			cy.wait('@musicComparison').its('response.statusCode').should('eq', 500);
			cy.getCy('alert-dialog').should('be.visible').within(() => {
				cy.getCy('close-alert-dialog').click();
			});
			cy.getCy('alert-dialog').should('not.exist');
			cy.getCy('media-comparison-details-error').should('be.visible').and('have.attr', 'role', 'alert');
			visibleCy('media-comparison-details-dialog-download-button').should('be.disabled');
			cy.then(() => {
				state.holdDetails = true;
			});
			cy.getCy('media-comparison-details-retry').click();
			cy.wrap(null).should(() => expect(state.releaseDetails).to.be.a('function'));
			visibleCy('media-comparison-details-dialog-download-button').should('be.disabled');
			cy.then(() => state.releaseDetails!());
			cy.wait('@musicComparison');
			cy.getCy('media-comparison-details-error').should('not.exist');
			visibleCy('dialog-close-button').click();
			cy.then(() => {
				state.holdDetails = true;
				state.releaseDetails = undefined;
				state.details.rows[0]!.title = 'Stale album response';
			});
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).click();
			cy.wrap(null).should(() => expect(state.releaseDetails).to.be.a('function'));
			visibleCy('dialog-close-button').click();
			cy.then(() => {
				state.details.rows[0]!.title = 'Fresh album response';
			});
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).click();
			cy.wait('@musicComparison');
			cy.getCy('media-comparison-details-table').should('contain.text', 'Fresh album response');
			cy.then(() => state.releaseDetails!());
			cy.getCy('media-comparison-details-table').should('contain.text', 'Fresh album response').and('not.contain.text', 'Stale album response');
		});
	});

	it('rejects Music upgrade URL filters and refreshes the badge through existing comparison-completion SignalR', () => {
		cy.viewport(1280, 800);
		setupMusic().then((state) => {
			state.artist.comparisonId = getPlexMediaComparisonStateId(PlexMediaComparisonState.Pending);
			cy.visit(route(`/music/${state.library.id}?comparisonState=HigherQuality&qualityId=9`));
			cy.getPageData();
			cy.wait('@musicBrowse').its('request.query').should((query) => {
				expect(query).not.to.have.property('comparisonState');
				expect(query).not.to.have.property('qualityId');
			});
			cy.location('search').should('not.include', 'HigherQuality').and('not.include', 'qualityId');
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Pending}`).click();
			cy.then(() => {
				expect(state.detailsRequests).to.equal(0);
				state.artist.comparisonId = getPlexMediaComparisonStateId(PlexMediaComparisonState.Partial);
			});
			cy.hubPublishJobStatusUpdate<LibraryComparisonCompletedDTO>(JobTypes.LibraryComparisonJob, JobStatus.Completed, {
				affectedLibraryIds: [state.library.id, state.source.id], mediaType: PlexMediaType.MusicArtist, completedAt: '2026-10-08T12:00:00Z',
			});
			cy.wait('@musicBrowse');
			visibleCy(`comparison-chip-${PlexMediaComparisonState.Partial}`).should('be.visible');
			cy.getCy(`comparison-chip-${PlexMediaComparisonState.Pending}`).should('not.exist');
		});
	});
});
