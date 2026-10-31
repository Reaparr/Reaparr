import { nextTick } from 'vue';
import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject, of, throwError } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateLibrarySyncJobQueue, generateResultDTO } from '@mock';
import { plexPhotoApi } from '@api';
import { LibrarySyncJobStatus, PhotoAssetKind, PhotoItemKind, PlexMediaType, RefreshDataType, type PhotoAlbumBrowseDTO, type PhotoAlbumPageDTO, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PlexLibraryDTO } from '@dto';
import { useAccountStore, useLibraryStore, useMediaStore, usePhotoBrowsingStore, useSignalrStore } from '@store';
import type { ResultDTO } from '@interfaces';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import { createDefaultMediaOverviewSort } from '@composables';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';

describe('PhotoBrowsingStore - refresh and eligibility lifecycle', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		vi.useFakeTimers();
		mockPhotoMetadata();
	});
	afterEach(() => vi.useRealTimers());

	const library = { id: 7, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO;
	const route = (partial: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
		mode: 'library', libraryId: 7, view: 'albums', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(), ...partial,
	});
	const album = (id: number): PhotoAlbumBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: 7, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Album ${id}`,
		assetCount: 1, assetBytes: 1, hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
	});
	const page = (items: PhotoAlbumBrowseDTO[] = [album(1)], totalCount = items.length): PhotoAlbumPageDTO => ({
		items, page: 1, pageSize: 100, totalCount, totalAssetBytes: totalCount,
	});
	const asset = (id: number): PhotoAssetBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: 7, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
		assetKind: PhotoAssetKind.Image, container: 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: false,
		plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
	});
	const assetPage = (items: PhotoAssetBrowseDTO[], pageNumber: number, totalCount: number): PhotoAssetPageDTO => ({
		items, page: pageNumber, pageSize: 100, totalCount, totalBytes: totalCount,
	});

	async function openAlbumView(response = page()) {
		useLibraryStore().updateLibrary(library);
		const notifications = new Subject<RefreshDataType>();
		vi.spyOn(useSignalrStore(), 'getRefreshNotification').mockReturnValue(notifications.asObservable());
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockReturnValue(of(generateResultDTO(response)));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		request.mockClear();
		return { notifications, request };
	}

	test('refreshes once for a newly completed publication and ignores duplicate notifications', async () => {
		const { notifications, request } = await openAlbumView(page([]));
		const store = usePhotoBrowsingStore();
		expect(store.overviewCount).toBe(0);
		request.mockReturnValue(of(generateResultDTO(page())));
		useLibraryStore().syncQueues = [generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-10-20T00:00:00Z' })];
		notifications.next(RefreshDataType.PlexLibrary);
		notifications.next(RefreshDataType.PlexLibrary);
		await vi.advanceTimersByTimeAsync(250);
		expect(request).toHaveBeenCalledOnce();
		expect(store.albums.items).toEqual([album(1)]);
		expect(store.overviewCount).toBe(1);
		notifications.next(RefreshDataType.PlexLibrary);
		await vi.advanceTimersByTimeAsync(250);
		expect(request).toHaveBeenCalledOnce();
	});

	test('does not refresh while a library job is processing', async () => {
		const { notifications, request } = await openAlbumView(page([]));
		useLibraryStore().syncQueues = [generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Processing })];
		notifications.next(RefreshDataType.PlexLibrary);
		await vi.advanceTimersByTimeAsync(250);
		expect(request).not.toHaveBeenCalled();
	});

	test('keeps the last successful range when a refresh request fails', async () => {
		useLibraryStore().updateLibrary(library);
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint')
			.mockReturnValueOnce(of(generateResultDTO(page())))
			.mockReturnValue(throwError(() => new Error('offline')));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.loadCurrent(true)).onComplete();

		expect(request).toHaveBeenCalledTimes(2);
		expect(store.albums.items).toEqual([album(1)]);
		expect(store.reloadError).toBe(true);
	});

	test('retains later-page selection identities while a forced viewer refresh evicts distant rows', async () => {
		useLibraryStore().updateLibrary(library);
		const pageOne = Array.from({ length: 100 }, (_, index) => asset(index + 1));
		const removed = asset(101);
		const current = asset(102);
		const fresh = asset(103);
		let refreshing = false;
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => {
			const pageNumber = query?.page ?? 1;
			const items = pageNumber === 1
				? pageOne
				: refreshing ? [current, fresh] : [removed, current];
			return of(generateResultDTO(assetPage(items, pageNumber, 102)));
		});
		const media = useMediaStore();
		vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((photo) => of(`image-${photo.id}`));
		vi.spyOn(media, 'releasePhotoImageUrl');
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route({ view: 'assets' }), owner)).onComplete();
		await subscribeSpyTo(store.requestRange(100, 102)).onComplete();
		store.toggleAsset(removed, true);
		store.toggleAsset(current, true);
		await subscribeSpyTo(store.openAsset(current, 101)).onComplete();
		const initialPageTwoReads = request.mock.calls.filter(([query]) => query?.page === 2).length;

		refreshing = true;
		await subscribeSpyTo(store.loadCurrent(true)).onComplete();

		expect(request.mock.calls.filter(([query]) => query?.page === 2).length).toBeGreaterThan(initialPageTwoReads);
		expect(store.selectionStale).toBe(true);
		expect(store.selectedSelectors).toEqual([
			{
				id: removed.id,
				plexServerId: removed.plexServerId,
				plexLibraryId: removed.plexLibraryId,
				kind: PhotoItemKind.Asset,
				type: PlexMediaType.PhotoImage,
				title: removed.title,
				stale: true,
			},
			{
				id: current.id,
				plexServerId: current.plexServerId,
				plexLibraryId: current.plexLibraryId,
				kind: PhotoItemKind.Asset,
				type: PlexMediaType.PhotoImage,
				title: current.title,
				stale: false,
			},
		]);
		expect(store.dialog.open).toBe(true);
		expect(store.current?.id).toBe(current.id);
		expect(store.assets.items).toEqual([...pageOne, current, fresh]);
		expect(store.assets.items).not.toContainEqual(removed);
	});

	test('purges catalog and selections when the active library becomes inaccessible', async () => {
		useLibraryStore().updateLibrary(library);
		const visible = { id: 4, plexServerId: 1, plexLibraryId: 7, title: 'Visible' } as PhotoAssetBrowseDTO;
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({ items: [visible], page: 1, pageSize: 100, totalCount: 1, totalBytes: 1 })));
		vi.spyOn(useSignalrStore(), 'getRefreshNotification').mockReturnValue(of());
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.activate(route({ view: 'assets' }), owner)).onComplete();
		store.toggleAsset(visible, true);
		useAccountStore().accounts = [{ id: 1, isEnabled: true, plexServerAccess: [], plexLibraryAccess: [] } as never];
		await nextTick();

		expect(store.assets.items).toEqual([]);
		expect(store.selectedCount).toBe(0);
		expect(store.disabledLibrary).toBe(true);
	});

	test('reloads when eligibility returns after a disabled-library transition', async () => {
		useLibraryStore().updateLibrary(library);
		const response = new Subject<ResultDTO<PhotoAlbumPageDTO>>();
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint')
			.mockReturnValueOnce(of(generateResultDTO(page([]))))
			.mockReturnValue(response.asObservable());
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		useLibraryStore().updateLibrary({ ...library, isEnabled: false });
		await nextTick();
		expect(store.disabledLibrary).toBe(true);
		useLibraryStore().updateLibrary(library);
		await nextTick();
		expect(request).toHaveBeenCalledTimes(2);
		response.next(generateResultDTO(page([])));
		response.complete();
	});
});
