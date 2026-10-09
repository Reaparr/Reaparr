import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { get, set } from '@vueuse/core';
import { useRouteQuery } from '@vueuse/router';
import { flushPromises } from '@vue/test-utils';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generatePlexLibrary, generatePlexMediaStatisticsDTO, generateResultDTO, Seed } from '@mock';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import { PlexMediaComparisonState, PlexMediaType } from '@dto';

const musicStates = [
	PlexMediaComparisonState.NotCompared,
	PlexMediaComparisonState.Owned,
	PlexMediaComparisonState.Pending,
	PlexMediaComparisonState.Missing,
	PlexMediaComparisonState.Partial,
];
const upgrades = [PlexMediaComparisonState.HigherQuality, PlexMediaComparisonState.PartialAndHigherQuality];

describe('MediaOverviewStore - Music comparison filters', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		mock.onGet('/api/PlexMedia').reply(200, generateResultDTO(generatePlexMediaStatisticsDTO([])));
	});

	test.each(musicStates)('Should preserve supported Music URL filter %s in the API request', async (comparisonState) => {
		// Arrange
		set(useRouteQuery<string>('comparisonState', ''), comparisonState);
		const store = useMediaOverviewStore();

		// Act
		store.applyRouteQueryState();
		await subscribeSpyTo(store.requestMediaPage(1)).onComplete();

		// Assert
		expect(store.metadata.comparisonState).toBe(comparisonState);
		expect(mock.history.get.at(-1)?.params).toMatchObject({ mediaType: PlexMediaType.MusicArtist, comparisonState });
		expect(get(useRouteQuery<string>('comparisonState', ''))).toBe(comparisonState);
	});

	test.each([...upgrades, PlexMediaComparisonState.Unknown])('Should remove unsupported Music URL filter %s before requesting media', async (comparisonState) => {
		// Arrange
		set(useRouteQuery<string>('comparisonState', ''), comparisonState);
		const store = useMediaOverviewStore();

		// Act
		store.applyRouteQueryState();
		await subscribeSpyTo(store.requestMediaPage(1)).onComplete();
		await flushPromises();

		// Assert
		expect(store.metadata.comparisonState).toBeNull();
		expect(mock.history.get.at(-1)?.params.comparisonState).toBeUndefined();
		expect(get(useRouteQuery<string>('comparisonState', ''))).toBe('');
		expect(store.getComparisonStateOptions.map((option) => option.value)).toEqual(expect.arrayContaining(musicStates));
		expect(store.getComparisonStateOptions).toHaveLength(musicStates.length);
	});

	test.each(upgrades)('Should reject direct Music upgrade filter %s and guard direct metadata assignments', async (comparisonState) => {
		// Arrange
		const store = useMediaOverviewStore();

		// Act
		await subscribeSpyTo(store.setComparisonStateFilter(comparisonState)).onComplete();
		store.metadata.comparisonState = comparisonState;
		await subscribeSpyTo(store.requestMediaPage(2)).onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia').map((request) => request.params.comparisonState)).toEqual([undefined, undefined]);
	});

	test.each([PlexMediaType.Movie, PlexMediaType.TvShow])('Should preserve existing %s upgrade queries', async (mediaType) => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = mediaType;
		const store = useMediaOverviewStore();

		// Act
		await subscribeSpyTo(store.setComparisonStateFilter(PlexMediaComparisonState.HigherQuality)).onComplete();

		// Assert
		expect(store.metadata.comparisonState).toBe(PlexMediaComparisonState.HigherQuality);
		expect(mock.history.get.at(-1)?.params.comparisonState).toBe(PlexMediaComparisonState.HigherQuality);
	});

	test.each([PlexMediaType.PhotoAlbum, PlexMediaType.OtherVideos])('Should never forward comparison URL filters for %s', async (mediaType) => {
		// Arrange
		useSettingsStore().displaySettings.allOverviewViewMode = mediaType;
		set(useRouteQuery<string>('comparisonState', ''), PlexMediaComparisonState.Missing);
		const store = useMediaOverviewStore();

		// Act
		store.applyRouteQueryState();
		await subscribeSpyTo(store.requestMediaPage(1)).onComplete();

		// Assert
		expect(store.metadata.comparisonState).toBeNull();
		expect(mock.history.get.at(-1)?.params.comparisonState).toBeUndefined();
	});

	test.each([PlexMediaComparisonState.Partial, PlexMediaComparisonState.HigherQuality])('Should validate %s after resolving an initially unknown Music library', async (comparisonState) => {
		// Arrange
		set(useRouteQuery<string>('comparisonState', ''), comparisonState);
		const library = generatePlexLibrary({
			seed: new Seed(624), plexServerId: 2, type: PlexMediaType.MusicArtist,
			partialData: { id: 4, isEnabled: true, syncedAt: '2026-10-09T00:00:00Z' },
		});
		mock.onGet('/api/PlexLibrary/4').reply(200, generateResultDTO(library));
		mock.onGet(/\/api\/PlexLibrary\/4\/metadata/).reply(200, generateResultDTO({
			mediaCount: 0, countryCount: 0, genreCount: 0, roleCount: 0, qualityCount: 0,
			countries: [], genres: [], roles: [], qualities: [],
		}));
		const store = useMediaOverviewStore();

		// Act
		await subscribeSpyTo(store.initializeLibrary(4)).onComplete();
		await flushPromises();

		// Assert
		const expected = comparisonState === PlexMediaComparisonState.Partial ? comparisonState : undefined;
		expect(store.metadata.comparisonState).toBe(expected ?? null);
		expect(mock.history.get.find((request) => request.url === '/api/PlexMedia')?.params.comparisonState).toBe(expected);
		expect(get(useRouteQuery<string>('comparisonState', ''))).toBe(expected ?? '');
	});
});
