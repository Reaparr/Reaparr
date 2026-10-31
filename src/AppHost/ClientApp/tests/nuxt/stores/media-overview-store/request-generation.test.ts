import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { of, Subject } from 'rxjs';
import { get, set } from '@vueuse/core';
import { useRouteQuery } from '@vueuse/router';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { plexLibraryApi, plexMediaApi } from '@api';
import { useLibraryStore, useMediaOverviewStore, useSettingsStore } from '@store';
import {
	type PlexLibraryDTO,
	type PlexMediaFilterMetadataDTO,
	type PlexMediaMetadataDTO,
	type PlexMediaStatisticsDTO,
	PlexMediaType,
	SortDirection,
	SortType,
} from '@dto';
import type { ResultDTO } from '@interfaces';

describe('MediaOverviewStore - Request generation ownership', () => {
	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		setSortMetadata(PlexMediaType.Movie);
	});

	function setSortMetadata(mediaType: PlexMediaType) {
		const store = useMediaOverviewStore();
		store.metadataList = createMetadata(0, mediaType);
	}

	function createPage(title: string, queryHash: string, totalCount: number, page = 1): PlexMediaStatisticsDTO {
		const mediaList = generatePlexMediaSlims({
			config: { seed: 263, movieCount: 1 },
			partialData: {
				id: title.charCodeAt(0),
				title,
				searchTitle: title.toLowerCase(),
				plexServerId: 1,
				plexLibraryId: 0,
				type: PlexMediaType.Movie,
			},
		});
		const pageData = generatePlexMediaStatisticsDTO(mediaList);
		pageData.page = page;
		pageData.pageSize = 100;
		pageData.queryHash = queryHash;
		pageData.mediaCount = totalCount;
		pageData.totalCount = totalCount;
		return pageData;
	}

	function createLibrary(id: number): PlexLibraryDTO {
		return {
			id,
			type: PlexMediaType.Movie,
			isEnabled: true,
			syncedAt: '2026-01-01T00:00:00.000Z',
		} as PlexLibraryDTO;
	}

	function createMetadata(mediaCount: number, mediaType: PlexMediaType): PlexMediaMetadataDTO {
		return {
			mediaCount,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roleCount: 0,
			countries: [],
			genres: [],
			qualities: [],
			roles: [],
			supportedSorts: generateSupportedSorts(mediaType),
		};
	}

	function createFilterMetadata(ids: number): PlexMediaFilterMetadataDTO {
		return {
			countries: [ids],
			genres: [ids + 1],
			qualities: [ids + 2],
			roles: [ids + 3],
		};
	}

	test('Should load required metadata after a search supersedes a pending library lookup', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const libraryStore = useLibraryStore();
		const library = createLibrary(4);
		libraryStore.updateLibrary(library);
		const libraryResponse = new Subject<ResultDTO<PlexLibraryDTO>>();
		const searchMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const initializationMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const filterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const searchResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const secondPage = createPage('S', 'query-second', 1);
		let metadataCall = 0;
		const metadataRequest = vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation(() =>
			metadataCall++ === 0
				? searchMetadataResponse.asObservable()
				: initializationMetadataResponse.asObservable());
		const filterMetadataRequest = vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockReturnValue(filterMetadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getPlexLibraryByIdEndpoint').mockReturnValue(libraryResponse.asObservable());
		const mediaRequest = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockReturnValue(searchResponse.asObservable());

		// Act
		const initialization = subscribeSpyTo(store.initializeLibrary(library.id));
		const search = subscribeSpyTo(store.setFilterQuery('second'));
		expect(metadataRequest).toHaveBeenCalledTimes(1);
		expect(filterMetadataRequest).not.toHaveBeenCalled();
		expect(mediaRequest).not.toHaveBeenCalled();
		searchMetadataResponse.next(generateResultDTO(createMetadata(24, PlexMediaType.Movie)));
		searchMetadataResponse.complete();
		expect(mediaRequest).toHaveBeenCalledTimes(1);
		searchResponse.next(generateResultDTO(secondPage));
		searchResponse.complete();
		await search.onComplete();
		libraryResponse.next(generateResultDTO(library));
		libraryResponse.complete();
		expect(metadataRequest).toHaveBeenCalledTimes(2);
		expect(filterMetadataRequest).toHaveBeenCalledTimes(1);
		initializationMetadataResponse.next(generateResultDTO(createMetadata(42, PlexMediaType.Movie)));
		initializationMetadataResponse.complete();
		filterMetadataResponse.next(generateResultDTO(createFilterMetadata(40)));
		filterMetadataResponse.complete();
		await initialization.onComplete();

		// Assert
		expect(mediaRequest).toHaveBeenCalledWith(expect.objectContaining({ q: 'second' }));
		expect(store.filterQuery).toBe('second');
		expect(store.metadataList.mediaCount).toBe(42);
		expect(store.availableCountryIds).toEqual([40]);
		expect(store.availableGenreIds).toEqual([41]);
		expect(store.availableQualityIds).toEqual([42]);
		expect(store.availableRoleIds).toEqual([43]);
		expect(store.getMediaItems).toEqual(secondPage.mediaList);
	});

	test('Should retain library metadata when a query supersedes the initial media request', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const libraryStore = useLibraryStore();
		const library = createLibrary(7);
		libraryStore.updateLibrary(library);
		const initialMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const queryMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const filterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const initialMediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const queryMediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		let metadataCall = 0;
		const metadataRequest = vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation(() =>
			metadataCall++ === 0
				? initialMetadataResponse.asObservable()
				: queryMetadataResponse.asObservable());
		const filterMetadataRequest = vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockReturnValue(filterMetadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getPlexLibraryByIdEndpoint').mockReturnValue(of(generateResultDTO(library)));
		const queryPage = createPage('Q', 'query-new', 1);
		const mediaRequest = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockImplementation((query) =>
			query.q === 'new-query' ? queryMediaResponse.asObservable() : initialMediaResponse.asObservable());
		const initialization = subscribeSpyTo(store.initializeLibrary(library.id));

		// Act
		const query = subscribeSpyTo(store.setFilterQuery('new-query'));

		// Assert
		expect(metadataRequest).toHaveBeenCalledTimes(2);
		expect(filterMetadataRequest).toHaveBeenCalledTimes(1);
		expect(mediaRequest).not.toHaveBeenCalled();
		expect(initialMediaResponse.observers).toHaveLength(0);

		// Act
		initialMetadataResponse.next(generateResultDTO(createMetadata(11, PlexMediaType.Movie)));
		initialMetadataResponse.complete();
		expect(mediaRequest).not.toHaveBeenCalled();
		queryMetadataResponse.next(generateResultDTO(createMetadata(42, PlexMediaType.Movie)));
		queryMetadataResponse.complete();
		expect(mediaRequest).toHaveBeenCalledTimes(1);
		queryMediaResponse.next(generateResultDTO(queryPage));
		queryMediaResponse.complete();
		await query.onComplete();
		filterMetadataResponse.next(generateResultDTO(createFilterMetadata(40)));
		filterMetadataResponse.complete();
		await initialization.onComplete();

		// Assert
		expect(store.filterQuery).toBe('new-query');
		expect(store.metadataList.mediaCount).toBe(42);
		expect(store.availableCountryIds).toEqual([40]);
		expect(store.availableGenreIds).toEqual([41]);
		expect(store.availableQualityIds).toEqual([42]);
		expect(store.availableRoleIds).toEqual([43]);
		expect(store.getMediaItems).toEqual(queryPage.mediaList);
	});
	test('Should cancel obsolete library metadata and media when switching libraries', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const libraryStore = useLibraryStore();
		const firstLibrary = createLibrary(1);
		const secondLibrary = createLibrary(2);
		libraryStore.updateLibrary(firstLibrary);
		libraryStore.updateLibrary(secondLibrary);
		const firstLibraryResponse = new Subject<ResultDTO<PlexLibraryDTO>>();
		const secondLibraryResponse = new Subject<ResultDTO<PlexLibraryDTO>>();
		const firstMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const secondMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const firstFilterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const secondFilterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const firstMediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const secondMediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		vi.spyOn(plexLibraryApi, 'getPlexLibraryByIdEndpoint').mockImplementation((libraryId) =>
			libraryId === firstLibrary.id ? firstLibraryResponse.asObservable() : secondLibraryResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation((libraryId) =>
			libraryId === firstLibrary.id ? firstMetadataResponse.asObservable() : secondMetadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockImplementation((libraryId) =>
			libraryId === firstLibrary.id ? firstFilterMetadataResponse.asObservable() : secondFilterMetadataResponse.asObservable());
		vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockImplementation((query) =>
			query.plexLibraryId === firstLibrary.id ? firstMediaResponse.asObservable() : secondMediaResponse.asObservable());
		const firstInitialization = subscribeSpyTo(store.initializeLibrary(firstLibrary.id));
		firstLibraryResponse.next(generateResultDTO(firstLibrary));
		const secondInitialization = subscribeSpyTo(store.initializeLibrary(secondLibrary.id));

		// Act
		await firstInitialization.onComplete();
		firstMetadataResponse.next(generateResultDTO(createMetadata(11, PlexMediaType.Movie)));
		firstFilterMetadataResponse.next(generateResultDTO(createFilterMetadata(10)));
		firstMediaResponse.next(generateResultDTO(createPage('O', 'old-library', 1)));
		firstMetadataResponse.complete();
		firstFilterMetadataResponse.complete();
		firstMediaResponse.complete();
		expect(store.libraryId).toBe(secondLibrary.id);
		expect(store.metadataList.mediaCount).toBe(0);
		expect(store.availableRoleIds).toEqual([]);
		expect(store.getMediaItems).toEqual([]);
		secondLibraryResponse.next(generateResultDTO(secondLibrary));
		secondLibraryResponse.complete();
		secondMetadataResponse.next(generateResultDTO(createMetadata(22, PlexMediaType.Movie)));
		secondMetadataResponse.complete();
		secondFilterMetadataResponse.next(generateResultDTO(createFilterMetadata(20)));
		secondFilterMetadataResponse.complete();
		const newPage = createPage('N', 'new-library', 1);
		secondMediaResponse.next(generateResultDTO(newPage));
		secondMediaResponse.complete();
		await secondInitialization.onComplete();

		// Assert
		expect(store.libraryId).toBe(secondLibrary.id);
		expect(store.metadataList.mediaCount).toBe(22);
		expect(store.availableCountryIds).toEqual([20]);
		expect(store.availableRoleIds).toEqual([23]);
		expect(store.getMediaItems).toEqual(newPage.mediaList);
	});

	test('Should ignore an obsolete refresh response and preserve active loading controls', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const firstPage = createPage('A', 'query-a', 100);
		const secondPage = createPage('B', 'query-b', 50);
		const firstResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const secondResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const request = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockImplementation((query) => {
			return query.q === 'second' ? secondResponse.asObservable() : firstResponse.asObservable();
		});

		// Act
		const first = subscribeSpyTo(store.refreshMediaData());
		const second = subscribeSpyTo(store.setFilterQuery('second'));
		expect(store.loading).toBe(true);
		secondResponse.next(generateResultDTO(secondPage));
		secondResponse.complete();
		await second.onComplete();
		firstResponse.next(generateResultDTO(firstPage));
		firstResponse.complete();
		await first.onComplete();

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(store.getMediaItems).toEqual(secondPage.mediaList);
		expect(store.queryHash).toBe('query-b');
		expect(store.itemsLength).toBe(1);
		expect(store.totalCount).toBe(50);
		expect(store.loading).toBe(false);
		expect(store.mediaLoadError).toBe(false);
	});

	test('Should deduplicate same-query concurrent pagination and release navigation loading once', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const pageResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const request = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockReturnValue(pageResponse.asObservable());

		// Act
		const first = subscribeSpyTo(store.requestRange(100, 150));
		const second = subscribeSpyTo(store.requestRange(100, 150));
		expect(request).toHaveBeenCalledTimes(1);
		expect(store.navLoading).toBe(true);
		const page = createPage('P', 'pagination-query', 250, 2);
		pageResponse.next(generateResultDTO(page));
		pageResponse.complete();
		await first.onComplete();
		await second.onComplete();

		// Assert
		expect(store.getMediaItems).toEqual(page.mediaList);
		expect(store.itemsLength).toBe(1);
		expect(store.totalCount).toBe(250);
		expect(store.navLoading).toBe(false);
	});

	test('Should wait for capabilities before the first media request without losing a restored audio sort', async () => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		const sortQuery = useRouteQuery<string>('sort', '');
		setSortMetadata(PlexMediaType.MusicArtist);
		set(sortQuery, `${SortType.AudioQuality}:desc`);
		const store = useMediaOverviewStore();
		const metadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const filterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const mediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const metadataRequest = vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockReturnValue(metadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockReturnValue(filterMetadataResponse.asObservable());
		const mediaRequest = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockReturnValue(mediaResponse.asObservable());

		// Act
		const initialization = subscribeSpyTo(store.initializeLibrary(0));

		// Assert
		expect(metadataRequest).toHaveBeenCalledTimes(1);
		expect(mediaRequest).not.toHaveBeenCalled();
		expect(store.sortedState).toEqual({ field: SortType.AudioQuality, sort: SortDirection.Desc });

		filterMetadataResponse.next(generateResultDTO(createFilterMetadata(1)));
		filterMetadataResponse.complete();
		metadataResponse.next(generateResultDTO(createMetadata(1, PlexMediaType.MusicArtist)));
		metadataResponse.complete();
		expect(mediaRequest).toHaveBeenCalledTimes(1);
		expect(mediaRequest.mock.calls[0]?.[0]?.sort).toBe(`${SortType.AudioQuality}:desc`);

		mediaResponse.next(generateResultDTO(createPage('A', 'music-query', 1)));
		mediaResponse.complete();
		await initialization.onComplete();

		expect(store.sortedState).toEqual({ field: SortType.AudioQuality, sort: SortDirection.Desc });
		expect(get(sortQuery)).toBe(`${SortType.AudioQuality}:desc`);
	});

	test('Should stop initialization when capability metadata fails without replacing a restored audio sort', async () => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		const sortQuery = useRouteQuery<string>('sort', '');
		setSortMetadata(PlexMediaType.MusicArtist);
		set(sortQuery, `${SortType.AudioQuality}:desc`);
		const store = useMediaOverviewStore();
		const metadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const filterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const mediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		const mediaRequest = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockReturnValue(mediaResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockReturnValue(metadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockReturnValue(filterMetadataResponse.asObservable());

		// Act
		const initialization = subscribeSpyTo(store.initializeLibrary(0));
		filterMetadataResponse.next(generateResultDTO(createFilterMetadata(1)));
		filterMetadataResponse.complete();
		metadataResponse.next({
			isSuccess: false,
			statusCode: 500,
			errors: [],
			successes: [],
			value: null,
		});
		metadataResponse.complete();
		await initialization.onComplete();

		// Assert
		expect(mediaRequest).not.toHaveBeenCalled();
		expect(store.mediaLoadError).toBe(true);
		expect(store.loading).toBe(false);
		expect(store.sortedState).toEqual({ field: SortType.AudioQuality, sort: SortDirection.Desc });
		expect(get(sortQuery)).toBe(`${SortType.AudioQuality}:desc`);
	});

	test('Should reload capabilities before retrying a restored audio sort after metadata failure', async () => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		const sortQuery = useRouteQuery<string>('sort', '');
		setSortMetadata(PlexMediaType.MusicArtist);
		set(sortQuery, `${SortType.AudioQuality}:desc`);
		const store = useMediaOverviewStore();
		const firstMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const secondMetadataResponse = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		const filterMetadataResponse = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		const mediaResponse = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		let metadataCall = 0;
		const metadataRequest = vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation(() =>
			metadataCall++ === 0 ? firstMetadataResponse.asObservable() : secondMetadataResponse.asObservable());
		vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockReturnValue(filterMetadataResponse.asObservable());
		const mediaRequest = vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockReturnValue(mediaResponse.asObservable());

		// Act
		const initialization = subscribeSpyTo(store.initializeLibrary(0));
		filterMetadataResponse.next(generateResultDTO(createFilterMetadata(1)));
		filterMetadataResponse.complete();
		firstMetadataResponse.next({
			isSuccess: false,
			statusCode: 500,
			errors: [],
			successes: [],
			value: null,
		});
		firstMetadataResponse.complete();
		await initialization.onComplete();
		expect(mediaRequest).not.toHaveBeenCalled();

		const retry = subscribeSpyTo(store.retryMediaLoad());
		expect(metadataRequest).toHaveBeenCalledTimes(2);
		expect(mediaRequest).not.toHaveBeenCalled();
		secondMetadataResponse.next(generateResultDTO(createMetadata(1, PlexMediaType.MusicArtist)));
		secondMetadataResponse.complete();
		expect(mediaRequest).toHaveBeenCalledTimes(1);
		expect(mediaRequest.mock.calls[0]?.[0]?.sort).toBe(`${SortType.AudioQuality}:desc`);
		mediaResponse.next(generateResultDTO(createPage('A', 'retry-query', 1)));
		mediaResponse.complete();
		await retry.onComplete();

		// Assert
		expect(store.mediaLoadError).toBe(false);
		expect(store.sortedState).toEqual({ field: SortType.AudioQuality, sort: SortDirection.Desc });
		expect(get(sortQuery)).toBe(`${SortType.AudioQuality}:desc`);
	});
});
