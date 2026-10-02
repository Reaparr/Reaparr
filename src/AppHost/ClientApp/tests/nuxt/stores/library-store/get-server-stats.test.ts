import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { computed } from 'vue';
import { PlexMediaType, type PlexLibraryDTO } from '@dto';
import { generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import { useLibraryStore } from '@store';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexLibraryPaths } from '@api-urls';

describe('LibraryStore.getServerStats()', () => {
	let { mock } = baseVars();
	let seed: Seed;
	let nextId: number;

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(async () => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		seed = new Seed(526);
		nextId = 1;
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO([]));
		await subscribeSpyTo(useLibraryStore().setup()).onComplete();
	});

	function library(partialData: Partial<PlexLibraryDTO> = {}): PlexLibraryDTO {
		return generatePlexLibrary({
			seed,
			plexServerId: partialData.plexServerId ?? 1,
			type: partialData.type ?? PlexMediaType.Movie,
			partialData: {
				id: nextId++,
				title: `Library ${nextId}`,
				syncedAt: '2026-09-30T12:00:00.000Z',
				...partialData,
			},
		});
	}

	test('Should isolate one server, ignore unsupported media and reject negative metadata sentinels', () => {
		// Arrange
		const libraryStore = useLibraryStore();
		libraryStore.libraries = [
			library({ type: PlexMediaType.Movie, count: 4, mediaSize: 400 }),
			library({ type: PlexMediaType.TvShow, count: 3, seasonCount: 6, episodeCount: 24, mediaSize: 600 }),
			library({ type: PlexMediaType.TvShow, count: -1, seasonCount: -1, episodeCount: -1, mediaSize: -1 }),
			library({ type: PlexMediaType.Music, count: 999, seasonCount: 999, episodeCount: 999, mediaSize: 999 }),
			library({ plexServerId: 2, type: PlexMediaType.Movie, count: 50, mediaSize: 5000 }),
		];

		// Act
		const result = libraryStore.getServerStats(1);

		// Assert
		expect(result.libraries).toHaveLength(3);
		expect(result.libraries.every(({ plexServerId }) => plexServerId === 1)).toBe(true);
		expect(result.libraries.map(({ type }) => type)).not.toContain(PlexMediaType.Music);
		expect(result).toMatchObject({
			mediaSize: 1000,
			movieCount: 4,
			tvShowCount: 3,
			seasonCount: 6,
			episodeCount: 24,
		});
	});

	test('Should include disabled metadata without treating disabled libraries as incomplete', () => {
		// Arrange
		const libraryStore = useLibraryStore();
		libraryStore.libraries = [
			library({ isEnabled: false, syncedAt: null, count: 7, mediaSize: 700 }),
			library({
				type: PlexMediaType.TvShow,
				isEnabled: false,
				count: 5,
				seasonCount: 10,
				episodeCount: 50,
				mediaSize: 1500,
			}),
		];

		// Act
		const result = libraryStore.getServerStats(1);

		// Assert
		expect(result).toMatchObject({
			mediaSize: 2200,
			movieCount: 7,
			tvShowCount: 5,
			seasonCount: 10,
			episodeCount: 50,
			hasIndexedData: true,
			status: 'no-enabled-libraries',
		});
		expect(result.libraries.every(({ isEnabled }) => !isEnabled)).toBe(true);
	});

	test('Should distinguish unknown inventory from a confirmed empty indexed server', () => {
		// Arrange
		const libraryStore = useLibraryStore();
		const unindexed = library({ syncedAt: null, count: 0, mediaSize: 0 });
		libraryStore.libraries = [unindexed];

		// Act
		const unknown = libraryStore.getServerStats(1);
		libraryStore.updateLibrary({ ...unindexed, syncedAt: '2026-10-01T08:00:00.000Z' });
		const confirmedEmpty = libraryStore.getServerStats(1);

		// Assert
		expect(unknown).toMatchObject({
			movieCount: 0,
			mediaSize: 0,
			hasIndexedData: false,
			status: 'not-indexed',
		});
		expect(confirmedEmpty).toMatchObject({
			movieCount: 0,
			mediaSize: 0,
			hasIndexedData: true,
			status: 'complete',
		});
	});

	test('Should move reactive consumers from partial to complete as live library metadata changes', () => {
		// Arrange
		const libraryStore = useLibraryStore();
		const movie = library({ count: 2, mediaSize: 200 });
		const tv = library({
			type: PlexMediaType.TvShow,
			syncedAt: null,
			count: 0,
			seasonCount: 0,
			episodeCount: 0,
			mediaSize: 0,
		});
		libraryStore.libraries = [movie, tv];
		const stats = computed(() => libraryStore.getServerStats(1));

		// Act
		const before = stats.value;
		libraryStore.updateLibrary({
			...tv,
			syncedAt: '2026-10-01T09:00:00.000Z',
			count: 4,
			seasonCount: 8,
			episodeCount: 40,
			mediaSize: 800,
		});
		const after = stats.value;

		// Assert
		expect(before).toMatchObject({ hasIndexedData: true, status: 'partial', tvShowCount: 0 });
		expect(after).toMatchObject({
			hasIndexedData: true,
			status: 'complete',
			mediaSize: 1000,
			tvShowCount: 4,
			seasonCount: 8,
			episodeCount: 40,
		});
	});

	test('Should group libraries by media type and alphabetize displayed names through live updates', () => {
		// Arrange
		const libraryStore = useLibraryStore();
		const alphaTv = library({ id: 1, type: PlexMediaType.TvShow, title: 'ALPHA TV' });
		const zebraMovie = library({ id: 2, title: 'Zebra Movies' });
		const zebraTv = library({ id: 3, type: PlexMediaType.TvShow, title: 'zebra TV' });
		const alphaMovie = library({ id: 4, title: 'alpha Movies' });
		const extraLibraries = Array.from({ length: 17 }, (_, index) => library({
			id: index + 5,
			title: `zz extra ${String(index).padStart(2, '0')}`,
		}));
		libraryStore.libraries = [alphaTv, zebraMovie, zebraTv, alphaMovie, ...extraLibraries.toReversed()];
		const stats = computed(() => libraryStore.getServerStats(1));

		// Act
		const initialIds = stats.value.libraries.map(({ id }) => id);
		libraryStore.updateLibrary({ ...zebraMovie, title: 'aardvark Movies' });
		const afterRename = stats.value.libraries.map(({ id }) => id);
		libraryStore.updateLibrary(library({ id: 22, title: 'blue Movies' }));
		const afterAddition = stats.value.libraries.map(({ id }) => id);
		libraryStore.libraries = libraryStore.libraries.filter(({ id }) => id !== 10);
		const afterRemoval = stats.value.libraries.map(({ id }) => id);

		// Assert
		const extraIds = extraLibraries.map(({ id }) => id);
		expect(initialIds).toEqual([4, 2, ...extraIds, 1, 3]);
		expect(afterRename).toEqual([2, 4, ...extraIds, 1, 3]);
		expect(afterAddition).toEqual([2, 4, 22, ...extraIds, 1, 3]);
		expect(afterRemoval).toEqual([2, 4, 22, ...extraIds.filter((id) => id !== 10), 1, 3]);
	});
});
