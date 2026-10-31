import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import { type PlexMediaStatisticsDTO, PlexMediaType } from '@dto';

describe('MediaOverviewStore.setMedia()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	function setupMocks(mediaStats: PlexMediaStatisticsDTO) {
		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(mediaStats));
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
	}

	test('Should preserve filterQuery when new media is set', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 10 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));
		store.filterQuery = 'some-filter';

		// Act
		store.addMediaPage(movies);

		// Assert
		expect(store.filterQuery).toBe('some-filter');
	});

	test('Should set all count fields correctly from PlexMediaStatisticsDTO', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 42 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));
		setupMocks(movies);

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(store.allMovieCount).toBe(movies.movieCount);
		expect(store.allTvShowCount).toBe(movies.tvShowCount);
		expect(store.allSeasonCount).toBe(movies.seasonCount);
		expect(store.allEpisodeCount).toBe(movies.episodeCount);
		expect(store.allFileSize).toBe(movies.mediaSize);
		expect(store.itemsLength).toBe(movies.mediaList.length);
		expect(store.totalCount).toBe(movies.mediaCount);
	});

	test('Should set all counts to zero when API returns null data (isSuccess: false)', async () => {
		// Arrange
		const store = useMediaOverviewStore();

		// Return failed result for media endpoint
		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, {
			isSuccess: false,
			statusCode: 400,
			errors: [],
			successes: [],
			value: null,
		});
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

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert — null data path in setMedia
		expect(store.allMovieCount).toBe(0);
		expect(store.allTvShowCount).toBe(0);
		expect(store.allSeasonCount).toBe(0);
		expect(store.allEpisodeCount).toBe(0);
		expect(store.allFileSize).toBe(0);
		expect(store.itemsLength).toBe(0);
		expect(store.getMediaItems).toEqual([]);
	});

	test('Should use API mediaCount as total count while keeping loaded item count', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const type = PlexMediaType.TvShow;
		const tvShows = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { tvShowCount: 3, seasonCount: 1, episodeCount: 1 },
			partialData: { plexServerId: 1, plexLibraryId: 23, type },
		}));
		tvShows.mediaCount = 100;
		tvShows.totalCount = 100;
		tvShows.tvShowCount = tvShows.mediaCount;
		setupMocks(tvShows);

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(store.getMediaItems.length).toBe(3);
		expect(store.itemsLength).toBe(3);
		expect(store.totalCount).toBe(100);
	});

	test('Should keep loaded page count separate from backend totalCount', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 100 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));
		movies.mediaList = movies.mediaList.slice(0, 10);
		movies.mediaCount = 100;
		movies.movieCount = 100;
		movies.totalMovieCount = 100;
		movies.totalCount = 100;
		setupMocks(movies);

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(store.getMediaItems.length).toBe(10);
		expect(store.itemsLength).toBe(10);
		expect(store.totalCount).toBe(100);
	});

	test('Should preserve the backend total while caching and replacing partial pages', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const createPage = (page: number, count: number) => {
			const mediaList = generatePlexMediaSlims({
				config: { seed: 700 + page, movieCount: count },
				partialData: { plexServerId: 1, plexLibraryId: 0, type: PlexMediaType.Movie },
			});
			const statistics = generatePlexMediaStatisticsDTO(mediaList);
			statistics.page = page;
			statistics.pageSize = 100;
			statistics.queryHash = 'total-query';
			statistics.mediaCount = 250;
			statistics.totalCount = 250;
			return statistics;
		};
		const firstPage = createPage(1, 100);
		const secondPage = createPage(2, 100);
		const thirdPage = createPage(3, 50);
		store.addMediaPage(firstPage);
		store.addMediaPage(secondPage);
		store.addMediaPage(thirdPage);

		// Act
		const replacementPage = createPage(2, 80);
		store.addMediaPage(replacementPage);

		// Assert
		expect(store.totalCount).toBe(250);
		expect(store.itemsLength).toBe(230);
		expect(store.getMediaItems).toEqual([
			...firstPage.mediaList,
			...replacementPage.mediaList,
			...thirdPage.mediaList,
		]);
	});

	test('Should correctly set TV show counts from statistics DTO', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const type = PlexMediaType.TvShow;
		const tvShows = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { tvShowCount: 15, seasonCount: 3, episodeCount: 5 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));
		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(tvShows));
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

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(store.allTvShowCount).toBe(tvShows.tvShowCount);
		expect(store.itemsLength).toBe(tvShows.mediaCount);
	});
});
