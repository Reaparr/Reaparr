import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { useMediaOverviewStore } from '@store';
import { type PlexMediaSlimDTO, PlexMediaType } from '@dto';

describe('MediaOverviewStore.requestMedia()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		useMediaOverviewStore().metadataList = {
			mediaCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roleCount: 0,
			countries: [],
			genres: [],
			qualities: [],
			roles: [],
			supportedSorts: generateSupportedSorts(PlexMediaType.Movie),
		};
	});

	test('Should set media in MediaOverviewStore when media is retrieved with success', async () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: {
				movieCount: 100,
			},
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type,
			},
		}));
		movies.navigationIndexes = [
			{ label: 'A', index: 0 },
			{ label: 'B', index: 14 },
		];

		let url = new RegExp(`/api/PlexMedia/*`);
		mock.onGet(url).reply(200, generateResultDTO(movies));
		url = new RegExp(`/api/PlexLibrary/0/metadata`);
		mock.onGet(url).reply((request) => [200, generateResultDTO({
			mediaCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roleCount: 0,
			countries: [],
			genres: [],
			qualities: [],
			roles: [],
			supportedSorts: generateSupportedSorts(request.params.mediaType as PlexMediaType),
		})]);

		// Act
		const result = subscribeSpyTo(mediaOverviewStore.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(result.receivedComplete()).toEqual(true);
		expect([...mediaOverviewStore.scrollDict.entries()]).toEqual([
			['A', 0],
			['B', 14],
		]);
		expect(mediaOverviewStore.allMovieCount).toEqual(movies.movieCount);
		expect(mediaOverviewStore.allTvShowCount).toEqual(movies.tvShowCount);
		expect(mediaOverviewStore.allSeasonCount).toEqual(movies.seasonCount);
		expect(mediaOverviewStore.allEpisodeCount).toEqual(movies.episodeCount);
		expect(mediaOverviewStore.allFileSize).toEqual(movies.mediaSize);
		expect(mediaOverviewStore.loading).toEqual(false);
	});

	test('Should request each unloaded page only once while a range request is in flight', async () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const pageItems = generatePlexMediaSlims({
			config: {
				movieCount: 10,
			},
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}).map((item, index) => ({
			...item,
			sortIndex: 1001 + index,
		})) as PlexMediaSlimDTO[];
		const pageTwo = generatePlexMediaStatisticsDTO(pageItems);
		pageTwo.page = 11;
		pageTwo.pageSize = 100;
		pageTwo.mediaCount = 2000;

		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(pageTwo));

		// Act
		const first = subscribeSpyTo(mediaOverviewStore.requestRange(1000, 1050));
		const second = subscribeSpyTo(mediaOverviewStore.requestRange(1000, 1050));
		await first.onComplete();
		await second.onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(1);
		expect(mediaOverviewStore.getMediaItems).toEqual(pageItems);
		expect(mediaOverviewStore.getMediaItems[0]).toStrictEqual(pageItems[0]);
		expect(mediaOverviewStore.getMediaItemsForRange(1000, 1003)).toEqual(pageItems.slice(0, 3));
	});

	test('Should not re-request a page that is already cached after the first request completes', async () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const pageItems = generatePlexMediaSlims({
			config: {
				movieCount: 10,
			},
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}).map((item, index) => ({
			...item,
			sortIndex: 1001 + index,
		})) as PlexMediaSlimDTO[];
		const pageTwo = generatePlexMediaStatisticsDTO(pageItems);
		pageTwo.page = 2;
		pageTwo.pageSize = 100;
		pageTwo.mediaCount = 2000;

		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(pageTwo));

		// Act
		const first = subscribeSpyTo(mediaOverviewStore.requestMediaPage(2));
		await first.onComplete();
		const second = subscribeSpyTo(mediaOverviewStore.requestMediaPage(2));
		await second.onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(1);
	});

	test('Should replace a cached page on force refresh while preserving the backend total', async () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const firstItems = generatePlexMediaSlims({
			config: { movieCount: 2 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}) as PlexMediaSlimDTO[];
		const secondItems = generatePlexMediaSlims({
			config: { movieCount: 3 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}) as PlexMediaSlimDTO[];
		const firstPage = generatePlexMediaStatisticsDTO(firstItems);
		firstPage.page = 1;
		firstPage.pageSize = 100;
		firstPage.queryHash = 'force-refresh-query';
		firstPage.mediaCount = 250;
		firstPage.totalCount = 250;
		const secondPage = generatePlexMediaStatisticsDTO(secondItems);
		secondPage.page = 1;
		secondPage.pageSize = 100;
		secondPage.queryHash = firstPage.queryHash;
		secondPage.mediaCount = 250;
		secondPage.totalCount = 250;
		const mediaRequestMatcher = new RegExp(`/api/PlexMedia`);
		mock.onGet(mediaRequestMatcher).replyOnce(200, generateResultDTO(firstPage));
		mock.onGet(mediaRequestMatcher).reply(200, generateResultDTO(secondPage));

		// Act
		await subscribeSpyTo(mediaOverviewStore.requestMediaPage(1)).onComplete();
		await subscribeSpyTo(mediaOverviewStore.requestMediaPage(1, 100, true)).onComplete();

		// Assert
		expect(mediaOverviewStore.getMediaItems).toEqual(secondItems);
		expect(mediaOverviewStore.itemsLength).toBe(secondItems.length);
		expect(mediaOverviewStore.totalCount).toBe(250);
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(2);
	});

	test('Should allow retrying a page request after a null response', async () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const pageItems = generatePlexMediaSlims({
			config: {
				movieCount: 10,
			},
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}).map((item, index) => ({
			...item,
			sortIndex: 1001 + index,
		})) as PlexMediaSlimDTO[];
		const pageTwo = generatePlexMediaStatisticsDTO(pageItems);
		pageTwo.page = 2;
		pageTwo.pageSize = 100;
		pageTwo.mediaCount = 2000;

		const mediaRequestMatcher = new RegExp(`/api/PlexMedia`);
		mock.onGet(mediaRequestMatcher).replyOnce(200, generateResultDTO(null));
		mock.onGet(mediaRequestMatcher).reply(200, generateResultDTO(pageTwo));

		// Act
		const first = subscribeSpyTo(mediaOverviewStore.requestMediaPage(2));
		await first.onComplete();
		const second = subscribeSpyTo(mediaOverviewStore.requestMediaPage(2));
		await second.onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(2);
		expect(mediaOverviewStore.getMediaItems).toEqual(pageItems);
	});

	test('Should clear old mediaPages and refill with new data when queryHash changes', () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		const firstItems = generatePlexMediaSlims({
			config: { movieCount: 2 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}) as PlexMediaSlimDTO[];
		const secondItems = generatePlexMediaSlims({
			config: { movieCount: 3 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}) as PlexMediaSlimDTO[];

		const firstPage = generatePlexMediaStatisticsDTO(firstItems);
		firstPage.page = 1;
		firstPage.queryHash = 'hash-1';
		firstPage.mediaCount = firstItems.length;
		firstPage.totalCount = firstItems.length;
		firstPage.navigationIndexes = [
			{ label: 'A', index: 0 },
			{ label: 'B', index: 1 },
		];

		const secondPage = generatePlexMediaStatisticsDTO(secondItems);
		secondPage.page = 1;
		secondPage.queryHash = 'hash-2';
		secondPage.mediaCount = secondItems.length;
		secondPage.totalCount = secondItems.length;
		secondPage.navigationIndexes = [
			{ label: 'C', index: 0 },
			{ label: 'D', index: 2 },
		];

		// Act
		mediaOverviewStore.addMediaPage(firstPage);
		mediaOverviewStore.addMediaPage(secondPage);

		// Assert
		expect(mediaOverviewStore.queryHash).toBe('hash-2');
		expect(mediaOverviewStore.getMediaItems).toEqual(secondItems);
		expect(mediaOverviewStore.itemsLength).toBe(secondItems.length);
		expect(mediaOverviewStore.totalCount).toBe(secondItems.length);
		expect([...mediaOverviewStore.scrollDict.entries()]).toEqual([
			['C', 0],
			['D', 2],
		]);
	});

	test('Should preserve the URL scroll index when the first media page initializes the cache', () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		mediaOverviewStore.setCurrentScrollIndex(199);
		const media = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 1 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 1,
				type: PlexMediaType.Movie,
			},
		}));
		media.queryHash = 'initial-query';

		// Act
		mediaOverviewStore.addMediaPage(media);

		// Assert
		expect(mediaOverviewStore.currentScrollIndex).toBe(199);
	});

	test('Should consume a pending media highlight only for the matching media', () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		mediaOverviewStore.setPendingMediaHighlight(42, 1);

		// Act
		mediaOverviewStore.consumePendingMediaHighlight(41);

		// Assert
		expect(mediaOverviewStore.pendingMediaHighlightId).toBe(42);

		mediaOverviewStore.consumePendingMediaHighlight(42);
		expect(mediaOverviewStore.pendingMediaHighlightId).toBeNull();
	});

	test('Should preserve a pending media highlight for the all-media overview', () => {
		// Arrange
		const mediaOverviewStore = useMediaOverviewStore();
		mediaOverviewStore.setPendingMediaHighlight(42, 0);

		// Act
		mediaOverviewStore.initializeLibrary(0);

		// Assert
		expect(mediaOverviewStore.pendingMediaHighlightId).toBe(42);
		expect(mediaOverviewStore.pendingMediaHighlightLibraryId).toBe(0);
	});
});
