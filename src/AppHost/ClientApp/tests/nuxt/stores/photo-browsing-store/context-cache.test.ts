import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { nextTick } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { of, Subject, throwError } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateLibrarySyncJobQueue, generatePlexAccount, generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { plexPhotoApi } from '@api';
import { useAccountStore, useLibraryStore, usePhotoBrowsingStore, useSignalrStore } from '@store';
import {
	LibrarySyncJobStatus, PhotoAssetKind, PhotoItemKind, PlexMediaType, SortType,
	type PhotoAlbumBrowseDTO, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO,
	type PhotoFolderBrowseDTO, type PhotoFolderChildrenDTO,
} from '@dto';
import type { ResultDTO } from '@interfaces';
import { createDefaultMediaOverviewSort, toggleMediaOverviewSort } from '@composables';

const owner = {};
const route = (overrides: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
	mode: 'library', libraryId: 7, view: 'albums', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(), ...overrides,
});
const titleDescending = toggleMediaOverviewSort(createDefaultMediaOverviewSort(), SortType.Title);
const asset = (id: number, libraryId = 7): PhotoAssetBrowseDTO => ({
	id, plexServerId: 1, plexLibraryId: libraryId, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
	assetKind: PhotoAssetKind.Image, container: 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: false,
	plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
});
const album = (id: number, libraryId = 7): PhotoAlbumBrowseDTO => ({
	id, plexServerId: 1, plexLibraryId: libraryId, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Album ${id}`,
	assetCount: 1, assetBytes: 10, hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
});
const albumsPage = (items: PhotoAlbumBrowseDTO[]) => ({ items, page: 1, pageSize: 100, totalCount: items.length, totalAssetBytes: items.length * 10 });
const assetsPage = (libraryId: number, page = 1, total = 1, version = 0): PhotoAssetPageDTO => ({
	items: Array.from({ length: Math.min(100, Math.max(0, total - (page - 1) * 100)) }, (_, offset) => asset(version * 1000 + (page - 1) * 100 + offset + 1, libraryId)),
	page, pageSize: 100, totalCount: total, totalBytes: total * 10,
});
const folder = (id: number): PhotoFolderBrowseDTO => ({
	id, plexLibraryId: 7, plexServerId: 1, title: `Folder ${id}`, relativePath: `Folder ${id}`,
	parentId: null, isRoot: false, isActive: null, recursiveAssetCount: 101, recursiveAssetBytes: 1010,
});
const folderPage = (id: number, page = 1): PhotoFolderChildrenDTO => ({
	folder: folder(id), breadcrumbs: [{ id, title: `Folder ${id}`, relativePath: `Folder ${id}` }],
	folders: { items: [], page: 1, pageSize: 100, totalCount: 0 },
	assets: assetsPage(7, page, id === 10 ? 101 : 1, id),
});
const activate = async (context: IPhotoBrowseActivation) => {
	await subscribeSpyTo(usePhotoBrowsingStore().activate(context, owner)).onComplete();
};

describe('PhotoBrowsingStore - context cache', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(async () => {
		vi.restoreAllMocks();
		mock = getAxiosMock();
		setActivePinia(createPinia());
		for (const id of [7, 8, 9, 10]) {
			useLibraryStore().updateLibrary(generatePlexLibrary({
				seed: new Seed(id), plexServerId: 1, type: PlexMediaType.PhotoAlbum,
				partialData: { id, isEnabled: true, photoAlbumCount: 2, syncedAt: '2026-01-01T00:00:00Z' },
			}));
		}
		vi.spyOn(useSignalrStore(), 'getRefreshNotification').mockReturnValue(of());
		await subscribeSpyTo(usePhotoBrowsingStore().setup()).onComplete();
	});
	afterEach(() => {
		usePhotoBrowsingStore().$reset();
		mock.restore();
		vi.restoreAllMocks();
	});

	test('Should restore folder pages, metadata, scroll and selection without refetching a visited folder', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoFolderChildrenEndpoint').mockImplementation((_libraryId, folderId, query) =>
			of(generateResultDTO(folderPage(folderId, query?.assetPage ?? 1))));
		const store = usePhotoBrowsingStore();
		const first = route({ view: 'folders', folderId: 10 });
		await activate(first);
		await subscribeSpyTo(store.requestRange(100, 101)).onComplete();
		store.setScrollIndex(100);
		store.toggleAsset(asset(10101), true);

		// Act
		await activate(route({ view: 'folders', folderId: 20 }));
		await activate(first);
		await subscribeSpyTo(store.requestRange(100, 101)).onComplete();

		// Assert
		expect(request).toHaveBeenCalledTimes(3);
		expect(store.folder?.title).toBe('Folder 10');
		expect(store.breadcrumbs).toEqual(folderPage(10).breadcrumbs);
		expect(store.folderAssets.totalCount).toBe(101);
		expect(store.folderAssets.totalBytes).toBe(1010);
		expect(store.overviewItemAt(100)).toEqual(expect.objectContaining({ kind: PhotoItemKind.Asset, asset: expect.objectContaining({ id: 10101 }) }));
		expect(store.scrollIndex).toBe(100);
		expect(store.selectedSelectors).toEqual([expect.objectContaining({ id: 10101, plexLibraryId: 7 })]);
	});

	test('Should reuse the album overview and each album membership feed independently', async () => {
		// Arrange
		const overview = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockReturnValue(of(generateResultDTO(albumsPage([album(1), album(2)]))));
		const members = vi.spyOn(plexPhotoApi, 'getPhotoAlbumMembersEndpoint').mockImplementation((_libraryId, albumId) =>
			of(generateResultDTO({ ...assetsPage(7), items: [asset(albumId * 10)] })));
		const first = route({ mode: 'album', view: 'assets', albumId: 1 });
		await activate(route());
		await activate(first);
		await activate(route({ mode: 'album', view: 'assets', albumId: 2 }));

		// Act
		await activate(route());
		expect(usePhotoBrowsingStore().albums.items.map((item) => item.id)).toEqual([1, 2]);
		await activate(first);

		// Assert
		expect(overview).toHaveBeenCalledOnce();
		expect(members).toHaveBeenCalledTimes(2);
		expect(usePhotoBrowsingStore().albumMembers.items.map((item) => item.id)).toEqual([10]);
	});

	test('Should isolate identical asset IDs by library and sort while reusing each visited context', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) =>
			of(generateResultDTO({ ...assetsPage(query?.plexLibraryId ?? 7), items: [asset(query?.sort === 'sortIndex:desc' ? 2 : 1, query?.plexLibraryId ?? 7)] })));
		const first = route({ view: 'assets' });
		const otherLibrary = route({ view: 'assets', libraryId: 8 });
		const descending = route({ view: 'assets', sort: titleDescending });
		await activate(first);
		await activate(otherLibrary);
		await activate(descending);

		// Act
		await activate(otherLibrary);
		expect(usePhotoBrowsingStore().assets.items).toEqual([asset(1, 8)]);
		await activate(first);
		expect(usePhotoBrowsingStore().assets.items).toEqual([asset(1, 7)]);
		await activate(descending);

		// Assert
		expect(request).toHaveBeenCalledTimes(3);
		expect(usePhotoBrowsingStore().assets.items).toEqual([asset(2, 7)]);
	});

	test('Should isolate search queries and restore successful empty results', async () => {
		// Arrange
		const search = vi.spyOn(plexPhotoApi, 'searchPhotoAssetsEndpoint').mockImplementation((query) =>
			of(generateResultDTO({ ...assetsPage(7), items: query?.q === 'empty' ? [] : [asset(42)], totalCount: query?.q === 'empty' ? 0 : 1 })));
		const empty = route({ searchQuery: 'empty' });
		await activate(empty);
		await activate(route({ searchQuery: 'cats' }));

		// Act
		await activate(empty);

		// Assert
		expect(search).toHaveBeenCalledTimes(2);
		expect(usePhotoBrowsingStore().search.items).toEqual([]);
		expect(usePhotoBrowsingStore().overviewCount).toBe(0);
		expect(usePhotoBrowsingStore().search.loaded).toBe(true);
	});

	test('Should retry an uncached failed context instead of restoring an error as an empty result', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint')
			.mockReturnValueOnce(throwError(() => new Error('offline')))
			.mockReturnValue(of(generateResultDTO(albumsPage([album(9)]))));
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO(assetsPage(7))));
		await activate(route());
		await activate(route({ view: 'assets' }));

		// Act
		await activate(route());

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(usePhotoBrowsingStore().albums.items.map((item) => item.id)).toEqual([9]);
		expect(usePhotoBrowsingStore().error).toBe(false);
	});

	test('Should evict the least recently visited context when combined cached and active rows exceed 600', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) =>
			of(generateResultDTO(assetsPage(query?.plexLibraryId ?? 7, query?.page ?? 1, 200))));
		const open = async (libraryId: number) => {
			await activate(route({ view: 'assets', libraryId }));
			await subscribeSpyTo(usePhotoBrowsingStore().requestRange(100, 200)).onComplete();
		};
		await open(7);
		await open(8);
		await open(9);

		// Act
		await open(7);
		await open(10);
		await open(7);
		await open(8);

		// Assert
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7)).toHaveLength(2);
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 8)).toHaveLength(4);
		expect(usePhotoBrowsingStore().assets.items).toHaveLength(200);
		expect(usePhotoBrowsingStore().assets.items.every((item) => item.plexLibraryId === 8)).toBe(true);
	});

	test('Should bound empty cached contexts rather than retaining every visited album indefinitely', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumMembersEndpoint').mockReturnValue(of(generateResultDTO(assetsPage(7, 1, 0))));
		for (let albumId = 1; albumId <= 22; albumId++) await activate(route({ mode: 'album', view: 'assets', albumId }));

		// Act
		await activate(route({ mode: 'album', view: 'assets', albumId: 1 }));
		await activate(route({ mode: 'album', view: 'assets', albumId: 22 }));

		// Assert
		expect(request.mock.calls.filter(([, albumId]) => albumId === 1)).toHaveLength(2);
		expect(request.mock.calls.filter(([, albumId]) => albumId === 22)).toHaveLength(1);
		expect(usePhotoBrowsingStore().albumMembers.totalCount).toBe(0);
	});

	test('Should invalidate an inactive published library without discarding another library cache', async () => {
		// Arrange
		let published = false;
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockImplementation((query) =>
			of(generateResultDTO(albumsPage([album(published ? 2 : 1, query?.plexLibraryId ?? 7)]))));
		await activate(route());
		await activate(route({ libraryId: 8 }));
		usePhotoBrowsingStore().deactivate(owner);

		// Act
		published = true;
		useLibraryStore().updateSyncQueue(generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-01-02T00:00:00Z' }));
		await nextTick();
		await activate(route());
		expect(usePhotoBrowsingStore().albums.items).toEqual([album(2)]);
		await activate(route({ libraryId: 8 }));

		// Assert
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7)).toHaveLength(2);
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 8)).toHaveLength(1);
		expect(usePhotoBrowsingStore().albums.items).toEqual([album(1, 8)]);
	});

	test.each(['aggregate', 'search'] as const)('Should invalidate cached %s results when a contributing library publishes', async (scope) => {
		// Arrange
		let published = false;
		const response = (libraryId = 7) => of(generateResultDTO({ ...assetsPage(libraryId), items: [asset(published ? 2 : 1, libraryId)] }));
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => response(query?.plexLibraryId ?? 7));
		const search = vi.spyOn(plexPhotoApi, 'searchPhotoAssetsEndpoint').mockImplementation(() => response());
		const context = scope === 'aggregate'
			? route({ mode: 'aggregate', libraryId: 0, view: 'assets' })
			: route({ libraryId: 8, searchQuery: 'cats' });
		await activate(context);
		await activate(route({ libraryId: 8, view: 'assets' }));

		// Act
		published = true;
		useLibraryStore().updateSyncQueue(generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-01-02T00:00:00Z' }));
		await nextTick();
		await activate(context);

		// Assert
		expect(usePhotoBrowsingStore().overviewItemAt(0)).toEqual(expect.objectContaining({ asset: expect.objectContaining({ id: 2 }) }));
		if (scope === 'search') expect(search).toHaveBeenCalledTimes(2);
	});

	test.each(['library', 'account'] as const)('Should purge cached rows when %s access is revoked while browsing is inactive', async (access) => {
		// Arrange
		const account = generatePlexAccount({ id: 1, config: { seed: 1209 }, partialData: { plexServerAccess: [1], plexLibraryAccess: [7] } });
		useAccountStore().accounts = [account];
		await nextTick();
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint')
			.mockReturnValueOnce(of(generateResultDTO(albumsPage([album(1)]))))
			.mockReturnValue(of(generateResultDTO(albumsPage([album(2)]))));
		await activate(route());
		const store = usePhotoBrowsingStore();
		store.deactivate(owner);
		const libraryStore = useLibraryStore();
		const library = libraryStore.getLibrary(7)!;

		// Act
		if (access === 'library') libraryStore.updateLibrary({ ...library, isEnabled: false });
		else useAccountStore().accounts = [{ ...account, plexLibraryAccess: [] }];
		await nextTick();
		await activate(route());

		// Assert
		expect(store.disabledLibrary).toBe(true);
		expect(store.overviewCount).toBe(0);
		expect(request).toHaveBeenCalledOnce();

		// Act
		if (access === 'library') libraryStore.updateLibrary({ ...library, isEnabled: true });
		else useAccountStore().accounts = [account];
		await nextTick();
		await activate(route());

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(store.albums.items).toEqual([album(2)]);
	});

	test.each([LibrarySyncJobStatus.Failed, LibrarySyncJobStatus.Cancelled])('Should retain cached published rows after a %s sync', async (status) => {
		// Arrange
		let version = 1;
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockImplementation((query) =>
			of(generateResultDTO(albumsPage([album(version, query?.plexLibraryId ?? 7)]))));
		await activate(route());
		await activate(route({ libraryId: 8 }));

		// Act
		version = 2;
		useLibraryStore().updateSyncQueue(generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status, completedAt: '2026-01-02T00:00:00Z' }));
		await nextTick();
		await activate(route());

		// Assert
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7)).toHaveLength(1);
		expect(usePhotoBrowsingStore().albums.items).toEqual([album(1)]);
	});

	test('Should clear saved contexts on session reset', async () => {
		// Arrange
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint')
			.mockReturnValueOnce(of(generateResultDTO(albumsPage([album(1)]))))
			.mockReturnValue(of(generateResultDTO(albumsPage([album(2)]))));
		await activate(route());
		usePhotoBrowsingStore().deactivate(owner);

		// Act
		usePhotoBrowsingStore().$reset();
		await subscribeSpyTo(usePhotoBrowsingStore().setup()).onComplete();
		await activate(route());

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(usePhotoBrowsingStore().albums.items).toEqual([album(2)]);
	});

	test('Should not restore old later pages after a forced publication reload', async () => {
		// Arrange
		let version = 0;
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) =>
			of(generateResultDTO(assetsPage(query?.plexLibraryId ?? 7, query?.page ?? 1, 200, version))));
		const store = usePhotoBrowsingStore();
		const context = route({ view: 'assets' });
		await activate(context);
		await subscribeSpyTo(store.requestRange(100, 200)).onComplete();

		// Act
		version = 1;
		await subscribeSpyTo(store.loadCurrent(true)).onComplete();
		await activate(route({ libraryId: 8, view: 'assets' }));
		await activate(context);
		expect(store.overviewItemAt(100)).toBeNull();
		await subscribeSpyTo(store.requestRange(100, 200)).onComplete();

		// Assert
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7 && query.page === 1)).toHaveLength(2);
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7 && query.page === 2)).toHaveLength(2);
		expect(store.overviewItemAt(100)).toEqual(expect.objectContaining({ asset: expect.objectContaining({ id: 1101 }) }));
	});

	test('Should exclude an initial late response after a forced reload in the same context', async () => {
		// Arrange
		const late = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint')
			.mockReturnValueOnce(late.asObservable())
			.mockReturnValue(of(generateResultDTO({ ...assetsPage(7), items: [asset(2)] })));
		const store = usePhotoBrowsingStore();
		const pending = subscribeSpyTo(store.activate(route({ view: 'assets' }), owner));

		// Act
		await subscribeSpyTo(store.loadCurrent(true)).onComplete();
		late.next(generateResultDTO(assetsPage(7)));
		late.complete();
		await pending.onComplete();
		store.deactivate(owner);
		await activate(route({ view: 'assets' }));

		// Assert
		expect(store.assets.items).toEqual([asset(2)]);
	});

	test('Should reject a late response after restoring another context and never cache that cancelled result', async () => {
		// Arrange
		const late = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		let otherReads = 0;
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => {
			if (query?.plexLibraryId === 7) return of(generateResultDTO(assetsPage(7)));
			return ++otherReads === 1 ? late.asObservable() : of(generateResultDTO({ ...assetsPage(8), items: [asset(3, 8)] }));
		});
		const first = route({ view: 'assets' });
		const other = route({ view: 'assets', libraryId: 8 });
		await activate(first);
		const pending = subscribeSpyTo(usePhotoBrowsingStore().activate(other, owner));

		// Act
		await activate(first);
		late.next(generateResultDTO({ ...assetsPage(8), items: [asset(2, 8)] }));
		late.complete();
		await pending.onComplete();
		expect(usePhotoBrowsingStore().assets.items).toEqual([asset(1)]);
		await activate(other);

		// Assert
		expect(request.mock.calls.filter(([query]) => query?.plexLibraryId === 7)).toHaveLength(1);
		expect(usePhotoBrowsingStore().assets.items).toEqual([asset(3, 8)]);
		expect(otherReads).toBe(2);
	});
});
