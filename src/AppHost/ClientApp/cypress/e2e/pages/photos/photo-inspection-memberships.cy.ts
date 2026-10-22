import { headers, route } from '@fixtures';
import { JobStatus, JobTypes, LibrarySyncJobStatus, PhotoAssetKind, PlexMediaType, type LibrarySyncJobQueueDTO, type PhotoAlbumSummaryDTO, type PhotoAssetBrowseDTO } from '@dto';
import { PlexLibraryPaths } from '@api/api-paths';
import { generateLibrarySyncJobQueue, generateResultDTO } from '@mock';

const libraryId = 41;
const serverId = 3;
const asset: PhotoAssetBrowseDTO = {
	id: 501, plexServerId: serverId, plexLibraryId: libraryId,
	plexServerName: 'Photo server', plexLibraryTitle: 'Photo library', title: 'Beach photo',
	assetKind: PhotoAssetKind.Image, hasThumbnail: false, plexApiRatingKey: 501, plexApiMetaDataKey: 501,
	originalFilename: 'beach.jpg', size: 2048, container: 'jpg', width: 6000, height: 4000,
};
const album = (id: number, title = `Membership ${String(id).padStart(3, '0')}`): PhotoAlbumSummaryDTO => ({
	id, plexServerId: serverId, plexLibraryId: libraryId, plexServerName: 'Photo server', plexLibraryTitle: 'Photo library', title,
	hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
});

function setupInspectionWorkflow() {
	const state = { memberships: Array.from({ length: 102 }, (_, index) => album(index + 1)), queues: [] as LibrarySyncJobQueueDTO[] };
	cy.basePageSetup({
		seed: 41, plexServerCount: 1, plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0,
		movieCount: 0, tvShowCount: 0, firstTimeSetup: false,
		override: {
			plexServer: (items) => items.map((item) => ({ ...item, id: serverId, name: 'Photo server' })),
			plexLibraries: (items) => items.map((item) => ({ ...item, id: libraryId, type: PlexMediaType.PhotoAlbum, plexServerId: serverId, title: 'Photo library', isEnabled: true, syncedAt: '2026-01-01T00:00:00Z' })),
			plexAccounts: (items) => items.map((item) => ({ ...item, plexServerAccess: [serverId], plexLibraryAccess: [libraryId] })),
		},
	});
	cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, {
		statusCode: 200, body: generateResultDTO({ items: [asset], page: 1, pageSize: 100, totalCount: 1, totalBytes: 2048 }), ...headers,
	}).as('photoAssets');
	cy.intercept({ method: 'GET', pathname: `/api/PlexPhoto/libraries/${libraryId}/assets/501` }, (request) => {
		const page = Number(request.query.membershipPage ?? 1);
		request.alias = `inspectionPage${page}`;
		request.reply({
			statusCode: 200,
			body: generateResultDTO({ ...asset, addedAt: '2026-01-01T00:00:00Z', summary: 'Photo inspection', breadcrumbs: [],
				albums: { items: state.memberships.slice((page - 1) * 100, page * 100), page, pageSize: 100, totalCount: state.memberships.length } }),
			...headers,
		});
	});
	cy.intercept({ method: 'GET', pathname: `/api/PlexPhoto/libraries/${libraryId}/assets/501/image` }, {
		statusCode: 200,
		body: '<svg xmlns="http://www.w3.org/2000/svg" width="6000" height="4000"><rect width="6000" height="4000" fill="#135"/></svg>',
		headers: { ...headers.headers, 'content-type': 'image/svg+xml' },
	});
	cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO(state.queues), ...headers });
	}).as('syncSnapshot');
	cy.intercept('POST', PlexLibraryPaths.refreshLibraryMediaEndpoint(libraryId), (request) => {
		state.queues = [generateLibrarySyncJobQueue({ plexLibraryId: libraryId, plexServerId: serverId, status: LibrarySyncJobStatus.Queued, completedAt: null })];
		request.reply({ statusCode: 200, body: generateResultDTO(state.queues[0]), ...headers });
	}).as('queueRefresh');
	return state;
}

describe('Photo inspector membership pages', () => {
	beforeEach(() => {
		cy.hubClear();
		Cypress.Commands.overwrite('hubPublish', (_original, hubName, messageType, payload) =>
			cy.window().then((autWindow) => {
				const hub = autWindow['cypress-signalr-mock']?.mocks.find((mock) => mock.name === hubName);
				if (!hub) throw new Error(`SignalR mock hub ${hubName} is not ready`);
				hub.publish(messageType, payload);
			}),
		);
	});

	it('keeps the first page reachable after scrolling and replaces old memberships when a library refresh publishes', () => {
		// Arrange
		cy.viewport(1280, 800);
		const state = setupInspectionWorkflow();
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		cy.wait('@syncSnapshot');
		cy.getCy('media-overview-refresh-library-btn').click();
		cy.getCy('refresh-mode-incremental-button').click();
		cy.wait('@queueRefresh');
		cy.getCy('media-overview-library-sync-queued-banner').should('be.visible');
		cy.getCy('photo-asset-41-501').click();
		cy.getCy('photo-viewer-image').should('have.prop', 'naturalWidth', 6000);
		cy.getCy('photo-viewer-details-action').click();
		cy.wait('@inspectionPage1');
		cy.getCy('photo-asset-dialog-albums').find('.q-virtual-scroll').as('memberships');
		cy.get('@memberships').scrollIntoView().should('contain.text', 'Membership 001');

		// Act
		cy.get('@memberships').scrollTo('bottom');
		cy.wait('@inspectionPage2').its('request.url').should('contain', 'membershipPage=2');
		cy.get('@memberships').scrollTo('bottom', { ensureScrollable: false });
		cy.get('@memberships').contains('Membership 102').should('be.visible');
		cy.get('@memberships').scrollTo('top', { ensureScrollable: false });

		// Assert
		cy.get('@memberships').contains('Membership 001').should('be.visible');
		cy.then(() => {
			state.memberships = [album(1, 'Renamed membership'), album(103, 'New membership')];
			state.queues = state.queues.map((queue) => ({ ...queue, status: LibrarySyncJobStatus.Completed, completedAt: '2026-10-22T00:00:00Z' }));
			cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Completed, state.queues[0]);
		});
		cy.wait('@syncSnapshot').its('response.body.value.0.status').should('eq', LibrarySyncJobStatus.Completed);
		cy.wait('@inspectionPage1').its('request.url').should('contain', 'membershipPage=1');
		cy.get('@memberships').should('contain.text', 'Renamed membership').and('contain.text', 'New membership');
		cy.get('@memberships').find('.q-item').should('have.length', 2);
		cy.get('@memberships').should('not.contain.text', 'Membership 001').and('not.contain.text', 'Membership 102');
	});
});
