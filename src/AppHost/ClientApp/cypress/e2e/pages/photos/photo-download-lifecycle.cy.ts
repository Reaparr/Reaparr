import { headers, route } from '@fixtures';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { FolderType, PlexMediaType, PhotoItemKind, type PhotoAlbumBrowseDTO } from '@dto';

const libraryId = 41;
const serverId = 3;
const albumId = 301;
const album: PhotoAlbumBrowseDTO = {
	id: albumId,
	plexServerId: serverId,
	plexLibraryId: libraryId,
	plexServerName: 'Photo server',
	plexLibraryTitle: 'Photo library',
	title: 'Downloadable album',
	hasThumbnail: false,
	plexApiRatingKey: 301,
	plexApiMetaDataKey: 301,
	assetCount: 1,
	assetBytes: 2048,
};

function setup() {
	cy.intercept({ method: 'GET', pathname: '/api/**' }, (request) => {
		if (request.url.includes('/api/PlexPhoto/')) return;
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});
	cy.basePageSetup({
		seed: 41,
		plexServerCount: 1,
		plexMovieLibraryCount: 1,
		plexTvShowLibraryCount: 0,
		movieCount: 0,
		tvShowCount: 0,
		firstTimeSetup: false,
		override: {
			plexServer: (items) => items.map((item) => ({ ...item, id: serverId, name: 'Photo server' })),
			plexServerConnections: (items) => items,
			plexLibraries: (items) => items.map((item) => ({ ...item, id: libraryId, type: PlexMediaType.PhotoAlbum, plexServerId: serverId, title: 'Photo library', isEnabled: true, syncedAt: '2026-01-01T00:00:00Z' })),
			plexAccounts: (items) => items.map((item) => ({ ...item, plexServerAccess: [serverId], plexLibraryAccess: [libraryId] })),
			downloadTasks: (items) => items,
			settings: (settings) => ({ ...settings, confirmationSettings: { ...settings.confirmationSettings, askDownloadPhotoAlbumConfirmation: true } }),
			folderPaths: (items) => [...items, {
				id: 42, directory: '/OldPhotos', displayName: 'Removed photo destination',
				mediaType: PlexMediaType.PhotoAlbum, folderType: FolderType.PhotosFolder,
				isDefault: false, isValid: true,
			}],
		},
	});
	cy.intercept('GET', '**/api/PlexPhoto/albums*', {
		statusCode: 200,
		body: generateResultDTO({ items: [album], page: 1, pageSize: 100, totalCount: 1, totalAssetBytes: 2048 }),
		...headers,
	}).as('photoAlbums');
	cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, {
		statusCode: 200,
		body: generateResultDTO({ items: [], page: 1, pageSize: 100, totalCount: 0, totalBytes: 0 }),
		...headers,
	}).as('photoAssets');
	cy.intercept('POST', '**/api/Download/preview', (request) => {
		request.reply({
			statusCode: 200,
			body: generateResultDTO({
				previews: [{ key: 'album-301', title: album.title, type: PlexMediaType.PhotoAlbum, size: album.assetBytes, qualities: [], children: [] }],
				totalSize: album.assetBytes,
				expanded: { 'album-301': true },
			}),
			...headers,
		});
	}).as('photoPreview');
	cy.intercept('POST', '**/api/Download/create', (request) => {
		request.reply({
			statusCode: 200,
			body: generateResultDTO({ movies: 0, tvShows: 0, seasons: 0, episodes: 0, musicArtists: 0, musicAlbums: 0, musicTracks: 0, photoFolders: 1, photoImages: 1, otherVideos: 0, total: 2 }),
			...headers,
		});
	}).as('photoCreate');
}

describe('Photos download lifecycle', () => {
	let releasePreview: (() => void) | undefined;
	afterEach(() => {
		releasePreview?.();
		releasePreview = undefined;
	});

	it('sends an explicit album selector through preview and create without legacy quality selectors', () => {
		setup();
		cy.visit(route('/photos'));
		cy.wait('@photoAlbums');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').contains('tbody tr', album.title).find('[role="checkbox"]').click();
		cy.getCy('media-overview-bar-download-button').click();
		cy.wait('@photoPreview').its('request.body').should((body) => {
			expect(body.downloadMedias).to.have.length(1);
			expect(body.downloadMedias[0]).to.include({ plexLibraryId: libraryId, plexServerId: serverId, type: PlexMediaType.PhotoAlbum, selectionKind: PhotoItemKind.Album });
			expect(body.downloadMedias[0].qualities).to.deep.equal([]);
			expect(body.destinationFolderPathId).to.be.a('number');
		});
		cy.getCy('download-confirmation-submit').should('be.visible').click();
		cy.wait('@photoCreate').its('request.body.downloadMedias[0]').should('include', { type: PlexMediaType.PhotoAlbum, selectionKind: PhotoItemKind.Album });
		cy.getCy('download-confirmation-submit').should('not.exist');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-poster-btn').filter(':visible').click();
		cy.getCy(`photo-album-${libraryId}-${albumId}`).should('be.visible');
	});

	it('recovers from a stale destination without closing the dialog or submitting a failed preview', () => {
		setup();
		let staleRejected = false;
		cy.intercept('POST', '**/api/Download/preview', (request) => {
			if (request.body.destinationFolderPathId === 42) {
				staleRejected = true;
				request.alias = 'missingDestination';
				request.reply({
					statusCode: 404,
					body: generateFailedResultDTO({
						statusCode: 404,
						errors: [{ message: 'Destination no longer exists', metadata: {}, reasons: [] }],
					}),
					...headers,
				});
			} else if (staleRejected) {
				request.alias = 'recoveredPreview';
				const { promise, resolve } = Promise.withResolvers<undefined>();
				releasePreview = () => {
					releasePreview = undefined;
					request.reply({
						statusCode: 200,
						body: generateResultDTO({
							previews: [{ key: 'album-301', title: 'Recovered album', type: PlexMediaType.PhotoAlbum, size: album.assetBytes, qualities: [], children: [] }],
							totalSize: album.assetBytes, expanded: {},
						}),
						...headers,
					});
					resolve(undefined);
				};
				return promise;
			}
		});
		cy.visit(route('/photos'));
		cy.wait('@photoAlbums');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').contains('tbody tr', album.title).find('[role="checkbox"]').click();
		cy.getCy('media-overview-bar-download-button').click();
		cy.wait('@photoPreview').its('request.body.destinationFolderPathId').should('eq', 5);
		cy.getCy('download-confirmation-submit').find('button').last().click();
		cy.getCy('download-confirmation-destination-42').click();
		cy.wait('@missingDestination').its('response.statusCode').should('eq', 404);
		cy.getCy('alert-dialog').should('contain.text', 'Destination no longer exists');
		cy.getCy('close-alert-dialog').click();
		cy.getCy('alert-dialog').should('not.exist');
		cy.getCy('download-confirmation-error').should('be.visible');
		cy.getCy('download-confirmation-submit').find('button').first().should('be.disabled');
		cy.get('@photoCreate.all').should('have.length', 0);
		cy.getCy('download-confirmation-submit').find('button').last().should('not.be.disabled').then(($arrow) => {
			if ($arrow.attr('aria-expanded') !== 'true') cy.wrap($arrow).click();
		});
		cy.getCy('download-confirmation-destination-5').click();
		cy.wrap(null).should(() => expect(releasePreview).to.be.a('function'));
		cy.getCy('download-confirmation-submit').find('button').first().should('be.disabled');
		cy.getCy('download-confirmation-submit').find('button').last().should('not.be.disabled').click();
		cy.get('@photoCreate.all').should('have.length', 0);
		cy.then(() => releasePreview!());
		cy.wait('@recoveredPreview').its('request.body').should('include', {
			destinationFolderPathId: 5, customDestinationFolderPath: '',
		});
		cy.getCy('download-confirmation-error').should('not.exist');
		cy.contains('Recovered album').should('be.visible');
		cy.get('.download-confirmation-destination-path').should('contain.text', '/Photos');
		cy.getCy('download-confirmation-submit').find('button').first().should('not.be.disabled').click();
		cy.wait('@photoCreate').its('request.body').should((body) => {
			expect(body.destinationFolderPathId).to.eq(5);
			expect(body.customDestinationFolderPath).to.eq('');
			expect(body.downloadMedias).to.have.length(1);
			expect(body.downloadMedias[0]).to.include({ type: PlexMediaType.PhotoAlbum, selectionKind: PhotoItemKind.Album });
			expect(body.downloadMedias[0].mediaIds).to.deep.equal([albumId]);
		});
		cy.get('@photoCreate.all').should('have.length', 1);
		cy.getCy('download-confirmation-submit').should('not.exist');
	});
});
