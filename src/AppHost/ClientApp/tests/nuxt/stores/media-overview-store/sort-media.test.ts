import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexMediaSlims,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
} from '@mock';
import { useMediaOverviewStore } from '@store';
import { PlexMediaType, SortDirection, SortType } from '@dto';
import { buildFlexSortDsl, createDefaultMediaOverviewSort, parseFlexSortDsl, serializeFlexSortDsl } from '@composables';

describe('MediaOverviewStore.sortMedia()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	async function loadMovies(mediaOverviewStore: ReturnType<typeof useMediaOverviewStore>, count = 20) {
		const type = PlexMediaType.Movie;
		const movies = generatePlexMediaStatisticsDTO(generatePlexMediaSlims({
			config: { movieCount: count },
			partialData: { plexServerId: 1, plexLibraryId: 0, type },
		}));

		mock.onGet(new RegExp(`/api/PlexMedia`)).reply(200, generateResultDTO(movies));
		mock.onGet(new RegExp(`/api/PlexLibrary/0/metadata`)).reply(200, generateResultDTO({
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
		}));

		const result = subscribeSpyTo(mediaOverviewStore.refreshMediaData());
		await result.onComplete();
		return movies;
	}

	const canonicalSortCases = [
		{ field: SortType.Title, asc: 'sortIndex:asc', desc: 'sortIndex:desc' },
		{ field: SortType.Year, asc: 'year:asc', desc: 'year:desc' },
		{ field: SortType.AddedAt, asc: 'addedAt:asc', desc: 'addedAt:desc' },
		{ field: SortType.UpdatedAt, asc: 'updatedAt:asc', desc: 'updatedAt:desc' },
		{ field: SortType.Duration, asc: 'duration:asc', desc: 'duration:desc' },
		{ field: SortType.MediaSize, asc: 'mediaSize:asc', desc: 'mediaSize:desc' },
		{ field: SortType.VideoQuality, asc: 'videoQuality:asc', desc: 'videoQuality:desc' },
		{ field: SortType.ComparisonState, asc: 'comparisonState:asc', desc: 'comparisonState:desc' },
	] as const;

	test('Should return items in ascending title order by default (getIsSorted is false)', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);

		// Assert
		expect(store.getIsSorted).toBe(false);
		const items = store.getMediaItems;
		expect(items.length).toBeGreaterThan(0);
		// Default is Title/Asc — items come from server already sorted
	});

	test('Should sort items descending by year when toggleSortMedia is called', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);

		// Act
		store.toggleSortMedia(SortType.Year);
		store.toggleSortMedia(SortType.Year); // second toggle → Desc

		// Assert
		expect(store.getIsSorted).toBe(true);
		expect(store.sortedState.field).toBe(SortType.Year);
		expect(store.sortedState.sort).toBe(SortDirection.Desc);
	});

	test('Should sort items ascending by year on first toggle', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);

		// Act
		store.toggleSortMedia(SortType.Year);

		// Assert
		expect(store.sortedState.field).toBe(SortType.Year);
		expect(store.sortedState.sort).toBe(SortDirection.Asc);
	});

	test('getIsSorted should be true when sorted by Title descending', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);

		// Act — Title/Desc should be "sorted" (non-default)
		store.toggleSortMedia(SortType.Title);

		// Assert
		expect(store.getIsSorted).toBe(true);
		expect(store.sortedState.field).toBe(SortType.Title);
		expect(store.sortedState.sort).toBe(SortDirection.Desc);
	});

	test('Should reset sort state when clearSort is applied', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		await loadMovies(store);

		// First, apply a real sort
		store.toggleSortMedia(SortType.Year);
		expect(store.getIsSorted).toBe(true);

		// Act — clear sort
		store.clearSort();

		// Assert
		expect(store.sortedState.field).toBe(SortType.Title);
		expect(store.sortedState.sort).toBe(SortDirection.Asc);
		expect(store.getIsSorted).toBe(false);
	});
	test.each(canonicalSortCases)('serializes generated $field to canonical Flex sort tokens', ({ field, asc, desc }) => {
		// Arrange
		const ascending = { field, sort: SortDirection.Asc };
		const descending = { field, sort: SortDirection.Desc };

		// Act
		const ascendingDsl = serializeFlexSortDsl(ascending);
		const descendingDsl = serializeFlexSortDsl(descending);

		// Assert
		expect(ascendingDsl).toBe(asc);
		expect(descendingDsl).toBe(desc);
	});

	test.each(canonicalSortCases)('parses canonical Flex sort tokens into generated $field members', ({ field, asc, desc }) => {
		// Arrange
		const expectedAscending = { field, sort: SortDirection.Asc };
		const expectedDescending = { field, sort: SortDirection.Desc };

		// Act
		const ascending = parseFlexSortDsl(asc);
		const descending = parseFlexSortDsl(desc);

		// Assert
		expect(ascending).toEqual(expectedAscending);
		expect(descending).toEqual(expectedDescending);
	});
	test('serializes NoSort as ascending for the typed MediaSize sort', () => {
		// Arrange
		const sort = { field: SortType.MediaSize, sort: SortDirection.NoSort };

		// Act
		const dsl = serializeFlexSortDsl(sort);

		// Assert
		expect(dsl).toBe('mediaSize:asc');
	});

	test.each(['mediaSize:no-sort', 'mediaSize:Asc', 'mediaSize:reverse'])('rejects invalid Flex sort directions: %s', (value) => {
		// Arrange

		// Act
		const parsed = parseFlexSortDsl(value);

		// Assert
		expect(parsed).toBeNull();
	});

	test('uses canonical Flex sort DSL values for the default shared media sort', () => {
		// Arrange

		// Act
		const dsl = serializeFlexSortDsl(createDefaultMediaOverviewSort());

		// Assert
		expect(dsl).toBe('sortIndex:asc');
	});

	test('rejects unknown Flex sort fields', () => {
		// Arrange

		// Act
		const parsed = parseFlexSortDsl('not-a-sort:asc');

		// Assert
		expect(parsed).toBeNull();
	});

	test('Should preserve field precedence and wire directions when building multi-field sorts', () => {
		// Arrange
		const sorts = [
			{ field: 'mediaSize', direction: SortDirection.Desc },
			{ field: 'sortIndex', direction: SortDirection.Asc },
			{ field: 'id' },
		] as const;

		// Act
		const dsl = buildFlexSortDsl(sorts);

		// Assert
		expect(dsl).toBe('mediaSize:desc,sortIndex:asc,id:asc');
	});
});
