import Log from 'consola';
import { cloneDeep, isNumber, sortBy, uniqueId } from 'lodash-es';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { computed, markRaw, reactive, toRefs } from 'vue';
import { get, set } from '@vueuse/core';
import { useRouteQuery } from '@vueuse/router';
import {
	PlexMediaComparisonState,
	type PlexMediaMetadataDTO,
	type PlexMediaSlimDTO,
	type PlexMediaStatisticsDTO,
	PlexMediaType,
	ViewMode,
	type LibraryComparisonCompletedDTO,
} from '@dto';
import type { IMediaOverviewSort } from '@composables/event-bus';
import { MediaSortField, SortDirection } from '@enums';
import { type IMetaDataMediaFilter, type ISelection, type ISortOption, StoreNames } from '@interfaces';
import { plexLibraryApi, plexMediaApi } from '@api';
import { map, switchMap, takeUntil, tap } from 'rxjs/operators';
import { BehaviorSubject, defer, finalize, forkJoin, type Observable, of, Subject } from 'rxjs';
import { useLibraryStore, useSettingsStore } from '@store';
import {
	buildFlexSortDsl,
	getVideoQualityColor,
	translateVideoQuality,
} from '@composables';
import { useSubscription } from '@vueuse/rxjs';

interface IMediaOverviewStoreState {
	libraryId: number;
	pageSize: number;
	totalCount: number;
	itemsLength: number;
	sortedState: IMediaOverviewSort;
	scrollDict: Map<string, number>;
	queryHash: string;
	selection: ISelection;
	downloadButtonVisible: boolean;
	filterQuery: string;
	lastMediaItemViewed: PlexMediaSlimDTO | null;
	pendingMediaHighlightId: number | null;
	pendingMediaHighlightLibraryId: number | null;
	loading: boolean;
	navLoading: boolean;
	filterMetadataLoading: boolean;
	isDetailView: boolean;
	allMovieCount: number;
	allTvShowCount: number;
	allSeasonCount: number;
	allEpisodeCount: number;
	allFileSize: number;
	metadata: IMetaDataMediaFilter;
	metadataList: PlexMediaMetadataDTO;
	availableRoleIds: number[];
	availableCountryIds: number[];
	availableGenreIds: number[];
	availableQualityIds: number[];
	// Meant to update to signify a reactive change in the mediaPages
	mediaPagesVersion: number;
	currentScrollIndex: number;
	scrollCommand: BehaviorSubject<{ index: number; highlight: boolean }>;
}

export const useMediaOverviewStore = defineStore(StoreNames.MediaOverviewStore, () => {
	const defaultState: IMediaOverviewStoreState = {
		libraryId: 0,
		pageSize: 100,
		totalCount: 0,
		itemsLength: 0,
		sortedState: { field: MediaSortField.Title, sort: SortDirection.Asc },
		scrollDict: new Map<string, number>([['#', 0]]),
		selection: { keys: [], allSelected: false, indexKey: 0 },
		downloadButtonVisible: false,
		filterQuery: '',
		queryHash: '',
		lastMediaItemViewed: null,
		pendingMediaHighlightId: null,
		pendingMediaHighlightLibraryId: null,
		loading: false,
		navLoading: false,
		filterMetadataLoading: false,
		isDetailView: false,
		allMovieCount: 0,
		allTvShowCount: 0,
		allSeasonCount: 0,
		allEpisodeCount: 0,
		allFileSize: 0,
		metadata: {
			countryId: 0,
			roleId: 0,
			genreId: 0,
			qualityId: 0,
			comparisonState: null,
		},
		metadataList: {
			mediaCount: 0,
			roleCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roles: [],
			countries: [],
			genres: [],
			qualities: [],
		},
		availableRoleIds: [],
		availableCountryIds: [],
		availableGenreIds: [],
		availableQualityIds: [],
		mediaPagesVersion: 0,
		currentScrollIndex: 0,
		scrollCommand: new BehaviorSubject<{ index: number; highlight: boolean }>({ index: 0, highlight: false }),
	};

	const state = reactive<IMediaOverviewStoreState>(cloneDeep(defaultState));
	const settingsStore = useSettingsStore();
	const libraryStore = useLibraryStore();
	const mediaPages = new Map<number, readonly PlexMediaSlimDTO[]>();
	const pendingPages = new Set<number>();
	const comparisonRefreshKeysInFlight = new Set<string>();

	const searchQuery = useRouteQuery('q', '', { mode: 'replace' });
	const countryIdQuery = useRouteQuery('countryId', 0, { mode: 'replace' });
	const genreIdQuery = useRouteQuery('genreId', 0, { mode: 'replace' });
	const roleIdQuery = useRouteQuery('roleId', 0, { mode: 'replace' });
	const qualityIdQuery = useRouteQuery('qualityId', 0, { mode: 'replace' });
	const comparisonStateQuery = useRouteQuery<string>('comparisonState', '', { mode: 'replace' });
	const scrollIndexQuery = useRouteQuery('scrollIndex', 0, { mode: 'replace' });
	const sortQuery = useRouteQuery('sort', '', { mode: 'replace' });

	// Subject to cancel in-flight requests when switching libraries
	const cancelSubject$ = new Subject<void>();

	const actions = {
		cancelPendingRequests() {
			Log.debug('Cancelling pending media requests');
			cancelSubject$.next();
			state.loading = false;
		},
		clearLibraryMediaData(libraryId: number): void {
			Log.debug('Clearing media overview data for disabled library', { libraryId });
			actions.cancelPendingRequests();
			mediaPages.clear();
			pendingPages.clear();
			state.libraryId = libraryId;
			state.totalCount = 0;
			state.itemsLength = 0;
			state.queryHash = '';
			state.selection = cloneDeep(defaultState.selection);
			state.downloadButtonVisible = false;
			state.lastMediaItemViewed = null;
			state.pendingMediaHighlightId = null;
			state.navLoading = false;
			state.filterMetadataLoading = false;
			state.isDetailView = false;
			state.allMovieCount = 0;
			state.allTvShowCount = 0;
			state.allSeasonCount = 0;
			state.allEpisodeCount = 0;
			state.allFileSize = 0;
			state.metadataList = cloneDeep(defaultState.metadataList);
			state.availableRoleIds = [];
			state.availableCountryIds = [];
			state.availableGenreIds = [];
			state.availableQualityIds = [];
			state.mediaPagesVersion++;
		},
		refreshCurrentMediaDataWhenComparisonCompleted(notification: LibraryComparisonCompletedDTO): Observable<PlexMediaStatisticsDTO | null> {
			const mediaType = get(getters.getMediaType);
			const libraryId = state.libraryId;

			if (libraryId <= 0 || mediaType !== notification.mediaType || !notification.affectedLibraryIds.includes(libraryId)) {
				return of(null);
			}

			const refreshKey = getters.getCurrentRequestKey();
			if (comparisonRefreshKeysInFlight.has(refreshKey)) {
				return of(null);
			}

			const pagesToRefresh = Array.from(mediaPages.keys());
			if (pagesToRefresh.length === 0) {
				return of(null);
			}

			comparisonRefreshKeysInFlight.add(refreshKey);
			Log.debug('Refreshing previously requested media pages after comparison completed', {
				libraryId,
				mediaType,
				pages: pagesToRefresh,
			});

			const requests = pagesToRefresh.map((page) => actions.requestMediaPage(page, state.pageSize, true));
			return forkJoin(requests).pipe(
				finalize(() => comparisonRefreshKeysInFlight.delete(refreshKey)),
				map(() => null),
			);
		},
		initializeLibrary(libraryId: number): Observable<PlexMediaStatisticsDTO | null> {
			// Cancel any in-flight requests first
			actions.cancelPendingRequests();
			const pendingMediaHighlightId = state.pendingMediaHighlightId;
			const pendingMediaHighlightLibraryId = state.pendingMediaHighlightLibraryId;

			Log.debug('Initializing library', { libraryId, mediaType: get(getters.getMediaType) });

			actions.$reset();

			// Update state
			state.libraryId = libraryId;
			if (pendingMediaHighlightLibraryId === libraryId) {
				state.pendingMediaHighlightId = pendingMediaHighlightId;
				state.pendingMediaHighlightLibraryId = pendingMediaHighlightLibraryId;
			}

			// Apply url query params
			actions.applyRouteQueryState();

			// Load the library first so metadata requests use the resolved media type.
			return defer(() =>
				state.libraryId > 0
					? libraryStore.refreshLibrary(state.libraryId)
					: of(null),
			).pipe(
				switchMap(() => {
					const library = libraryStore.getLibrary(state.libraryId);
					if (library && (!library.isEnabled || library.syncedAt === null)) {
						state.loading = false;
						return of(null);
					}

					return forkJoin([
						actions.refreshFilterMetadata(),
						actions.refreshMetaData(),
						actions.refreshMediaData(),
					]).pipe(map(([, , requestMediaResult]) => requestMediaResult));
				}),
			);
		},
		refreshMetaData() {
			return plexLibraryApi.getLibraryMediaMetadata(state.libraryId, { mediaType: get(getters.getMediaType) }).pipe(
				takeUntil(cancelSubject$),
				tap((result) => {
					if (result.isSuccess && result.value) {
						state.metadataList = Object.freeze(result.value);
					}
				}),
			);
		},
		refreshFilterMetadata() {
			state.filterMetadataLoading = true;
			return plexLibraryApi.getMetadataFilter(state.libraryId, { mediaType: get(getters.getMediaType) }).pipe(
				takeUntil(cancelSubject$),
				tap((result) => {
					if (result.isSuccess && result.value) {
						state.availableRoleIds = result.value.roles ?? [];
						state.availableCountryIds = result.value.countries ?? [];
						state.availableGenreIds = result.value.genres ?? [];
						state.availableQualityIds = result.value.qualities ?? [];
					}
				}),
				finalize(() => {
					state.filterMetadataLoading = false;
				}),
			);
		},
		refreshMediaData(): Observable<PlexMediaStatisticsDTO | null> {
			state.loading = true;
			mediaPages.clear();
			pendingPages.clear();
			state.itemsLength = 0;
			Log.debug('Starting media request', { libraryId: state.libraryId, mediaType: get(getters.getMediaType) });
			return actions.requestMediaPage(1, state.pageSize).pipe(
				tap({
					next: () => {
						state.loading = false;
						Log.debug('Media request completed successfully');
					},
					error: (err) => {
						state.loading = false;
						Log.error('Media request failed', err);
					},
					complete: () => {
						// Handle cancellation
						if (state.loading) {
							state.loading = false;
							Log.debug('Media request was cancelled');
						}
					},
				}),
			);
		},
		requestMediaPage(page: number, size: number = state.pageSize, forceRefresh: boolean = false): Observable<PlexMediaStatisticsDTO | null> {
			if (pendingPages.has(page) || (!forceRefresh && mediaPages.has(page))) {
				return of(null);
			}

			const mediaType = get(getters.getMediaType);
			if (state.libraryId > 0 && mediaType === PlexMediaType.None) {
				return libraryStore.refreshLibrary(state.libraryId).pipe(
					switchMap((library) => library ? actions.requestMediaPage(page, size, forceRefresh) : of(null)),
				);
			}

			pendingPages.add(page);
			return plexMediaApi.getAllMediaByTypeEndpoint({
				q: state.filterQuery,
				page,
				size,
				sort: get(getters.getSortDSL),
				countryId: state.metadata.countryId > 0 ? state.metadata.countryId : undefined,
				genreId: state.metadata.genreId > 0 ? state.metadata.genreId : undefined,
				qualityId: state.metadata.qualityId > 0 ? state.metadata.qualityId : undefined,
				comparisonState: state.metadata.comparisonState ?? undefined,
				roleId: state.metadata.roleId > 0 ? state.metadata.roleId : undefined,
				mediaType,
				plexLibraryId: state.libraryId > 0 ? state.libraryId : undefined,
				filterOwnedMedia: settingsStore.generalSettings.hideMediaFromOwnedServers,
				filterOfflineMedia: settingsStore.generalSettings.hideMediaFromOfflineServers,
			}).pipe(
				takeUntil(cancelSubject$),
				map(({ isSuccess, value }): PlexMediaStatisticsDTO | null => {
					if (isSuccess && value) {
						return value;
					}

					return null;
				}),
				tap((data) => actions.addMediaPage(data)),
				finalize(() => pendingPages.delete(page)),
			);
		},
		// Adds the requested media page to the cache
		addMediaPage(data: PlexMediaStatisticsDTO | null) {
			if (!data) {
				Log.warn('Media page request returned no data');
				return;
			}

			if (state.queryHash !== data.queryHash) {
				if (state.queryHash) {
					Log.warn(`mediaPages was cleared, with ${state.queryHash} vs ${data.queryHash}`);
				} else {
					Log.debug(`mediaPages was initialized with ${data.queryHash}`);
				}
				mediaPages.clear();
				pendingPages.clear();
				state.itemsLength = 0;
				if (state.queryHash) {
					state.currentScrollIndex = 0;
				}
				state.scrollDict = cloneDeep(defaultState.scrollDict);
			}

			const mediaList = Array.isArray(data.mediaList) ? data.mediaList : [];
			const previousMediaCount = mediaPages.get(data.page)?.length ?? 0;
			const mediaCount = data.mediaCount ?? mediaList.length;
			mediaPages.set(data.page, markRaw(mediaList));
			state.mediaPagesVersion++;
			state.queryHash = data.queryHash ?? '';
			state.itemsLength += mediaCount - previousMediaCount;
			state.totalCount = data.totalCount ?? state.totalCount;

			state.allMovieCount = data.totalMovieCount;
			state.allTvShowCount = data.totalTvShowCount;
			state.allSeasonCount = data.totalSeasonCount;
			state.allEpisodeCount = data.totalEpisodeCount;
			state.allFileSize = data.totalMediaSize;

			// navigationIndexes are identical across all pages — only set once
			if (state.scrollDict.size <= 1 && data.navigationIndexes?.length) {
				state.scrollDict = new Map(data.navigationIndexes.map((x) => [x.label, x.index]));
			}

			Log.debug('mediaPages', mediaPages);
		},
		getPageForIndex(index: number): number {
			return Math.floor(index / state.pageSize) + 1;
		},
		requestRange(startIndex: number, endIndex: number): Observable<(PlexMediaStatisticsDTO | null)[]> {
			const firstPage = actions.getPageForIndex(Math.max(0, startIndex));
			const lastPage = actions.getPageForIndex(Math.max(0, endIndex));
			const requests: Observable<PlexMediaStatisticsDTO | null>[] = [];

			for (let page = firstPage; page <= lastPage; page++) {
				if (!mediaPages.has(page)) {
					requests.push(actions.requestMediaPage(page));
				}
			}

			if (requests.length) {
				state.navLoading = true;
				return forkJoin(requests).pipe(
					finalize(() => {
						state.navLoading = false;
					}),
				);
			}

			return of([]);
		},
		scrollToIndex(scrollIndex: number, highlight = true): Observable<(PlexMediaStatisticsDTO | null)[]> {
			if (scrollIndex < 0 || scrollIndex >= state.totalCount) {
				Log.warn(`Scroll index ${scrollIndex} is out of bounds for total count ${state.totalCount}`);
				return of([]);
			}

			return actions.requestRange(scrollIndex - 50, scrollIndex + 51).pipe(
				tap(() => state.scrollCommand.next({ index: scrollIndex, highlight })),
			);
		},
		setCountryFilter(countryId?: number | null): Observable<PlexMediaStatisticsDTO | null> {
			return of(countryId).pipe(
				map((x) => isNumber(x) && x > 0 ? x : 0),
				tap((value) => {
					state.metadata.countryId = value;
					set(countryIdQuery, value > 0 ? value : undefined);
				}),
				switchMap(() => actions.refreshMediaData()),
			);
		},

		setRoleFilter(roleId?: number | null): Observable<PlexMediaStatisticsDTO | null> {
			return of(roleId).pipe(
				map((x) => isNumber(x) && x > 0 ? x : 0),
				tap((value) => {
					state.metadata.roleId = value;
					set(roleIdQuery, value > 0 ? value : undefined);
				}),
				switchMap(() => actions.refreshMediaData()),
			);
		},

		setGenreFilter(genreId?: number | null): Observable<PlexMediaStatisticsDTO | null> {
			return of(genreId).pipe(
				map((x) => isNumber(x) && x > 0 ? x : 0),
				tap((value) => {
					state.metadata.genreId = value;
					set(genreIdQuery, value > 0 ? value : undefined);
				}),
				switchMap(() => actions.refreshMediaData()),
			);
		},

		setQualityFilter(qualityId?: number | null): Observable<PlexMediaStatisticsDTO | null> {
			return of(qualityId).pipe(
				map((x) => isNumber(x) && x > 0 ? x : 0),
				tap((value) => {
					state.metadata.qualityId = value;
					set(qualityIdQuery, value > 0 ? value : undefined);
				}),
				switchMap(() => actions.refreshMediaData()),
			);
		},
		setComparisonStateFilter(comparisonState?: PlexMediaComparisonState | null): Observable<PlexMediaStatisticsDTO | null> {
			return of(comparisonState).pipe(
				map((value) => value && Object.values(PlexMediaComparisonState).includes(value) ? value : null),
				tap((value) => {
					state.metadata.comparisonState = value;
					set(comparisonStateQuery, value ?? undefined);
				}),
				switchMap(() => actions.refreshMediaData()),
			);
		},
		resetMetaDataFilterState() {
			state.metadata = {
				countryId: 0,
				roleId: 0,
				genreId: 0,
				qualityId: 0,
				comparisonState: null,
			};
		},
		clearMetaDataFilter() {
			actions.resetMetaDataFilterState();
			set(countryIdQuery, undefined);
			set(roleIdQuery, undefined);
			set(genreIdQuery, undefined);
			set(qualityIdQuery, undefined);
			set(comparisonStateQuery, undefined);
		},
		applyRouteQueryState() {
			const search = String(get(searchQuery)).trim();
			const countryId = Number(get(countryIdQuery));
			const genreId = Number(get(genreIdQuery));
			const roleId = Number(get(roleIdQuery));
			const qualityId = Number(get(qualityIdQuery));
			const comparisonState = String(get(comparisonStateQuery)).trim();
			const scrollIndex = Number(get(scrollIndexQuery));
			const sort = String(get(sortQuery)).trim();

			if (search) {
				state.filterQuery = search;
			}

			if (countryId > 0) {
				state.metadata.countryId = countryId;
			}

			if (genreId > 0) {
				state.metadata.genreId = genreId;
			}

			if (roleId > 0) {
				state.metadata.roleId = roleId;
			}

			if (qualityId > 0) {
				state.metadata.qualityId = qualityId;
			}

			if (Object.values(PlexMediaComparisonState).includes(comparisonState as PlexMediaComparisonState)) {
				state.metadata.comparisonState = comparisonState as PlexMediaComparisonState;
			}

			if (scrollIndex > 0) {
				state.currentScrollIndex = scrollIndex;
			}

			const [sortField, sortDirection] = sort.split(':');
			const validSortFields = Object.values(MediaSortField);
			const validSortDirections = [SortDirection.Asc, SortDirection.Desc];
			if (validSortFields.includes(sortField as MediaSortField) && validSortDirections.includes(sortDirection as SortDirection)) {
				state.sortedState = {
					field: sortField as MediaSortField,
					sort: sortDirection as SortDirection,
				};
			}
		},
		changeAllMediaOverviewType(mediaType: PlexMediaType) {
			settingsStore.displaySettings.allOverviewViewMode = mediaType;
			useSubscription(actions.initializeLibrary(0).subscribe());
		},
		setSelection(selection: ISelection) {
			state.selection = selection;
		},
		setSelectionRange(min: number, max: number) {
			actions.setSelection({
				indexKey: state.selection.indexKey,
				keys: get(getters.getMediaItems)
					.filter((x) => x && x.sortIndex >= min && x.sortIndex <= max)
					.map((x) => x.id),
				allSelected: false,
			} as ISelection);
		},
		setRootSelected(value: boolean) {
			actions.setSelection({
				indexKey: state.selection?.indexKey ?? 0,
				keys: value ? get(getters.getMediaItems).map((x) => x.id) : [],
				allSelected: value,
			} as ISelection);
		},
		clearSort() {
			state.sortedState = { field: MediaSortField.Title, sort: SortDirection.Asc };
			set(sortQuery, undefined);
			return actions.refreshMediaData();
		},
		setCurrentScrollIndex(scrollIndex: number) {
			state.currentScrollIndex = scrollIndex;
			set(scrollIndexQuery, scrollIndex);
		},
		setPendingMediaHighlight(mediaId: number, libraryId: number) {
			state.pendingMediaHighlightId = mediaId;
			state.pendingMediaHighlightLibraryId = libraryId;
		},
		clearPendingMediaHighlight() {
			state.pendingMediaHighlightId = null;
			state.pendingMediaHighlightLibraryId = null;
		},
		consumePendingMediaHighlight(mediaId: number) {
			if (state.pendingMediaHighlightId === mediaId) {
				actions.clearPendingMediaHighlight();
			}
		},
		setFilterQuery(query: string): Observable<PlexMediaStatisticsDTO | null> {
			state.filterQuery = query;
			set(searchQuery, query);
			return actions.refreshMediaData();
		},
		clearFilter(): Observable<PlexMediaStatisticsDTO | null> {
			state.filterQuery = '';
			return actions.refreshMediaData();
		},
		toggleSortMedia(field: MediaSortField) {
			if (state.sortedState.field === field) {
				state.sortedState.sort = state.sortedState.sort === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
			} else {
				state.sortedState = { field, sort: SortDirection.Asc };
			}

			// If default sort then undefined
			const sort = get(getters.getSortDSL);
			set(sortQuery, sort !== 'sortIndex:asc' ? sort : undefined);
			useSubscription(actions.refreshMediaData().subscribe());
		},
		$reset() {
			mediaPages.clear();
			pendingPages.clear();
			Object.assign(state, cloneDeep(defaultState));
			state.loading = true;
		},
	};

	const getters = {
		hasSelectedMedia: computed((): boolean => {
			return state.selection.keys.length > 0;
		}),

		hasNoSearchResults: computed((): boolean => {
			return state.filterQuery != '' && get(getters.getMediaItems).length === 0;
		}),
		hasActiveFilter: computed((): boolean => {
			return state.metadata.countryId > 0
				|| state.metadata.roleId > 0
				|| state.metadata.genreId > 0
				|| state.metadata.qualityId > 0
				|| state.metadata.comparisonState !== null;
		}),
		hasNoFilterResults: computed((): boolean => {
			return get(getters.hasActiveFilter) && state.metadataList.mediaCount > 0 && get(getters.getMediaItems).length === 0;
		}),
		allMediaMode: computed(() => state.libraryId === 0),
		library: computed(() => libraryStore.getLibrary(state.libraryId)),
		getCurrentRequestKey: (): string => JSON.stringify({
			libraryId: state.libraryId,
			mediaType: get(getters.getMediaType),
			pageSize: state.pageSize,
			sort: get(getters.getSortDSL),
			query: state.filterQuery,
			countryId: state.metadata.countryId,
			genreId: state.metadata.genreId,
			qualityId: state.metadata.qualityId,
			comparisonState: state.metadata.comparisonState,
			roleId: state.metadata.roleId,
			filterOwnedMedia: settingsStore.generalSettings.hideMediaFromOwnedServers,
			filterOfflineMedia: settingsStore.generalSettings.hideMediaFromOfflineServers,
		}),
		getMediaItems: computed((): Readonly<PlexMediaSlimDTO[]> => {
			void state.mediaPagesVersion; // Trigger reactive change
			return Array.from(mediaPages.values()).flat();
		}),
		getScrollCommand(): Observable<{ index: number; highlight: boolean }> {
			return state.scrollCommand.asObservable();
		},
		getMediaItemsForRange: (start: number, end: number): Readonly<PlexMediaSlimDTO[]> => {
			void state.mediaPagesVersion; // Trigger reactive change
			const normalizedStart = Math.max(0, Math.floor(start));
			const normalizedEnd = Math.max(normalizedStart, Math.floor(end));
			const result: PlexMediaSlimDTO[] = [];

			for (let index = normalizedStart; index < normalizedEnd; index++) {
				const page = actions.getPageForIndex(index);
				const pageItems = mediaPages.get(page);

				if (!pageItems) {
					continue;
				}

				const indexInPage = index - ((page - 1) * state.pageSize);
				const mediaItem = pageItems[indexInPage];
				if (mediaItem) {
					result.push(mediaItem);
				}
			}

			return result;
		},
		getMediaViewMode: computed((): ViewMode => {
			switch (get(getters.getMediaType)) {
				case PlexMediaType.Movie:
					return settingsStore.displaySettings.movieViewMode;
				case PlexMediaType.TvShow:
					return settingsStore.displaySettings.tvShowViewMode;
				default:
					return ViewMode.Poster;
			}
		}),
		showSelectionButton: computed((): boolean => {
			return get(getters.getMediaViewMode) === ViewMode.Table && !state.isDetailView;
		}),
		showDownloadButton: computed((): boolean => {
			return state.downloadButtonVisible || (get(getters.hasSelectedMedia) && get(getters.getMediaViewMode) === ViewMode.Table);
		}),
		isRootSelected: computed((): boolean | null => {
			if (state.selection?.keys.length === state.itemsLength) {
				return true;
			}

			if (state.selection?.keys.length === 0) {
				return false;
			}

			return null;
		}),
		getActiveSort: computed((): IMediaOverviewSort => {
			return state.sortedState;
		}),
		getMediaType: computed((): PlexMediaType => state.libraryId === 0 ? settingsStore.displaySettings.allOverviewViewMode : libraryStore.getLibrary(state.libraryId)?.type ?? PlexMediaType.None),
		getIsSorted: computed((): boolean => {
			if (state.sortedState.sort === SortDirection.NoSort) {
				return false;
			}
			return !(state.sortedState.field === MediaSortField.Title && state.sortedState.sort === SortDirection.Asc);
		}),
		getSortOptions: (): ISortOption[] => {
			const { $i18n } = useNuxtApp();
			const { t } = $i18n;
			const options: ISortOption[] = [
				{ field: MediaSortField.Title, label: t('general.sort.title'), direction: SortDirection.NoSort },
				{ field: MediaSortField.Year, label: t('general.sort.year'), direction: SortDirection.NoSort },
				{ field: MediaSortField.AddedAt, label: t('general.sort.added-at'), direction: SortDirection.NoSort },
				{ field: MediaSortField.UpdatedAt, label: t('general.sort.updated-at'), direction: SortDirection.NoSort },
				{ field: MediaSortField.Duration, label: t('general.sort.duration'), direction: SortDirection.NoSort },
				{ field: MediaSortField.MediaSize, label: t('general.sort.size'), direction: SortDirection.NoSort },
				{ field: MediaSortField.Quality, label: t('general.sort.quality'), direction: SortDirection.NoSort },
			];

			for (const option of options) {
				if (option.field === state.sortedState.field) {
					option.direction = state.sortedState.sort;
				}
			}

			return options;
		},
		getGenres: computed(() => sortBy(
			state.metadataList.genres.filter((x) => state.availableGenreIds.includes(x.id)),
			(x) => x.name,
		)),
		getRoles: computed(() => sortBy(
			state.metadataList.roles.filter((x) => state.availableRoleIds.includes(x.id)),
			(x) => x.name,
		)),
		getCountries: computed(() => sortBy(
			state.metadataList.countries.filter((x) => state.availableCountryIds.includes(x.id)),
			(x) => x.name,
		)),
		getQualities: computed(() => state.metadataList.qualities.filter((x) => {
			return state.availableQualityIds.includes(x.id);
		})),
		getComparisonStateOptions: computed(() => {
			const states = [
				PlexMediaComparisonState.NotCompared,
				PlexMediaComparisonState.Owned,
				PlexMediaComparisonState.Missing,
				PlexMediaComparisonState.HigherQuality,
				PlexMediaComparisonState.Pending,
				PlexMediaComparisonState.Partial,
				PlexMediaComparisonState.PartialAndHigherQuality,
			];

			return states.map((state) => ({
				value: state,
				label: translateMediaComparisonState(state),
			}));
		}),
		getFilterChips: computed(() => {
			const result: {
				text: string;
				key: keyof IMetaDataMediaFilter;
				color?: string;
				id: string;
				unset: Observable<PlexMediaStatisticsDTO | null>;
			}[] = [];

			if (state.metadata.countryId > 0) {
				result.push({
					text: state.metadataList.countries.find((x) => x.id === state.metadata.countryId)?.name ?? '',
					key: 'countryId',
					id: uniqueId(),
					unset: actions.setCountryFilter(),
				});
			}

			if (state.metadata.roleId > 0) {
				result.push({
					text: state.metadataList.roles.find((x) => x.id === state.metadata.roleId)?.name ?? '',
					key: 'roleId',
					id: uniqueId(),
					unset: actions.setRoleFilter(),
				});
			}

			if (state.metadata.genreId > 0) {
				result.push({
					text: state.metadataList.genres.find((x) => x.id === state.metadata.genreId)?.name ?? '',
					key: 'genreId',
					id: uniqueId(),
					unset: actions.setGenreFilter(),
				});
			}

			if (state.metadata.qualityId > 0) {
				const quality = state.metadataList.qualities.find((x) => x.id === state.metadata.qualityId)?.quality;
				result.push({
					text: translateVideoQuality(quality),
					key: 'qualityId',
					color: getVideoQualityColor(quality),
					id: uniqueId(),
					unset: actions.setQualityFilter(),
				});
			}

			if (state.metadata.comparisonState) {
				result.push({
					text: get(getters.getComparisonStateOptions).find((x) => x.value === state.metadata.comparisonState)?.label ?? '',
					key: 'comparisonState',
					id: uniqueId(),
					unset: actions.setComparisonStateFilter(),
				});
			}

			return result;
		}),
		getSortDSL: computed((): string => {
			return buildFlexSortDsl([
				{
					field: state.sortedState.field,
					direction: state.sortedState.sort === SortDirection.Desc ? 'desc' : 'asc',
				},
			]) ?? '';
		}),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useMediaOverviewStore, import.meta.hot));
}
