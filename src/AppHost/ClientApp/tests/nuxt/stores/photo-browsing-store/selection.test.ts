import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO, generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoAssetKind, PhotoItemKind, PlexMediaType, SortType, type PhotoAlbumBrowseDTO, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PhotoFolderPageDTO } from '@dto';
import { useLibraryStore, usePhotoBrowsingStore } from '@store';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import { createDefaultMediaOverviewSort } from '@composables';

describe('PhotoBrowsingStore - complete-result selection', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		mockPhotoMetadata();
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(7),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 7, title: 'Photos', isEnabled: true },
		}));
	});

	const route = (partial: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
		mode: 'library', libraryId: 7, view: 'assets', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(), ...partial,
	});

	const asset = (id: number, libraryId = 7): PhotoAssetBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: libraryId, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
		assetKind: PhotoAssetKind.Image, container: 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: false,
		plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
	});

	const album = (id: number): PhotoAlbumBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: 99, plexServerName: 'Other', plexLibraryTitle: 'Other', title: `Album ${id}`,
		assetCount: 1, assetBytes: 1, hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
	});

	test('enumerates every bounded page while leaving the visible sparse cache bounded', async () => {
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => {
			const page = query?.page ?? 1;
			const items = page === 1 ? [asset(1), asset(2)] : page === 3 ? [asset(201)] : [];
			return of(generateResultDTO({ items, page, pageSize: 100, totalCount: 205, totalBytes: 205 } as PhotoAssetPageDTO));
		});
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.selectAll(true)).onComplete();

		expect(request).toHaveBeenCalledWith({ page: 2, size: 100, sort: `${SortType.Title}:asc`, plexLibraryId: 7 }, expect.any(Object));
		expect(request).toHaveBeenCalledWith({ page: 3, size: 100, sort: `${SortType.Title}:asc`, plexLibraryId: 7 }, expect.any(Object));
		expect(store.selectedSelectors.map((selector) => selector.id)).toEqual([1, 2, 201]);
		expect(store.allVisibleSelected).toBe(true);
		expect(store.overviewItemAt(200)).toBeNull();
	});

	test('does not partially mutate selection when a later enumeration page fails', async () => {
		const existing = asset(900);
		const store = usePhotoBrowsingStore();
		store.toggleAsset(existing, true);
		const firstPage = generateResultDTO({ items: [asset(1)], page: 1, pageSize: 100, totalCount: 101, totalBytes: 101 } as PhotoAssetPageDTO);
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint')
			.mockReturnValueOnce(of(firstPage))
			.mockReturnValueOnce(of(firstPage))
			.mockReturnValueOnce(of(generateFailedResultDTO({ statusCode: 503 })));
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		store.allVisibleSelected = true;
		await subscribeSpyTo(store.selectAll(true)).onComplete();

		expect(store.selectedSelectors).toEqual([expect.objectContaining({ id: 900, kind: PhotoItemKind.Asset })]);
		expect(store.selectionError).toBe(true);
		expect(store.allVisibleSelected).toBe(false);
	});

	test('root select all includes configured folders and unmapped assets', async () => {
		const roots = vi.spyOn(plexPhotoApi, 'getPhotoSourceRootsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [{ id: 10, plexServerId: 1, plexLibraryId: 7, title: 'Root', relativePath: 'Root', parentId: null, isRoot: true, recursiveAssetCount: 2, recursiveAssetBytes: 2 }],
			page: 1, pageSize: 100, totalCount: 1,
		} as PhotoFolderPageDTO)));
		const assets = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [asset(20)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1,
		} as PhotoAssetPageDTO)));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'folders' }), owner)).onComplete();
		await subscribeSpyTo(store.selectAll(true)).onComplete();

		expect(roots).toHaveBeenCalled();
		expect(assets).toHaveBeenCalledWith(expect.objectContaining({ unmappedOnly: true }), expect.any(Object));
		expect(store.selectedSelectors).toEqual(expect.arrayContaining([
			expect.objectContaining({ id: 10, kind: PhotoItemKind.Folder }),
			expect.objectContaining({ id: 20, kind: PhotoItemKind.Asset }),
		]));
	});

	test('deselects the complete active listing without removing unrelated selectors', async () => {
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [asset(1)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1,
		} as PhotoAssetPageDTO)));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		store.toggleAlbum(album(42), true);
		await subscribeSpyTo(store.selectAll(true)).onComplete();
		await subscribeSpyTo(store.selectAll(false)).onComplete();

		expect(store.selectedSelectors).toEqual([expect.objectContaining({ id: 42, kind: PhotoItemKind.Album })]);
		expect(store.allVisibleSelected).toBe(false);
	});
});
