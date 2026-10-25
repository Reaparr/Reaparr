import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoAssetKind, PhotoItemKind, PlexMediaType, SortType, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PlexLibraryDTO } from '@dto';
import type { ResultDTO } from '@interfaces';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import { createDefaultMediaOverviewSort } from '@composables';

describe('PhotoBrowsingStore - range and context cancellation', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
	});

	const route = (partial: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
		mode: 'library', libraryId: 7, view: 'assets', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(), ...partial,
	});

	const asset = (id: number, libraryId = 7): PhotoAssetBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: libraryId, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
		assetKind: PhotoAssetKind.Image, container: 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: false,
		plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
	});

	test('ignores a late range response after the active context changes', async () => {
		const oldResponse = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		const newResponse = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => query?.plexLibraryId === 7 ? oldResponse.asObservable() : newResponse.asObservable());
		useLibraryStore().updateLibrary({ id: 7, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO);
		useLibraryStore().updateLibrary({ id: 8, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO);
		const store = usePhotoBrowsingStore();
		const first = subscribeSpyTo(store.activate(route(), owner));
		const second = subscribeSpyTo(store.activate(route({ libraryId: 8 }), owner));
		oldResponse.next(generateResultDTO({ items: [asset(1)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1 } as PhotoAssetPageDTO));
		oldResponse.complete();
		newResponse.next(generateResultDTO({ items: [asset(2, 8)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1 } as PhotoAssetPageDTO));
		newResponse.complete();
		await first.onComplete();
		await second.onComplete();

		expect(request).toHaveBeenCalledTimes(2);
		expect(store.assets.items.map((item) => item.id)).toEqual([2]);
		expect(store.libraryId).toBe(8);
	});

	test('does not reuse search ranges after the query context changes', async () => {
		const oldResponse = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		const newResponse = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		const request = vi.spyOn(plexPhotoApi, 'searchPhotoAssetsEndpoint').mockImplementation((query) => query?.q === 'old' ? oldResponse.asObservable() : newResponse.asObservable());
		const store = usePhotoBrowsingStore();
		const first = subscribeSpyTo(store.activate(route({ mode: 'aggregate', libraryId: 0, searchQuery: 'old' }), owner));
		const second = subscribeSpyTo(store.activate(route({ mode: 'aggregate', libraryId: 0, searchQuery: 'new' }), owner));
		oldResponse.next(generateResultDTO({ items: [asset(1)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1 } as PhotoAssetPageDTO));
		oldResponse.complete();
		newResponse.next(generateResultDTO({ items: [asset(2)], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1 } as PhotoAssetPageDTO));
		newResponse.complete();
		await first.onComplete();
		await second.onComplete();

		expect(request).toHaveBeenNthCalledWith(1, { q: 'old', page: 1, size: 100, sort: `${SortType.Title}:asc` }, expect.any(Object));
		expect(request).toHaveBeenNthCalledWith(2, { q: 'new', page: 1, size: 100, sort: `${SortType.Title}:asc` }, expect.any(Object));
		expect(store.search.items.map((item) => item.id)).toEqual([2]);
	});

	test('keeps selectors distinct by server, library, kind and id', () => {
		const store = usePhotoBrowsingStore();
		const first = asset(10);
		store.toggleAsset(first, true);
		store.toggleAsset({ ...first, plexServerId: 2, title: 'Other server' }, true);
		store.toggleFolder({ id: 10, plexServerId: 1, plexLibraryId: 7, title: 'Folder' } as never, true);
		store.toggleAsset(first, false);

		expect(store.selectedCount).toBe(2);
		expect(store.selectedSelectors).toEqual(expect.arrayContaining([
			expect.objectContaining({ id: 10, plexServerId: 2, kind: PhotoItemKind.Asset }),
			expect.objectContaining({ id: 10, plexServerId: 1, kind: PhotoItemKind.Folder }),
		]));
	});
});
