import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import Axios from 'axios';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PhotoAssetKind, PhotoImageRepresentation, type PhotoAssetBrowseDTO } from '@dto';
import { useMediaStore } from '@store';

const DEFAULT_VIEWPORT = { width: 1280, height: 720 };
const photoImagePath = (photo: PhotoAssetBrowseDTO) =>
	`/api/PlexPhoto/libraries/${photo.plexLibraryId}/assets/${photo.id}/image`;
const photoImagePathMatcher = /\/api\/PlexPhoto\/libraries\/\d+\/assets\/\d+\/image/;

const asset: PhotoAssetBrowseDTO = {
	id: 501, plexServerId: 3, plexLibraryId: 41,
	plexServerName: 'Photo server', plexLibraryTitle: 'Photos', title: 'Photo image',
	assetKind: PhotoAssetKind.Image, hasThumbnail: true,
	plexApiRatingKey: 501, plexApiMetaDataKey: 502,
	originalFilename: 'original.jpg', container: 'jpg', size: 4096, width: 6000, height: 4000,
};

function photoAsset(overrides: Partial<PhotoAssetBrowseDTO> = {}): PhotoAssetBrowseDTO {
	return { ...asset, ...overrides };
}

function photoImageParams(size = DEFAULT_VIEWPORT) {
	return {
		representation: PhotoImageRepresentation.Converted,
		width: size.width,
		height: size.height,
	};
}

function setViewport(width: number, height: number, pixelRatio = 1): void {
	Object.defineProperty(window, 'innerWidth', { configurable: true, writable: true, value: width });
	Object.defineProperty(window, 'innerHeight', { configurable: true, writable: true, value: height });
	Object.defineProperty(window, 'devicePixelRatio', { configurable: true, writable: true, value: pixelRatio });
}

function resizeViewport(width: number, height: number, pixelRatio = 1): void {
	setViewport(width, height, pixelRatio);
	window.dispatchEvent(new Event('resize'));
}

function blobWithSize(size: number): Blob {
	const image = new Blob(['image'], { type: 'image/jpeg' });
	Object.defineProperty(image, 'size', { configurable: true, value: size });
	return image;
}

describe('MediaStore photo image ownership', () => {
	let { mock } = baseVars();

	beforeAll(() => baseSetup());
	beforeEach(() => {
		setViewport(DEFAULT_VIEWPORT.width, DEFAULT_VIEWPORT.height);
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});
	afterEach(() => vi.restoreAllMocks());

	test('Should request image assets without metadata thumbnails while skipping non-image assets', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:without-metadata-thumbnail');
		const noThumbnail = photoAsset({ hasThumbnail: false });
		mock.onGet(photoImagePath(noThumbnail), { params: photoImageParams() })
			.reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const image = subscribeSpyTo(store.getPhotoImageUrl(noThumbnail));
		const videoClip = subscribeSpyTo(store.preloadPhotoImage(photoAsset({ assetKind: PhotoAssetKind.VideoClip })));
		await Promise.all([image.onComplete(), videoClip.onComplete()]);

		// Assert
		expect(image.getLastValue()).toBe('blob:without-metadata-thumbnail');
		expect(videoClip.getValues()).toEqual([undefined]);
		expect(mock.history.get).toHaveLength(1);
		expect(mock.history.get[0]?.url).toBe(photoImagePath(noThumbnail));
		store.releasePhotoImageUrl(image.getLastValue()!);
	});

	test('Should request a leased converted preview with viewport sizing from the photo image endpoint', async () => {
		// Arrange
		const createUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:converted');
		const image = new Blob(['converted'], { type: 'image/jpeg' });
		mock.onGet(photoImagePath(asset), { params: photoImageParams() }).reply(200, image);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await response.onComplete();

		// Assert
		expect(store.photoPreviewSize).toEqual(DEFAULT_VIEWPORT);
		expect(response.getLastValue()).toBe('blob:converted');
		expect(createUrl).toHaveBeenCalledWith(image);
		expect(mock.history.get).toHaveLength(1);
		expect(mock.history.get[0]?.url).toBe(photoImagePath(asset));
		expect(mock.history.get[0]?.params).toEqual(photoImageParams());
		expect(mock.history.get[0]?.url).not.toContain('/api/PlexMedia/thumbnail');
		expect(mock.history.get[0]?.params?.representation).toBe(PhotoImageRepresentation.Converted);
		store.releasePhotoImageUrl(response.getLastValue()!);
	});

	test('Should calculate converted preview dimensions from a device pixel ratio of two', async () => {
		// Arrange
		const viewport = { width: 640, height: 360 };
		setViewport(viewport.width, viewport.height, 2);
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:high-density');
		mock.onGet(photoImagePath(asset), { params: photoImageParams({ width: 1280, height: 720 }) })
			.reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await response.onComplete();

		// Assert
		expect(store.photoPreviewSize).toEqual({ width: 1280, height: 720 });
		expect(mock.history.get[0]?.params).toEqual(photoImageParams({ width: 1280, height: 720 }));
		store.releasePhotoImageUrl(response.getLastValue()!);
	});

	test('Should clamp converted preview dimensions to the supported range', async () => {
		// Arrange
		setViewport(0, 5000, 2);
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:clamped');
		mock.onGet(photoImagePath(asset), { params: photoImageParams({ width: 1, height: 4096 }) })
			.reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await response.onComplete();

		// Assert
		expect(store.photoPreviewSize).toEqual({ width: 1, height: 4096 });
		expect(mock.history.get[0]?.params).toEqual(photoImageParams({ width: 1, height: 4096 }));
		store.releasePhotoImageUrl(response.getLastValue()!);
	});

	test('Should release leased converted previews exactly once without revoking an active cached URL', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:converted');
		const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['image'], { type: 'image/png' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();
		const first = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await first.onComplete();
		const second = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await second.onComplete();

		// Act
		store.releasePhotoImageUrl(first.getLastValue()!);
		store.releasePhotoImageUrl(first.getLastValue()!);
		store.releasePhotoImageUrl('blob:unrelated');
		store.releasePhotoImageUrl(second.getLastValue()!);
		store.$reset();
		store.releasePhotoImageUrl(second.getLastValue()!);

		// Assert
		expect(revoke).toHaveBeenCalledTimes(1);
		expect(revoke).toHaveBeenCalledWith('blob:converted');
	});

	test('Should reject a successful non-image payload instead of presenting it as a photo preview', async () => {
		// Arrange
		const createUrl = vi.spyOn(URL, 'createObjectURL');
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['{"isSuccess":false}'], { type: 'application/json' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await response.onComplete();

		// Assert
		expect(response.getLastValue()).toBe('');
		expect(createUrl).not.toHaveBeenCalled();
	});

	test('Should treat a missing converted preview as a display failure without deleting the photo asset', async () => {
		// Arrange
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(404, { isSuccess: false, errors: [], value: null });
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await response.onComplete();

		// Assert
		expect(response.getLastValue()).toBe('');
		expect(mock.history.get).toHaveLength(1);
	});

	test('Should keep an unavailable converted preview in the viewer display-error path instead of the global request-error pipeline', async () => {
		// Arrange
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(502, new Blob(['Unavailable'], { type: 'application/json' }));
		const rejected = vi.fn((error: unknown) => Promise.reject(error));
		const interceptor = Axios.interceptors.response.use(undefined, rejected);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		try {
			// Act
			const response = subscribeSpyTo(store.getPhotoImageUrl(asset));
			await response.onComplete();

			// Assert
			expect(response.getLastValue()).toBe('');
			expect(rejected).not.toHaveBeenCalled();
		} finally {
			Axios.interceptors.response.eject(interceptor);
		}
	});

	test('Should reuse a preloaded converted preview for fullscreen without another network request', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:converted');
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['converted'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const preload = subscribeSpyTo(store.preloadPhotoImage(asset));
		await preload.onComplete();
		const fullscreen = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await fullscreen.onComplete();

		// Assert
		expect(fullscreen.getLastValue()).toBe('blob:converted');
		expect(mock.history.get.filter((request) => request.url === photoImagePath(asset))).toHaveLength(1);
		store.releasePhotoImageUrl(fullscreen.getLastValue()!);
	});

	test('Should join a pending converted preload from a foreground request', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:converted');
		const image = new Blob(['converted'], { type: 'image/jpeg' });
		const { promise, resolve } = Promise.withResolvers<[number, Blob]>();
		mock.onGet(photoImagePath(asset), { params: photoImageParams() }).replyOnce(() => promise);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const preload = subscribeSpyTo(store.preloadPhotoImage(asset));
		const fullscreen = subscribeSpyTo(store.getPhotoImageUrl(asset));
		resolve([200, image]);
		await Promise.all([preload.onComplete(), fullscreen.onComplete()]);

		// Assert
		expect(fullscreen.getLastValue()).toBe('blob:converted');
		expect(mock.history.get.filter((request) => request.url === photoImagePath(asset))).toHaveLength(1);
		store.releasePhotoImageUrl(fullscreen.getLastValue()!);
	});

	test('Should separate converted preview cache identities by server, library, asset, and metadata', async () => {
		// Arrange
		let imageIndex = 0;
		const createUrl = vi.spyOn(URL, 'createObjectURL').mockImplementation(() => `blob:image-${++imageIndex}`);
		mock.onGet(photoImagePathMatcher).reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();
		const scopedAssets = [
			asset,
			photoAsset({ plexServerId: 4 }),
			photoAsset({ plexLibraryId: 42 }),
			photoAsset({ id: 601, plexApiRatingKey: 601 }),
			photoAsset({ plexApiMetaDataKey: 602 }),
		];

		// Act
		const urls: string[] = [];
		for (const scopedAsset of scopedAssets) {
			const response = subscribeSpyTo(store.getPhotoImageUrl(scopedAsset));
			await response.onComplete();
			urls.push(response.getLastValue()!);
		}

		// Assert
		expect(urls).toEqual([
			'blob:image-1', 'blob:image-2', 'blob:image-3', 'blob:image-4', 'blob:image-5',
		]);
		expect(createUrl).toHaveBeenCalledTimes(5);
		expect(mock.history.get.filter((request) => request.url?.match(photoImagePathMatcher))).toHaveLength(5);
		for (const url of urls) store.releasePhotoImageUrl(url);
	});

	test('Should separate converted preview cache entries by viewport size and reuse a previous size after resizing back', async () => {
		// Arrange
		let imageIndex = 0;
		vi.spyOn(URL, 'createObjectURL').mockImplementation(() => `blob:image-${++imageIndex}`);
		const resized = { width: 640, height: 360 };
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['default'], { type: 'image/jpeg' }))
			.onGet(photoImagePath(asset), { params: photoImageParams(resized) })
			.reply(200, new Blob(['resized'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const first = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await first.onComplete();
		store.releasePhotoImageUrl(first.getLastValue()!);
		resizeViewport(resized.width, resized.height);
		const resizedResponse = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await resizedResponse.onComplete();
		store.releasePhotoImageUrl(resizedResponse.getLastValue()!);
		resizeViewport(DEFAULT_VIEWPORT.width, DEFAULT_VIEWPORT.height);
		const reused = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await reused.onComplete();

		// Assert
		expect(store.photoPreviewSize).toEqual(DEFAULT_VIEWPORT);
		expect(reused.getLastValue()).toBe(first.getLastValue());
		expect(mock.history.get.filter((request) => request.url === photoImagePath(asset))).toHaveLength(2);
		store.releasePhotoImageUrl(reused.getLastValue()!);
	});

	test('Should capture the original converted preview size for a pending request across a resize', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:resized');
		const resized = { width: 640, height: 360 };
		const { promise, resolve } = Promise.withResolvers<[number, Blob]>();
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.replyOnce(() => promise)
			.onGet(photoImagePath(asset), { params: photoImageParams(resized) })
			.reply(200, new Blob(['resized'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const preload = subscribeSpyTo(store.preloadPhotoImage(asset));
		resizeViewport(resized.width, resized.height);
		const resizedResponse = subscribeSpyTo(store.getPhotoImageUrl(asset));
		resolve([200, new Blob(['default'], { type: 'image/jpeg' })]);
		await Promise.all([preload.onComplete(), resizedResponse.onComplete()]);

		// Assert
		expect(mock.history.get).toHaveLength(2);
		expect(mock.history.get[0]?.params).toEqual(photoImageParams());
		expect(mock.history.get[1]?.params).toEqual(photoImageParams(resized));
		store.releasePhotoImageUrl(resizedResponse.getLastValue()!);
	});

	test('Should abort an in-flight converted preview request when its last consumer unsubscribes', async () => {
		// Arrange
		const abort = vi.spyOn(AbortController.prototype, 'abort');
		const { promise, resolve } = Promise.withResolvers<[number, Blob]>();
		mock.onGet(photoImagePath(asset), { params: photoImageParams() }).replyOnce(() => promise);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const preload = subscribeSpyTo(store.preloadPhotoImage(asset));
		preload.unsubscribe();
		resolve([200, new Blob(['stale'], { type: 'image/jpeg' })]);

		// Assert
		expect(abort).toHaveBeenCalled();
	});

	test('Should invalidate pending and cached converted previews without allowing stale data to refill the cache', async () => {
		// Arrange
		const createUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fresh');
		const { promise, resolve } = Promise.withResolvers<[number, Blob]>();
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.replyOnce(() => promise)
			.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['fresh'], { type: 'image/jpeg' }));
		const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const preload = subscribeSpyTo(store.preloadPhotoImage(asset));
		store.invalidatePhotoImages(new Set([asset.plexLibraryId]));
		resolve([200, new Blob(['stale'], { type: 'image/jpeg' })]);
		await preload.onComplete();
		const fresh = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await fresh.onComplete();
		store.invalidatePhotoImages(new Set([asset.plexLibraryId]));

		// Assert
		expect(createUrl).toHaveBeenCalledTimes(1);
		expect(fresh.getLastValue()).toBe('blob:fresh');
		expect(revoke).not.toHaveBeenCalledWith('blob:fresh');
		store.releasePhotoImageUrl(fresh.getLastValue()!);
		expect(revoke).toHaveBeenCalledWith('blob:fresh');
	});

	test('Should evict least-recently-used unleased converted previews at the count limit', async () => {
		// Arrange
		let imageIndex = 0;
		const createUrl = vi.spyOn(URL, 'createObjectURL').mockImplementation(() => `blob:image-${++imageIndex}`);
		const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
		mock.onGet(photoImagePathMatcher).reply(200, new Blob(['image'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		for (let id = 1; id <= 25; id += 1) {
			const preload = subscribeSpyTo(store.preloadPhotoImage(photoAsset({ id, plexApiRatingKey: id })));
			await preload.onComplete();
		}

		// Assert
		expect(createUrl).toHaveBeenCalledTimes(25);
		expect(revoke).toHaveBeenCalledWith('blob:image-1');
	});

	test('Should keep leased viewer converted previews valid while evicting oversized unleased previews', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL')
			.mockReturnValueOnce('blob:pinned')
			.mockReturnValueOnce('blob:oversized');
		const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
		const oversized = blobWithSize(64 * 1024 * 1024 + 1);
		mock.onGet(photoImagePathMatcher).reply(200, oversized);
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const pinned = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await pinned.onComplete();
		const preload = subscribeSpyTo(store.preloadPhotoImage(photoAsset({ id: 602, plexApiRatingKey: 602 })));
		await preload.onComplete();

		// Assert
		expect(revoke).toHaveBeenCalledWith('blob:oversized');
		expect(revoke).not.toHaveBeenCalledWith('blob:pinned');
		store.releasePhotoImageUrl(pinned.getLastValue()!);
	});

	test('Should retry a failed converted preview request instead of reusing a poisoned observable', async () => {
		// Arrange
		vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:retry');
		mock.onGet(photoImagePath(asset), { params: photoImageParams() })
			.replyOnce(404, { isSuccess: false, errors: [], value: null })
			.onGet(photoImagePath(asset), { params: photoImageParams() })
			.reply(200, new Blob(['retry'], { type: 'image/jpeg' }));
		const store = useMediaStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const failed = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await failed.onComplete();
		const retried = subscribeSpyTo(store.getPhotoImageUrl(asset));
		await retried.onComplete();

		// Assert
		expect(failed.getLastValue()).toBe('');
		expect(retried.getLastValue()).toBe('blob:retry');
		expect(mock.history.get.filter((request) => request.url === photoImagePath(asset))).toHaveLength(2);
		store.releasePhotoImageUrl(retried.getLastValue()!);
	});
});
