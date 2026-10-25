import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { nextTick } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { finalize, of, Subject, throwError } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateLibrarySyncJobQueue, generatePlexAccount, generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { plexPhotoApi } from '@api';
import { useAccountStore, useLibraryStore, useMediaStore, usePhotoBrowsingStore, useSignalrStore } from '@store';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import { LibrarySyncJobStatus, PhotoAssetKind, PlexMediaType, type PhotoAssetBrowseDTO } from '@dto';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import { createDefaultMediaOverviewSort } from '@composables';

const owner = {};
const route = (libraryId = 7): IPhotoBrowseActivation => ({
	mode: 'library', libraryId, view: 'assets', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(),
});
const asset = (id: number, libraryId = 7): PhotoAssetBrowseDTO => ({
	id, plexServerId: 1, plexLibraryId: libraryId, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
	assetKind: PhotoAssetKind.Image, container: 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: false,
	plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
});
const activate = async (libraryId = 7) => {
	await subscribeSpyTo(usePhotoBrowsingStore().activate(route(libraryId), owner)).onComplete();
};
const settle = async () => {
	await vi.advanceTimersByTimeAsync(75);
};

describe('PhotoBrowsingStore - background image preloading', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(async () => {
		vi.restoreAllMocks();
		vi.useFakeTimers();
		mock = getAxiosMock();
		setActivePinia(createPinia());
		mockPhotoMetadata();
		for (const id of [7, 8]) {
			useLibraryStore().updateLibrary(generatePlexLibrary({
				seed: new Seed(id), plexServerId: 1, type: PlexMediaType.PhotoAlbum,
				partialData: { id, isEnabled: true, syncedAt: '2026-01-01T00:00:00Z' },
			}));
		}
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) => of(generateResultDTO({
			items: [1, 2, 3, 4, 5].map((id) => asset(id, query?.plexLibraryId ?? 7)), page: 1, pageSize: 100, totalCount: 5, totalBytes: 50,
		})));
		vi.spyOn(useSignalrStore(), 'getRefreshNotification').mockReturnValue(of());
		await subscribeSpyTo(useMediaStore().setup()).onComplete();
		await subscribeSpyTo(usePhotoBrowsingStore().setup()).onComplete();
	});
	afterEach(() => {
		usePhotoBrowsingStore().$reset();
		useMediaStore().$reset();
		mock.restore();
		vi.useRealTimers();
		vi.restoreAllMocks();
	});

	test('Should keep two background requests active and skip queued photos that leave the viewport', async () => {
		// Arrange
		const responses = new Map([1, 2, 3, 4].map((id) => [id, new Subject<void>()]));
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockImplementation((photo) => responses.get(photo.id)!);
		await activate();
		const store = usePhotoBrowsingStore();

		// Act
		for (const id of [1, 2, 3, 4]) store.setImagePreloadCandidate(asset(id), true, owner);
		await settle();
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([1, 2]);
		store.setImagePreloadCandidate(asset(3), false, owner);
		responses.get(1)!.complete();

		// Assert
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([1, 2, 4]);
		expect(responses.get(2)!.observed).toBe(true);
		expect(responses.get(3)!.observed).toBe(false);
		expect(responses.get(4)!.observed).toBe(true);
	});

	test('Should preserve pending visible preloads when more photos become visible', async () => {
		// Arrange
		const first = new Subject<void>();
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockImplementation((photo) => photo.id === 1 ? first : of(undefined));
		await activate();
		const store = usePhotoBrowsingStore();
		store.setImagePreloadCandidate(asset(1), true, owner);
		await settle();

		// Act
		store.setImagePreloadCandidate(asset(2), true, owner);
		await settle();

		// Assert
		expect(first.observed).toBe(true);
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([1, 2]);
		store.setImagePreloadCandidate(asset(1), false, owner);
		expect(first.observed).toBe(false);
	});
	test('Should retain a visible preload when an older component releases the same image', async () => {
		// Arrange
		const pending = new Subject<void>();
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockReturnValue(pending);
		await activate();
		const store = usePhotoBrowsingStore();
		const nextOwner = {};
		const photo = asset(1);
		store.setImagePreloadCandidate(photo, true, owner);
		await settle();

		// Act
		store.setImagePreloadCandidate(photo, true, nextOwner);
		store.setImagePreloadCandidate(photo, false, owner);
		await settle();

		// Assert
		expect(pending.observed).toBe(true);
		expect(preload).toHaveBeenCalledOnce();
		store.setImagePreloadCandidate(photo, false, nextOwner);
		expect(pending.observed).toBe(false);
	});

	test('Should cancel old folder work and allow the new context to preload immediately', async () => {
		// Arrange
		const old = new Subject<void>();
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockImplementation((photo) => photo.plexLibraryId === 7 ? old : of(undefined));
		await activate();
		const store = usePhotoBrowsingStore();
		for (const id of [1, 2, 3]) store.setImagePreloadCandidate(asset(id), true, owner);
		await settle();

		// Act
		await activate(8);
		store.setImagePreloadCandidate(asset(1, 8), true, owner);
		await settle();
		old.complete();

		// Assert
		expect(old.observed).toBe(false);
		expect(preload.mock.calls.map(([photo]) => [photo.plexLibraryId, photo.id])).toEqual([[7, 1], [7, 2], [8, 1]]);
		expect(store.current).toBeNull();
		expect(store.assets.items.every((photo) => photo.plexLibraryId === 8)).toBe(true);
	});

	test('Should prioritize the current fullscreen image and its navigation direction before resuming grid preloads', async () => {
		// Arrange
		const stopped: number[] = [];
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockImplementation((photo) =>
			new Subject<void>().pipe(finalize(() => stopped.push(photo.id))));
		const foreground = vi.spyOn(useMediaStore(), 'getPhotoImageUrl').mockImplementation((photo) => of(`original-${photo.id}`));
		await activate();
		const store = usePhotoBrowsingStore();
		for (const id of [4, 5, 1]) store.setImagePreloadCandidate(asset(id), true, owner);
		await settle();

		// Act
		await subscribeSpyTo(store.openAsset(asset(2), 1)).onComplete();
		await Promise.resolve();

		// Assert
		expect(foreground.mock.calls.map(([photo]) => photo.id)).toEqual([2, 3, 1]);
		expect(store.current?.id).toBe(2);
		expect(store.slides.find((slide) => slide.asset.id === 2)?.url).toBe('original-2');
		expect(stopped).toEqual([4, 5]);
		await settle();
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([4, 5]);

		// Act
		await subscribeSpyTo(store.showAsset(0)).onComplete();
		await Promise.resolve();
		expect(store.slides.map((slide) => slide.asset.id)).toEqual([1, 5, 2]);
		store.closeAssetDialog();
		await settle();

		// Assert
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([4, 5, 4, 5]);
	});

	test('Should keep background failures silent and continue preloading the remaining visible images', async () => {
		// Arrange
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockImplementation((photo) =>
			photo.id === 1 ? throwError(() => new Error('Unavailable')) : of(undefined));
		await activate();
		const store = usePhotoBrowsingStore();

		// Act
		store.setImagePreloadCandidate(asset(1), true, owner);
		store.setImagePreloadCandidate(asset(2), true, owner);
		await settle();

		// Assert
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([1, 2]);
		expect(store.error).toBe(false);
		expect(store.dialog.open).toBe(false);
		expect(store.assets.items.map((photo) => photo.id)).toEqual([1, 2, 3, 4, 5]);
	});

	test.each(['publication', 'forced reload'] as const)('Should replace preloaded image bytes after a %s', async (refresh) => {
		// Arrange
		let version = 1;
		mock.onGet('/api/PlexPhoto/libraries/7/assets/1/image').reply(() => [200, new Blob([`image-${version}`], { type: 'image/jpeg' })]);
		vi.spyOn(URL, 'createObjectURL').mockImplementation(() => `blob:image-${version}`);
		await activate();
		const store = usePhotoBrowsingStore();
		store.setImagePreloadCandidate(asset(1), true, owner);
		await settle();
		store.setImagePreloadCandidate(asset(1), false, owner);

		// Act
		version = 2;
		if (refresh === 'publication') {
			store.deactivate(owner);
			useLibraryStore().updateSyncQueue(generateLibrarySyncJobQueue({
				plexLibraryId: 7, plexServerId: 1, status: LibrarySyncJobStatus.Completed, completedAt: '2026-01-02T00:00:00Z',
			}));
			await nextTick();
			await activate();
		} else await subscribeSpyTo(store.loadCurrent(true)).onComplete();
		const response = subscribeSpyTo(useMediaStore().getPhotoImageUrl(asset(1), 'original'));
		await response.onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url?.endsWith('/image'))).toHaveLength(2);
		expect(response.getLastValue()).toBe('blob:image-2');
		useMediaStore().releasePhotoImageUrl(response.getLastValue()!);
	});

	test('Should revoke preloaded bytes and reject new background work after account access is lost while inactive', async () => {
		// Arrange
		const account = generatePlexAccount({ id: 1, config: { seed: 1209 }, partialData: { plexServerAccess: [1], plexLibraryAccess: [7] } });
		useAccountStore().accounts = [account];
		await nextTick();
		mock.onGet('/api/PlexPhoto/libraries/7/assets/1/image').reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:private-photo');
		const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
		await activate();
		const store = usePhotoBrowsingStore();
		store.setImagePreloadCandidate(asset(1), true, owner);
		await settle();
		store.deactivate(owner);

		// Act
		useAccountStore().accounts = [{ ...account, plexLibraryAccess: [] }];
		await nextTick();
		await activate();
		store.setImagePreloadCandidate(asset(1), true, owner);
		await settle();

		// Assert
		expect(revoke).toHaveBeenCalledWith('blob:private-photo');
		expect(mock.history.get.filter((request) => request.url?.endsWith('/image'))).toHaveLength(1);
		expect(store.disabledLibrary).toBe(true);
	});

	test('Should drop active and queued preloads on reset without disabling the next store session', async () => {
		// Arrange
		const pending = new Subject<void>();
		const preload = vi.spyOn(useMediaStore(), 'preloadPhotoImage').mockReturnValue(pending);
		await activate();
		const store = usePhotoBrowsingStore();
		for (const id of [1, 2, 3]) store.setImagePreloadCandidate(asset(id), true, owner);
		await settle();

		// Act
		store.$reset();
		pending.complete();
		await subscribeSpyTo(store.setup()).onComplete();
		await activate();
		preload.mockReturnValue(of(undefined));
		store.setImagePreloadCandidate(asset(5), true, owner);
		await settle();

		// Assert
		expect(pending.observed).toBe(false);
		expect(preload.mock.calls.map(([photo]) => photo.id)).toEqual([1, 2, 5]);
	});
});
