import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoAssetKind, PhotoItemKind, PlexMediaType, SortDirection, SortType, type PhotoAlbumBrowseDTO, type PhotoAlbumPageDTO, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PhotoFolderChildrenDTO, type PhotoFolderPageDTO } from '@dto';
import { useLibraryStore, usePhotoBrowsingStore } from '@store';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import { createDefaultMediaOverviewSort, toggleMediaOverviewSort } from '@composables';

describe('PhotoBrowsingStore - range request contracts', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(7),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 7, title: 'Photos', isEnabled: true },
		}));
	});
	const route = (partial: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
		mode: 'library',
		libraryId: 7,
		view: 'assets',
		folderId: null,
		searchQuery: '',
		sort: createDefaultMediaOverviewSort(),
		...partial,
	});
	const album = (id: number): PhotoAlbumBrowseDTO => ({
		id,
		plexServerId: 1,
		plexLibraryId: 7,
		plexServerName: 'Server',
		plexLibraryTitle: 'Photos',
		title: `Album ${id}`,
		assetCount: 1,
		assetBytes: 1,
		hasThumbnail: false,
		plexApiRatingKey: id,
		plexApiMetaDataKey: id,
	});

	const asset = (id: number): PhotoAssetBrowseDTO => ({
		id,
		plexServerId: 1,
		plexLibraryId: 7,
		plexServerName: 'Server',
		plexLibraryTitle: 'Photos',
		title: `Asset ${id}`,
		assetKind: PhotoAssetKind.Image,
		container: 'jpg',
		originalFilename: `${id}.jpg`,
		hasThumbnail: false,
		plexApiRatingKey: id,
		plexApiMetaDataKey: id,
		size: 10,
	});

	test('requests only the selected aggregate feed without route page fields', async () => {
		const albums = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [album(1)], page: 1, pageSize: 100, totalCount: 1, totalAssetBytes: 1,
		} as PhotoAlbumPageDTO)));
		const assets = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint');
		const store = usePhotoBrowsingStore();

		await subscribeSpyTo(store.activate({ mode: 'aggregate', libraryId: 0, view: 'albums', folderId: null, searchQuery: '', sort: toggleMediaOverviewSort(createDefaultMediaOverviewSort(), SortType.Title) }, owner)).onComplete();

		expect(albums).toHaveBeenCalledWith({ page: 1, size: 100, sort: `${SortType.Title}:desc` }, expect.any(Object));
		expect(assets).not.toHaveBeenCalled();
		expect(store.overviewCount).toBe(1);
		expect(store.overviewItemAt(0)).toEqual({ kind: 'Album', album: album(1), index: 0 });
	});
	test.each([
		{ field: SortType.AddedAt, sort: SortDirection.Desc, wire: `${SortType.AddedAt}:desc` },
		{ field: SortType.MediaSize, sort: SortDirection.Asc, wire: `${SortType.MediaSize}:asc` },
	])('serializes $field/$sort using the shared photo sort DSL', async ({ field, sort, wire }) => {
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [asset(1)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 10,
		} as PhotoAssetPageDTO)));
		const store = usePhotoBrowsingStore();

		await subscribeSpyTo(store.activate(route({ sort: { field, sort } }), owner)).onComplete();

		expect(request).toHaveBeenCalledWith({ page: 1, size: 100, sort: wire, plexLibraryId: 7 }, expect.any(Object));
	});

	test('loads a distant asset range without requesting preceding pages', async () => {
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => {
			const page = query?.page ?? 1;
			const items = page === 1 ? [asset(1)] : page === 3 ? [asset(201)] : [];
			return of(generateResultDTO({ items, page, pageSize: 100, totalCount: 250, totalBytes: 250 } as PhotoAssetPageDTO));
		});
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		request.mockClear();

		await subscribeSpyTo(store.requestRange(200, 220)).onComplete();

		expect(request).toHaveBeenCalledTimes(1);
		expect(request).toHaveBeenCalledWith({ page: 3, size: 100, sort: `${SortType.Title}:asc`, plexLibraryId: 7 }, expect.any(Object));
		expect(store.overviewCount).toBe(250);
		expect(store.overviewItemAt(200)).toEqual(expect.objectContaining({ kind: 'Asset', index: 200 }));
	});

	test('combines root folders with unmapped assets and marks the asset request explicitly', async () => {
		const roots = vi.spyOn(plexPhotoApi, 'getPhotoSourceRootsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [{ id: 10, plexServerId: 1, plexLibraryId: 7, title: 'Root', relativePath: 'Root', parentId: null, isRoot: true, recursiveAssetCount: 2, recursiveAssetBytes: 2 }],
			page: 1, pageSize: 100, totalCount: 1,
		} as PhotoFolderPageDTO)));
		const assets = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [asset(20)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 10,
		} as PhotoAssetPageDTO)));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'folders' }), owner)).onComplete();

		expect(roots).toHaveBeenCalledWith(7, { page: 1, size: 100, sort: `${SortType.Title}:asc` }, expect.any(Object));
		expect(assets).toHaveBeenCalledWith(expect.objectContaining({ plexLibraryId: 7, unmappedOnly: true }), expect.any(Object));
		expect(store.overviewCount).toBe(2);
		expect(store.overviewFolderCount).toBe(1);
		expect(store.overviewItemAt(1)).toEqual(expect.objectContaining({ kind: 'Asset', index: 1 }));
	});

	test('uses one mixed folder-child transport request for a distant folder and asset page', async () => {
		const children = vi.spyOn(plexPhotoApi, 'getPhotoFolderChildrenEndpoint').mockImplementation((_libraryId, _folderId, query) => of(generateResultDTO({
			folder: { id: 99, plexServerId: 1, plexLibraryId: 7, title: 'Folder', relativePath: 'Folder', parentId: null, isRoot: true, recursiveAssetCount: 250, recursiveAssetBytes: 250 },
			breadcrumbs: [],
			folders: { items: query?.folderPage === 3 ? [{ id: 300, plexServerId: 1, plexLibraryId: 7, title: 'Child', relativePath: 'Child', parentId: 99, isRoot: false, recursiveAssetCount: 1, recursiveAssetBytes: 1 }] : [], page: query?.folderPage ?? 1, pageSize: 100, totalCount: 250 } as PhotoFolderPageDTO,
			assets: { items: query?.assetPage === 3 ? [asset(301)] : [], page: query?.assetPage ?? 1, pageSize: 100, totalCount: 250, totalBytes: 250 } as PhotoAssetPageDTO,
		} as unknown as PhotoFolderChildrenDTO)));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'folders', folderId: 99 }), owner)).onComplete();
		children.mockClear();

		await subscribeSpyTo(store.requestRange(200, 220)).onComplete();

		expect(children).toHaveBeenCalledWith(7, 99, { folderPage: 3, folderSize: 100, assetPage: 1, assetSize: 100, sort: `${SortType.Title}:asc` }, expect.any(Object));
		expect(store.overviewItemAt(200)).toEqual(expect.objectContaining({ kind: 'Folder', index: 200 }));
	});
	test('fetches the second root asset page for an unaligned range', async () => {
		vi.spyOn(plexPhotoApi, 'getPhotoSourceRootsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 0,
		} as PhotoFolderPageDTO)));
		const assets = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => {
			const page = query?.page ?? 1;
			const items = page === 1
				? Array.from({ length: 100 }, (_, index) => asset(index))
				: Array.from({ length: 11 }, (_, index) => asset(100 + index));
			return of(generateResultDTO({
				items, page, pageSize: 100, totalCount: 200, totalBytes: 200,
			} as PhotoAssetPageDTO));
		});
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'folders' }), owner)).onComplete();
		assets.mockClear();

		await subscribeSpyTo(store.requestRange(95, 115)).onComplete();

		expect(assets).toHaveBeenCalledWith(expect.objectContaining({ page: 2 }), expect.any(Object));
		expect(store.overviewItemAt(110)).toEqual(expect.objectContaining({ kind: PhotoItemKind.Asset, index: 110 }));
	});

	test('fetches the second child-folder page for an unaligned range', async () => {
		const children = vi.spyOn(plexPhotoApi, 'getPhotoFolderChildrenEndpoint').mockImplementation((_libraryId, _folderId, query) => {
			const folderPage = query?.folderPage ?? 1;
			const firstPage = folderPage === 1;
			const startId = firstPage ? 0 : 100;
			const items = Array.from({ length: firstPage ? 100 : 11 }, (_, index) => {
				const id = startId + index;
				return { id, plexServerId: 1, plexLibraryId: 7, title: `Child ${id}`, relativePath: `Child ${id}`, parentId: 99, isRoot: false, recursiveAssetCount: 1, recursiveAssetBytes: 1 };
			});
			return of(generateResultDTO({
				folder: { id: 99, plexServerId: 1, plexLibraryId: 7, title: 'Folder', relativePath: 'Folder', parentId: null, isRoot: true, recursiveAssetCount: 200, recursiveAssetBytes: 200 },
				breadcrumbs: [],
				folders: { items, page: folderPage, pageSize: 100, totalCount: 200 } as PhotoFolderPageDTO,
				assets: { items: [], page: query?.assetPage ?? 1, pageSize: 100, totalCount: 0, totalBytes: 0 } as PhotoAssetPageDTO,
			} as unknown as PhotoFolderChildrenDTO));
		});
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'folders', folderId: 99 }), owner)).onComplete();
		children.mockClear();

		await subscribeSpyTo(store.requestRange(95, 115)).onComplete();

		expect(children).toHaveBeenCalledWith(7, 99, expect.objectContaining({ folderPage: 2, assetPage: 1 }), expect.any(Object));
		expect(store.overviewItemAt(110)).toEqual(expect.objectContaining({ kind: PhotoItemKind.Folder, index: 110 }));
	});

	test.each(['albums', 'folders'] as const)('global search takes precedence over the %s container view', async (view) => {
		const search = vi.spyOn(plexPhotoApi, 'searchPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [asset(77)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 10,
		} as PhotoAssetPageDTO)));
		const albums = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint');
		const roots = vi.spyOn(plexPhotoApi, 'getPhotoSourceRootsEndpoint');
		const store = usePhotoBrowsingStore();

		await subscribeSpyTo(store.activate(route({ mode: 'aggregate', libraryId: 0, view, searchQuery: 'sun' }), owner)).onComplete();

		expect(search).toHaveBeenCalledWith({ q: 'sun', page: 1, size: 100, sort: `${SortType.Title}:asc` }, expect.any(Object));
		expect(albums).not.toHaveBeenCalled();
		expect(roots).not.toHaveBeenCalled();
		expect(store.overviewCount).toBe(1);
		expect(store.overviewFolderCount).toBe(0);
		expect(store.overviewItemAt(0)).toEqual({ kind: PhotoItemKind.Asset, asset: asset(77), index: 0 });

		await subscribeSpyTo(store.selectAll(true)).onComplete();
		expect(store.selectedSelectors).toEqual([expect.objectContaining({ id: 77, kind: PhotoItemKind.Asset })]);
	});
});
