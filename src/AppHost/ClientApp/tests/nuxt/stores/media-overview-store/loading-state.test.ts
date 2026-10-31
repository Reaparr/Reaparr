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
import { type PlexMediaStatisticsDTO, PlexMediaType } from '@dto';

describe('MediaOverviewStore - Loading State', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	function setupSuccessMocks(movies: PlexMediaStatisticsDTO) {
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
	}

	test('loading should be false before any request is made', () => {
		// Arrange
		const store = useMediaOverviewStore();

		// Assert
		expect(store.loading).toBe(false);
	});

	test('loading should be false after a successful requestMedia completes', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 10 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type: PlexMediaType.Movie },
		}));
		setupSuccessMocks(movies);

		// Act
		const result = subscribeSpyTo(store.refreshMediaData());
		await result.onComplete();

		// Assert
		expect(store.loading).toBe(false);
		expect(result.receivedComplete()).toBe(true);
	});

	test('refreshMediaData should execute again even when loading is already true', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: 5 },
			partialData: { plexServerId: 1, plexLibraryId: 0, type: PlexMediaType.Movie },
		}));
		setupSuccessMocks(movies);

		// Start first request but don't await it
		const first = subscribeSpyTo(store.refreshMediaData());

		// Act — second call while loading is true (no longer skipped)
		const second = subscribeSpyTo(store.refreshMediaData());
		await second.onComplete();

		// Assert — second call returns data, not null (loading guard removed)
		expect(second.getLastValue()).not.toBeNull();
		expect(second.receivedComplete()).toBe(true);

		// Clean up first
		await first.onComplete();
	});

	test('loading should be false after initializeLibrary with a failed API response', async () => {
		// Arrange
		const store = useMediaOverviewStore();

		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, {
			isSuccess: false,
			statusCode: 500,
			errors: [{ message: 'Server error', reasons: [], metadata: {} }],
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
		const result = subscribeSpyTo(store.initializeLibrary(0));
		await result.onComplete();

		// Assert — loading must be reset to false even on failed response
		expect(result.getLastValue()).toBeNull();
		expect(store.loading).toBe(false);
	});
});
