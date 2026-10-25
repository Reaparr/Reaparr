import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { get, set } from '@vueuse/core';
import { useRouteQuery } from '@vueuse/router';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMedia,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import { AudioQuality, PlexMediaComparisonState, PlexMediaType, SortDirection, SortType, VideoQuality } from '@dto';
import { flushPromises } from '@vue/test-utils';

describe('MediaOverviewStore - Filter / Search', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	async function loadMovies(store: ReturnType<typeof useMediaOverviewStore>, count = 20) {
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: count },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));

		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(movies));
		mock.onGet(new RegExp(`/api/PlexLibrary/0/metadata`)).reply((request) => [200, generateResultDTO({
			episodeCount: 0,
			mediaCount: 0,
			mediaSize: 0,
			movieCount: 0,
			seasonCount: 0,
			tvShowCount: 0,
			mediaList: [],
			roles: [],
			countries: [],
			genres: [],
			qualities: [],
			roleCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			supportedSorts: generateSupportedSorts(request.params.mediaType as PlexMediaType),
		})]);

		await subscribeSpyTo(store.refreshMediaData()).onComplete();
		return movies;
	}

	test('getMediaItems should return all items when filterQuery is empty', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const movies = await loadMovies(store, 20);

		// Assert
		expect(store.getMediaItems.length).toBe(movies.mediaCount);
	});

	test('getMediaItems should return backend items when filterQuery is set', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const movies = await loadMovies(store, 20);

		// Act
		store.filterQuery = 'backend-search';

		// Assert
		expect(store.getMediaItems.length).toBe(movies.mediaCount);
	});

	test('hasNoSearchResults should be true when backend returns no items for filterQuery', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);
		store.addMediaPage(generatePlexMediaStatisticsDTO([]));

		// Act
		store.filterQuery = 'zzz-no-match-xyz-impossible-string';

		// Assert
		expect(store.hasNoSearchResults).toBe(true);
		expect(store.getMediaItems.length).toBe(0);
	});

	test('hasNoSearchResults should be false when filterQuery is empty', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);

		// Assert
		expect(store.hasNoSearchResults).toBe(false);
	});

	test('clearFilter should reset filterQuery and remove search from route and requests', async () => {
		// Arrange
		const searchQuery = useRouteQuery('q', '');
		set(searchQuery, undefined);
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);

		// Act
		await subscribeSpyTo(store.setFilterQuery('some-search')).onComplete();
		expect(get(searchQuery)).toBe('some-search');
		await subscribeSpyTo(store.clearFilter()).onComplete();

		// Assert
		expect(store.filterQuery).toBe('');
		expect(get(useRouteQuery('q', ''))).toBe('');
		const mediaRequests = mock.history.get.filter((request) => request.url === '/api/PlexMedia');
		expect(mediaRequests.at(-1)).toMatchObject({ params: { q: '' } });

		await subscribeSpyTo(store.initializeLibrary(0)).onComplete();

		expect(store.filterQuery).toBe('');
		expect(get(useRouteQuery('q', ''))).toBe('');
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia').at(-1)).toMatchObject({ params: { q: '' } });
	});

	test('setFilterQuery should request media from the backend', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const firstResponse = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 20 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type: PlexMediaType.Movie },
		}));
		const secondResponse = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 3 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type: PlexMediaType.Movie },
		}));

		mock.onGet(new RegExp(`/api/PlexMedia`))
			.replyOnce(200, generateResultDTO(firstResponse))
			.onGet(new RegExp(`/api/PlexMedia`))
			.reply(200, generateResultDTO(secondResponse));
		mock.onGet(new RegExp(`/api/PlexLibrary/0/metadata`)).reply((request) => [200, generateResultDTO({
			episodeCount: 0,
			mediaCount: 0,
			mediaSize: 0,
			movieCount: 0,
			seasonCount: 0,
			tvShowCount: 0,
			mediaList: [],
			roles: [],
			countries: [],
			genres: [],
			qualities: [],
			roleCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			supportedSorts: generateSupportedSorts(request.params.mediaType as PlexMediaType),
		})]);
		await subscribeSpyTo(store.refreshMediaData()).onComplete();

		// Act
		const result = subscribeSpyTo(store.setFilterQuery('matrix'));
		await result.onComplete();

		// Assert
		expect(store.filterQuery).toBe('matrix');
		expect(store.getMediaItems.length).toBe(secondResponse.mediaCount);
	});

	for (const family of [PlexMediaType.MusicArtist, PlexMediaType.PhotoAlbum]) {
		test(`Should remove stale Movie upgrade filters and quality sorting when browsing ${family}`, async () => {
			// Arrange
			const settings = useSettingsStore();
			settings.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
			const store = useMediaOverviewStore();
			mock.onGet(/\/api\/PlexLibrary\/0\/metadata/).reply((request) => [200, generateResultDTO({
				mediaCount: 1, countryCount: 0, genreCount: 0, roleCount: 0, qualityCount: 0,
				countries: [], genres: [], roles: [], qualities: [],
				supportedSorts: generateSupportedSorts(request.params.mediaType as PlexMediaType),
			})]);
			mock.onGet('/api/PlexMedia').reply((request) => {
				const type = request.params.mediaType as PlexMediaType;
				const unsupported = type !== PlexMediaType.Movie && (
					request.params.qualityId
					|| [PlexMediaComparisonState.HigherQuality, PlexMediaComparisonState.PartialAndHigherQuality].includes(request.params.comparisonState)
					|| (type === PlexMediaType.PhotoAlbum && request.params.comparisonState)
					|| request.params.sort.startsWith('videoQuality:')
				);
				const item = generatePlexMedia({
					config: { seed: 625 }, partialData: { id: 100, type, plexLibraryId: 4, plexServerId: 3, title: 'Root item' },
				});
				const response = generatePlexMediaStatisticsDTO(unsupported ? [] : [item]);
				response.totalCount = response.mediaCount;
				response.queryHash = `${type}-${request.params.sort}`;
				return [200, generateResultDTO(response)];
			});
			await subscribeSpyTo(store.setQualityFilter(1080)).onComplete();
			await subscribeSpyTo(store.setComparisonStateFilter(PlexMediaComparisonState.HigherQuality)).onComplete();
			store.toggleSortMedia(SortType.VideoQuality);
			await flushPromises();

			// Act
			settings.displaySettings.allOverviewViewMode = family;
			await subscribeSpyTo(store.initializeLibrary(0)).onComplete();

			// Assert
			expect(store.getMediaItems.map((item) => item.type)).toEqual([family]);
			expect(store.metadata.qualityId).toBe(0);
			expect(store.metadata.comparisonState).toBeNull();
			expect(store.getActiveSort).toEqual({ field: SortType.Title, sort: SortDirection.Asc });
			expect(store.getFilterChips).toEqual([]);
		});
	}

	test('Should retain Other Videos quality filtering without sending a comparison filter', async () => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.OtherVideos;
		const store = useMediaOverviewStore();
		store.metadataList = {
			mediaCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roleCount: 0,
			countries: [],
			genres: [],
			qualities: [],
			roles: [],
			supportedSorts: generateSupportedSorts(PlexMediaType.OtherVideos),
		};
		const videos = [VideoQuality.FullHD, VideoQuality.HD].map((videoQuality, index) => generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id: 11 + index, type: PlexMediaType.OtherVideos, plexLibraryId: 5, plexServerId: 3,
				qualities: [{ dataId: 100 + index, mediaId: 11 + index, mediaDataType: PlexMediaType.OtherVideos, audioQuality: AudioQuality.None, videoQuality }],
			},
		}));
		mock.onGet('/api/PlexMedia').reply((request) => {
			const items = request.params.comparisonState ? [] : request.params.qualityId === 1080 ? videos.slice(0, 1) : videos;
			const response = generatePlexMediaStatisticsDTO(items);
			response.totalCount = items.length;
			response.queryHash = `other-${request.params.qualityId ?? 'all'}`;
			return [200, generateResultDTO(response)];
		});
		await subscribeSpyTo(store.refreshMediaData()).onComplete();
		expect(store.getMediaItems.map((item) => item.id)).toEqual([11, 12]);

		// Act
		await subscribeSpyTo(store.setQualityFilter(1080)).onComplete();
		await subscribeSpyTo(store.setComparisonStateFilter(PlexMediaComparisonState.NotCompared)).onComplete();

		// Assert
		expect(store.getMediaItems.map((item) => item.id)).toEqual([11]);
		expect(store.getMediaItems[0]?.qualities[0]?.videoQuality).toBe(VideoQuality.FullHD);
		expect(store.metadata.comparisonState).toBeNull();
	});
});
