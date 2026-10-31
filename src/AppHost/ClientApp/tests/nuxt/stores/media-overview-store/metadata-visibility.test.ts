import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexLibrary,
	generatePlexMedia,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	generateSupportedSorts,
	Seed,
} from '@mock';
import { plexLibraryApi, plexMediaApi } from '@api';
import { useLibraryStore, useMediaOverviewStore, useSettingsStore } from '@store';
import {
	PlexMediaType,
	VideoQuality,
	type PlexMediaFilterMetadataDTO,
	type PlexMediaMetadataDTO,
	type PlexMediaStatisticsDTO,
} from '@dto';
import type { ResultDTO } from '@interfaces';

function metadataFor(scopeId: number, mediaType: PlexMediaType): PlexMediaMetadataDTO {
	return {
		countries: [{ id: scopeId, name: `Country ${scopeId}` }],
		countryCount: 1,
		genreCount: 1,
		genres: [{ id: scopeId, name: `Genre ${scopeId}` }],
		mediaCount: scopeId,
		qualities: [{
			count: 1,
			id: scopeId,
			name: `Quality ${scopeId}`,
			quality: VideoQuality.FullHD,
		}],
		qualityCount: 1,
		roleCount: 1,
		roles: [{ id: scopeId, name: `Role ${scopeId}` }],
		supportedSorts: generateSupportedSorts(mediaType),
	};
}

function filterMetadataFor(scopeId: number): PlexMediaFilterMetadataDTO {
	return {
		countries: [scopeId],
		genres: [scopeId],
		qualities: [scopeId],
		roles: [scopeId],
	};
}

function mediaFor(scopeId: number): PlexMediaStatisticsDTO {
	const item = generatePlexMedia({
		config: { seed: 2400 + scopeId },
		partialData: {
			id: scopeId,
			plexLibraryId: 0,
			plexServerId: 1,
			title: `Media ${scopeId}`,
			type: PlexMediaType.Movie,
		},
	});
	const result = generatePlexMediaStatisticsDTO([item]);
	result.mediaCount = scopeId;
	result.totalCount = scopeId;
	result.queryHash = `scope-${scopeId}`;
	return result;
}

function setupControlledRequests() {
	const mediaMetadataRequests: Array<Subject<ResultDTO<PlexMediaMetadataDTO>>> = [];
	const filterMetadataRequests: Array<Subject<ResultDTO<PlexMediaFilterMetadataDTO>>> = [];
	const mediaRequests: Array<Subject<ResultDTO<PlexMediaStatisticsDTO>>> = [];
	vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation(() => {
		const request = new Subject<ResultDTO<PlexMediaMetadataDTO>>();
		mediaMetadataRequests.push(request);
		return request.asObservable();
	});
	vi.spyOn(plexLibraryApi, 'getMetadataFilter').mockImplementation(() => {
		const request = new Subject<ResultDTO<PlexMediaFilterMetadataDTO>>();
		filterMetadataRequests.push(request);
		return request.asObservable();
	});
	vi.spyOn(plexMediaApi, 'getAllMediaByTypeEndpoint').mockImplementation(() => {
		const request = new Subject<ResultDTO<PlexMediaStatisticsDTO>>();
		mediaRequests.push(request);
		return request.asObservable();
	});
	return { mediaMetadataRequests, filterMetadataRequests, mediaRequests };
}

describe('MediaOverviewStore metadata visibility scope', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	test.each([
		{ owned: false, offline: false },
		{ owned: false, offline: true },
		{ owned: true, offline: false },
		{ owned: true, offline: true },
	])('Should send both all-library metadata scope flags for owned=$owned and offline=$offline', async ({ owned, offline }) => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		settingsStore.generalSettings.hideMediaFromOwnedServers = owned;
		settingsStore.generalSettings.hideMediaFromOfflineServers = offline;
		const store = useMediaOverviewStore();
		mock.onGet('/api/PlexLibrary/0/metadata').reply((request) => [200, generateResultDTO(metadataFor(1, request.params.mediaType as PlexMediaType))]);
		mock.onGet('/api/PlexLibrary/0/metadata-filter').reply(200, generateResultDTO(filterMetadataFor(1)));

		// Act
		await subscribeSpyTo(store.refreshMetaData()).onComplete();
		await subscribeSpyTo(store.refreshFilterMetadata()).onComplete();

		// Assert
		const metadataRequest = mock.history.get.find((request) => request.url === '/api/PlexLibrary/0/metadata');
		const filterRequest = mock.history.get.find((request) => request.url === '/api/PlexLibrary/0/metadata-filter');
		expect(metadataRequest?.params).toEqual({
			mediaType: PlexMediaType.Movie,
			filterOwnedMedia: owned,
			filterOfflineMedia: offline,
		});
		expect(filterRequest?.params).toEqual({
			mediaType: PlexMediaType.Movie,
			filterOwnedMedia: owned,
			filterOfflineMedia: offline,
		});
	});

	test('Should keep direct-library metadata query shape unchanged', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.generalSettings.hideMediaFromOwnedServers = true;
		settingsStore.generalSettings.hideMediaFromOfflineServers = true;
		const library = generatePlexLibrary({
			seed: new Seed(2401),
			plexServerId: 1,
			type: PlexMediaType.Movie,
			partialData: { id: 42, title: 'Movies' },
		});
		useLibraryStore().updateLibrary(library);
		const store = useMediaOverviewStore();
		store.libraryId = library.id;
		mock.onGet(`/api/PlexLibrary/${library.id}/metadata`).reply(200, generateResultDTO(metadataFor(1, library.type)));
		mock.onGet(`/api/PlexLibrary/${library.id}/metadata-filter`).reply(200, generateResultDTO(filterMetadataFor(1)));

		// Act
		await subscribeSpyTo(store.refreshMetaData()).onComplete();
		await subscribeSpyTo(store.refreshFilterMetadata()).onComplete();

		// Assert
		const metadataRequest = mock.history.get.find((request) => request.url === `/api/PlexLibrary/${library.id}/metadata`);
		const filterRequest = mock.history.get.find((request) => request.url === `/api/PlexLibrary/${library.id}/metadata-filter`);
		expect(metadataRequest?.params).toEqual({ mediaType: PlexMediaType.Movie });
		expect(filterRequest?.params).toEqual({ mediaType: PlexMediaType.Movie });
	});

	test('Should update media, metadata counts, quality rows, and filter ids when all-library visibility changes', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		const store = useMediaOverviewStore();
		const { mediaMetadataRequests, filterMetadataRequests, mediaRequests } = setupControlledRequests();
		store.filterQuery = 'keep-this-search';
		store.metadata.genreId = 77;

		// Act
		const first = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		mediaMetadataRequests[0]!.next(generateResultDTO(metadataFor(1, PlexMediaType.Movie)));
		mediaRequests[0]!.next(generateResultDTO(mediaFor(1)));
		filterMetadataRequests[0]!.next(generateResultDTO(filterMetadataFor(1)));
		mediaRequests[0]!.complete();
		mediaMetadataRequests[0]!.complete();
		filterMetadataRequests[0]!.complete();
		await first.onComplete();

		settingsStore.generalSettings.hideMediaFromOwnedServers = true;
		settingsStore.generalSettings.hideMediaFromOfflineServers = true;
		const second = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		mediaMetadataRequests[1]!.next(generateResultDTO(metadataFor(2, PlexMediaType.Movie)));
		mediaRequests[1]!.next(generateResultDTO(mediaFor(2)));
		filterMetadataRequests[1]!.next(generateResultDTO(filterMetadataFor(2)));
		mediaRequests[1]!.complete();
		mediaMetadataRequests[1]!.complete();
		filterMetadataRequests[1]!.complete();
		await second.onComplete();

		// Assert
		expect(store.getMediaItems.map((item) => item.id)).toEqual([2]);
		expect(store.metadataList.mediaCount).toBe(2);
		expect(store.metadataList.qualityCount).toBe(1);
		expect(store.metadataList.qualities[0]?.id).toBe(2);
		expect(store.availableQualityIds).toEqual([2]);
		expect(store.availableGenreIds).toEqual([2]);
		expect(store.filterQuery).toBe('keep-this-search');
		expect(store.metadata.genreId).toBe(77);
	});

	test('Should load metadata once before media when visibility refresh starts without sort capabilities', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		const store = useMediaOverviewStore();
		const { mediaMetadataRequests, filterMetadataRequests, mediaRequests } = setupControlledRequests();

		// Act
		const refresh = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		expect(store.metadataList.supportedSorts).toEqual([]);
		expect(mediaMetadataRequests).toHaveLength(1);
		expect(mediaRequests).toHaveLength(0);
		mediaMetadataRequests[0]!.next(generateResultDTO(metadataFor(5, PlexMediaType.Movie)));
		expect(mediaMetadataRequests).toHaveLength(1);
		expect(mediaRequests).toHaveLength(1);
		mediaRequests[0]!.next(generateResultDTO(mediaFor(5)));
		filterMetadataRequests[0]!.next(generateResultDTO(filterMetadataFor(5)));
		mediaRequests[0]!.complete();
		mediaMetadataRequests[0]!.complete();
		filterMetadataRequests[0]!.complete();
		await refresh.onComplete();

		// Assert
		expect(mediaMetadataRequests).toHaveLength(1);
		expect(mediaRequests).toHaveLength(1);
		expect(store.getMediaItems.map((item) => item.id)).toEqual([5]);
		expect(store.loading).toBe(false);
		expect(store.mediaLoadError).toBe(false);
	});

	test('Should keep metadata updates alive when a search refresh supersedes the visibility media request', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		const store = useMediaOverviewStore();
		const { mediaMetadataRequests, filterMetadataRequests, mediaRequests } = setupControlledRequests();

		const visibilityRefresh = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		mediaMetadataRequests[0]!.next(generateResultDTO(metadataFor(2, PlexMediaType.Movie)));
		const searchRefresh = subscribeSpyTo(store.setFilterQuery('new-query'));
		mediaRequests[1]!.next(generateResultDTO(mediaFor(3)));
		mediaRequests[1]!.complete();
		await searchRefresh.onComplete();
		mediaRequests[0]!.next(generateResultDTO(mediaFor(2)));
		mediaRequests[0]!.complete();
		expect(store.filterMetadataLoading).toBe(true);
		filterMetadataRequests[0]!.next(generateResultDTO(filterMetadataFor(2)));
		mediaMetadataRequests[0]!.complete();
		filterMetadataRequests[0]!.complete();
		await visibilityRefresh.onComplete();

		// Assert
		expect(store.filterQuery).toBe('new-query');
		expect(store.getMediaItems.map((item) => item.id)).toEqual([3]);
		expect(store.metadataList.mediaCount).toBe(2);
		expect(store.metadataList.qualityCount).toBe(1);
		expect(store.metadataList.qualities[0]?.id).toBe(2);
		expect(store.availableQualityIds).toEqual([2]);
	});

	test('Should ignore late metadata, filter, and media responses from an older visibility scope', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		const store = useMediaOverviewStore();
		const { mediaMetadataRequests, filterMetadataRequests, mediaRequests } = setupControlledRequests();

		// Act
		const first = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		mediaMetadataRequests[0]!.next(generateResultDTO(metadataFor(1, PlexMediaType.Movie)));
		settingsStore.generalSettings.hideMediaFromOwnedServers = true;
		settingsStore.generalSettings.hideMediaFromOfflineServers = true;
		const second = subscribeSpyTo(store.refreshMediaDataWithMetadata());
		mediaMetadataRequests[1]!.next(generateResultDTO(metadataFor(2, PlexMediaType.Movie)));
		mediaRequests[1]!.next(generateResultDTO(mediaFor(2)));
		filterMetadataRequests[1]!.next(generateResultDTO(filterMetadataFor(2)));
		mediaRequests[1]!.complete();
		mediaMetadataRequests[1]!.complete();
		filterMetadataRequests[1]!.complete();
		await second.onComplete();
		mediaRequests[0]!.next(generateResultDTO(mediaFor(1)));
		mediaMetadataRequests[0]!.next(generateResultDTO(metadataFor(1, PlexMediaType.Movie)));
		filterMetadataRequests[0]!.next(generateResultDTO(filterMetadataFor(1)));
		mediaRequests[0]!.complete();
		mediaMetadataRequests[0]!.complete();
		filterMetadataRequests[0]!.complete();
		await first.onComplete();

		// Assert
		expect(store.getMediaItems.map((item) => item.id)).toEqual([2]);
		expect(store.metadataList.mediaCount).toBe(2);
		expect(store.metadataList.qualities[0]?.id).toBe(2);
		expect(store.availableQualityIds).toEqual([2]);
		expect(store.availableGenreIds).toEqual([2]);
	});
});
