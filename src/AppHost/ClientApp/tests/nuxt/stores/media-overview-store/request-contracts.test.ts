import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
} from '@mock';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import {
	PlexMediaComparisonState,
	PlexMediaType,
	type PlexLibraryDTO,
	type PlexMediaSlimDTO,
	type PlexMediaStatisticsDTO,
} from '@dto';
import { MediaSortField, SortDirection } from '@enums/mediaSortField';

describe('MediaOverviewStore - Request Contracts', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	function createMediaStatistics(count = 5, queryHash = 'query-hash'): PlexMediaStatisticsDTO {
		const mediaItems = generatePlexMediaSlims({
			config: { movieCount: count },
			partialData: { plexServerId: 1, plexLibraryId: 1, type: PlexMediaType.Movie },
		}).map((item, index) => ({
			...item,
			id: index + 1,
			sortIndex: index + 1,
		})) as PlexMediaSlimDTO[];
		const statistics = generatePlexMediaStatisticsDTO(mediaItems);
		statistics.page = 1;
		statistics.pageSize = 100;
		statistics.queryHash = queryHash;
		statistics.totalCount = mediaItems.length;
		statistics.roles = [11, 12];
		statistics.countries = [21, 22];
		statistics.genres = [31, 32];
		statistics.qualities = [41, 42];
		statistics.navigationIndexes = [
			{ label: 'A', index: 0 },
			{ label: 'Z', index: mediaItems.length - 1 },
		];
		return statistics;
	}

	function mockMediaResponse(response = createMediaStatistics()) {
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, generateResultDTO(response));
		return response;
	}

	function lastMediaRequest() {
		const request = mock.history.get.filter((x) => x.url === '/api/PlexMedia').at(-1);
		if (!request) {
			throw new Error('Expected a PlexMedia request to be made');
		}

		return request;
	}

	test('Should send only non-zero metadata filters in the media request params', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.metadata.countryId = 21;
		store.metadata.genreId = 31;
		store.metadata.qualityId = 0;
		store.metadata.roleId = 0;
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.countryId).toBe(21);
		expect(lastMediaRequest().params.genreId).toBe(31);
		expect(lastMediaRequest().params.qualityId).toBeUndefined();
		expect(lastMediaRequest().params.roleId).toBeUndefined();
	});

	test('Should include settings-based owned and offline filters in media request params', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.generalSettings.hideMediaFromOwnedServers = true;
		settingsStore.generalSettings.hideMediaFromOfflineServers = true;
		const store = useMediaOverviewStore();
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.filterOwnedMedia).toBe(true);
		expect(lastMediaRequest().params.filterOfflineMedia).toBe(true);
	});

	test('Should omit plexLibraryId when browsing all media', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.libraryId = 0;
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.plexLibraryId).toBeUndefined();
	});

	test('Should include plexLibraryId when browsing a specific library', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.libraryId = 45;
		mockMediaResponse();
		// requestMediaPage calls refreshLibrary when the library type is not yet known
		mock.onGet(`/api/PlexLibrary/${store.libraryId}`).reply(200, generateResultDTO({
			id: 45,
			type: PlexMediaType.Movie,
		} as PlexLibraryDTO));

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.plexLibraryId).toBe(45);
	});

	test('Should send the current search query in media request params', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.filterQuery = 'blade runner';
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.q).toBe('blade runner');
	});

	test('Should send the current sort DSL in media request params', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.sortedState = { field: MediaSortField.Year, sort: SortDirection.Desc };
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.sort).toBe('year:desc');
	});

	test('Should preserve load failure state when media response fails', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, {
			isSuccess: false,
			statusCode: 500,
			errors: [{ message: 'failure', reasons: [], metadata: {} }],
			successes: [],
			value: null,
		});

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toBeNull();
		expect(store.mediaLoadError).toBe(true);
	});

	test('Should clear media load failure after a successful retry', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const success = createMediaStatistics(1);
		mock.onGet(new RegExp('/api/PlexMedia'))
			.replyOnce(200, { isSuccess: false, statusCode: 500, errors: [], successes: [], value: null })
			.onGet(new RegExp('/api/PlexMedia'))
			.reply(200, generateResultDTO(success));

		// Act
		const first = subscribeSpyTo(store.refreshMediaData());
		await first.onComplete();
		const second = subscribeSpyTo(store.retryMediaLoad());
		await second.onComplete();

		// Assert
		expect(store.mediaLoadError).toBe(false);
		expect(store.getMediaItems).toEqual(success.mediaList);
	});

	test('Should map failed media responses to null without caching a page', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, {
			isSuccess: false,
			statusCode: 500,
			errors: [{ message: 'failure', reasons: [], metadata: {} }],
			successes: [],
			value: null,
		});

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toBeNull();
		expect(store.getMediaItems).toEqual([]);
		expect(store.itemsLength).toBe(0);
	});

	test('Should allow retrying the same page after a failed media response finalizes pending state', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const success = createMediaStatistics(2);
		mock.onGet(new RegExp('/api/PlexMedia'))
			.replyOnce(200, { isSuccess: false, statusCode: 500, errors: [], successes: [], value: null })
			.onGet(new RegExp('/api/PlexMedia'))
			.reply(200, generateResultDTO(success));

		// Act
		const first = subscribeSpyTo(store.requestMediaPage(1));
		await first.onComplete();
		const second = subscribeSpyTo(store.requestMediaPage(1));
		await second.onComplete();

		// Assert
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia')).toHaveLength(2);
		expect(store.getMediaItems).toEqual(success.mediaList);
	});

	test('Should update aggregate totals, available filter IDs, and navigation indexes from media statistics', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const statistics = createMediaStatistics(3);
		statistics.totalMovieCount = 10;
		statistics.totalTvShowCount = 11;
		statistics.totalSeasonCount = 12;
		statistics.totalEpisodeCount = 13;
		statistics.totalMediaSize = 123456;

		// Act
		store.addMediaPage(statistics);

		// Assert
		expect(store.allMovieCount).toBe(10);
		expect(store.allTvShowCount).toBe(11);
		expect(store.allSeasonCount).toBe(12);
		expect(store.allEpisodeCount).toBe(13);
		expect(store.allFileSize).toBe(123456);
		expect(store.availableRoleIds).toEqual([]);
		expect(store.availableCountryIds).toEqual([]);
		expect(store.availableGenreIds).toEqual([]);
		expect(store.availableQualityIds).toEqual([]);
		expect([...store.scrollDict.entries()]).toEqual([['A', 0], ['Z', 2]]);
	});

	test('Should clear cached media and reset itemsLength when a new query hash arrives', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const first = createMediaStatistics(3, 'first-query');
		const second = createMediaStatistics(2, 'second-query');
		store.addMediaPage(first);

		// Act
		store.addMediaPage(second);

		// Assert
		expect(store.queryHash).toBe('second-query');
		expect(store.getMediaItems).toEqual(second.mediaList);
		expect(store.itemsLength).toBe(second.mediaList.length);
	});

	test('Should preserve cached media and append item length when the same query hash adds another page', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const first = createMediaStatistics(2, 'same-query');
		const second = createMediaStatistics(3, 'same-query');
		second.page = 2;
		store.addMediaPage(first);

		// Act
		store.addMediaPage(second);

		// Assert
		expect(store.getMediaItems).toEqual([...first.mediaList, ...second.mediaList]);
		expect(store.itemsLength).toBe(first.mediaList.length + second.mediaList.length);
	});

	test('Should request every missing page in a range that spans multiple pages', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const first = createMediaStatistics(1, 'range-query');
		const second = createMediaStatistics(1, 'range-query');
		const third = createMediaStatistics(1, 'range-query');
		first.page = 1;
		second.page = 2;
		third.page = 3;
		mock.onGet(new RegExp('/api/PlexMedia'))
			.replyOnce(200, generateResultDTO(first))
			.onGet(new RegExp('/api/PlexMedia'))
			.replyOnce(200, generateResultDTO(second))
			.onGet(new RegExp('/api/PlexMedia'))
			.reply(200, generateResultDTO(third));

		// Act
		const result = subscribeSpyTo(store.requestRange(0, 250));
		await result.onComplete();

		// Assert
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia')).toHaveLength(3);
		expect(result.getLastValue()).toHaveLength(3);
	});

	test('Should request only pages missing from the range when some pages are already cached', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const cached = createMediaStatistics(1, 'range-query');
		const missing = createMediaStatistics(1, 'range-query');
		cached.page = 1;
		missing.page = 2;
		store.addMediaPage(cached);
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, generateResultDTO(missing));

		// Act
		const result = subscribeSpyTo(store.requestRange(0, 150));
		await result.onComplete();

		// Assert
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia')).toHaveLength(1);
		expect(lastMediaRequest().params.page).toBe(2);
	});

	test('Should not emit a scroll command for an out-of-bounds scroll index', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.totalCount = 10;
		const commands: { index: number; highlight: boolean }[] = [];
		const subscription = store.getScrollCommand().subscribe((value) => commands.push(value));

		// Act
		const result = subscribeSpyTo(store.scrollToIndex(10));
		await result.onComplete();

		// Assert
		expect(commands).toEqual([{ index: 0, highlight: false }]);
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia')).toHaveLength(0);
		subscription.unsubscribe();
	});

	test('Should load target pages before emitting a navigation scroll command', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.totalCount = 2000;
		const targetPage = createMediaStatistics(100, 'year-query');
		targetPage.page = 18;
		targetPage.pageSize = 100;
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, generateResultDTO(targetPage));
		const commands: { index: number; highlight: boolean }[] = [];
		const subscription = store.getScrollCommand().subscribe((value) => commands.push(value));

		// Act
		const result = subscribeSpyTo(store.scrollToIndex(1776));
		expect(commands).toEqual([{ index: 0, highlight: false }]);
		await result.onComplete();

		// Assert
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia').map((x) => x.params.page)).toEqual([18, 19]);
		expect(commands).toEqual([
			{ index: 0, highlight: false },
			{ index: 1776, highlight: true },
		]);
		subscription.unsubscribe();
	});

	test('Should reset metadata filters without clearing loaded media pages', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const statistics = createMediaStatistics(2);
		store.metadata = {
			countryId: 21,
			roleId: 11,
			genreId: 31,
			qualityId: 41,
			comparisonState: PlexMediaComparisonState.Missing,
		};
		store.addMediaPage(statistics);

		// Act
		store.resetMetaDataFilterState();

		// Assert
		expect(store.metadata).toEqual({ countryId: 0, roleId: 0, genreId: 0, qualityId: 0, comparisonState: null });
		expect(store.getMediaItems).toEqual(statistics.mediaList);
	});

	test('Should clear metadata filter query params and reset metadata values without refreshing media', () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.metadata = {
			countryId: 21,
			roleId: 11,
			genreId: 31,
			qualityId: 41,
			comparisonState: PlexMediaComparisonState.Missing,
		};

		// Act
		store.clearMetaDataFilter();

		// Assert
		expect(store.metadata).toEqual({ countryId: 0, roleId: 0, genreId: 0, qualityId: 0, comparisonState: null });
		expect(mock.history.get.filter((x) => x.url === '/api/PlexMedia')).toHaveLength(0);
	});

	test('Should clear cached pages and reset selection when reset is invoked', () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.addMediaPage(createMediaStatistics(2));
		store.setSelection({ keys: [1], allSelected: false, indexKey: 5 });
		store.filterQuery = 'before reset';

		// Act
		store.$reset();

		// Assert
		expect(store.getMediaItems).toEqual([]);
		expect(store.selection).toEqual({ keys: [], allSelected: false, indexKey: 0 });
		expect(store.filterQuery).toBe('');
		expect(store.itemsLength).toBe(0);
	});

	test('Should ignore null media page data without changing cached items', () => {
		// Arrange
		const store = useMediaOverviewStore();
		const existing = createMediaStatistics(2);
		store.addMediaPage(existing);

		// Act
		store.addMediaPage(null);

		// Assert
		expect(store.getMediaItems).toEqual(existing.mediaList);
		expect(store.itemsLength).toBe(existing.mediaList.length);
		expect(store.queryHash).toBe(existing.queryHash);
	});

	test('Should set country, role, genre, quality, and comparison filter params when their values are set', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		store.libraryId = 0;
		store.metadata.countryId = 21;
		store.metadata.roleId = 11;
		store.metadata.genreId = 31;
		store.metadata.qualityId = 41;
		store.metadata.comparisonState = PlexMediaComparisonState.Missing;
		mockMediaResponse();

		// Act
		const result = subscribeSpyTo(store.requestMediaPage(1));
		await result.onComplete();

		// Assert
		expect(lastMediaRequest().params.countryId).toBe(21);
		expect(lastMediaRequest().params.roleId).toBe(11);
		expect(lastMediaRequest().params.genreId).toBe(31);
		expect(lastMediaRequest().params.qualityId).toBe(41);
		expect(lastMediaRequest().params.comparisonState).toBe(PlexMediaComparisonState.Missing);
	});
});
