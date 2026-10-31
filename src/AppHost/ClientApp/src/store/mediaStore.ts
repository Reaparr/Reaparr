import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { defer, from, type Observable, of, Subject } from 'rxjs';
import { map, take, catchError, finalize, shareReplay, takeUntil, tap } from 'rxjs/operators';
import type { PlexMediaComparisonDetailsDTO, PlexMediaType, PlexMediaDTO, BaseResultDTO, PhotoAssetBrowseDTO } from '@dto';
import { PhotoAssetKind, PhotoImageRepresentation } from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { plexMediaApi, plexPhotoApi } from '@api';
import { cloneDeep, clamp } from 'lodash-es';
import Log from 'consola';
import Axios, { type AxiosRequestConfig } from 'axios';
import { get, useDevicePixelRatio, useWindowSize } from '@vueuse/core';

const PHOTO_IMAGE_CACHE_MAX_ENTRIES = 24;
const PHOTO_IMAGE_CACHE_MAX_BYTES = 64 * 1024 * 1024;

interface IMediaUrlStoreState {
	mediaUrls: IObjectUrl[];
}

interface IObjectUrl {
	plexServerId: number;
	plexKey: string;
	metaDataKey: number;
	url: string;
}

interface IPhotoImageCacheEntry {
	key: string;
	plexLibraryId: number;
	url: string;
	bytes: number;
	leases: number;
	lastUsed: number;
	cached: boolean;
}

interface IPhotoPreviewSize {
	readonly width: number;
	readonly height: number;
}

interface IPhotoImageRequest {
	key: string;
	plexLibraryId: number;
	invalidated: boolean;
	cancel$: Subject<void>;
	source$: Observable<string>;
}

export const useMediaStore = defineStore(StoreNames.MediaStore, () => {
	const defaultState: IMediaUrlStoreState = {
		mediaUrls: [],
	};
	const state = reactive<IMediaUrlStoreState>(cloneDeep(defaultState));

	const photoImageCache = new Map<string, IPhotoImageCacheEntry>();
	const photoImageUrls = new Map<string, IPhotoImageCacheEntry>();
	const photoImageRequests = new Map<string, IPhotoImageRequest>();
	let photoImageClock = 0;
	const { width: viewportWidth, height: viewportHeight } = useWindowSize();
	const { pixelRatio } = useDevicePixelRatio();

	const actions = {
		setup(): Observable<ISetupResult> {
			return of({ name: StoreNames.MediaStore, isSuccess: true }).pipe(take(1));
		},
		getMediaDataDetailById(mediaId: number, mediaType: PlexMediaType): Observable<PlexMediaDTO> {
			return plexMediaApi
				.getMediaDetailByIdEndpoint(mediaId, {
					type: mediaType,
				})
				.pipe(map((response) => response.value!));
		},
		getMediaComparisonDetails(mediaId: number, mediaType: PlexMediaType): Observable<PlexMediaComparisonDetailsDTO> {
			return plexMediaApi
				.getMediaComparisonDetailsEndpoint(mediaId, {
					type: mediaType,
				})
				.pipe(map((response) => response.value!));
		},
		getMediaThumbnailUrl(query: {
			plexServerId: number;
			plexKey: string;
			metaDataKey: number;
			height: number;
			width: number;
		}, requestOptions?: Pick<AxiosRequestConfig, 'handleErrorsLocally'>): Observable<string> {
			// Fast-path: return cached object URL if present
			const existing = state.mediaUrls.find((x) => x.plexServerId === query.plexServerId && x.plexKey === query.plexKey && x.metaDataKey === query.metaDataKey);
			if (existing)
				return of(existing.url);

			return from(
				Axios.request<Blob | BaseResultDTO>({
					url: `/api/PlexMedia/thumbnail`,
					method: 'GET',
					params: query,
					responseType: 'blob',
					...requestOptions,
				}),
			)
				.pipe(
					map((res) => {
						if (res.status === 200) {
							return actions.updateMediaUrl({
								plexServerId: query.plexServerId,
								plexKey: query.plexKey,
								metaDataKey: query.metaDataKey,
								image: res.data as Blob,
							});
						}
						Log.warn('Failed to get media thumbnail image', res);
						return '';
					}),
					catchError((error) => {
					// Handle network errors, timeouts, 502/504 gateway errors silently
					// Return empty string to trigger fallback image display
						Log.debug('Media thumbnail request failed', { query, error: error?.message || error });
						return of('');
					}),
				);
		},

		getPhotoImageUrl(asset: PhotoAssetBrowseDTO): Observable<string> {
			if (asset.assetKind !== PhotoAssetKind.Image) return of('');
			return requestPhotoImage(asset, true, currentPhotoPreviewSize());
		},
		preloadPhotoImage(asset: PhotoAssetBrowseDTO): Observable<void> {
			if (asset.assetKind !== PhotoAssetKind.Image) return of(undefined);
			return requestPhotoImage(asset, false, currentPhotoPreviewSize()).pipe(map(() => undefined));
		},
		releasePhotoImageUrl(url: string): void {
			releasePhotoImage(url);
		},
		invalidatePhotoImages(libraryIds: ReadonlySet<number> | null): void {
			invalidatePhotoImages(libraryIds);
		},

		updateMediaUrl({
			plexServerId,
			plexKey,
			metaDataKey,
			image,
		}: {
			plexServerId: number;
			plexKey: string;
			metaDataKey: number;
			image: Blob;
		}): string {
			const index = state.mediaUrls.findIndex((x) => x.plexServerId === plexServerId && x.plexKey === plexKey && x.metaDataKey === metaDataKey);
			const mediaObject = Object.freeze({
				plexServerId,
				plexKey,
				metaDataKey,
				url: URL.createObjectURL(image),
			});

			void (index === -1 ? state.mediaUrls.push(mediaObject) : state.mediaUrls.splice(index, 1, mediaObject));

			return mediaObject.url;
		},
		$reset() {
			cancelPhotoImageRequests(null);
			for (const entry of photoImageUrls.values()) revokePhotoImage(entry);
			photoImageUrls.clear();
			photoImageCache.clear();
			photoImageClock = 0;
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	function requestPhotoImage(
		asset: PhotoAssetBrowseDTO,
		grantLease: boolean,
		previewSize: IPhotoPreviewSize,
	): Observable<string> {
		const key = photoImageKey(asset, previewSize);

		return defer(() => {
			const cached = photoImageCache.get(key);
			const source = cached ? of(cached.url) : getOrCreatePhotoImageRequest(asset, previewSize).source$;
			return source.pipe(tap((url) => {
				const entry = cached ?? photoImageUrls.get(url);
				if (!entry || entry.key !== key) return;
				if (grantLease) entry.leases += 1;
				touchPhotoImage(entry);
			}));
		});
	}

	function getOrCreatePhotoImageRequest(
		asset: PhotoAssetBrowseDTO,
		previewSize: IPhotoPreviewSize,
	): IPhotoImageRequest {
		const capturedPreviewSize = Object.freeze({
			width: previewSize.width,
			height: previewSize.height,
		});
		const key = photoImageKey(asset, capturedPreviewSize);
		const existing = photoImageRequests.get(key);
		if (existing) return existing;

		const query = Object.freeze({
			representation: PhotoImageRepresentation.Converted,
			width: capturedPreviewSize.width,
			height: capturedPreviewSize.height,
		});
		const cancel$ = new Subject<void>();
		const source$ = defer(() =>
			plexPhotoApi.getPhotoAssetImageEndpoint(
				asset.plexLibraryId,
				asset.id,
				query,
				{
					handleErrorsLocally: true,
					validateStatus: (status) => status !== 401,
					// Keep non-success responses as result objects so image-like error blobs are never cached.
					transformResponse: (data: Blob, _headers, status) => status === 200
						? data
						: { isSuccess: false, statusCode: status, value: null, errors: [], successes: [] },
				},
			).pipe(
				map((response) => photoResponseToUrl(request, response as Blob | BaseResultDTO)),
			),
		).pipe(
			takeUntil(cancel$),
			finalize(() => {
				if (photoImageRequests.get(key) === request) photoImageRequests.delete(key);
				enforcePhotoImageCache();
			}),
			shareReplay({ bufferSize: 1, refCount: true }),
		);

		const request: IPhotoImageRequest = {
			key,
			plexLibraryId: asset.plexLibraryId,
			invalidated: false,
			cancel$,
			source$,
		};

		photoImageRequests.set(key, request);
		return request;
	}

	function currentPhotoPreviewSize(): IPhotoPreviewSize {
		const ratio = get(pixelRatio);
		return {
			width: clamp(Math.ceil(get(viewportWidth) * ratio), 1, 4096),
			height: clamp(Math.ceil(get(viewportHeight) * ratio), 1, 4096),
		};
	}

	function photoResponseToUrl(request: IPhotoImageRequest, response: Blob | BaseResultDTO): string {
		if (!(response instanceof Blob)) return '';
		if (!response.type.startsWith('image/')) return '';
		return cachePhotoImage(request, response);
	}

	function cachePhotoImage(request: IPhotoImageRequest, image: Blob): string {
		if (request.invalidated || photoImageRequests.get(request.key) !== request) return '';

		const existing = photoImageCache.get(request.key);
		if (existing) return existing.url;

		const entry: IPhotoImageCacheEntry = {
			key: request.key,
			plexLibraryId: request.plexLibraryId,
			url: URL.createObjectURL(image),
			bytes: image.size,
			leases: 0,
			lastUsed: ++photoImageClock,
			cached: true,
		};
		photoImageCache.set(entry.key, entry);
		photoImageUrls.set(entry.url, entry);
		return entry.url;
	}

	function photoImageKey(asset: PhotoAssetBrowseDTO, previewSize: IPhotoPreviewSize): string {
		return [
			asset.plexServerId,
			asset.plexLibraryId,
			asset.id,
			asset.plexApiMetaDataKey,
			previewSize.width,
			previewSize.height,
		].join(':');
	}
	function touchPhotoImage(entry: IPhotoImageCacheEntry): void {
		entry.lastUsed = ++photoImageClock;
	}

	function releasePhotoImage(url: string): void {
		const entry = photoImageUrls.get(url);
		if (!entry || entry.leases === 0) return;

		entry.leases -= 1;
		if (entry.leases !== 0) return;

		if (!entry.cached || photoImageCache.get(entry.key) !== entry) {
			revokePhotoImage(entry);
			return;
		}
		enforcePhotoImageCache();
	}

	function invalidatePhotoImages(libraryIds: ReadonlySet<number> | null): void {
		cancelPhotoImageRequests(libraryIds);

		for (const entry of photoImageCache.values()) {
			if (!isPhotoLibraryAffected(entry.plexLibraryId, libraryIds)) continue;
			photoImageCache.delete(entry.key);
			entry.cached = false;
			if (entry.leases === 0) revokePhotoImage(entry);
		}
	}

	function cancelPhotoImageRequests(libraryIds: ReadonlySet<number> | null): void {
		for (const [key, request] of photoImageRequests) {
			if (!isPhotoLibraryAffected(request.plexLibraryId, libraryIds)) continue;
			request.invalidated = true;
			photoImageRequests.delete(key);
			request.cancel$.next();
			request.cancel$.complete();
		}
	}

	function isPhotoLibraryAffected(plexLibraryId: number, libraryIds: ReadonlySet<number> | null): boolean {
		return libraryIds === null || libraryIds.has(plexLibraryId);
	}

	function oldestUnleasedPhotoImage(): IPhotoImageCacheEntry | undefined {
		let candidate: IPhotoImageCacheEntry | undefined;
		for (const entry of photoImageCache.values()) {
			if (entry.leases !== 0) continue;
			if (!candidate || entry.lastUsed < candidate.lastUsed) candidate = entry;
		}
		return candidate;
	}

	function enforcePhotoImageCache(): void {
		let entryCount = photoImageCache.size;
		let retainedBytes = 0;
		for (const entry of photoImageCache.values()) retainedBytes += entry.bytes;

		while (entryCount > PHOTO_IMAGE_CACHE_MAX_ENTRIES || retainedBytes > PHOTO_IMAGE_CACHE_MAX_BYTES) {
			const candidate = oldestUnleasedPhotoImage();
			if (!candidate) break;

			photoImageCache.delete(candidate.key);
			candidate.cached = false;
			entryCount -= 1;
			retainedBytes -= candidate.bytes;
			revokePhotoImage(candidate);
		}
	}

	function revokePhotoImage(entry: IPhotoImageCacheEntry): void {
		if (!photoImageUrls.delete(entry.url)) return;
		entry.cached = false;
		URL.revokeObjectURL(entry.url);
	}

	const getters = {
		photoPreviewSize: computed<IPhotoPreviewSize>(() => currentPhotoPreviewSize()),
	};
	return {
		...toRefs(state), ...actions, ...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useMediaStore, import.meta.hot));
}
