import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of, Subject, throwError } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoAssetKind, PlexMediaType, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PlexLibraryDTO } from '@dto';
import { useLibraryStore, useMediaStore, usePhotoBrowsingStore } from '@store';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import type { ResultDTO } from '@interfaces';
import { createDefaultMediaOverviewSort } from '@composables';

describe('PhotoBrowsingStore - bounded viewer sequence', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		mockPhotoMetadata();
	});

	const route = (partial: Partial<IPhotoBrowseActivation> = {}): IPhotoBrowseActivation => ({
		mode: 'library', libraryId: 7, view: 'assets', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort(), ...partial,
	});
	const asset = (id: number, assetKind = PhotoAssetKind.Image): PhotoAssetBrowseDTO => ({
		id, plexServerId: 1, plexLibraryId: 7, plexServerName: 'Server', plexLibraryTitle: 'Photos', title: `Asset ${id}`,
		assetKind, container: assetKind === PhotoAssetKind.VideoClip ? 'mp4' : 'jpg', originalFilename: `${id}.jpg`, hasThumbnail: true,
		plexApiRatingKey: id, plexApiMetaDataKey: id, size: 10,
	});

	function setupAssets(items: PhotoAssetBrowseDTO[], totalCount = items.length) {
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({ items, page: 1, pageSize: 100, totalCount, totalBytes: totalCount } as PhotoAssetPageDTO)));
		useLibraryStore().updateLibrary({ id: 7, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO);
		const media = useMediaStore();
		vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((photo) => of(`image-${photo.id}`));
		vi.spyOn(media, 'getMediaThumbnailUrl').mockReturnValue(of('thumb'));
		vi.spyOn(media, 'releasePhotoImageUrl');
	}

	test('wraps the complete sequence and keeps folder prefixes out of asset indexes', async () => {
		const items = [asset(1), asset(2), asset(3)];
		setupAssets(items);
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(items[1]!, 1)).onComplete();
		expect(store.current?.id).toBe(2);
		expect(store.index).toBe(1);
		expect(store.count).toBe(3);
		await subscribeSpyTo(store.showAsset(3)).onComplete();
		expect(store.current?.id).toBe(1);
		await subscribeSpyTo(store.showAsset(-1)).onComplete();
		expect(store.current?.id).toBe(3);
	});

	test('restores the last viewed overview focus and clears it when acknowledged', async () => {
		const items = [asset(1), asset(2)];
		setupAssets(items);
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(items[1]!, 1)).onComplete();
		store.closeAssetDialog();
		expect(store.focusIndex).toBe(1);
		expect(store.scrollIndex).toBe(1);
		store.acknowledgeFocus();
		expect(store.focusIndex).toBeNull();
	});

	test('keeps at most three unique slides and releases image thumbnail URLs outside the window', async () => {
		const items = Array.from({ length: 10 }, (_, index) => asset(index + 1));
		setupAssets(items, 10);
		const media = useMediaStore();
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(items[4]!, 4)).onComplete();
		await subscribeSpyTo(store.showAsset(7)).onComplete();

		expect(store.slides.length).toBeLessThanOrEqual(3);
		expect(new Set(store.slides.map((slide) => `${slide.asset.plexLibraryId}:${slide.asset.id}`)).size).toBe(store.slides.length);
		expect(media.releasePhotoImageUrl).toHaveBeenCalled();
	});

	test('uses leased thumbnails for images and unleased thumbnails for clips', async () => {
		const still = asset(1);
		const clip = asset(2, PhotoAssetKind.VideoClip);
		setupAssets([still, clip]);
		const media = useMediaStore();
		const imageRequest = vi.spyOn(media, 'getPhotoImageUrl').mockReturnValue(of('image-url'));
		const thumbnailRequest = vi.spyOn(media, 'getMediaThumbnailUrl').mockReturnValue(of('thumb-url'));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(still, 0)).onComplete();

		expect(store.slides.find((slide) => slide.asset.id === 1)).toEqual(expect.objectContaining({ url: 'image-url', error: false }));
		expect(imageRequest).toHaveBeenCalledTimes(1);
		expect(imageRequest).toHaveBeenCalledWith(still);

		await subscribeSpyTo(store.showAsset(1)).onComplete();
		expect(store.slides.find((slide) => slide.asset.id === 2)).toEqual(expect.objectContaining({ url: 'thumb-url', error: false }));
		expect(imageRequest).toHaveBeenCalledTimes(1);
		expect(thumbnailRequest).toHaveBeenCalled();

		store.closeAssetDialog();
		expect(media.releasePhotoImageUrl).toHaveBeenCalledWith('image-url');
		expect(media.releasePhotoImageUrl).not.toHaveBeenCalledWith('thumb-url');
	});

	test.each([PhotoAssetKind.Image, PhotoAssetKind.VideoClip])('shows a display error when a %s preview request fails', async (kind) => {
		// Arrange
		const item = asset(1, kind);
		setupAssets([item]);
		const media = useMediaStore();
		const failure = throwError(() => new Error('Preview unavailable'));
		vi.spyOn(media, 'getPhotoImageUrl').mockReturnValue(failure);
		vi.spyOn(media, 'getMediaThumbnailUrl').mockReturnValue(failure);
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();

		// Act
		await subscribeSpyTo(store.openAsset(item, 0)).onComplete();

		// Assert
		expect(store.slides[0]).toEqual(expect.objectContaining({ asset: item, url: '', loading: false, error: true }));
		expect(store.current?.id).toBe(item.id);
		expect(store.dialog.open).toBe(true);
		expect(store.navigating).toBe(false);
	});

	test('keeps decoded-image failures terminal without fallback loops and retries the thumbnail', async () => {
		const item = asset(1);
		setupAssets([item]);
		const media = useMediaStore();
		const imageRequest = vi.spyOn(media, 'getPhotoImageUrl').mockReturnValue(of('image-url'));
		const thumbnailRequest = vi.spyOn(media, 'getMediaThumbnailUrl').mockReturnValue(of('thumb-url'));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(item, 0)).onComplete();

		expect(imageRequest).toHaveBeenCalledTimes(1);
		await subscribeSpyTo(store.imageFailed(0)).onComplete();
		expect(store.slides[0]).toEqual(expect.objectContaining({ url: '', loading: false, error: true }));
		expect(imageRequest).toHaveBeenCalledTimes(1);
		expect(thumbnailRequest).not.toHaveBeenCalled();

		await subscribeSpyTo(store.imageFailed(0)).onComplete();
		expect(store.slides[0]).toEqual(expect.objectContaining({ url: '', loading: false, error: true }));
		expect(imageRequest).toHaveBeenCalledTimes(1);

		await subscribeSpyTo(store.retryImage()).onComplete();
		expect(store.slides[0]).toEqual(expect.objectContaining({ url: 'image-url', loading: false, error: false }));
		expect(imageRequest).toHaveBeenCalledTimes(2);
	});

	test('cancels unfinished previews before loading the resized image', async () => {
		// Arrange
		const item = asset(1);
		setupAssets([item]);
		const pending = new Subject<string>();
		const media = useMediaStore();
		const request = vi.spyOn(media, 'getPhotoImageUrl').mockReturnValueOnce(pending).mockReturnValue(of('resized-image'));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		const opening = subscribeSpyTo(store.openAsset(item, 0));
		expect(pending.observed).toBe(true);

		// Act
		await subscribeSpyTo(store.resizeImages()).onComplete();
		await opening.onComplete();
		pending.next('obsolete-image');

		// Assert
		expect(pending.observed).toBe(false);
		expect(request).toHaveBeenCalledTimes(2);
		expect(store.slides[0]).toEqual(expect.objectContaining({ url: 'resized-image', loading: false, error: false }));
		expect(store.current?.id).toBe(item.id);
		expect(store.navigating).toBe(false);
	});

	test('reloads the visible preview window without losing the current photo when the viewport changes', async () => {
		// Arrange
		const items = [asset(1), asset(2), asset(3)];
		setupAssets(items);
		const media = useMediaStore();
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(items[1]!, 1)).onComplete();
		const previousUrls = store.slides.map((slide) => slide.url);
		const request = vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((photo) => of(`resized-${photo.id}`));
		request.mockClear();
		vi.mocked(media.releasePhotoImageUrl).mockClear();

		// Act
		await subscribeSpyTo(store.resizeImages()).onComplete();

		// Assert
		expect(store.current?.id).toBe(2);
		expect(store.index).toBe(1);
		expect(store.slides.map((slide) => ({ id: slide.asset.id, url: slide.url, loading: slide.loading, error: slide.error })).sort((left, right) => left.id - right.id))
			.toEqual([
				{ id: 1, url: 'resized-1', loading: false, error: false },
				{ id: 2, url: 'resized-2', loading: false, error: false },
				{ id: 3, url: 'resized-3', loading: false, error: false },
			]);
		expect(request.mock.calls.map(([photo]) => photo.id).sort()).toEqual([1, 2, 3]);
		expect(vi.mocked(media.releasePhotoImageUrl).mock.calls.map(([url]) => url).sort()).toEqual(previousUrls.sort());
	});

	test('keeps a page-boundary navigation alive while resizing the viewer', async () => {
		// Arrange
		const pageOne = Array.from({ length: 100 }, (_, index) => asset(index + 1));
		const pageTwoAsset = asset(101);
		const pageTwo = new Subject<ResultDTO<PhotoAssetPageDTO>>();
		vi.spyOn(plexPhotoApi, 'getPhotoAssetsEndpoint').mockImplementation((query) =>
			query?.page === 1
				? of(generateResultDTO({ items: pageOne, page: 1, pageSize: 100, totalCount: 101, totalBytes: 101 } as PhotoAssetPageDTO))
				: pageTwo.asObservable());
		useLibraryStore().updateLibrary({ id: 7, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO);
		const media = useMediaStore();
		vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((photo) => of(`image-${photo.id}`));
		vi.spyOn(media, 'releasePhotoImageUrl');
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		const opening = subscribeSpyTo(store.showAsset(99));
		expect(pageTwo.observed).toBe(true);

		// Act
		await subscribeSpyTo(store.resizeImages()).onComplete();
		pageTwo.next(generateResultDTO({ items: [pageTwoAsset], page: 2, pageSize: 100, totalCount: 101, totalBytes: 101 } as PhotoAssetPageDTO));
		pageTwo.complete();
		await opening.onComplete();

		// Assert
		expect(store.current?.id).toBe(100);
		expect(store.index).toBe(99);
		expect(store.slides.map((slide) => slide.asset.id).sort((left, right) => left - right)).toEqual([99, 100, 101]);
		expect(store.slides.every((slide) => slide.url === `image-${slide.asset.id}` && !slide.loading && !slide.error)).toBe(true);
	});

	test('closes the viewer when its active context is replaced', async () => {
		const items = [asset(1)];
		setupAssets(items);
		vi.spyOn(plexPhotoApi, 'searchPhotoAssetsEndpoint').mockReturnValue(of(generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 0, totalBytes: 0,
		})));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(items[0]!, 0)).onComplete();
		await subscribeSpyTo(store.activate(route({ searchQuery: 'other' }), owner)).onComplete();
		expect(store.dialog.open).toBe(false);
		expect(store.slides).toEqual([]);
	});
});
