import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoAssetKind, PlexMediaType, type PhotoAssetBrowseDTO, type PhotoAssetPageDTO, type PlexLibraryDTO } from '@dto';
import { useLibraryStore, useMediaStore, usePhotoBrowsingStore } from '@store';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
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
		vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((photo, representation) => of(`${representation}-${photo.id}`));
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

	test('keeps at most three unique slides and releases original URLs outside the window', async () => {
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

	test('uses converted originals after original failure and thumbnails only for clips', async () => {
		const still = asset(1);
		const clip = asset(2, PhotoAssetKind.VideoClip);
		setupAssets([still, clip]);
		const media = useMediaStore();
		vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((_photo, representation) => of(representation === 'original' ? '' : 'converted-url'));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(still, 0)).onComplete();
		expect(store.slides.find((slide) => slide.asset.id === 1)?.representation).toBe('converted');
		await subscribeSpyTo(store.showAsset(1)).onComplete();
		expect(store.slides.find((slide) => slide.asset.id === 2)?.representation).toBe('preview');
		expect(media.getPhotoImageUrl).toHaveBeenCalledTimes(2);
	});

	test('advances decoded-image failures from converted to preview to terminal error without looping', async () => {
		const item = asset(1);
		setupAssets([item]);
		const media = useMediaStore();
		const imageRequest = vi.spyOn(media, 'getPhotoImageUrl').mockImplementation((_photo, representation) => of(`${representation}-url`));
		const thumbnailRequest = vi.spyOn(media, 'getMediaThumbnailUrl').mockReturnValue(of('preview-url'));
		const store = usePhotoBrowsingStore();
		await subscribeSpyTo(store.activate(route(), owner)).onComplete();
		await subscribeSpyTo(store.openAsset(item, 0)).onComplete();

		await subscribeSpyTo(store.imageFailed(0)).onComplete();
		expect(store.slides[0]!.representation).toBe('converted');
		await subscribeSpyTo(store.imageFailed(0)).onComplete();
		expect(store.slides[0]).toEqual(expect.objectContaining({ representation: 'preview', url: 'preview-url', error: false }));
		await subscribeSpyTo(store.imageFailed(0)).onComplete();
		expect(store.slides[0]).toEqual(expect.objectContaining({ representation: 'preview', url: '', loading: false, error: true }));
		expect(imageRequest).toHaveBeenCalledTimes(2);
		expect(thumbnailRequest).toHaveBeenCalledTimes(1);
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
