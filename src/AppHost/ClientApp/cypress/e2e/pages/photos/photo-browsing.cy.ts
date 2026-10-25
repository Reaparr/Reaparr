import type { Interception } from 'cypress/types/net-stubbing';
import { headers, route } from '@fixtures';
import { JobStatus, JobTypes, LibrarySyncJobStatus, PhotoAssetKind, PhotoItemKind, PlexMediaType, SortType, type LibrarySyncJobQueueDTO, type PhotoAlbumBrowseDTO, type PhotoAssetBrowseDTO } from '@dto';
import { PlexLibraryPaths } from '@api/api-paths';
import { generateLibrarySyncJobQueue } from '@factories';
import { generateResultDTO } from '@mock';

const libraryId = 41;
const serverId = 3;
const albumId = 301;
const assetId = 501;

const album: PhotoAlbumBrowseDTO = {
	id: albumId,
	plexServerId: serverId,
	plexLibraryId: libraryId,
	plexServerName: 'Photo server',
	plexLibraryTitle: 'Photo library',
	title: 'Summer trip',
	hasThumbnail: false,
	plexApiRatingKey: 301,
	plexApiMetaDataKey: 301,
	assetCount: 2,
	assetBytes: 4096,
};
const asset: PhotoAssetBrowseDTO = {
	id: assetId,
	plexServerId: serverId,
	plexLibraryId: libraryId,
	plexServerName: 'Photo server',
	plexLibraryTitle: 'Photo library',
	title: 'Beach photo',
	assetKind: PhotoAssetKind.Image,
	hasThumbnail: false,
	plexApiRatingKey: 501,
	plexApiMetaDataKey: 501,
	originalFilename: 'beach.jpg',
	size: 2048,
	container: 'jpg',
	width: 1920,
	height: 1080,
};

function setupPhotoApi(assets: PhotoAssetBrowseDTO[] = [asset]) {
	const pageReply = (items: PhotoAssetBrowseDTO[], request: { url: string }) => {
		const query = new URL(request.url).searchParams;
		const page = Number(query.get('page') ?? 1);
		const sort = query.get('sort') ?? `${SortType.Title}:asc`;
		const [sortField, sortDirection] = sort.split(':');
		const sorted = [...items].sort((left, right) => {
			let comparison: number;
			switch (sortField) {
				case SortType.AddedAt:
					comparison = left.id - right.id;
					break;
				case SortType.MediaSize:
					comparison = left.size - right.size;
					break;
				case SortType.Title:
				default:
					comparison = left.title.localeCompare(right.title);
					break;
			}
			return sortDirection === 'desc' ? -comparison : comparison;
		});
		return generateResultDTO({
			items: sorted.slice((page - 1) * 100, page * 100), page, pageSize: 100,
			totalCount: items.length, totalBytes: items.reduce((total, item) => total + item.size, 0),
		});
	};
	cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/albums' }, (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO({ items: [album], page: 1, pageSize: 100, totalCount: 1, totalAssetBytes: 4096 }), ...headers });
	}).as('photoAlbums');
	cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets/search' }, (request) => {
		request.reply({ statusCode: 200, body: pageReply(assets, request), ...headers });
	}).as('photoSearch');
	cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, (request) => {
		const items = new URL(request.url).searchParams.get('unmappedOnly') === 'true' ? [] : assets;
		request.reply({ statusCode: 200, body: pageReply(items, request), ...headers });
	}).as('photoAssets');
	cy.intercept('GET', `**/api/PlexPhoto/libraries/${libraryId}/roots*`, (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO({ items: [{ id: 601, plexServerId: serverId, plexLibraryId: libraryId, title: 'Trips', relativePath: 'Trips', parentId: null, isRoot: true, isActive: true, recursiveAssetCount: 1, recursiveAssetBytes: 2048 }], page: 1, pageSize: 100, totalCount: 1 }), ...headers });
	}).as('photoRoots');
	cy.intercept('GET', `**/api/PlexPhoto/libraries/${libraryId}/folders/601/children*`, (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO({
			folder: { id: 601, plexServerId: serverId, plexLibraryId: libraryId, title: 'Trips', relativePath: 'Trips', parentId: null, isRoot: true, isActive: true, recursiveAssetCount: 1, recursiveAssetBytes: 2048 },
			breadcrumbs: [{ id: 601, title: 'Trips', relativePath: 'Trips' }],
			folders: { items: [], page: 1, pageSize: 100, totalCount: 0 },
			assets: { items: [asset], page: 1, pageSize: 100, totalCount: 1, totalBytes: 2048 },
		}), ...headers });
	}).as('photoFolderChildren');
	cy.intercept('GET', `**/api/PlexPhoto/libraries/${libraryId}/albums/${albumId}/assets*`, (request) => {
		request.reply({ statusCode: 200, body: pageReply(assets, request), ...headers });
	}).as('photoAlbumMembers');
	cy.intercept({ method: 'GET', pathname: new RegExp(`/api/PlexPhoto/libraries/${libraryId}/assets/\\d+$`) }, (request) => {
		const id = Number(new URL(request.url).pathname.split('/').at(-1));
		const item = assets.find((candidate) => candidate.id === id) ?? asset;
		request.reply({ statusCode: 200, body: generateResultDTO({ ...item, addedAt: '2026-01-01T00:00:00Z', summary: 'A summer photo', breadcrumbs: [], albums: { items: [album], page: 1, pageSize: 100, totalCount: 1 } }), ...headers });
	}).as('photoInspection');
	cy.intercept({ method: 'GET', pathname: new RegExp(`/api/PlexPhoto/libraries/${libraryId}/assets/\\d+/image$`) }, {
		statusCode: 200,
		body: '<svg xmlns="http://www.w3.org/2000/svg" width="6000" height="4000"><rect width="6000" height="4000" fill="#135"/></svg>',
		headers: { ...headers.headers, 'content-type': 'image/svg+xml' },
	}).as('photoImage');
}

function setupPage(libraryCount = 1) {
	cy.intercept({ method: 'GET', pathname: '/api/**' }, (request) => {
		if (request.url.includes('/api/PlexPhoto/')) return;
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});
	return cy.basePageSetup({
		seed: 41,
		plexServerCount: 1,
		plexMovieLibraryCount: libraryCount,
		plexTvShowLibraryCount: 0,
		movieCount: 0,
		tvShowCount: 0,
		firstTimeSetup: false,
		override: {
			plexServer: (items) => items.map((item) => ({ ...item, id: serverId, name: 'Photo server' })),
			plexServerConnections: (items) => items,
			plexLibraries: (items) => items.map((item, index) => ({ ...item, id: libraryId + index, type: PlexMediaType.PhotoAlbum, plexServerId: serverId, title: index === 0 ? 'Photo library' : 'Other photos', photoAlbumCount: 1, isEnabled: true, syncedAt: '2026-01-01T00:00:00Z' })),
			plexAccounts: (items) => items.map((item) => ({ ...item, plexServerAccess: [serverId], plexLibraryAccess: Array.from({ length: libraryCount }, (_, index) => libraryId + index) })),
			downloadTasks: (items) => items,
			settings: (settings) => ({ ...settings, confirmationSettings: { ...settings.confirmationSettings, askDownloadPhotoImageConfirmation: true } }),
		},
	});
}

function selectPhotoTableAsset(title: string) {
	cy.getCy('photo-overview-table').then(($table) => {
		const rows = $table.find('[data-cy="media-q-table-mobile-card"], tbody tr');
		const matches = rows.filter((_, row) => row.textContent?.includes(title) ?? false);
		expect(matches, `photo table row ${title}`).to.have.length(1);
		cy.wrap(matches).find('[role="checkbox"]').click();
	});
}

function scrollPhotoOverview(position: 'top' | 'middle' | 'bottom') {
	cy.getCy('photo-overview-scroll').should(($host) => {
		const target = $host.find('.q-scrollarea__container').get(0) ?? $host.get(0);
		expect(target.clientHeight, 'scroll viewport height').to.be.greaterThan(0);
		if (!target.classList.contains('q-scrollarea__container')) return;

		// Data can arrive before ResizeObserver settles the responsive column count.
		const row = target.querySelector<HTMLElement>('.poster-table-item');
		expect(row, 'rendered poster row').not.to.equal(null);
		const rowBounds = row!.getBoundingClientRect();
		for (const card of row!.children) {
			const bounds = card.getBoundingClientRect();
			expect(bounds.width, 'poster card width').to.be.greaterThan(0);
			expect(bounds.left, 'poster fits the row start').to.be.at.least(rowBounds.left - 1);
			expect(bounds.right, 'poster fits the row end').to.be.at.most(rowBounds.right + 1);
		}
	}).then(($host) => {
		const target = $host.find('.q-scrollarea__container').get(0) ?? $host.get(0);
		let top: number;
		switch (position) {
			case 'top':
				top = 0;
				break;
			case 'bottom':
				top = target.scrollHeight;
				break;
			default:
				top = Math.max(0, (target.scrollHeight - target.clientHeight) / 2);
		}
		cy.wrap(target).scrollTo(0, top, { ensureScrollable: false });
	});
}

describe('Photos browsing', () => {
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

	it('returns to the originating aggregate state and searches globally from an album', () => {
		setupPage();
		setupPhotoApi();
		cy.visit(route(`/photos?sort=${SortType.Title}:desc`));
		cy.wait('@photoAlbums').its('request.url').should((url) => {
			expect(new URL(url).searchParams.get('sort')).to.equal(`${SortType.Title}:desc`);
		});
		cy.getCy('media-overview-bar-header').click();
		cy.getCy('media-overview-type-selector').filter(':visible').should('be.visible');
		cy.getCy(`media-overview-type-${PlexMediaType.Movie}`).filter(':visible').should('be.visible');
		cy.get('body').type('{esc}');
		cy.getCy('photo-album-41-301').should('contain.text', 'Summer trip').click();
		cy.location('pathname').should('eq', `/photos/${libraryId}/details/${albumId}`);
		cy.wait('@photoAlbumMembers');
		cy.getCy('photo-overview-list').should('contain.text', 'Beach photo');
		cy.getCy('photo-album-back').click();
		cy.location('pathname').should('eq', '/photos');
		cy.location('search').should((search) => expect(new URLSearchParams(search).get('sort')).to.equal(`${SortType.Title}:desc`));
		cy.getCy('photo-album-41-301').click();
		cy.location('pathname').should('eq', `/photos/${libraryId}/details/${albumId}`);
		cy.getCy('photo-overview-list').should('contain.text', 'Beach photo');
		cy.get<Interception[]>('@photoAlbumMembers.all').should('have.length', 1);
		cy.getCy('photo-search-input').filter(':visible').type('beach');
		cy.get<Interception[]>('@photoSearch.all').should((requests) => {
			expect(requests.at(-1)?.request.url).to.contain('q=beach');
		});
		cy.location('search').should('contain', 'q=beach');
		cy.get('main').should('not.contain.text', 'Search results for');
		cy.getCy('photo-asset-41-501').should('contain.text', 'Beach photo');
	});
	it('switches scoped photo views through the shared dropdown and changes the route and content', () => {
		setupPage();
		setupPhotoApi();
		cy.visit(route(`/photos/${libraryId}?view=albums`));
		cy.wait('@photoLibraryMetadata').its('request.query').should('include', { photoView: PhotoItemKind.Album });
		cy.wait('@photoAlbums');
		cy.getCy('photo-album-41-301').should('contain.text', 'Summer trip');
		cy.getCy('photo-overview-list').find('h2').should('not.exist');
		cy.get('main').should('not.contain.text', 'Searches all enabled Photos libraries');

		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-folders-btn').filter(':visible').click();
		cy.wait('@photoLibraryMetadata').its('request.query').should('include', { photoView: PhotoItemKind.Folder });
		cy.wait('@photoRoots');
		cy.location('search').should('contain', 'view=folders');
		cy.getCy('photo-folder-601').should('contain.text', 'Trips');
		cy.getCy('media-overview-sort-btn').filter(':visible').click();
		cy.get('[data-cy^="sort-option-"]').filter(':visible').should('have.length', 1)
			.and('have.attr', 'data-cy', `sort-option-${SortType.Title}-btn`);

		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-assets-btn').filter(':visible').click();
		cy.wait('@photoAssets');
		cy.location('search').should('contain', 'view=assets');
		cy.getCy('photo-asset-41-501').should('contain.text', 'Beach photo');
		cy.getCy('photo-overview-list').find('h2').should('not.exist');

		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-albums-btn').filter(':visible').click();
		cy.location('search').should('contain', 'view=albums');
		cy.getCy('photo-album-41-301').should('contain.text', 'Summer trip');
	});
	it('sorts photo assets by date and size with the shared sort DSL', () => {
		setupPage();
		setupPhotoApi();
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoLibraryMetadata').its('request.query').should('include', { photoView: PhotoItemKind.Asset });
		cy.wait('@photoAssets');

		cy.getCy('media-overview-sort-btn').click();
		cy.getCy(`sort-option-${SortType.AddedAt}-btn`).click();
		cy.wait('@photoAssets').its('request.url').should((url) => {
			expect(new URL(url).searchParams.get('sort')).to.equal(`${SortType.AddedAt}:asc`);
		});
		cy.location('search').should((search) => expect(new URLSearchParams(search).get('sort')).to.equal(`${SortType.AddedAt}:asc`));

		cy.getCy('media-overview-sort-btn').click();
		cy.getCy(`sort-option-${SortType.MediaSize}-btn`).click();
		cy.wait('@photoAssets').its('request.url').should((url) => {
			expect(new URL(url).searchParams.get('sort')).to.equal(`${SortType.MediaSize}:asc`);
		});
		cy.location('search').should((search) => expect(new URLSearchParams(search).get('sort')).to.equal(`${SortType.MediaSize}:asc`));
	});


	it('switches shared photo display modes while retaining multiple scoped assets and selection', () => {
		setupPage();
		const secondAsset: PhotoAssetBrowseDTO = {
			...asset,
			id: assetId + 1,
			title: 'Mountain photo',
			originalFilename: 'mountain.jpg',
		};
		setupPhotoApi([asset, secondAsset]);
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		cy.getCy('photo-asset-41-501').should('contain.text', 'Beach photo');
		cy.getCy('photo-asset-41-502').should('contain.text', 'Mountain photo');

		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').should('be.visible').and('contain.text', 'Beach photo').and('contain.text', 'Mountain photo');
		selectPhotoTableAsset('Mountain photo');
		cy.getCy('media-overview-bar-download-button').should('be.visible');
		cy.viewport(390, 844);
		cy.getCy('photo-overview-table').find('thead').should('contain.text', 'Title').and('contain.text', 'Actions');
		cy.getCy('photo-select-all').should('have.attr', 'aria-checked', 'mixed').click({ scrollBehavior: false });
		cy.getCy('photo-overview-table').find('tbody [role="checkbox"]').should('have.length', 2)
			.each(($checkbox) => cy.wrap($checkbox).should('have.attr', 'aria-checked', 'true'));
		cy.viewport(1280, 800);

		cy.getCy('change-view-mode-btn').filter(':visible').click({ scrollBehavior: false });
		cy.getCy('view-mode-poster-btn').filter(':visible').click();
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.getCy('photo-asset-41-502').should('be.visible');
		cy.getCy('photo-asset-41-501').find('[role="checkbox"]').should('not.exist');
		cy.getCy('photo-asset-41-502').find('[role="checkbox"]').should('not.exist');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').find('tbody [role="checkbox"][aria-checked="true"]').should('have.length', 2);
	});

	it('scrolls one mixed folder listing across transport ranges without pagers or a section gap', () => {
		setupPage();
		setupPhotoApi();
		cy.viewport(1280, 800);
		const folders = Array.from({ length: 101 }, (_, index) => ({
			id: 701 + index, plexServerId: serverId, plexLibraryId: libraryId,
			title: `Child ${index + 1}`, relativePath: `Trips/Child ${index + 1}`,
			parentId: 601, isRoot: false, isActive: null, recursiveAssetCount: 0, recursiveAssetBytes: 0,
		}));
		const assets = Array.from({ length: 101 }, (_, index) => ({
			...asset, id: assetId + index, title: `Photo ${index + 1}`,
		}));
		cy.intercept('GET', `**/api/PlexPhoto/libraries/${libraryId}/folders/601/children*`, (request) => {
			const query = new URL(request.url).searchParams;
			const folderPage = Number(query.get('folderPage') ?? 1);
			const assetPage = Number(query.get('assetPage') ?? 1);
			request.reply({ statusCode: 200, body: generateResultDTO({
				folder: { id: 601, plexServerId: serverId, plexLibraryId: libraryId, title: 'Trips', relativePath: '', parentId: null, isRoot: true, isActive: true, recursiveAssetCount: 101, recursiveAssetBytes: 206848 },
				breadcrumbs: [{ id: 601, title: 'Trips', relativePath: '' }],
				folders: { items: folders.slice((folderPage - 1) * 100, folderPage * 100), page: folderPage, pageSize: 100, totalCount: 101 },
				assets: { items: assets.slice((assetPage - 1) * 100, assetPage * 100), page: assetPage, pageSize: 100, totalCount: 101, totalBytes: 206848 },
			}), ...headers });
		}).as('folderRange');
		cy.visit(route(`/photos/${libraryId}?view=folders&folderId=601&folderPage=1&assetPage=1`));
		cy.wait('@folderRange');
		cy.location('search').should((search) => {
			const query = new URLSearchParams(search);
			expect(query.get('folderId')).to.equal('601');
			expect(query.has('folderPage')).to.equal(false);
			expect(query.has('assetPage')).to.equal(false);
		});
		cy.getCy('photo-folder-701').should('contain.text', 'Child 1');
		cy.get('main').should('not.contain.text', 'Selecting a folder includes every mapped descendant');
		cy.get('[data-cy$="-pagination"]').should('not.exist');
		scrollPhotoOverview('middle');
		cy.getCy('photo-folder-801').should('be.visible');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.getCy('photo-folder-801').then(($folder) => {
			cy.getCy('photo-asset-41-501').should(($image) => {
				expect(Math.abs($folder[0]!.getBoundingClientRect().top - $image[0]!.getBoundingClientRect().top)).to.be.lessThan(2);
			});
		});
		scrollPhotoOverview('bottom');
		cy.getCy('photo-asset-41-601').should('contain.text', 'Photo 101');
		cy.get<Interception[]>('@folderRange.all').should((requests) => {
			expect(requests.some((entry) => new URL(entry.request.url).searchParams.get('assetPage') === '2')).to.equal(true);
		});
	});

	it('keeps distant poster and table scroll positions instead of feeding observations back as scroll commands', () => {
		setupPage();
		const assets = Array.from({ length: 301 }, (_, index) => ({ ...asset, id: assetId + index, title: `Photo ${String(index + 1).padStart(3, '0')}` }));
		setupPhotoApi(assets);
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		scrollPhotoOverview('bottom');
		cy.getCy('photo-asset-41-801').should('be.visible');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-overview-table').should('be.visible');
		scrollPhotoOverview('bottom');
		cy.getCy('photo-asset-41-801').should('be.visible');
		scrollPhotoOverview('top');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-poster-btn').filter(':visible').click();
		cy.getCy('photo-asset-41-501').should('be.visible');
	});

	it('shows and selects unmapped images directly beneath root folders', () => {
		setupPage();
		setupPhotoApi();
		cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, {
			statusCode: 200, body: generateResultDTO({ items: [asset], page: 1, pageSize: 100, totalCount: 1, totalBytes: asset.size }), ...headers,
		}).as('rootAssets');
		cy.visit(route(`/photos/${libraryId}?view=folders`));
		cy.wait('@photoRoots');
		cy.wait('@rootAssets').its('request.url').should('contain', 'unmappedOnly=true');
		cy.getCy('photo-folder-601').should('be.visible');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		selectPhotoTableAsset('Beach photo');
		cy.getCy('media-overview-bar-download-button').should('be.visible');
	});

	it('wraps an image-first carousel across uncached ranges and returns focus to the last viewed image', () => {
		setupPage();
		const assets = Array.from({ length: 101 }, (_, index) => ({ ...asset, id: assetId + index, title: `Photo ${String(index + 1).padStart(3, '0')}` }));
		setupPhotoApi(assets);
		cy.intercept('POST', '**/api/Download/preview', {
			statusCode: 200, body: generateResultDTO({ previews: [], totalSize: 0, expanded: {} }), ...headers,
		}).as('viewerDownloadPreview');
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		cy.getCy('photo-asset-41-501').focus().type('{enter}');
		cy.wait('@photoImage');
		cy.getCy('photo-viewer-image').should('have.attr', 'alt', 'Photo 001').and('have.prop', 'naturalWidth', 6000);
		cy.getCy('photo-asset-dialog').should('not.contain.text', 'A summer photo');
		cy.get<Interception[]>('@photoInspection.all').should('have.length', 0);
		cy.getCy('photo-viewer-image').then(($image) => {
			const fittedWidth = $image[0]!.getBoundingClientRect().width;
			cy.wrap($image).trigger('wheel', { deltaY: -100, eventConstructor: 'WheelEvent' });
			cy.getCy('photo-viewer-image').should(($zoomed) => {
				expect($zoomed[0]!.getBoundingClientRect().width).to.be.greaterThan(fittedWidth);
			});
			cy.getCy('photo-viewer-image').trigger('wheel', { deltaY: 100, eventConstructor: 'WheelEvent' });
			cy.getCy('photo-viewer-image').should(($fitted) => {
				expect($fitted[0]!.getBoundingClientRect().width).to.be.closeTo(fittedWidth, 1);
			});
		});
		cy.getCy('photo-viewer-previous').click();
		cy.getCy('photo-viewer-image').should('have.attr', 'alt', 'Photo 101');
		cy.getCy('photo-viewer-details-action').click();
		cy.wait('@photoInspection').its('request.url').should('contain', '/assets/601');
		cy.getCy('photo-viewer-details').should('contain.text', 'A summer photo');
		cy.getCy('photo-viewer-details-close').click();
		cy.getCy('photo-viewer-download').click();
		cy.wait('@viewerDownloadPreview').its('request.body.downloadMedias').should('have.length', 1).its('0').should('deep.include', {
			plexServerId: serverId,
			plexLibraryId: libraryId,
			mediaIds: [601],
			type: PlexMediaType.PhotoImage,
			selectionKind: PhotoItemKind.Asset,
			qualities: [],
		});
		cy.getCy('download-confirmation-dialog').should('be.visible');
		cy.getCy('download-confirmation-cancel').click();
		cy.getCy('photo-viewer-zoom').click();
		cy.getCy('photo-viewer-zoom').should('have.attr', 'aria-pressed', 'true');
		cy.getCy('photo-asset-dialog-close').click();
		cy.getCy('photo-asset-dialog').should('not.exist');
		cy.getCy('photo-asset-41-601').should('be.visible');
		cy.focused().should('have.attr', 'data-cy', 'photo-asset-41-601');
	});

	it('selects every global-search match through the table header, including unloaded matches in another library', () => {
		setupPage(2);
		const assets = [
			...Array.from({ length: 301 }, (_, index) => ({ ...asset, id: assetId + index, title: `Photo ${index + 1}` })),
			{ ...asset, id: 901, plexLibraryId: 42, plexLibraryTitle: 'Other photos', title: 'Photo 302' },
		];
		setupPhotoApi(assets);
		cy.intercept('POST', '**/api/Download/preview', {
			statusCode: 200, body: generateResultDTO({ previews: [], totalSize: 0, expanded: {} }), ...headers,
		}).as('selectedPreview');
		cy.visit(route('/photos?q=photo'));
		cy.wait('@photoSearch');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-table-btn').filter(':visible').click();
		cy.getCy('photo-select-all').click().should('have.attr', 'aria-checked', 'true');
		cy.getCy('media-overview-bar-download-button').click();
		cy.wait('@selectedPreview').its('request.body.downloadMedias').should((groups) => {
			expect(groups).to.have.length(2);
			const local = groups.find((group: { plexLibraryId: number }) => group.plexLibraryId === 41);
			const other = groups.find((group: { plexLibraryId: number }) => group.plexLibraryId === 42);
			expect(local.mediaIds).to.have.length(301).and.include(801);
			expect(other.mediaIds).to.deep.equal([901]);
			expect(local.selectionKind).to.equal(PhotoItemKind.Asset);
			expect(other.plexServerId).to.equal(serverId);
		});
	});

	it('exposes per-item photo actions on mobile and returns focus to the card after inspection', () => {
		setupPage();
		setupPhotoApi();
		cy.viewport(1280, 800);
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@photoAssets');
		cy.getCy('change-view-mode-btn').filter(':visible').click();
		cy.getCy('view-mode-poster-btn').click();
		cy.viewport(390, 844);

		cy.getCy('photo-asset-41-501').within(() => {
			cy.getCy('media-poster-menu-trigger').should('be.visible').click();
		});
		cy.getCy('media-poster-menu').should('be.visible').find('button[aria-label="Download"]').should('be.visible');
		cy.getCy('media-poster-menu').find('button[aria-label="View"]').click();
		cy.wait('@photoImage');
		cy.getCy('media-poster-menu').should('not.exist');
		cy.getCy('photo-viewer-image').should('have.attr', 'alt', 'Beach photo');
		cy.get('body').type('{esc}');
		cy.getCy('photo-asset-dialog').should('not.exist');
		cy.focused().should('have.attr', 'data-cy', 'photo-asset-41-501');
		cy.getCy('photo-asset-41-501').find('[role="checkbox"]').should('not.exist');
	});

	it('keeps the cached catalog through refresh failure and publishes new assets only after authoritative completion', () => {
		setupPage();
		setupPhotoApi();
		let queues: LibrarySyncJobQueueDTO[] = [];
		let published = false;
		let assetReads = 0;
		cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(queues), ...headers });
		}).as('syncSnapshot');
		cy.intercept('POST', PlexLibraryPaths.refreshLibraryMediaEndpoint(libraryId), (request) => {
			queues = [generateLibrarySyncJobQueue({ plexLibraryId: libraryId, plexServerId: serverId, status: LibrarySyncJobStatus.Queued, completedAt: null })];
			request.reply({ statusCode: 200, body: generateResultDTO(queues[0]), ...headers });
		}).as('queueRefresh');
		cy.intercept({ method: 'GET', pathname: '/api/PlexPhoto/assets' }, (request) => {
			assetReads++;
			const items = published ? [{ ...asset, id: 502, title: 'Newly synchronized photo' }] : [asset];
			request.reply({ statusCode: 200, body: generateResultDTO({ items, page: 1, pageSize: 100, totalCount: 1, totalBytes: 2048 }), ...headers });
		}).as('publishedAssets');
		cy.visit(route(`/photos/${libraryId}?view=assets`));
		cy.wait('@publishedAssets');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.getCy('media-overview-refresh-library-btn').click();
		cy.getCy('refresh-mode-incremental-button').click();
		cy.wait('@queueRefresh').its('request.body').should('deep.equal', { forceLibrarySync: true, forceMediaRefresh: false });
		cy.getCy('media-overview-library-sync-queued-banner').should('be.visible');
		cy.getCy('media-overview-refresh-library-btn').should('be.disabled');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.then(() => {
			queues = queues.map((queue) => ({ ...queue, status: LibrarySyncJobStatus.Processing }));
			cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Started, queues[0]);
		});
		cy.wait('@syncSnapshot');
		cy.getCy('refresh-library-container').should('be.visible');
		cy.getCy('media-overview-library-sync-processing-banner').should('not.exist');
		cy.getCy('photo-overview-list').should('not.exist');
		cy.getCy('media-overview-refresh-library-btn').should('not.exist');
		cy.then(() => {
			queues = queues.map((queue) => ({ ...queue, status: LibrarySyncJobStatus.Failed, completedAt: '2026-01-02T00:00:00Z', errorMessage: 'Plex is offline' }));
			cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Completed, queues[0]);
		});
		cy.wait('@syncSnapshot');
		cy.getCy('media-overview-refresh-library-btn').should('not.be.disabled');
		cy.getCy('photo-asset-41-501').should('be.visible');
		cy.then(() => expect(assetReads, 'failure preserves the last published catalog').to.equal(1));
		cy.getCy('media-overview-refresh-library-btn').click();
		cy.getCy('refresh-mode-incremental-button').click();
		cy.wait('@queueRefresh');
		cy.getCy('media-overview-library-sync-queued-banner').should('be.visible');
		cy.getCy('photo-asset-41-501').click();
		cy.wait('@photoImage');
		cy.getCy('photo-viewer-details-action').click();
		cy.wait('@photoInspection');
		cy.getCy('photo-viewer-details').should('contain.text', 'A summer photo').and('contain.text', 'Summer trip');
		cy.then(() => {
			published = true;
			queues = queues.map((queue) => ({ ...queue, status: LibrarySyncJobStatus.Completed, completedAt: '2026-01-03T00:00:00Z' }));
			cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Completed, queues[0]);
		});
		cy.wait('@publishedAssets');
		cy.getCy('photo-asset-dialog').should('not.exist');
		cy.getCy('photo-asset-41-502').should('contain.text', 'Newly synchronized photo');
		cy.getCy('photo-asset-41-501').should('not.exist');
		cy.getCy('media-overview-library-sync-queued-banner').should('not.exist');
		cy.then(() => expect(assetReads, 'only successful completion reloads the catalog').to.equal(2));
	});
});
