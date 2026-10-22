import { headers, route } from '@fixtures';
import { PhotoAssetKind, PlexMediaType, type PhotoAssetBrowseDTO } from '@dto';
import { generateResultDTO } from '@mock';

const libraryId = 41;
const serverId = 3;
const assets: PhotoAssetBrowseDTO[] = [501, 502].map((id) => ({
	id, plexServerId: serverId, plexLibraryId: libraryId,
	plexServerName: 'Photo server', plexLibraryTitle: 'Photo library', title: `Photo ${id}`,
	assetKind: PhotoAssetKind.Image, hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
	originalFilename: `${id}.jpg`, size: 2048, container: 'jpg', width: 6000, height: 4000,
}));

describe('Photo selection cancellation', () => {
	it('keeps the toolbar clear authoritative over pending select all and permits another selection', () => {
		// Arrange
		cy.viewport(1280, 800);
		cy.basePageSetup({
			seed: 41, plexServerCount: 1, plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0,
			movieCount: 0, tvShowCount: 0, firstTimeSetup: false,
			override: {
				plexServer: (items) => items.map((item) => ({ ...item, id: serverId, name: 'Photo server' })),
				plexLibraries: (items) => items.map((item) => ({ ...item, id: libraryId, type: PlexMediaType.PhotoAlbum, plexServerId: serverId, title: 'Photo library', isEnabled: true, syncedAt: '2026-01-01T00:00:00Z' })),
				plexAccounts: (items) => items.map((item) => ({ ...item, plexServerAccess: [serverId], plexLibraryAccess: [libraryId] })),
			},
		});
		let holdSelection = false;
		let releaseSelection: (() => void) | undefined;
		const response = { statusCode: 200, body: generateResultDTO({ items: assets, page: 1, pageSize: 100, totalCount: 2, totalBytes: 4096 }), ...headers };
		cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, (request) => {
			if (!holdSelection) {
				request.reply(response);
				return;
			}
			holdSelection = false;
			return new Cypress.Promise<void>((resolve) => {
				releaseSelection = () => {
					request.reply(response);
					resolve();
				};
			});
		}).as('photoAssets');
		cy.intercept({ method: 'GET', pathname: /\/api\/PlexPhoto\/libraries\/41\/assets\/\d+\/image$/ }, {
			statusCode: 200,
			body: '<svg xmlns="http://www.w3.org/2000/svg" width="6000" height="4000"><rect width="6000" height="4000" fill="#135"/></svg>',
			headers: { ...headers.headers, 'content-type': 'image/svg+xml' },
		});
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').contains('tbody tr', 'Photo 501').find('[role="checkbox"]').click();
		cy.getCy('photo-select-all').should('have.attr', 'aria-checked', 'mixed');

		// Act
		cy.then(() => {
			holdSelection = true;
		});
		cy.getCy('photo-select-all').click();
		cy.wrap(null).should(() => {
			expect(releaseSelection, 'pending select-all response').to.be.a('function');
		});
		cy.getCy('media-overview-bar-clear-selection-button').should('be.visible').click();
		cy.getCy('photo-overview-list').should('have.attr', 'aria-busy', 'false');
		cy.then(() => {
			releaseSelection!();
		});

		// Assert
		cy.getCy('photo-select-all').should('have.attr', 'aria-checked', 'false');
		cy.getCy('photo-overview-table').find('tbody [role="checkbox"]').each(($checkbox) => {
			cy.wrap($checkbox).should('have.attr', 'aria-checked', 'false');
		});
		cy.getCy('media-overview-bar-clear-selection-button').should('not.exist');
		cy.getCy('photo-select-all').click();
		cy.getCy('photo-select-all').should('have.attr', 'aria-checked', 'true');
		cy.getCy('photo-overview-table').find('tbody [role="checkbox"]').each(($checkbox) => {
			cy.wrap($checkbox).should('have.attr', 'aria-checked', 'true');
		});
	});
});
