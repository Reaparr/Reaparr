import Log from 'consola';
import { isAxiosError, type AxiosRequestConfig } from 'axios';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { computed, reactive, shallowReactive, toRefs } from 'vue';
import {
	Subject,
	EMPTY,
	asapScheduler,
	defer,
	from,
	forkJoin,
	of,
	range as rxRange,
	type Observable,
	type Subscription,
	throwError,
} from 'rxjs';
import {
	auditTime,
	catchError,
	concatMap,
	finalize,
	map,
	mergeMap,
	startWith,
	switchMap,
	takeUntil,
	tap,
	shareReplay,
	toArray,
} from 'rxjs/operators';
import { cloneDeep, pick, uniqBy } from 'lodash-es';
import {
	PlexMediaType,
	type SortType,
	LibrarySyncJobStatus,
	type LibrarySyncJobQueueDTO,
	RefreshDataType,
	type DownloadMediaDTO,
	type PhotoAlbumBrowseDTO,
	type PhotoAlbumPageDTO,
	type PhotoAlbumSummaryPageDTO,
	type PhotoAssetBrowseDTO,
	type PhotoAssetInspectionDTO,
	type PhotoAssetPageDTO,
	type PhotoFolderBrowseDTO,
	type PhotoFolderChildrenDTO,
	type PhotoFolderPageDTO,
} from '@dto';
import { PhotoAssetKind, PhotoItemKind } from '@dto';
import { StoreNames, type ISetupResult, type ResultDTO } from '@interfaces';
import type { IMediaOverviewSort } from '@composables/event-bus';
import { createDefaultMediaOverviewSort, serializeFlexSortDsl } from '@composables';
import { plexLibraryApi, plexPhotoApi } from '@api';
import { useAccountStore, useLibraryStore, useMediaStore, useServerStore, useSettingsStore, useSignalrStore } from '@store';

export type PhotoBrowseView = 'albums' | 'folders' | 'assets';
export type PhotoBrowseMode = 'aggregate' | 'library' | 'album';

export interface IPhotoBrowseRoute {
	mode: PhotoBrowseMode;
	libraryId: number;
	albumId?: number;
	view: PhotoBrowseView;
	folderId?: number | null;
	searchQuery: string;
	sort: IMediaOverviewSort;
}

export type IPhotoBrowseActivation = IPhotoBrowseRoute;

export interface IPhotoSelector {
	id: number;
	plexServerId: number;
	plexLibraryId: number;
	kind: PhotoItemKind;
	type: PlexMediaType;
	title: string;
	stale: boolean;
}

export type PhotoOverviewItem
	= | { kind: PhotoItemKind.Album; album: PhotoAlbumBrowseDTO; index: number }
		| { kind: PhotoItemKind.Folder; folder: PhotoFolderBrowseDTO; index: number }
		| { kind: PhotoItemKind.Asset; asset: PhotoAssetBrowseDTO; index: number };

export interface PhotoViewerSlide {
	asset: PhotoAssetBrowseDTO;
	index: number;
	url: string;
	loading: boolean;
	error: boolean;
}

interface IPhotoPageState<T> {
	items: T[];
	page: number;
	lastLoadedPage: number;
	pageSize: number;
	totalCount: number;
	totalBytes: number;
	totalAssetBytes: number;
	loading: boolean;
	loaded: boolean;
	error: boolean;
}

interface IPhotoInspectionIdentity {
	libraryId: number;
	assetId: number;
}

interface IPhotoDialogState {
	open: boolean;
	loading: boolean;
	error: boolean;
	asset: PhotoAssetInspectionDTO | null;
}

interface IPhotoBrowsingState {
	active: boolean;
	mode: PhotoBrowseMode;
	libraryId: number;
	albumId: number | null;
	view: PhotoBrowseView;
	folderId: number | null;
	sort: IMediaOverviewSort;
	supportedSorts: SortType[];
	sortMetadataProfileKey: string;
	sortMetadataReady: boolean;
	searchQuery: string;
	searchOrigin: IPhotoBrowseRoute | null;
	albums: IPhotoPageState<PhotoAlbumBrowseDTO>;
	assets: IPhotoPageState<PhotoAssetBrowseDTO>;
	search: IPhotoPageState<PhotoAssetBrowseDTO>;
	folders: IPhotoPageState<PhotoFolderBrowseDTO>;
	folderAssets: IPhotoPageState<PhotoAssetBrowseDTO>;
	folder: PhotoFolderBrowseDTO | null;
	breadcrumbs: Array<{ id: number; title: string; relativePath: string }>;
	albumMembers: IPhotoPageState<PhotoAssetBrowseDTO>;
	inspectionAlbums: PhotoAlbumSummaryPageDTO | null;
	inspectionIdentity: IPhotoInspectionIdentity | null;
	selectedSelectors: IPhotoSelector[];
	selectionStale: boolean;
	selectionLoading: boolean;
	selectionError: boolean;
	allVisibleSelected: boolean;
	disabledLibrary: boolean;
	loading: boolean;
	error: boolean;
	reloadError: boolean;
	contextMissing: boolean;
	dialog: IPhotoDialogState;
	current: PhotoAssetBrowseDTO | null;
	index: number;
	count: number;
	detailsOpen: boolean;
	zoomed: boolean;
	navigating: boolean;
	slides: PhotoViewerSlide[];
	scrollIndex: number;
	focusIndex: number | null;
}

interface PhotoBrowseReadContext extends IPhotoBrowseRoute {
	force: boolean;
	key: string;
}

type Surface = 'albums' | 'assets' | 'folders' | 'folderAssets' | 'albumMembers' | 'search';
type PageItem = PhotoAlbumBrowseDTO | PhotoAssetBrowseDTO | PhotoFolderBrowseDTO;
interface PhotoPageIdentity {
	id: number;
	plexServerId: number;
	plexLibraryId: number;
}
type PageDTO = PhotoAlbumPageDTO | PhotoAssetPageDTO | PhotoFolderPageDTO;
type SelectionPage = PageDTO | PhotoFolderChildrenDTO;
type ApiResult<T> = ResultDTO<T>;

interface CachedBrowseContext {
	context: IPhotoBrowseRoute;
	surfaces: Array<{
		surface: Surface;
		items: Map<number, PageItem>;
		loadedPages: Set<number>;
		page: Pick<IPhotoPageState<PageItem>, 'page' | 'pageSize' | 'totalCount' | 'totalBytes' | 'totalAssetBytes'>;
	}>;
	folder: PhotoFolderBrowseDTO | null;
	breadcrumbs: IPhotoBrowsingState['breadcrumbs'];
	itemCount: number;
}

interface ImagePreloadCandidate {
	asset: PhotoAssetBrowseDTO;
	cancel$: Subject<void>;
	queued: boolean;
	owners: Set<object>;
}

const PAGE_SIZE = 100;
const RANGE_CACHE_LIMIT = 600;
const HISTORY_LIMIT = 20;
const IMAGE_PRELOAD_CONCURRENCY = 2;
const IMAGE_PRELOAD_SETTLE_MS = 75;

// Photo browse failures belong to their inline states; 401 still uses the shared login redirect.
const photoBrowseRequestParams: AxiosRequestConfig = {
	validateStatus: (status) => status !== 401,
};

const createPage = <T>(): IPhotoPageState<T> => ({
	items: [],
	page: 1,
	lastLoadedPage: 0,
	pageSize: PAGE_SIZE,
	totalCount: 0,
	totalBytes: 0,
	totalAssetBytes: 0,
	loading: false,
	loaded: false,
	error: false,
});

const createDefaultState = (): IPhotoBrowsingState => ({
	active: false,
	mode: 'aggregate',
	libraryId: 0,
	albumId: null,
	view: 'albums',
	folderId: null,
	sort: createDefaultMediaOverviewSort(),
	supportedSorts: [],
	sortMetadataProfileKey: '',
	sortMetadataReady: false,
	searchQuery: '',
	searchOrigin: null,
	albums: createPage<PhotoAlbumBrowseDTO>(),
	assets: createPage<PhotoAssetBrowseDTO>(),
	search: createPage<PhotoAssetBrowseDTO>(),
	folders: createPage<PhotoFolderBrowseDTO>(),
	folderAssets: createPage<PhotoAssetBrowseDTO>(),
	folder: null,
	breadcrumbs: [],
	albumMembers: createPage<PhotoAssetBrowseDTO>(),
	inspectionAlbums: null,
	inspectionIdentity: null,
	selectedSelectors: [],
	selectionStale: false,
	selectionLoading: false,
	selectionError: false,
	allVisibleSelected: false,
	disabledLibrary: false,
	loading: false,
	error: false,
	reloadError: false,
	contextMissing: false,
	dialog: { open: false, loading: false, error: false, asset: null },
	current: null,
	index: 0,
	count: 0,
	detailsOpen: false,
	zoomed: false,
	navigating: false,
	slides: [],
	scrollIndex: 0,
	focusIndex: null,
});

export const usePhotoBrowsingStore = defineStore(StoreNames.PhotoBrowsingStore, () => {
	// 1. defaultState
	const defaultState = createDefaultState();

	// 2. state
	const state = reactive<IPhotoBrowsingState>(cloneDeep(defaultState));

	// 3. Inter-store dependencies
	const accountStore = useAccountStore();
	const libraryStore = useLibraryStore();
	const mediaStore = useMediaStore();
	const serverStore = useServerStore();
	const settingsStore = useSettingsStore();
	const signalrStore = useSignalrStore();

	let sessionGeneration = 0;
	let contextGeneration = 0;
	let activeContextKey = '';
	let setupComplete = false;
	let setupSubscription: Subscription | null = null;
	let libraryStoreUnsubscribe: (() => void) | null = null;
	let accountStoreUnsubscribe: (() => void) | null = null;
	let serverStoreUnsubscribe: (() => void) | null = null;
	let lifecycle$ = new Subject<void>();
	let browseCancel$ = new Subject<void>();
	let selectionCancel$ = new Subject<void>();
	let viewerCancel$ = new Subject<void>();
	let refreshInFlight = false;
	let sortMetadataRequestKey = '';
	let sortMetadataRequest$: Observable<boolean> | null = null;
	let activeOwner: object | null = null;
	let eligibilityMetadataKey = '';
	let eligibilityInitialized = false;
	let eligibleLibraryIds = new Set<number>();
	let selectionGeneration = 0;
	let viewerGeneration = 0;
	let viewerImageGeneration = 0;
	let inspectionGeneration = 0;
	const observedQueues = new Map<string, { status: LibrarySyncJobStatus; completedAt?: string | null; eligible: boolean }>();
	const pendingRefreshLibraries = new Set<number>();
	const rangeCancellers = new Map<string, Subject<void>>();
	const scrollHistory = new Map<string, number>();
	const slideVersions = new Map<string, number>();
	const imagePreloadCandidates = new Map<string, ImagePreloadCandidate>();
	const imagePreloadChanges$ = new Subject<void>();
	const imagePreloadSessions$ = new Subject<void>();
	let viewerDirection = 1;

	const feedMaps: Record<Surface, Map<number, PageItem>> = {
		albums: shallowReactive(new Map<number, PhotoAlbumBrowseDTO>()),
		assets: shallowReactive(new Map<number, PhotoAssetBrowseDTO>()),
		folders: shallowReactive(new Map<number, PhotoFolderBrowseDTO>()),
		folderAssets: shallowReactive(new Map<number, PhotoAssetBrowseDTO>()),
		albumMembers: shallowReactive(new Map<number, PhotoAssetBrowseDTO>()),
		search: shallowReactive(new Map<number, PhotoAssetBrowseDTO>()),
	};
	const loadedPages: Record<Surface, Set<number>> = {
		albums: new Set<number>(),
		assets: new Set<number>(),
		folders: new Set<number>(),
		folderAssets: new Set<number>(),
		albumMembers: new Set<number>(),
		search: new Set<number>(),
	};
	const forcedReadIdentities: Record<Surface, Map<number, PhotoPageIdentity[]>> = {
		albums: new Map<number, PhotoPageIdentity[]>(),
		assets: new Map<number, PhotoPageIdentity[]>(),
		folders: new Map<number, PhotoPageIdentity[]>(),
		folderAssets: new Map<number, PhotoPageIdentity[]>(),
		albumMembers: new Map<number, PhotoPageIdentity[]>(),
		search: new Map<number, PhotoPageIdentity[]>(),
	};
	const cacheOrder: Array<{ surface: Surface; index: number }> = [];
	const contextCache = new Map<string, CachedBrowseContext>();
	let cachedItemCount = 0;

	// 4. Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			if (setupComplete) return of({ name: StoreNames.PhotoBrowsingStore, isSuccess: true });
			setupComplete = true;
			syncEligibility();
			seedQueueObservations();
			imagePreloadSessions$.pipe(
				startWith(undefined),
				switchMap(createImagePreloadWorker),
				takeUntil(lifecycle$),
			).subscribe();
			setupSubscription = signalrStore
				.getRefreshNotification(RefreshDataType.PlexLibrary)
				.pipe(auditTime(250), takeUntil(lifecycle$))
				.subscribe(observeQueueCompletions);
			libraryStoreUnsubscribe = libraryStore.$subscribe(() => handleLibraryState());
			accountStoreUnsubscribe = accountStore.$subscribe(() => handleLibraryState());
			serverStoreUnsubscribe = serverStore.$subscribe(() => syncEligibility());
			return of({ name: StoreNames.PhotoBrowsingStore, isSuccess: true });
		},

		activate(route: IPhotoBrowseActivation, owner: object): Observable<void> {
			const key = contextKey(route);
			const sortProfileKey = photoSortMetadataKey(route);
			const contextChanged = activeContextKey !== key;
			if (contextChanged || activeOwner !== owner) {
				cancelBrowseRequests();
				clearImagePreloadCandidates();
			}
			if (contextChanged) {
				if (activeContextKey) rememberScrollPosition(activeContextKey);
				rememberContextResults();
				activeContextKey = key;
				contextGeneration += 1;
				if (state.sortMetadataProfileKey !== sortProfileKey) clearSortMetadata();
				clearViewer();
				state.dialog.open = false;
				clearContextResults();
				restoreContextResults(key);
				state.searchOrigin = null;
				state.scrollIndex = scrollHistory.get(key) ?? 0;
				state.focusIndex = null;
			}
			activeOwner = owner;
			state.active = true;
			state.mode = route.mode;
			state.libraryId = route.libraryId;
			state.albumId = route.albumId ?? null;
			state.view = route.view;
			state.folderId = route.folderId ?? null;
			state.sort = route.sort;
			state.searchQuery = route.searchQuery.trim();
			if (state.searchQuery && !state.searchOrigin) {
				state.searchOrigin = { ...route, searchQuery: '' };
			}
			if (!state.searchQuery) state.searchOrigin = null;
			state.disabledLibrary = !activeLibraryEnabled();
			state.count = assetSequenceCount();
			return actions.loadCurrent(false);
		},

		deactivate(owner: object): void {
			if (activeOwner !== owner) return;
			rememberScrollPosition(activeContextKey);
			rememberContextResults();
			activeOwner = null;
			state.active = false;
			cancelBrowseRequests();
			clearImagePreloadCandidates();
			clearViewer();
			state.dialog.open = false;
			activeContextKey = '';
			clearContextResults();
		},

		leavePhotos(owner: object): void {
			if (activeOwner !== null && activeOwner !== owner) return;
			actions.deactivate(owner);
			actions.clearSelection();
		},

		loadCurrent(force = false): Observable<void> {
			if (!state.active || state.disabledLibrary) return of(undefined);
			const currentIndex = force && state.dialog.open && state.current ? findOverviewIndex(state.current) : -1;
			prepareForcedRead(force);
			const context = currentContext(force);
			if (!context) return of(undefined);
			const initial = ensureSortMetadata(context, contextGeneration).pipe(
				switchMap((sortSupported) => sortSupported ? requestRangeInternal(0, PAGE_SIZE, force) : of(undefined)),
			);
			let rangeRequest = initial;
			if (currentIndex >= PAGE_SIZE) {
				rangeRequest = forkJoin([initial, requestRangeInternal(currentIndex, currentIndex + 1, true)]).pipe(map(() => undefined));
			}
			const viewerRequest = force && state.dialog.open ? rangeRequest.pipe(switchMap(() => reloadViewerImages())) : rangeRequest;
			return viewerRequest.pipe(finalize(() => imagePreloadChanges$.next()));
		},

		requestRange(start: number, end: number): Observable<void> {
			return requestRangeInternal(start, end, false).pipe(finalize(() => imagePreloadChanges$.next()));
		},

		openAsset(asset: PhotoAssetBrowseDTO, overviewIndex?: number): Observable<void> {
			state.dialog.open = true;
			state.dialog.loading = false;
			state.dialog.error = false;
			state.dialog.asset = null;
			state.inspectionAlbums = null;
			state.detailsOpen = false;
			state.zoomed = false;
			state.focusIndex = null;
			state.inspectionIdentity = { libraryId: asset.plexLibraryId, assetId: asset.id };
			const resolvedOverviewIndex = overviewIndex ?? findOverviewIndex(asset);
			const sequenceIndex = resolvedOverviewIndex >= 0
				? Math.max(0, resolvedOverviewIndex - overviewFolderCount())
				: 0;
			return showAssetInternal(sequenceIndex, asset);
		},

		showAsset(index: number): Observable<void> {
			return showAssetInternal(index);
		},

		closeAssetDialog(): void {
			const lastOverviewIndex = state.current ? findOverviewIndex(state.current) : -1;
			if (lastOverviewIndex >= 0) {
				state.focusIndex = lastOverviewIndex;
				actions.setScrollIndex(lastOverviewIndex);
			}
			clearViewer();
			state.dialog.open = false;
			state.dialog.loading = false;
			state.dialog.error = false;
			state.dialog.asset = null;
			state.inspectionAlbums = null;
			state.inspectionIdentity = null;
			state.detailsOpen = false;
			imagePreloadChanges$.next();
		},

		openDetails(): Observable<void> {
			if (!state.dialog.open || !state.inspectionIdentity) return of(undefined);
			state.detailsOpen = true;
			if (state.dialog.asset) return of(undefined);
			return requestInspectionPage(state.inspectionIdentity, 1).pipe(map(() => undefined));
		},

		closeDetails(): void {
			state.detailsOpen = false;
		},

		setZoomed(zoomed: boolean): void {
			state.zoomed = zoomed && state.current?.assetKind !== PhotoAssetKind.VideoClip;
		},

		retryImage(): Observable<void> {
			const slide = state.slides.find((candidate) => candidate.index === state.index);
			return slide ? retrySlide(slide) : of(undefined);
		},

		resizeImages(): Observable<void> {
			cancelImagePreloads();
			cancelViewerImageLoads();
			imagePreloadChanges$.next();
			return reloadViewerImages();
		},

		imageFailed(index: number): Observable<void> {
			const slide = state.slides.find((candidate) => candidate.index === index);
			if (!slide) return of(undefined);
			releaseSlideUrl(slide);
			slide.url = '';
			slide.loading = false;
			slide.error = true;
			nextSlideVersion(slideKey(slide.asset));
			return of(undefined);
		},

		retryInspection(): Observable<PhotoAssetInspectionDTO | null> {
			const identity = state.inspectionIdentity;
			if (!identity) return of(null);
			state.dialog.open = true;
			state.detailsOpen = true;
			state.dialog.error = false;
			return requestInspectionPage(identity, 1);
		},

		requestInspectionMembershipPage(page: number): Observable<PhotoAssetInspectionDTO | null> {
			const identity = state.inspectionIdentity;
			if (!identity || !Number.isSafeInteger(page) || page < 1) return of(null);
			state.detailsOpen = true;
			state.dialog.error = false;
			return requestInspectionPage(identity, page);
		},

		toggleAlbum(album: PhotoAlbumBrowseDTO, selected: boolean): void {
			setSelector({
				id: album.id,
				plexServerId: album.plexServerId,
				plexLibraryId: album.plexLibraryId,
				kind: PhotoItemKind.Album,
				type: PlexMediaType.PhotoAlbum,
				title: album.title,
				stale: false,
			}, selected);
		},

		toggleAsset(asset: PhotoAssetBrowseDTO, selected: boolean): void {
			setSelector({
				id: asset.id,
				plexServerId: asset.plexServerId,
				plexLibraryId: asset.plexLibraryId,
				kind: PhotoItemKind.Asset,
				type: PlexMediaType.PhotoImage,
				title: asset.title,
				stale: false,
			}, selected);
		},

		toggleFolder(folder: PhotoFolderBrowseDTO, selected: boolean): void {
			setSelector({
				id: folder.id,
				plexServerId: folder.plexServerId,
				plexLibraryId: folder.plexLibraryId,
				kind: PhotoItemKind.Folder,
				type: PlexMediaType.PhotoImage,
				title: folder.title,
				stale: false,
			}, selected);
		},

		setSelector(selector: IPhotoSelector, selected: boolean): void {
			setSelector(selector, selected);
		},

		isSelected(selector: Pick<IPhotoSelector, 'id' | 'plexServerId' | 'plexLibraryId' | 'kind'>): boolean {
			return state.selectedSelectors.some((item) => selectorKey(item) === selectorKey(selector));
		},

		clearSelection(): void {
			state.selectedSelectors = [];
			state.selectionStale = false;
			state.allVisibleSelected = false;
		},

		selectAll(selected: boolean): Observable<void> {
			cancelSelectionRequests();
			const generation = ++selectionGeneration;
			const session = sessionGeneration;
			const contextGenerationAtStart = contextGeneration;
			const context = currentContext(false);
			if (!state.active || state.disabledLibrary || !context) return of(undefined);
			state.selectionLoading = true;
			state.selectionError = false;
			return ensureSortMetadata(context, contextGeneration).pipe(
				switchMap((sortSupported) => sortSupported ? enumerateContext(context) : throwError(() => new Error('Photo sort metadata is unavailable'))),
				takeUntil(selectionCancel$),
				tap((selectors) => {
					if (!isSelectionCurrent(generation, session, contextGenerationAtStart, context.key)) return;
					const targetKeys = new Set(selectors.map(selectorKey));
					const unrelated = state.selectedSelectors.filter((selector) => !targetKeys.has(selectorKey(selector)));
					state.selectedSelectors = selected ? dedupeSelectors([...unrelated, ...selectors]) : unrelated;
					state.selectionStale = state.selectedSelectors.some((selector) => selector.stale);
					state.allVisibleSelected = selected;
				}),
				map(() => undefined),
				catchError((error) => {
					if (isSelectionCurrent(generation, session, contextGenerationAtStart, context.key)) {
						state.selectionError = true;
						state.allVisibleSelected = state.allVisibleSelected && !selected;
					}
					Log.debug('Photo selection enumeration failed', error);
					return of(undefined);
				}),
				finalize(() => {
					if (generation === selectionGeneration && session === sessionGeneration) state.selectionLoading = false;
				}),
			);
		},

		getDownloadSelectors(selectors: readonly IPhotoSelector[] = state.selectedSelectors): DownloadMediaDTO[] {
			const groups = new Map<string, DownloadMediaDTO>();
			for (const selector of selectors) {
				const groupKey = `${selector.plexServerId}:${selector.plexLibraryId}:${selector.kind}`;
				const current = groups.get(groupKey);
				if (current) {
					current.mediaIds.push(selector.id);
					continue;
				}
				groups.set(groupKey, {
					plexServerId: selector.plexServerId,
					plexLibraryId: selector.plexLibraryId,
					mediaIds: [selector.id],
					type: selector.type,
					selectionKind: selector.kind,
					qualities: [],
					keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
				});
			}
			return Array.from(groups.values());
		},

		setSearchQuery(query: string): void {
			const normalized = query.trim();
			if (state.searchQuery !== normalized) {
				contextGeneration += 1;
				cancelBrowseRequests();
				clearSurface('search');
				state.searchQuery = normalized;
				state.allVisibleSelected = false;
			}
		},

		setScrollIndex(index: number): void {
			if (!Number.isSafeInteger(index) || index < 0) return;
			state.scrollIndex = index;
			rememberScrollPosition(activeContextKey);
		},

		acknowledgeFocus(): void {
			state.focusIndex = null;
		},

		setImagePreloadCandidate(asset: PhotoAssetBrowseDTO, visible: boolean, owner: object): void {
			const key = imagePreloadKey(asset);
			const existing = imagePreloadCandidates.get(key);
			if (!visible) {
				releaseImagePreloadOwner(key, existing, owner);
				return;
			}
			if (asset.assetKind !== PhotoAssetKind.Image) return;
			if (!state.active || state.disabledLibrary) return;
			if (!libraryIsBrowseable(asset.plexLibraryId)) return;
			if (existing) {
				existing.asset = asset;
				existing.owners.add(owner);
				return;
			}
			imagePreloadCandidates.set(key, { asset, cancel$: new Subject<void>(), queued: false, owners: new Set([owner]) });
			imagePreloadChanges$.next();
		},

		$reset(): void {
			sessionGeneration += 1;
			activeOwner = null;
			state.active = false;
			pendingRefreshLibraries.clear();
			cancelBrowseRequests();
			clearImagePreloadCandidates();
			clearViewer();
			lifecycle$.next();
			lifecycle$.complete();
			setupSubscription?.unsubscribe();
			setupSubscription = null;
			libraryStoreUnsubscribe?.();
			accountStoreUnsubscribe?.();
			serverStoreUnsubscribe?.();
			accountStoreUnsubscribe = null;
			libraryStoreUnsubscribe = null;
			serverStoreUnsubscribe = null;
			setupComplete = false;
			observedQueues.clear();
			eligibleLibraryIds = new Set<number>();
			eligibilityMetadataKey = '';
			eligibilityInitialized = false;
			refreshInFlight = false;
			sortMetadataRequestKey = '';
			sortMetadataRequest$ = null;
			activeContextKey = '';
			contextGeneration += 1;
			selectionGeneration += 1;
			viewerGeneration += 1;
			inspectionGeneration += 1;
			for (const surface of surfaces()) clearSurface(surface);
			cacheOrder.length = 0;
			invalidateCachedContexts(null);
			rangeCancellers.clear();
			scrollHistory.clear();
			slideVersions.clear();
			Object.assign(state, cloneDeep(defaultState));
			lifecycle$ = new Subject<void>();
		},
	};

	// 5. Private helpers
	function releaseImagePreloadOwner(key: string, candidate: ImagePreloadCandidate | undefined, owner: object): void {
		if (!candidate) return;
		candidate.owners.delete(owner);
		if (candidate.owners.size === 0) removeImagePreloadCandidate(key);
	}

	function imagePreloadKey(asset: PhotoAssetBrowseDTO): string {
		return `${asset.plexServerId}:${asset.plexLibraryId}:${asset.id}`;
	}

	function removeImagePreloadCandidate(key: string): void {
		const candidate = imagePreloadCandidates.get(key);
		imagePreloadCandidates.delete(key);
		candidate?.cancel$.next();
		candidate?.cancel$.complete();
	}

	function clearImagePreloadCandidates(): void {
		for (const key of imagePreloadCandidates.keys()) removeImagePreloadCandidate(key);
	}

	function cancelImagePreloads(): void {
		imagePreloadSessions$.next();
		for (const candidate of imagePreloadCandidates.values()) candidate.queued = false;
	}

	function pauseImagePreloads(): void {
		if (state.dialog.open) cancelImagePreloads();
	}

	function pendingImagePreloads(): ImagePreloadCandidate[] {
		if (!state.active || state.disabledLibrary) return [];
		if (state.dialog.open || state.loading) return [];
		const candidates: ImagePreloadCandidate[] = [];
		for (const candidate of imagePreloadCandidates.values()) {
			if (candidate.queued) continue;
			candidate.queued = true;
			candidates.push(candidate);
		}
		return candidates;
	}

	function preloadCandidate(candidate: ImagePreloadCandidate): Observable<void> {
		return defer(() => {
			if (imagePreloadCandidates.get(imagePreloadKey(candidate.asset)) !== candidate) return EMPTY;
			if (!state.active || state.dialog.open) return EMPTY;
			if (!libraryIsBrowseable(candidate.asset.plexLibraryId)) return EMPTY;
			return mediaStore.preloadPhotoImage(candidate.asset).pipe(
				takeUntil(candidate.cancel$),
				catchError(() => EMPTY),
			);
		});
	}

	function createImagePreloadWorker(): Observable<void> {
		return imagePreloadChanges$.pipe(
			auditTime(IMAGE_PRELOAD_SETTLE_MS),
			mergeMap(() => from(pendingImagePreloads())),
			mergeMap(preloadCandidate, IMAGE_PRELOAD_CONCURRENCY),
		);
	}

	function invalidatePreloadedImages(libraryIds: ReadonlySet<number> | null): void {
		const affectsVisible = libraryIds === null || Array.from(imagePreloadCandidates.values())
			.some(({ asset }) => libraryIds.has(asset.plexLibraryId));
		if (affectsVisible) cancelImagePreloads();
		mediaStore.invalidatePhotoImages(libraryIds);
		if (affectsVisible) imagePreloadChanges$.next();
	}

	function surfaces(): Surface[] {
		return ['albums', 'assets', 'folders', 'folderAssets', 'albumMembers', 'search'];
	}

	function pageState(surface: Surface): IPhotoPageState<PageItem> {
		return state[surface];
	}

	function selectorKey(selector: Pick<IPhotoSelector, 'id' | 'plexServerId' | 'plexLibraryId' | 'kind'>): string {
		return `${selector.plexServerId}:${selector.plexLibraryId}:${selector.kind}:${selector.id}`;
	}

	function dedupeSelectors(selectors: IPhotoSelector[]): IPhotoSelector[] {
		return uniqBy(selectors, selectorKey);
	}

	function setSelector(selector: IPhotoSelector, selected: boolean): void {
		const key = selectorKey(selector);
		const existing = state.selectedSelectors.findIndex((item) => selectorKey(item) === key);
		if (selected && existing < 0) state.selectedSelectors.push(selector);
		if (!selected && existing >= 0) state.selectedSelectors.splice(existing, 1);
		state.selectionStale = state.selectedSelectors.some((item) => item.stale);
		state.allVisibleSelected = false;
	}

	function photoSortView(route: Pick<IPhotoBrowseRoute, 'mode' | 'view' | 'searchQuery'>): PhotoItemKind {
		if (route.mode === 'album' || route.view === 'assets' || route.searchQuery.trim()) return PhotoItemKind.Asset;
		if (route.view === 'folders') return PhotoItemKind.Folder;
		return PhotoItemKind.Album;
	}

	function photoSortMetadataKey(route: IPhotoBrowseRoute): string {
		return `${route.libraryId}:${photoSortView(route)}`;
	}

	function clearSortMetadata(): void {
		state.supportedSorts = [];
		state.sortMetadataProfileKey = '';
		state.sortMetadataReady = false;
	}

	function contextKey(route: IPhotoBrowseRoute): string {
		return [
			route.mode,
			route.libraryId,
			route.albumId ?? 0,
			route.view,
			route.folderId ?? 0,
			serializeFlexSortDsl(route.sort),
			route.searchQuery.trim(),
		].join(':');
	}

	function currentContext(force: boolean): PhotoBrowseReadContext | null {
		if (!activeContextKey) return null;
		return {
			mode: state.mode,
			libraryId: state.libraryId,
			albumId: state.albumId ?? undefined,
			view: state.view,
			folderId: state.folderId,
			searchQuery: state.searchQuery,
			sort: state.sort,
			force,
			key: activeContextKey,
		};
	}

	function isCurrentContext(context: PhotoBrowseReadContext, generation = contextGeneration): boolean {
		return state.active && generation === contextGeneration && context.key === activeContextKey;
	}

	function ensureSortMetadata(context: PhotoBrowseReadContext, generation: number): Observable<boolean> {
		const key = photoSortMetadataKey(context);
		if (state.sortMetadataReady && state.sortMetadataProfileKey === key) {
			return of(state.supportedSorts.includes(context.sort.field));
		}
		if (sortMetadataRequest$ && sortMetadataRequestKey === key) return sortMetadataRequest$;

		sortMetadataRequestKey = key;
		const request$ = plexLibraryApi.getLibraryMediaMetadata(context.libraryId, {
			mediaType: PlexMediaType.PhotoAlbum,
			photoView: photoSortView(context),
		}, photoBrowseRequestParams).pipe(
			takeUntil(browseCancel$),
			map((response) => apiValue(response)),
			tap((metadata) => {
				if (!isCurrentContext(context, generation)) return;
				state.sortMetadataProfileKey = key;
				state.sortMetadataReady = metadata !== null;
				state.supportedSorts = metadata?.supportedSorts ? [...metadata.supportedSorts] : [];
				state.error = metadata === null;
				state.reloadError = false;
			}),
			map((metadata) => metadata !== null
				&& isCurrentContext(context, generation)
				&& state.supportedSorts.includes(context.sort.field)),
			catchError((error) => {
				if (isCurrentContext(context, generation)) {
					clearSortMetadata();
					state.error = true;
					state.reloadError = false;
				}
				Log.debug('Photo sort metadata request failed', error);
				return of(false);
			}),
			finalize(() => {
				if (sortMetadataRequest$ === request$) {
					sortMetadataRequest$ = null;
					sortMetadataRequestKey = '';
				}
			}),
			shareReplay({ bufferSize: 1, refCount: true }),
		);
		sortMetadataRequest$ = request$;
		return request$;
	}

	function apiValue<T>(response: ApiResult<T>): T | null {
		return response?.isSuccess ? response.value ?? null : null;
	}

	function responseStatus(error: unknown): number | undefined {
		const candidate = error as { statusCode?: number; response?: { status?: number } } | undefined;
		return isAxiosError(error) ? error.response?.status : candidate?.statusCode ?? candidate?.response?.status;
	}

	function filterEligibleItems<T extends { plexLibraryId: number }>(items: T[]): T[] {
		if (!eligibilityInitialized) return items;
		return items.filter((item) => eligibleLibraryIds.has(item.plexLibraryId));
	}

	function syncPageState(surface: Surface): void {
		const page = pageState(surface);
		page.items = Array.from(feedMaps[surface].entries())
			.sort(([left], [right]) => left - right)
			.map(([, item]) => item);
		page.loaded = loadedPages[surface].size > 0;
		page.lastLoadedPage = page.loaded ? Math.max(...loadedPages[surface]) : 0;
	}

	function clearPage<T>(page: IPhotoPageState<T>): void {
		page.items = [];
		page.page = 1;
		page.lastLoadedPage = 0;
		page.pageSize = PAGE_SIZE;
		page.totalCount = 0;
		page.totalBytes = 0;
		page.totalAssetBytes = 0;
		page.loading = false;
		page.loaded = false;
		page.error = false;
	}

	function clearSurface(surface: Surface): void {
		feedMaps[surface].clear();
		loadedPages[surface].clear();
		const page = pageState(surface);
		clearPage(page);
	}

	function clearContextResults(): void {
		clearForcedReadIdentities();
		for (const surface of surfaces()) clearSurface(surface);
		cacheOrder.length = 0;
		state.folder = null;
		state.breadcrumbs = [];
		state.inspectionAlbums = null;
		state.contextMissing = false;
		state.error = false;
		state.reloadError = false;
		state.selectionError = false;
		state.allVisibleSelected = false;
	}

	function clearForcedReadIdentities(): void {
		for (const surface of surfaces()) forcedReadIdentities[surface].clear();
	}

	function prepareForcedRead(force: boolean): void {
		if (!force) return;
		cancelBrowseRequests();
		invalidatePreloadedImages(state.libraryId > 0 && !state.searchQuery ? new Set([state.libraryId]) : null);
		contextGeneration += 1;
		for (const surface of surfaces()) {
			for (const [index, item] of feedMaps[surface]) {
				const pageNumber = Math.floor(index / PAGE_SIZE) + 1;
				const identities = forcedReadIdentities[surface].get(pageNumber) ?? [];
				identities.push({
					id: item.id,
					plexServerId: item.plexServerId,
					plexLibraryId: item.plexLibraryId,
				});
				forcedReadIdentities[surface].set(pageNumber, identities);
				if (index >= PAGE_SIZE) feedMaps[surface].delete(index);
			}
			loadedPages[surface].clear();
			syncPageState(surface);
		}
		compactCacheOrder();
	}

	function takeCachedContext(key: string): CachedBrowseContext | undefined {
		const cached = contextCache.get(key);
		if (cached) {
			contextCache.delete(key);
			cachedItemCount -= cached.itemCount;
		}
		return cached;
	}

	function rememberContextResults(): void {
		const context = currentContext(false);
		if (!context) return;
		if (state.disabledLibrary || state.contextMissing) return;
		if (!surfaces().some((surface) => loadedPages[surface].size > 0)) return;
		const cachedSurfaces: CachedBrowseContext['surfaces'] = surfaces().map((surface) => {
			const items = new Map<number, PageItem>();
			for (const [index, item] of feedMaps[surface]) {
				if (loadedPages[surface].has(Math.floor(index / PAGE_SIZE) + 1)) items.set(index, item);
			}
			return {
				surface,
				items,
				loadedPages: new Set(loadedPages[surface]),
				page: pick(pageState(surface), ['page', 'pageSize', 'totalCount', 'totalBytes', 'totalAssetBytes']),
			};
		});
		takeCachedContext(context.key);
		const itemCount = cachedSurfaces.reduce((total, entry) => total + entry.items.size, 0);
		contextCache.set(context.key, {
			context, surfaces: cachedSurfaces, folder: state.folder, breadcrumbs: state.breadcrumbs, itemCount,
		});
		cachedItemCount += itemCount;
		while (contextCache.size > HISTORY_LIMIT) takeCachedContext(contextCache.keys().next().value!);
	}

	function restoreContextResults(key: string): void {
		const cached = takeCachedContext(key);
		if (!cached) return;
		for (const entry of cached.surfaces) {
			for (const [index, item] of entry.items) {
				feedMaps[entry.surface].set(index, item);
				cacheOrder.push({ surface: entry.surface, index });
			}
			loadedPages[entry.surface] = entry.loadedPages;
			Object.assign(pageState(entry.surface), entry.page);
			syncPageState(entry.surface);
		}
		state.folder = cached.folder;
		state.breadcrumbs = cached.breadcrumbs;
	}

	function invalidateCachedContexts(libraryIds: ReadonlySet<number> | null): void {
		invalidatePreloadedImages(libraryIds);
		for (const [key, cached] of contextCache) {
			const globalContext = cached.context.libraryId === 0 || cached.context.searchQuery.length > 0;
			const affectedLibrary = libraryIds === null || libraryIds.has(cached.context.libraryId);
			if (globalContext || affectedLibrary) takeCachedContext(key);
		}
	}

	function compactCacheOrder(): void {
		const live = new Set<string>();
		for (const surface of surfaces()) {
			for (const index of feedMaps[surface].keys()) live.add(`${surface}:${index}`);
		}
		const compacted = cacheOrder.filter((entry) => live.has(`${entry.surface}:${entry.index}`));
		cacheOrder.splice(0, cacheOrder.length, ...compacted);
	}

	function cacheSurfaceItem(surface: Surface, index: number, item: PageItem): void {
		if (!feedMaps[surface].has(index)) cacheOrder.push({ surface, index });
		feedMaps[surface].set(index, item);
	}

	function trimCache(): void {
		const size = surfaces().reduce((total, surface) => total + feedMaps[surface].size, 0);
		let remaining = size;
		while (remaining + cachedItemCount > RANGE_CACHE_LIMIT && contextCache.size > 0) {
			takeCachedContext(contextCache.keys().next().value!);
		}
		while (remaining > RANGE_CACHE_LIMIT && cacheOrder.length > 0) {
			const oldest = cacheOrder.shift()!;
			if (!feedMaps[oldest.surface].has(oldest.index)) continue;
			feedMaps[oldest.surface].delete(oldest.index);
			remaining -= 1;
			const pageNumber = Math.floor(oldest.index / PAGE_SIZE) + 1;
			loadedPages[oldest.surface].delete(pageNumber);
		}
		for (const surface of surfaces()) syncPageState(surface);
	}

	function removePageItems(surface: Surface, page: number): PageItem[] {
		const first = (page - 1) * PAGE_SIZE;
		const previous: PageItem[] = [];
		for (let index = first; index < first + PAGE_SIZE; index += 1) {
			const item = feedMaps[surface].get(index);
			if (item) previous.push(item);
			feedMaps[surface].delete(index);
		}
		compactCacheOrder();
		return previous;
	}

	function markSelectorsMissing<T extends { id: number; plexServerId: number; plexLibraryId: number }>(
		previousItems: T[],
		nextItems: T[],
		kind: PhotoItemKind,
	): void {
		const previousKeys = new Set(previousItems.map((item) => `${item.plexServerId}:${item.plexLibraryId}:${item.id}`));
		const nextKeys = new Set(nextItems.map((item) => `${item.plexServerId}:${item.plexLibraryId}:${item.id}`));
		state.selectedSelectors = state.selectedSelectors.map((selector) => {
			const key = `${selector.plexServerId}:${selector.plexLibraryId}:${selector.id}`;
			return selector.kind === kind && previousKeys.has(key) && !nextKeys.has(key)
				? { ...selector, stale: true }
				: selector;
		});
		state.selectionStale = state.selectedSelectors.some((selector) => selector.stale);
	}

	function mergePage<T extends PageItem>(
		surface: Surface,
		pageNumber: number,
		items: T[],
		totalCount: number,
		totalBytes: number,
		totalAssetBytes: number,
		force: boolean,
	): void {
		const previousItems = removePageItems(surface, pageNumber);
		let kind = PhotoItemKind.Asset;
		if (surface === 'albums') kind = PhotoItemKind.Album;
		else if (surface === 'folders') kind = PhotoItemKind.Folder;
		if (force) markSelectorsMissing([...previousItems, ...(forcedReadIdentities[surface].get(pageNumber) ?? [])], items, kind);
		const first = (pageNumber - 1) * PAGE_SIZE;
		items.forEach((item, offset) => cacheSurfaceItem(surface, first + offset, item));
		loadedPages[surface].add(pageNumber);
		const target = pageState(surface);
		target.page = pageNumber;
		target.pageSize = PAGE_SIZE;
		target.totalCount = totalCount;
		target.totalBytes = totalBytes;
		target.totalAssetBytes = totalAssetBytes;
		target.error = false;
		target.loaded = true;
		trimCache();
		state.error = false;
		state.reloadError = false;
		state.count = assetSequenceCount();
		if (force && surface === activeAssetSurface() && currentAssetKnownMissing()) closeViewerForInvalidAsset();
	}

	function handlePageFailure(surface: Surface, statusCode?: number): void {
		pageState(surface).error = true;
		state.reloadError = pageState(surface).items.length > 0;
		state.error = pageState(surface).items.length === 0;
		if (surface === 'albumMembers' && statusCode === 404) markMissingAlbumContext();
	}

	function markMissingAlbumContext(): void {
		state.selectedSelectors = state.selectedSelectors.map((selector) =>
			selector.plexLibraryId === state.libraryId && selector.kind === PhotoItemKind.Album && selector.id === state.albumId
				? { ...selector, stale: true }
				: selector,
		);
		state.selectionStale = state.selectedSelectors.some((selector) => selector.stale);
		if (state.dialog.open) closeViewerForInvalidAsset();
		clearSurface('albumMembers');
		state.contextMissing = true;
		state.error = true;
		state.reloadError = false;
	}

	function markMissingFolderContext(): void {
		state.selectedSelectors = state.selectedSelectors.map((selector) =>
			selector.plexLibraryId === state.libraryId && selector.kind === PhotoItemKind.Folder && selector.id === state.folderId
				? { ...selector, stale: true }
				: selector,
		);
		state.selectionStale = state.selectedSelectors.some((selector) => selector.stale);
		if (state.dialog.open) closeViewerForInvalidAsset();
		clearSurface('folders');
		clearSurface('folderAssets');
		state.folder = null;
		state.breadcrumbs = [];
		state.contextMissing = true;
		state.error = true;
		state.reloadError = false;
	}

	function markTransientFolderError(): void {
		state.folders.error = true;
		state.folderAssets.error = true;
		state.contextMissing = false;
		state.reloadError = state.folders.items.length > 0 || state.folderAssets.items.length > 0;
		state.error = !state.reloadError;
	}

	function cancelSelectionRequests(): void {
		selectionCancel$.next();
		selectionCancel$.complete();
		selectionCancel$ = new Subject<void>();
		selectionGeneration += 1;
		state.selectionLoading = false;
	}

	function cancelViewerImageLoads(): void {
		viewerCancel$.next();
		viewerCancel$.complete();
		viewerCancel$ = new Subject<void>();
		viewerImageGeneration += 1;
	}

	function cancelViewerLoads(): void {
		viewerGeneration += 1;
		cancelViewerImageLoads();
	}

	function cancelBrowseRequests(): void {
		clearForcedReadIdentities();
		cancelImagePreloads();
		browseCancel$.next();
		browseCancel$.complete();
		browseCancel$ = new Subject<void>();
		sortMetadataRequestKey = '';
		sortMetadataRequest$ = null;
		cancelSelectionRequests();
		cancelViewerLoads();
		for (const canceler of rangeCancellers.values()) {
			canceler.next();
			canceler.complete();
		}
		rangeCancellers.clear();
		state.loading = false;
		state.dialog.loading = false;
	}

	function libraryIsBrowseable(libraryId: number): boolean {
		const library = libraryStore.getLibrary(libraryId);
		if (!library?.isEnabled) return false;
		if (accountStore.accounts.length === 0) return true;
		return accountStore.accounts.some((account) => account.plexServerAccess.includes(library.plexServerId))
			&& accountStore.accounts.some((account) => account.plexLibraryAccess.includes(libraryId));
	}

	function activeLibraryEnabled(): boolean {
		if (state.libraryId <= 0) return true;
		return getEligibleLibraryIds().has(state.libraryId);
	}

	function purgeInspectionForLibrary(libraryId: number): void {
		if (state.dialog.asset?.plexLibraryId !== libraryId && state.inspectionIdentity?.libraryId !== libraryId) return;
		inspectionGeneration += 1;
		state.inspectionIdentity = null;
		state.inspectionAlbums = null;
		state.dialog = { open: false, loading: false, error: false, asset: null };
		state.detailsOpen = false;
	}

	function removeLibraryFromResults(libraryId: number): void {
		for (const surface of surfaces()) {
			const map = feedMaps[surface];
			for (const [index, item] of map.entries()) {
				if (item.plexLibraryId === libraryId) map.delete(index);
			}
			syncPageState(surface);
		}
		compactCacheOrder();
		state.selectedSelectors = state.selectedSelectors.filter((selector) => selector.plexLibraryId !== libraryId);
		state.selectionStale = state.selectedSelectors.some((selector) => selector.stale);
		if (state.folder?.plexLibraryId === libraryId) {
			state.folder = null;
			state.breadcrumbs = [];
			state.contextMissing = false;
		}
		if (state.current?.plexLibraryId === libraryId && state.dialog.open) closeViewerForInvalidAsset();
		purgeInspectionForLibrary(libraryId);
	}

	function isPhotoLibrary(libraryId: number): boolean {
		return libraryStore.getLibrary(libraryId)?.type === PlexMediaType.PhotoAlbum;
	}

	function getEligibleLibraryIds(): Set<number> {
		return new Set(
			libraryStore.libraries
				.filter((library) => {
					const server = serverStore.getServer(library.plexServerId);
					const serverEnabled = serverStore.servers.length === 0 || !!server?.isEnabled;
					return library.type === PlexMediaType.PhotoAlbum && serverEnabled && libraryIsBrowseable(library.id);
				})
				.map((library) => library.id),
		);
	}

	function getEligibilityMetadataKey(): string {
		const libraries = [...libraryStore.libraries]
			.sort((left, right) => left.id - right.id)
			.map((library) => [library.id, library.type, library.isEnabled, library.plexServerId, library.title, library.updatedAt ?? '', library.syncedAt ?? ''].join(':'))
			.join('|');
		const accounts = [...accountStore.accounts]
			.sort((left, right) => left.id - right.id)
			.map((account) => [account.id, account.plexLibraryAccess.slice().sort((left, right) => left - right).join(','), account.plexServerAccess.slice().sort((left, right) => left - right).join(',')].join(':'))
			.join('|');
		const servers = [...serverStore.servers]
			.sort((left, right) => left.id - right.id)
			.map((server) => [server.id, server.isEnabled, server.name, server.machineIdentifier].join(':'))
			.join('|');
		return `${libraries}#${accounts}#${servers}`;
	}

	function updateEligibilityMetadata(metadataKey: string): boolean {
		const changed = eligibilityInitialized && eligibilityMetadataKey !== metadataKey;
		if (!eligibilityInitialized || changed) invalidateCachedContexts(null);
		eligibilityMetadataKey = metadataKey;
		eligibilityInitialized = true;
		return changed;
	}

	function syncEligibility(): void {
		const nextEligible = getEligibleLibraryIds();
		const changed = updateEligibilityMetadata(getEligibilityMetadataKey());
		const lost = Array.from(eligibleLibraryIds).filter((libraryId) => !nextEligible.has(libraryId));
		eligibleLibraryIds = nextEligible;
		for (const libraryId of lost) removeLibraryFromResults(libraryId);
		const wasDisabled = state.disabledLibrary;
		state.disabledLibrary = state.libraryId > 0 && !nextEligible.has(state.libraryId);
		if (!state.active) return;
		if (!changed && !wasDisabled) return;
		if (state.disabledLibrary) cancelBrowseRequests();
		else actions.loadCurrent(true).subscribe();
	}

	function handleLibraryState(): void {
		syncEligibility();
		observeQueueCompletions();
	}

	function queueIdentity(queue: LibrarySyncJobQueueDTO): string {
		return `${queue.plexLibraryId}:${queue.createdAt}`;
	}

	function queueAffectsContext(libraryId: number): boolean {
		return state.searchQuery.length > 0 || state.libraryId === 0 || state.libraryId === libraryId;
	}

	function refreshAffectedLibraries(libraryIds: number[]): void {
		invalidateCachedContexts(new Set(libraryIds));
		if (!state.active) return;
		const eligible = libraryIds.filter((libraryId) => eligibleLibraryIds.has(libraryId));
		refreshInspectionFor(eligible);
		for (const libraryId of eligible.filter(queueAffectsContext)) pendingRefreshLibraries.add(libraryId);
		flushPendingRefresh();
	}

	function refreshInspectionFor(libraryIds: number[]): void {
		const identity = state.inspectionIdentity;
		if (!state.dialog.open || !identity || !libraryIds.includes(identity.libraryId)) return;
		requestInspectionPage(identity, state.inspectionAlbums?.page ?? 1).subscribe();
	}

	function flushPendingRefresh(): void {
		if (refreshInFlight || pendingRefreshLibraries.size === 0) return;
		pendingRefreshLibraries.clear();
		refreshInFlight = true;
		actions.loadCurrent(true).pipe(finalize(() => {
			refreshInFlight = false;
			flushPendingRefresh();
		})).subscribe();
	}

	function observeQueueCompletion(queue: LibrarySyncJobQueueDTO): boolean {
		const key = queueIdentity(queue);
		const previous = observedQueues.get(key);
		const eligible = eligibleLibraryIds.has(queue.plexLibraryId);
		observedQueues.set(key, { status: queue.status, completedAt: queue.completedAt, eligible });
		if (!eligible || queue.status !== LibrarySyncJobStatus.Completed) return false;
		if (!previous) return true;
		return previous.status !== LibrarySyncJobStatus.Completed || previous.completedAt !== queue.completedAt || !previous.eligible;
	}

	function seedQueueObservations(): void {
		for (const queue of libraryStore.syncQueues.filter((item) => isPhotoLibrary(item.plexLibraryId))) {
			observedQueues.set(queueIdentity(queue), {
				status: queue.status,
				completedAt: queue.completedAt,
				eligible: eligibleLibraryIds.has(queue.plexLibraryId),
			});
		}
	}

	function observeQueueCompletions(): void {
		const queues = libraryStore.syncQueues.filter((queue) => isPhotoLibrary(queue.plexLibraryId));
		const currentKeys = new Set(queues.map(queueIdentity));
		const completed = queues.filter(observeQueueCompletion).map((queue) => queue.plexLibraryId);
		for (const key of observedQueues.keys()) if (!currentKeys.has(key)) observedQueues.delete(key);
		if (completed.length > 0) refreshAffectedLibraries([...new Set(completed)]);
	}

	function rememberScrollPosition(key: string): void {
		if (!key) return;
		scrollHistory.delete(key);
		scrollHistory.set(key, state.scrollIndex);
		while (scrollHistory.size > HISTORY_LIMIT) scrollHistory.delete(scrollHistory.keys().next().value!);
	}

	function overviewFolderCount(): number {
		return !state.searchQuery && state.view === 'folders' ? state.folders.totalCount : 0;
	}

	function activeAssetPage(): IPhotoPageState<PhotoAssetBrowseDTO> {
		if (state.searchQuery) return state.search;
		if (state.mode === 'album') return state.albumMembers;
		if (state.view === 'folders') return state.folderAssets;
		return state.assets;
	}

	function activeAssetSurface(): Surface {
		if (state.searchQuery) return 'search';
		if (state.mode === 'album') return 'albumMembers';
		if (state.view === 'folders') return 'folderAssets';
		return 'assets';
	}
	function currentAssetKnownMissing(): boolean {
		if (!state.dialog.open || !state.current) return false;
		const surface = activeAssetSurface();
		const total = pageState(surface).totalCount;
		const pageCount = Math.ceil(total / PAGE_SIZE);
		for (let page = 1; page <= pageCount; page += 1) {
			if (!loadedPages[surface].has(page)) return false;
		}
		return !Array.from(feedMaps[surface].values()).some((candidate) =>
			candidate.id === state.current?.id
			&& candidate.plexLibraryId === state.current?.plexLibraryId
			&& candidate.plexServerId === state.current?.plexServerId,
		);
	}

	function assetSequenceCount(): number {
		return activeAssetPage().totalCount;
	}

	function overviewCountValue(): number {
		if (state.searchQuery) return state.search.totalCount;
		if (state.view === 'folders') return state.folders.totalCount + state.folderAssets.totalCount;
		if (state.view === 'albums') return state.albums.totalCount;
		return activeAssetPage().totalCount;
	}

	function findOverviewIndex(asset: PhotoAssetBrowseDTO): number {
		const surface = activeAssetSurface();
		for (const [index, candidate] of feedMaps[surface].entries()) {
			if (candidate.id === asset.id && candidate.plexLibraryId === asset.plexLibraryId && candidate.plexServerId === asset.plexServerId) {
				return !state.searchQuery && state.view === 'folders' ? overviewFolderCount() + index : index;
			}
		}
		return -1;
	}

	function normalizeAssetIndex(index: number, count = assetSequenceCount()): number {
		if (count <= 0) return 0;
		const normalized = Math.trunc(index) % count;
		return normalized < 0 ? normalized + count : normalized;
	}

	function desiredAssetIndexes(index: number, count: number): number[] {
		if (count <= 0) return [];
		if (count === 1) return [0];
		const candidates = [normalizeAssetIndex(index, count), normalizeAssetIndex(index + viewerDirection, count), normalizeAssetIndex(index - viewerDirection, count)];
		return [...new Set(candidates)];
	}

	function assetOverviewIndex(sequenceIndex: number): number {
		if (state.searchQuery) return sequenceIndex;
		return state.view === 'folders' ? overviewFolderCount() + sequenceIndex : sequenceIndex;
	}

	function contextRangePages(start: number, end: number, total: number, prefix = 0): number[] {
		if (total <= 0 || end <= prefix || start >= prefix + total) return [];
		const first = Math.max(0, start - prefix);
		const last = Math.min(total, end - prefix);
		const pages: number[] = [];
		const lastPage = Math.ceil(last / PAGE_SIZE);
		for (let page = Math.floor(first / PAGE_SIZE) + 1; page <= lastPage; page++) pages.push(page);
		return pages;
	}

	function knownOverviewCount(): number | null {
		let surface: Surface;
		if (!state.searchQuery && state.view === 'folders') surface = 'folders';
		else if (!state.searchQuery && state.view === 'albums') surface = 'albums';
		else surface = activeAssetSurface();
		return loadedPages[surface].size > 0 ? overviewCountValue() : null;
	}

	function requestRangeInternal(startValue: number, endValue: number, force: boolean): Observable<void> {
		const start = Number.isSafeInteger(startValue) ? Math.max(0, startValue) : 0;
		const end = Number.isSafeInteger(endValue) ? Math.max(start, endValue) : start;
		if (!state.active || state.disabledLibrary || end <= start) return of(undefined);
		const knownCount = force ? null : knownOverviewCount();
		if (knownCount !== null && start >= knownCount) return of(undefined);
		const boundedEnd = knownCount === null ? end : Math.min(end, knownCount);
		const context = currentContext(force);
		if (!context || boundedEnd <= start) return of(undefined);
		const generation = contextGeneration;
		const rangeKey = `${context.key}:${start}:${boundedEnd}`;
		const previousCanceller = rangeCancellers.get(rangeKey);
		previousCanceller?.next();
		previousCanceller?.complete();
		const rangeCanceller = new Subject<void>();
		rangeCancellers.set(rangeKey, rangeCanceller);
		state.loading = true;
		state.error = false;
		return ensureSortMetadata(context, generation).pipe(
			takeUntil(rangeCanceller),
			switchMap((sortSupported) => sortSupported
				? requestContextRange(context, start, boundedEnd, force, generation, rangeCanceller)
				: of(undefined)),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) {
					state.error = true;
					state.reloadError = overviewCountValue() > 0;
				}
				Log.debug('Photo browse range request failed', error);
				return of(undefined);
			}),
			finalize(() => {
				if (isCurrentContext(context, generation)) state.loading = false;
				if (rangeCancellers.get(rangeKey) === rangeCanceller) rangeCancellers.delete(rangeKey);
			}),
			takeUntil(browseCancel$),
		);
	}

	function requestContextRange(
		context: PhotoBrowseReadContext,
		start: number,
		end: number,
		force: boolean,
		generation: number,
		rangeCanceller: Subject<void>,
	): Observable<void> {
		if (context.searchQuery) return requestSimpleAssetRange('search', context, start, end, force, generation, rangeCanceller);
		if (context.mode === 'album' && context.albumId) return requestSimpleAssetRange('albumMembers', context, start, end, force, generation, rangeCanceller);
		if (context.view === 'albums') return requestSimpleAlbumRange(context, start, end, force, generation, rangeCanceller);
		if (context.view === 'folders') {
			if (context.libraryId <= 0) return of(undefined);
			return context.folderId
				? requestFolderRange(context, start, end, force, generation, rangeCanceller)
				: requestRootRange(context, start, end, force, generation, rangeCanceller);
		}
		return requestSimpleAssetRange('assets', context, start, end, force, generation, rangeCanceller);
	}

	function requestSimpleAlbumRange(
		context: PhotoBrowseReadContext,
		start: number,
		end: number,
		force: boolean,
		generation: number,
		rangeCanceller: Subject<void>,
	): Observable<void> {
		const firstPage = Math.floor(start / PAGE_SIZE) + 1;
		const lastPage = Math.floor((end - 1) / PAGE_SIZE) + 1;
		const needInitial = force || !loadedPages.albums.has(1);
		const initial = needInitial ? requestAlbumsPage(context, 1, force, generation, rangeCanceller) : of(undefined);
		return initial.pipe(
			switchMap(() => {
				if (!isCurrentContext(context, generation)) return of(undefined);
				if (needInitial && !loadedPages.albums.has(1)) return of(undefined);
				const requests: Observable<void>[] = [];
				for (let page = firstPage; page <= lastPage; page += 1) {
					if (page === 1 && needInitial) continue;
					if (force || !loadedPages.albums.has(page)) requests.push(requestAlbumsPage(context, page, force, generation, rangeCanceller));
				}
				return requests.length > 0 ? forkJoin(requests).pipe(map(() => undefined)) : of(undefined);
			}),
		);
	}

	function requestSimpleAssetRange(
		surface: 'assets' | 'search' | 'albumMembers',
		context: PhotoBrowseReadContext,
		start: number,
		end: number,
		force: boolean,
		generation: number,
		rangeCanceller: Subject<void>,
	): Observable<void> {
		const firstPage = Math.floor(start / PAGE_SIZE) + 1;
		const lastPage = Math.floor((end - 1) / PAGE_SIZE) + 1;
		const needInitial = force || !loadedPages[surface].has(1);
		const initial = needInitial ? requestAssetsPage(surface, context, 1, force, generation, rangeCanceller) : of(undefined);
		return initial.pipe(
			switchMap(() => {
				if (!isCurrentContext(context, generation)) return of(undefined);
				if (needInitial && !loadedPages[surface].has(1)) return of(undefined);
				const total = pageState(surface).totalCount;
				const requests: Observable<void>[] = [];
				for (let page = firstPage; page <= lastPage; page += 1) {
					if (page === 1 && needInitial) continue;
					if (total > 0 && (page - 1) * PAGE_SIZE >= total) continue;
					if (force || !loadedPages[surface].has(page)) requests.push(requestAssetsPage(surface, context, page, force, generation, rangeCanceller));
				}
				return requests.length > 0 ? forkJoin(requests).pipe(map(() => undefined)) : of(undefined);
			}),
		);
	}

	function requestRootRange(
		context: PhotoBrowseReadContext,
		start: number,
		end: number,
		force: boolean,
		generation: number,
		rangeCanceller: Subject<void>,
	): Observable<void> {
		const needInitial = force || !loadedPages.folders.has(1) || !loadedPages.folderAssets.has(1);
		const initial = needInitial
			? forkJoin([
					requestRootsPage(context, 1, force, generation, rangeCanceller),
					requestUnmappedAssetsPage(context, 1, force, generation, rangeCanceller),
				]).pipe(map(() => undefined))
			: of(undefined);
		return initial.pipe(
			switchMap(() => {
				if (!isCurrentContext(context, generation)) return of(undefined);
				const folderPages = contextRangePages(start, end, state.folders.totalCount, 0);
				const assetPages = contextRangePages(start, end, state.folderAssets.totalCount, state.folders.totalCount);
				const requests: Observable<void>[] = [];
				for (const page of folderPages) if (force || !loadedPages.folders.has(page)) requests.push(requestRootsPage(context, page, force, generation, rangeCanceller));
				for (const page of assetPages) if (force || !loadedPages.folderAssets.has(page)) requests.push(requestUnmappedAssetsPage(context, page, force, generation, rangeCanceller));
				return requests.length > 0 ? forkJoin(requests).pipe(map(() => undefined)) : of(undefined);
			}),
		);
	}

	function requestFolderRange(
		context: PhotoBrowseReadContext,
		start: number,
		end: number,
		force: boolean,
		generation: number,
		rangeCanceller: Subject<void>,
	): Observable<void> {
		const needInitial = force || !loadedPages.folders.has(1) || !loadedPages.folderAssets.has(1);
		const initial = needInitial
			? requestFolderChildrenPage(context, 1, 1, force, generation, rangeCanceller)
			: of(undefined);
		return initial.pipe(
			switchMap(() => {
				if (!isCurrentContext(context, generation)) return of(undefined);
				const folderPages = contextRangePages(start, end, state.folders.totalCount, 0);
				const assetPages = contextRangePages(start, end, state.folderAssets.totalCount, state.folders.totalCount);
				const missingFolders = folderPages.filter((page) => force || !loadedPages.folders.has(page));
				const missingAssets = assetPages.filter((page) => force || !loadedPages.folderAssets.has(page));
				const count = Math.max(missingFolders.length, missingAssets.length);
				const requests: Observable<void>[] = [];
				for (let index = 0; index < count; index += 1) {
					const folderPage = missingFolders[index] ?? 1;
					const assetPage = missingAssets[index] ?? 1;
					requests.push(requestFolderChildrenPage(context, folderPage, assetPage, force, generation, rangeCanceller));
				}
				return requests.length > 0 ? forkJoin(requests).pipe(map(() => undefined)) : of(undefined);
			}),
		);
	}

	function requestAlbumsPage(context: PhotoBrowseReadContext, page: number, force: boolean, generation: number, rangeCanceller: Subject<void>): Observable<void> {
		if (!force && loadedPages.albums.has(page)) return of(undefined);
		state.albums.loading = true;
		const libraryId = context.mode === 'aggregate' ? 0 : context.libraryId;
		const query = { page, size: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort), ...(libraryId > 0 ? { plexLibraryId: libraryId } : {}) };
		return plexPhotoApi.getPhotoAlbumsEndpoint(query, photoBrowseRequestParams).pipe(
			takeUntil(rangeCanceller),
			map((response) => ({ value: apiValue<PhotoAlbumPageDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (!isCurrentContext(context, generation) || !value) {
					if (!value && isCurrentContext(context, generation)) handlePageFailure('albums', statusCode);
					return;
				}
				mergePage('albums', page, filterEligibleItems(value.items), value.totalCount, 0, value.totalAssetBytes, force);
			}),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) handlePageFailure('albums', responseStatus(error));
				Log.debug('Photo album range request failed', error);
				return of(undefined);
			}),
			finalize(() => { if (isCurrentContext(context, generation)) state.albums.loading = false; }),
		);
	}

	function assetPageRequest(
		surface: 'assets' | 'search' | 'albumMembers',
		context: PhotoBrowseReadContext,
		page: number,
	): Observable<ApiResult<PhotoAssetPageDTO>> {
		const sort = serializeFlexSortDsl(context.sort);
		switch (surface) {
			case 'search':
				return plexPhotoApi.searchPhotoAssetsEndpoint({ q: context.searchQuery, page, size: PAGE_SIZE, sort }, photoBrowseRequestParams);
			case 'albumMembers':
				return plexPhotoApi.getPhotoAlbumMembersEndpoint(context.libraryId, context.albumId!, { page, size: PAGE_SIZE, sort }, photoBrowseRequestParams);
			default:
				return plexPhotoApi.getPhotoAssetsEndpoint({ page, size: PAGE_SIZE, sort, ...(context.mode === 'aggregate' ? {} : { plexLibraryId: context.libraryId }) }, photoBrowseRequestParams);
		}
	}

	function requestAssetsPage(surface: 'assets' | 'search' | 'albumMembers', context: PhotoBrowseReadContext, page: number, force: boolean, generation: number, rangeCanceller: Subject<void>): Observable<void> {
		if (!force && loadedPages[surface].has(page)) return of(undefined);
		pageState(surface).loading = true;
		return assetPageRequest(surface, context, page).pipe(
			takeUntil(rangeCanceller),
			map((response) => ({ value: apiValue<PhotoAssetPageDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (!isCurrentContext(context, generation) || !value) {
					if (!value && isCurrentContext(context, generation)) handlePageFailure(surface, statusCode);
					return;
				}
				mergePage(surface, page, filterEligibleItems(value.items), value.totalCount, value.totalBytes, 0, force);
			}),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) handlePageFailure(surface, responseStatus(error));
				Log.debug('Photo asset range request failed', surface, error);
				return of(undefined);
			}),
			finalize(() => { if (isCurrentContext(context, generation)) pageState(surface).loading = false; }),
		);
	}

	function requestRootsPage(context: PhotoBrowseReadContext, page: number, force: boolean, generation: number, rangeCanceller: Subject<void>): Observable<void> {
		if (!force && loadedPages.folders.has(page)) return of(undefined);
		state.folders.loading = true;
		return plexPhotoApi.getPhotoSourceRootsEndpoint(context.libraryId, { page, size: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort) }, photoBrowseRequestParams).pipe(
			takeUntil(rangeCanceller),
			map((response) => ({ value: apiValue<PhotoFolderPageDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (!isCurrentContext(context, generation) || !value) {
					if (!value && isCurrentContext(context, generation)) handlePageFailure('folders', statusCode);
					return;
				}
				mergePage('folders', page, filterEligibleItems(value.items), value.totalCount, 0, 0, force);
			}),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) handlePageFailure('folders', responseStatus(error));
				Log.debug('Photo root range request failed', error);
				return of(undefined);
			}),
			finalize(() => { if (isCurrentContext(context, generation)) state.folders.loading = false; }),
		);
	}

	function requestUnmappedAssetsPage(context: PhotoBrowseReadContext, page: number, force: boolean, generation: number, rangeCanceller: Subject<void>): Observable<void> {
		if (!force && loadedPages.folderAssets.has(page)) return of(undefined);
		state.folderAssets.loading = true;
		const query = { page, size: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort), plexLibraryId: context.libraryId, unmappedOnly: true };
		return plexPhotoApi.getPhotoAssetsEndpoint(query, photoBrowseRequestParams).pipe(
			takeUntil(rangeCanceller),
			map((response) => ({ value: apiValue<PhotoAssetPageDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (!isCurrentContext(context, generation) || !value) {
					if (!value && isCurrentContext(context, generation)) handlePageFailure('folderAssets', statusCode);
					return;
				}
				mergePage('folderAssets', page, filterEligibleItems(value.items), value.totalCount, value.totalBytes, 0, force);
			}),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) handlePageFailure('folderAssets', responseStatus(error));
				Log.debug('Photo unmapped asset range request failed', error);
				return of(undefined);
			}),
			finalize(() => { if (isCurrentContext(context, generation)) state.folderAssets.loading = false; }),
		);
	}

	function requestFolderChildrenPage(context: PhotoBrowseReadContext, folderPage: number, assetPage: number, force: boolean, generation: number, rangeCanceller: Subject<void>): Observable<void> {
		const folderAlreadyLoaded = loadedPages.folders.has(folderPage);
		const assetsAlreadyLoaded = loadedPages.folderAssets.has(assetPage);
		if (!force && folderAlreadyLoaded && assetsAlreadyLoaded) return of(undefined);
		state.folders.loading = true;
		state.folderAssets.loading = true;
		const query = { folderPage, folderSize: PAGE_SIZE, assetPage, assetSize: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort) };
		return plexPhotoApi.getPhotoFolderChildrenEndpoint(context.libraryId, context.folderId!, query, photoBrowseRequestParams).pipe(
			takeUntil(rangeCanceller),
			map((response) => ({ value: apiValue<PhotoFolderChildrenDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (!isCurrentContext(context, generation) || !value) {
					if (isCurrentContext(context, generation) && statusCode === 404) markMissingFolderContext();
					else if (isCurrentContext(context, generation)) markTransientFolderError();
					return;
				}
				if (eligibilityInitialized && !eligibleLibraryIds.has(value.folder.plexLibraryId)) {
					markMissingFolderContext();
					return;
				}
				state.folder = value.folder;
				state.breadcrumbs = value.breadcrumbs;
				mergePage('folders', folderPage, filterEligibleItems(value.folders.items), value.folders.totalCount, 0, 0, force);
				mergePage('folderAssets', assetPage, filterEligibleItems(value.assets.items), value.assets.totalCount, value.assets.totalBytes, 0, force);
			}),
			map(() => undefined),
			catchError((error) => {
				if (isCurrentContext(context, generation)) {
					if (responseStatus(error) === 404) markMissingFolderContext();
					else markTransientFolderError();
				}
				Log.debug('Photo folder range request failed', error);
				return of(undefined);
			}),
			finalize(() => {
				if (isCurrentContext(context, generation)) {
					state.folders.loading = false;
					state.folderAssets.loading = false;
				}
			}),
		);
	}

	function requestInspectionPage(identity: IPhotoInspectionIdentity, page: number): Observable<PhotoAssetInspectionDTO | null> {
		const generation = ++inspectionGeneration;
		const session = sessionGeneration;
		state.dialog.loading = true;
		return plexPhotoApi.getPhotoAssetInspectionEndpoint(identity.libraryId, identity.assetId, { membershipPage: page, membershipSize: PAGE_SIZE }, photoBrowseRequestParams).pipe(
			takeUntil(browseCancel$),
			map((response) => ({ value: apiValue<PhotoAssetInspectionDTO>(response), statusCode: response.statusCode })),
			tap(({ value, statusCode }) => {
				if (generation !== inspectionGeneration || session !== sessionGeneration || state.inspectionIdentity?.libraryId !== identity.libraryId || state.inspectionIdentity?.assetId !== identity.assetId) return;
				if (value) {
					state.dialog.asset = value;
					state.inspectionAlbums = appendMembershipPage(state.inspectionAlbums, value.albums, page);
					state.dialog.error = false;
					return;
				}
				if (statusCode === 404) {
					state.dialog.asset = null;
					state.inspectionAlbums = null;
				}
				state.dialog.error = true;
			}),
			map(({ value }) => value),
			catchError((error) => {
				if (generation === inspectionGeneration && session === sessionGeneration) {
					if (responseStatus(error) === 404) {
						state.dialog.asset = null;
						state.inspectionAlbums = null;
					}
					state.dialog.error = true;
				}
				Log.debug('Photo asset inspection request failed', error);
				return of(null);
			}),
			finalize(() => { if (generation === inspectionGeneration && session === sessionGeneration) state.dialog.loading = false; }),
		);
	}

	function appendMembershipPage(current: PhotoAlbumSummaryPageDTO | null, next: PhotoAlbumSummaryPageDTO, page: number): PhotoAlbumSummaryPageDTO {
		const items = [...(current?.items ?? []), ...(next?.items ?? [])];
		const deduped = uniqBy(items, (item) => `${item.plexServerId}:${item.plexLibraryId}:${item.id}`);
		return {
			items: deduped,
			page,
			pageSize: next.pageSize || PAGE_SIZE,
			totalCount: next.totalCount,
		};
	}

	function nextSlideVersion(key: string): number {
		const version = (slideVersions.get(key) ?? 0) + 1;
		slideVersions.set(key, version);
		return version;
	}

	function slideKey(asset: PhotoAssetBrowseDTO): string {
		return `${asset.plexLibraryId}:${asset.id}`;
	}

	function thumbnailUrl(asset: PhotoAssetBrowseDTO): Observable<string> {
		if (!asset.hasThumbnail) return of('');
		return mediaStore.getMediaThumbnailUrl({
			plexServerId: asset.plexServerId,
			plexKey: String(asset.plexApiRatingKey),
			metaDataKey: asset.plexApiMetaDataKey,
			width: 1280,
			height: 720,
		}, { handleErrorsLocally: true }).pipe(catchError(() => of('')));
	}

	function setSlideResult(slide: PhotoViewerSlide, url: string, error: boolean): void {
		slide.url = url;
		slide.loading = false;
		slide.error = error;
	}

	function retrySlide(slide: PhotoViewerSlide): Observable<void> {
		releaseSlideUrl(slide);
		slide.url = '';
		slide.loading = true;
		slide.error = false;
		return loadSlide(slide, nextSlideVersion(slideKey(slide.asset))).pipe(map(() => undefined));
	}

	function loadSlide(slide: PhotoViewerSlide, version: number): Observable<string> {
		const { asset, index } = slide;
		const key = slideKey(asset);
		const contextAtStart = activeContextKey;
		const imageGeneration = viewerImageGeneration;
		const valid = () =>
			imageGeneration === viewerImageGeneration
			&& slideVersions.get(key) === version
			&& contextAtStart === activeContextKey
			&& state.dialog.open
			&& state.slides.includes(slide);
		const source = asset.assetKind === PhotoAssetKind.VideoClip
			? thumbnailUrl(asset)
			: mediaStore.getPhotoImageUrl(asset);
		return source.pipe(
			tap({ subscribe: () => {
				// Let the foreground subscriber join its shared request before background subscribers detach.
				if (index === state.index) asapScheduler.schedule(pauseImagePreloads);
			} }),
			takeUntil(viewerCancel$),
			catchError(() => of('')),
			tap((url) => {
				if (!valid()) return;
				setSlideResult(slide, url, !url);
			}),
		);
	}
	function reloadViewerImages(): Observable<void> {
		if (!state.dialog.open || state.slides.length === 0) return of(undefined);
		const loads = state.slides.map((slide) => {
			releaseSlideUrl(slide);
			slide.url = '';
			slide.loading = true;
			slide.error = false;
			return loadSlide(slide, nextSlideVersion(slideKey(slide.asset)));
		});
		return forkJoin(loads).pipe(map(() => undefined));
	}

	function releaseSlideUrl(slide: PhotoViewerSlide): void {
		if (slide.asset.assetKind === PhotoAssetKind.Image && slide.url) mediaStore.releasePhotoImageUrl(slide.url);
	}

	function clearViewer(): void {
		cancelViewerLoads();
		for (const slide of state.slides) releaseSlideUrl(slide);
		slideVersions.clear();
		state.slides = [];
		state.current = null;
		state.index = 0;
		state.count = 0;
		state.detailsOpen = false;
		state.zoomed = false;
		state.navigating = false;
	}
	function closeViewerForInvalidAsset(): void {
		const lastOverviewIndex = state.current ? findOverviewIndex(state.current) : -1;
		if (lastOverviewIndex >= 0) state.focusIndex = lastOverviewIndex;
		clearViewer();
		state.dialog.open = false;
		state.dialog.loading = false;
		state.dialog.error = true;
		state.dialog.asset = null;
		state.inspectionAlbums = null;
		state.inspectionIdentity = null;
		state.detailsOpen = false;
	}

	function setViewerPosition(index: number, count: number): void {
		viewerDirection = index === normalizeAssetIndex(state.index - 1, count) ? -1 : 1;
		state.index = index;
	}

	function showAssetInternal(indexValue: number, fallbackAsset?: PhotoAssetBrowseDTO): Observable<void> {
		if (!state.dialog.open) state.dialog.open = true;
		const count = assetSequenceCount();
		if (count <= 0 && !fallbackAsset) {
			state.current = null;
			state.count = 0;
			state.slides = [];
			return of(undefined);
		}
		const targetIndex = count > 0 ? normalizeAssetIndex(indexValue, count) : 0;
		const generation = ++viewerGeneration;
		cancelViewerImageLoads();
		const context = currentContext(false);
		if (!context) {
			if (!fallbackAsset) return of(undefined);
			state.current = fallbackAsset;
			state.index = 0;
			state.count = 1;
			state.navigating = true;
			for (const old of state.slides) releaseSlideUrl(old);
			const fallbackSlide: PhotoViewerSlide = {
				asset: fallbackAsset,
				index: 0,
				url: '',
				loading: true,
				error: false,
			};
			state.slides = [fallbackSlide];
			return loadSlide(fallbackSlide, nextSlideVersion(slideKey(fallbackAsset))).pipe(
				map(() => undefined),
				finalize(() => { if (generation === viewerGeneration) state.navigating = false; }),
			);
		}
		state.zoomed = false;
		state.detailsOpen = false;
		setViewerPosition(targetIndex, count);
		state.count = count || 1;
		const targetOverview = assetOverviewIndex(targetIndex);
		const needed = new Set<number>([targetOverview]);
		for (const neighbor of desiredAssetIndexes(targetIndex, count || 1)) needed.add(assetOverviewIndex(neighbor));
		const requests = Array.from(needed)
			.filter((overviewIndex) => !overviewItemAtValue(overviewIndex))
			.map((overviewIndex) => actions.requestRange(overviewIndex, overviewIndex + 1));
		const fetch$ = requests.length > 0 ? forkJoin(requests).pipe(map(() => undefined)) : of(undefined);
		return fetch$.pipe(
			switchMap(() => {
				if (generation !== viewerGeneration || !isCurrentContext(context)) return of(undefined);
				const countNow = assetSequenceCount() || (fallbackAsset ? 1 : 0);
				const normalized = countNow > 0 ? normalizeAssetIndex(targetIndex, countNow) : 0;
				state.index = normalized;
				state.count = countNow;
				const assets: Array<{ asset: PhotoAssetBrowseDTO; index: number }> = [];
				for (const sequenceIndex of desiredAssetIndexes(normalized, countNow)) {
					const candidate = feedMaps[activeAssetSurface()].get(sequenceIndex) as PhotoAssetBrowseDTO | undefined;
					if (candidate) assets.push({ asset: candidate, index: sequenceIndex });
				}
				if (assets.length === 0 && fallbackAsset) assets.push({ asset: fallbackAsset, index: normalized });
				const active = assets.find((entry) => entry.index === normalized) ?? assets[0];
				if (!active) {
					state.current = null;
					state.slides = [];
					return of(undefined);
				}
				if (state.current?.id !== active.asset.id || state.current?.plexLibraryId !== active.asset.plexLibraryId) {
					inspectionGeneration += 1;
					state.inspectionIdentity = { libraryId: active.asset.plexLibraryId, assetId: active.asset.id };
					state.dialog.asset = null;
					state.inspectionAlbums = null;
					state.dialog.error = false;
					state.dialog.loading = false;
				}
				state.current = active.asset;
				const nextKeys = new Set(assets.map(({ asset }) => slideKey(asset)));
				for (const old of state.slides) {
					if (!nextKeys.has(slideKey(old.asset))) {
						releaseSlideUrl(old);
						slideVersions.delete(slideKey(old.asset));
					}
				}
				state.slides = assets.map(({ asset, index: slideIndex }) => {
					const existing = state.slides.find((candidate) => slideKey(candidate.asset) === slideKey(asset));
					return existing
						? { ...existing, asset, index: slideIndex }
						: { asset, index: slideIndex, url: '', loading: true, error: false };
				});
				const loads = state.slides
					.filter((slide) => slide.loading || !slide.url)
					.map((slide) => loadSlide(slide, nextSlideVersion(slideKey(slide.asset))));
				return loads.length > 0 ? forkJoin(loads).pipe(map(() => undefined)) : of(undefined);
			}),
			catchError((error) => {
				Log.debug('Photo viewer asset request failed', error);
				return of(undefined);
			}),
			finalize(() => { if (generation === viewerGeneration) state.navigating = false; }),
		);
	}

	function overviewItemAtValue(index: number): PhotoOverviewItem | null {
		if (!Number.isSafeInteger(index) || index < 0) return null;
		if (state.searchQuery) {
			const asset = feedMaps.search.get(index) as PhotoAssetBrowseDTO | undefined;
			return asset ? { kind: PhotoItemKind.Asset, asset, index } : null;
		}
		if (state.view === 'albums') {
			const album = feedMaps.albums.get(index) as PhotoAlbumBrowseDTO | undefined;
			return album ? { kind: PhotoItemKind.Album, album, index } : null;
		}
		if (state.view === 'folders') {
			if (index < state.folders.totalCount) {
				const folder = feedMaps.folders.get(index) as PhotoFolderBrowseDTO | undefined;
				return folder ? { kind: PhotoItemKind.Folder, folder, index } : null;
			}
			const asset = feedMaps.folderAssets.get(index - state.folders.totalCount) as PhotoAssetBrowseDTO | undefined;
			return asset ? { kind: PhotoItemKind.Asset, asset, index } : null;
		}
		const asset = feedMaps[activeAssetSurface()].get(index) as PhotoAssetBrowseDTO | undefined;
		return asset ? { kind: PhotoItemKind.Asset, asset, index } : null;
	}

	function isSelectionCurrent(generation: number, session: number, contextAtStart: number, key: string): boolean {
		return generation === selectionGeneration && session === sessionGeneration && contextAtStart === contextGeneration && key === activeContextKey && state.active;
	}

	function requestSelectionPage<T>(request: Observable<ApiResult<T>>): Observable<T> {
		return request.pipe(
			takeUntil(selectionCancel$),
			switchMap((response) => {
				const value = apiValue(response);
				return value ? of(value) : throwError(() => new Error(`Photo selection request failed (${response.statusCode ?? 'unknown'})`));
			}),
		);
	}

	function pageSequence<T extends PageDTO>(first: Observable<T>, next: (page: number) => Observable<T>): Observable<T[]> {
		return first.pipe(
			switchMap((firstPage) => {
				const pageCount = Math.ceil(firstPage.totalCount / PAGE_SIZE);
				if (pageCount <= 1) return of([firstPage]);
				return rxRange(2, pageCount - 1).pipe(
					concatMap((page) => next(page)),
					toArray(),
					map((remaining) => [firstPage, ...remaining]),
				);
			}),
		);
	}

	function enumerateSimpleSurface(surface: 'albums' | 'assets' | 'search' | 'albumMembers', context: PhotoBrowseReadContext): Observable<PageDTO[]> {
		const first = selectionPageRequest(surface, context, 1);
		const next = (page: number) => selectionPageRequest(surface, context, page);
		return pageSequence(first, next);
	}

	function selectionPageRequest(surface: 'albums' | 'assets' | 'search' | 'albumMembers', context: PhotoBrowseReadContext, page: number): Observable<PageDTO> {
		const request = surface === 'albums'
			? plexPhotoApi.getPhotoAlbumsEndpoint({ page, size: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort), ...(context.mode === 'aggregate' ? {} : { plexLibraryId: context.libraryId }) }, photoBrowseRequestParams)
			: assetPageRequest(surface, context, page);
		return requestSelectionPage(request as Observable<ApiResult<PageDTO>>);
	}

	function enumerateFolderContext(context: PhotoBrowseReadContext): Observable<SelectionPage[]> {
		const root = !context.folderId;
		if (root) {
			const folders = enumerateRootFolders(context);
			const assets = enumerateUnmappedAssets(context);
			return forkJoin([folders, assets]).pipe(map(([folderPages, assetPages]) => [...folderPages, ...assetPages]));
		}
		const sort = serializeFlexSortDsl(context.sort);
		const first = requestSelectionPage(plexPhotoApi.getPhotoFolderChildrenEndpoint(context.libraryId, context.folderId!, { folderPage: 1, folderSize: PAGE_SIZE, assetPage: 1, assetSize: PAGE_SIZE, sort }, photoBrowseRequestParams) as Observable<ApiResult<PhotoFolderChildrenDTO>>);
		return first.pipe(
			switchMap((firstPage) => {
				const folderCount = Math.ceil(firstPage.folders.totalCount / PAGE_SIZE);
				const assetCount = Math.ceil(firstPage.assets.totalCount / PAGE_SIZE);
				const max = Math.max(folderCount, assetCount);
				if (max <= 1) return of([firstPage]);
				return rxRange(2, max - 1).pipe(
					concatMap((page) => requestSelectionPage(plexPhotoApi.getPhotoFolderChildrenEndpoint(context.libraryId, context.folderId!, {
						folderPage: page <= folderCount ? page : 1,
						folderSize: PAGE_SIZE,
						assetPage: page <= assetCount ? page : 1,
						assetSize: PAGE_SIZE,
						sort,
					}, photoBrowseRequestParams) as Observable<ApiResult<PhotoFolderChildrenDTO>>)),
					toArray(),
					map((remaining) => [firstPage, ...remaining]),
				);
			}),
		);
	}

	function enumerateRootFolders(context: PhotoBrowseReadContext): Observable<PhotoFolderPageDTO[]> {
		const sort = serializeFlexSortDsl(context.sort);
		const first = requestSelectionPage(plexPhotoApi.getPhotoSourceRootsEndpoint(context.libraryId, { page: 1, size: PAGE_SIZE, sort }, photoBrowseRequestParams) as Observable<ApiResult<PhotoFolderPageDTO>>);
		return pageSequence(first, (page) => requestSelectionPage(plexPhotoApi.getPhotoSourceRootsEndpoint(context.libraryId, { page, size: PAGE_SIZE, sort }, photoBrowseRequestParams) as Observable<ApiResult<PhotoFolderPageDTO>>));
	}

	function enumerateUnmappedAssets(context: PhotoBrowseReadContext): Observable<PhotoAssetPageDTO[]> {
		const requestPage = (page: number) => {
			const query = { page, size: PAGE_SIZE, sort: serializeFlexSortDsl(context.sort), plexLibraryId: context.libraryId, unmappedOnly: true };
			return requestSelectionPage(plexPhotoApi.getPhotoAssetsEndpoint(query, photoBrowseRequestParams) as Observable<ApiResult<PhotoAssetPageDTO>>);
		};
		return pageSequence(requestPage(1), requestPage);
	}

	function selectorsFromPages(pages: SelectionPage[]): IPhotoSelector[] {
		const selectors: IPhotoSelector[] = [];
		const append = (item: PageItem): void => {
			if (eligibilityInitialized && !eligibleLibraryIds.has(item.plexLibraryId)) return;
			if ('assetKind' in item) selectors.push({ id: item.id, plexServerId: item.plexServerId, plexLibraryId: item.plexLibraryId, kind: PhotoItemKind.Asset, type: PlexMediaType.PhotoImage, title: item.title, stale: false });
			else if ('recursiveAssetCount' in item) selectors.push({ id: item.id, plexServerId: item.plexServerId, plexLibraryId: item.plexLibraryId, kind: PhotoItemKind.Folder, type: PlexMediaType.PhotoImage, title: item.title, stale: false });
			else selectors.push({ id: item.id, plexServerId: item.plexServerId, plexLibraryId: item.plexLibraryId, kind: PhotoItemKind.Album, type: PlexMediaType.PhotoAlbum, title: item.title, stale: false });
		};
		for (const page of pages) {
			if ('items' in page) {
				for (const item of page.items) append(item as PageItem);
				continue;
			}
			for (const item of page.folders.items) append(item);
			for (const item of page.assets.items) append(item);
		}
		return dedupeSelectors(selectors);
	}

	function enumerateContext(context: PhotoBrowseReadContext): Observable<IPhotoSelector[]> {
		let pages: Observable<SelectionPage[]>;
		if (context.searchQuery) pages = enumerateSimpleSurface('search', context);
		else if (context.view === 'folders') pages = enumerateFolderContext(context);
		else if (context.mode === 'album') pages = enumerateSimpleSurface('albumMembers', context);
		else pages = enumerateSimpleSurface(context.view === 'albums' ? 'albums' : 'assets', context);
		return pages.pipe(map(selectorsFromPages));
	}

	const getters = {
		selectedCount: computed(() => state.selectedSelectors.length),
		hasSelection: computed(() => state.selectedSelectors.length > 0),
		visibleAssets: computed(() => activeAssetPage().items),
		visibleAlbums: computed(() => state.searchQuery ? [] : state.albums.items),
		visibleFolders: computed(() => state.searchQuery ? [] : state.folders.items),
		hasAnyData: computed(() => overviewCountValue() > 0),
		isRefreshing: computed(() => state.loading || state.albums.loading || state.assets.loading || state.folders.loading || state.folderAssets.loading || state.albumMembers.loading || state.search.loading),
		overviewCount: computed(() => overviewCountValue()),
		overviewFolderCount: computed(() => overviewFolderCount()),
		overviewItemAt: (index: number): PhotoOverviewItem | null => overviewItemAtValue(index),
		isSortSupported: (route: IPhotoBrowseRoute): boolean =>
			!(state.sortMetadataReady && state.sortMetadataProfileKey === photoSortMetadataKey(route))
			|| state.supportedSorts.includes(route.sort.field),
	};

	return { ...toRefs(state), ...actions, ...getters };
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(usePhotoBrowsingStore, import.meta.hot));
}
