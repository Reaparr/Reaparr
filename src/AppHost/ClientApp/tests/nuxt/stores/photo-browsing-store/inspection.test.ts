import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of, Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO, generateLibrarySyncJobQueue, generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { plexPhotoApi } from '@api';
import { LibrarySyncJobStatus, PhotoAssetKind, PlexMediaType, type RefreshDataType, type PhotoAlbumSummaryDTO, type PhotoAssetBrowseDTO, type PhotoAssetInspectionDTO } from '@dto';
import type { ResultDTO } from '@interfaces';
import { useLibraryStore, useMediaStore, usePhotoBrowsingStore, useSignalrStore } from '@store';
import { createDefaultMediaOverviewSort } from '@composables';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';

const owner = {};
const asset = (id: number): PhotoAssetBrowseDTO => ({
	id, plexServerId: 1, plexLibraryId: 7, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Photo ${id}`,
	assetKind: PhotoAssetKind.Image, hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
	originalFilename: `${id}.jpg`, size: 10, container: 'jpg', width: 6000, height: 4000,
	duration: null, relativeFolderPath: null, sourceRootTitle: null,
});
const album = (id: number, title = `Album ${id}`): PhotoAlbumSummaryDTO => ({
	id, plexServerId: 1, plexLibraryId: 7, plexServerName: 'Server', plexLibraryTitle: 'Photos', title,
	hasThumbnail: false, plexApiRatingKey: id, plexApiMetaDataKey: id,
});
const inspection = (id: number, page: number, items: PhotoAlbumSummaryDTO[], totalCount: number): PhotoAssetInspectionDTO => ({
	...asset(id), addedAt: '2026-01-01T00:00:00Z', updatedAt: null, originallyAvailableAt: null,
	summary: `Details ${id} page ${page}`, videoCodec: null, audioCodec: null, breadcrumbs: [],
	albums: { items, page, pageSize: 100, totalCount },
});
const firstAlbums = Array.from({ length: 100 }, (_, index) => album(index + 1));

describe('PhotoBrowsingStore - inspector membership publication', () => {
	let { mock } = baseVars();
	let notifications: Subject<RefreshDataType>;
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		mockPhotoMetadata();
		vi.useFakeTimers();
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(7), plexServerId: 1, type: PlexMediaType.PhotoAlbum,
			partialData: { id: 7, title: 'Photos', isEnabled: true },
		}));
		mock.onGet(/\/api\/PlexPhoto\/assets(?:\?|$)/).reply(200, generateResultDTO({
			items: [asset(1), asset(2)], page: 1, pageSize: 100, totalCount: 2, totalBytes: 20,
		}));
		notifications = new Subject<RefreshDataType>();
		vi.spyOn(useSignalrStore(), 'getRefreshNotification').mockReturnValue(notifications.asObservable());
		vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockReturnValue(of(undefined));
		vi.spyOn(useMediaStore(), 'getPhotoImageUrl').mockImplementation((item) => of(`blob:photo-${item.id}`));
	});
	afterEach(() => {
		usePhotoBrowsingStore().$reset();
		vi.useRealTimers();
		vi.restoreAllMocks();
	});

	async function openFirstInspection() {
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.activate({ mode: 'library', libraryId: 7, view: 'assets', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort() }, owner)).onComplete();
		await subscribeSpyTo(store.openAsset(asset(1), 0)).onComplete();
		await subscribeSpyTo(store.openDetails()).onComplete();
		return store;
	}

	test('Should retain earlier memberships in the rendered inspector model after loading another page', async () => {
		// Arrange
		vi.spyOn(plexPhotoApi, 'getPhotoAssetInspectionEndpoint').mockImplementation((_libraryId, id, query) => {
			const page = query?.membershipPage ?? 1;
			return of(generateResultDTO(inspection(id, page, page === 1 ? firstAlbums : [album(101), album(102)], 102)));
		});
		const store = await openFirstInspection();
		expect(store.dialog.asset?.albums.items).toEqual(firstAlbums);

		// Act
		await subscribeSpyTo(store.requestInspectionMembershipPage(2)).onComplete();

		// Assert
		expect(store.dialog.asset?.albums.items).toEqual([...firstAlbums, album(101), album(102)]);
		expect(store.dialog.asset).toMatchObject({ id: 1, summary: 'Details 1 page 2', albums: { page: 2, pageSize: 100, totalCount: 102 } });
		expect(store.dialog.loading).toBe(false);
	});

	test('Should restart membership paging and replace removed or renamed rows after library publication', async () => {
		// Arrange
		let published = false;
		const refreshed = [album(1, 'Renamed album'), album(103, 'New album')];
		const publishedPage = new Subject<ResultDTO<PhotoAssetInspectionDTO>>();
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetInspectionEndpoint').mockImplementation((_libraryId, id, query) => {
			const page = query?.membershipPage ?? 1;
			if (published) return publishedPage.asObservable();
			return of(generateResultDTO(inspection(id, page, page === 1 ? firstAlbums : [album(101), album(102)], 102)));
		});
		const store = await openFirstInspection();
		await subscribeSpyTo(store.requestInspectionMembershipPage(2)).onComplete();

		// Act
		published = true;
		useLibraryStore().syncQueues = [generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-10-22T00:00:00Z' })];
		await vi.advanceTimersByTimeAsync(250);
		expect(store.dialog.loading).toBe(true);
		publishedPage.next(generateResultDTO(inspection(1, 1, refreshed, 2)));
		publishedPage.complete();

		// Assert
		expect(request).toHaveBeenLastCalledWith(7, 1, { membershipPage: 1, membershipSize: 100 }, expect.any(Object));
		expect(store.dialog.asset?.albums).toEqual({ items: refreshed, page: 1, pageSize: 100, totalCount: 2 });
		expect(store.dialog.error).toBe(false);
		expect(store.dialog.loading).toBe(false);
	});

	test('Should replace old membership pages when retrying a failed later page', async () => {
		// Arrange
		let retrying = false;
		const refreshed = [album(1, 'Renamed album')];
		vi.spyOn(plexPhotoApi, 'getPhotoAssetInspectionEndpoint').mockImplementation((_libraryId, id, query) => {
			const page = query?.membershipPage ?? 1;
			if (retrying) return of(generateResultDTO(inspection(id, page, refreshed, 1)));
			if (page === 3) return of(generateFailedResultDTO({ statusCode: 503 }));
			const items = page === 1 ? firstAlbums : Array.from({ length: 100 }, (_, index) => album(index + 101));
			return of(generateResultDTO(inspection(id, page, items, 201)));
		});
		const store = await openFirstInspection();
		await subscribeSpyTo(store.requestInspectionMembershipPage(2)).onComplete();
		await subscribeSpyTo(store.requestInspectionMembershipPage(3)).onComplete();
		expect(store.dialog.error).toBe(true);

		// Act
		retrying = true;
		await subscribeSpyTo(store.retryInspection()).onComplete();

		// Assert
		expect(store.dialog.asset?.albums).toEqual({ items: refreshed, page: 1, pageSize: 100, totalCount: 1 });
		expect(store.dialog.error).toBe(false);
		expect(store.dialog.loading).toBe(false);
	});

	test('Should ignore a late membership page after switching to another asset', async () => {
		// Arrange
		const oldPage = new Subject<ResultDTO<PhotoAssetInspectionDTO>>();
		vi.spyOn(plexPhotoApi, 'getPhotoAssetInspectionEndpoint').mockImplementation((_libraryId, id, query) => {
			if (id === 1 && query?.membershipPage === 2) return oldPage.asObservable();
			return of(generateResultDTO(inspection(id, 1, id === 1 ? firstAlbums : [album(201)], id === 1 ? 101 : 1)));
		});
		const store = await openFirstInspection();
		const pending = subscribeSpyTo(store.requestInspectionMembershipPage(2));

		// Act
		await subscribeSpyTo(store.showAsset(1)).onComplete();
		await subscribeSpyTo(store.openDetails()).onComplete();
		oldPage.next(generateResultDTO(inspection(1, 2, [album(101)], 101)));
		oldPage.complete();
		await pending.onComplete();

		// Assert
		expect(store.current?.id).toBe(2);
		expect(store.dialog.asset).toMatchObject({ id: 2, albums: { items: [album(201)], page: 1, totalCount: 1 } });
		expect(store.dialog.loading).toBe(false);
	});

	test('Should not start queued inspection work while deactivating a pending publication refresh', async () => {
		// Arrange
		let published = false;
		const pendingPage = new Subject<ResultDTO<PhotoAssetInspectionDTO>>();
		const request = vi.spyOn(plexPhotoApi, 'getPhotoAssetInspectionEndpoint').mockImplementation((_libraryId, id) =>
			published ? pendingPage.asObservable() : of(generateResultDTO(inspection(id, 1, firstAlbums, 101))),
		);
		const store = await openFirstInspection();
		published = true;
		useLibraryStore().syncQueues = [generateLibrarySyncJobQueue({ plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-10-22T00:00:00Z' })];
		await vi.advanceTimersByTimeAsync(250);
		expect(store.dialog.loading).toBe(true);
		useLibraryStore().syncQueues = useLibraryStore().syncQueues.map((queue) => ({ ...queue, completedAt: '2026-10-22T00:00:01Z' }));
		await vi.advanceTimersByTimeAsync(250);
		expect(request).toHaveBeenCalledTimes(2);

		// Act
		store.deactivate(owner);
		pendingPage.next(generateResultDTO(inspection(1, 1, [album(103)], 1)));
		pendingPage.complete();
		await vi.advanceTimersByTimeAsync(0);

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(store.active).toBe(false);
		expect(store.dialog.open).toBe(false);
		expect(store.dialog.loading).toBe(false);
	});
});
