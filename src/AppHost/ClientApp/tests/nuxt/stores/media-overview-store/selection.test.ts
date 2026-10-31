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
import { PlexMediaType } from '@dto';

describe('MediaOverviewStore - Selection', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	async function loadMovies(store: ReturnType<typeof useMediaOverviewStore>, count = 10, totalCount = count) {
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: count },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));
		movies.mediaCount = totalCount;
		movies.totalCount = totalCount;
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

	test('hasSelectedMedia should be false initially', () => {
		// Arrange
		const store = useMediaOverviewStore();

		// Assert
		expect(store.hasSelectedMedia).toBe(false);
	});

	test('hasSelectedMedia should be true after selecting items', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);
		const firstId = store.getMediaItems[0]!.id;

		// Act
		store.setSelection({ keys: [firstId], allSelected: false, indexKey: 0 });

		// Assert
		expect(store.hasSelectedMedia).toBe(true);
	});

	test('isRootSelected should be null when only some items are selected (indeterminate)', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);
		const someIds = store.getMediaItems.slice(0, 3).map((x) => x.id);

		// Act
		store.setSelection({ keys: someIds, allSelected: false, indexKey: 0 });

		// Assert
		expect(store.isRootSelected).toBeNull();
	});

	test('isRootSelected should reflect all loaded items when backend total exceeds the loaded cache', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 100, 250);

		// Act
		store.setRootSelected(true);

		// Assert
		expect(store.totalCount).toBe(250);
		expect(store.itemsLength).toBe(100);
		expect(store.isRootSelected).toBe(true);
		expect(store.selection.keys).toHaveLength(100);
	});

	test('isRootSelected should be true when all items are selected', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);

		// Act
		store.setRootSelected(true);

		// Assert
		expect(store.isRootSelected).toBe(true);
		expect(store.selection.allSelected).toBe(true);
		expect(store.selection.keys.length).toBe(store.getMediaItems.length);
	});

	test('isRootSelected should be false when no items are selected', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);

		// Act
		store.setRootSelected(false);

		// Assert
		expect(store.isRootSelected).toBe(false);
		expect(store.selection.keys.length).toBe(0);
	});

	test('setSelectionRange should select only items within the given sortIndex range', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);

		// Items are sorted by title by default so sortIndex is assigned 1..N
		// Act — select items with sortIndex 2 through 5
		store.setSelectionRange(2, 5);

		// Assert
		const selectedIds = store.selection.keys;
		const selectedItems = store.getMediaItems.filter((x) => selectedIds.includes(x.id));
		expect(selectedItems.length).toBe(4); // sortIndex 2, 3, 4, 5
		for (const item of selectedItems) {
			expect(item.sortIndex).toBeGreaterThanOrEqual(2);
			expect(item.sortIndex).toBeLessThanOrEqual(5);
		}
	});

	test('setRootSelected(false) should clear all previously selected items', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 10);
		store.setRootSelected(true);
		expect(store.selection.keys.length).toBeGreaterThan(0);

		// Act
		store.setRootSelected(false);

		// Assert
		expect(store.selection.keys.length).toBe(0);
		expect(store.hasSelectedMedia).toBe(false);
	});

	test('$reset should clear selection state', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store, 5);
		store.setRootSelected(true);
		expect(store.hasSelectedMedia).toBe(true);

		// Act
		store.$reset();

		// Assert
		expect(store.hasSelectedMedia).toBe(false);
		expect(store.selection.keys).toEqual([]);
		expect(store.getMediaItems).toEqual([]);
	});
});
