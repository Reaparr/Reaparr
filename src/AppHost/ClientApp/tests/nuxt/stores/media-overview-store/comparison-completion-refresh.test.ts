import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	generatePlexLibrary,
	generatePlexMedia,
	generatePlexMediaStatisticsDTO,
	generateResultDTO,
	Seed,
} from '@mock';
import { useLibraryStore, useMediaOverviewStore } from '@store';
import { type LibraryComparisonCompletedDTO, type PlexMediaStatisticsDTO, PlexMediaType } from '@dto';

describe('MediaOverviewStore.refreshCurrentMediaDataWhenComparisonCompleted()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	function createMediaPage(page: number, queryHash: string, type = PlexMediaType.TvShow): PlexMediaStatisticsDTO {
		const mediaItems = Array.from({ length: 3 }, (_, index) => generatePlexMedia({
			config: { seed: 4817, seasonCount: 0 },
			partialData: {
				plexServerId: 1,
				plexLibraryId: 17,
				type,
				id: (page * 100) + index,
				sortIndex: (page * 100) + index,
			},
		}));
		const pageData = generatePlexMediaStatisticsDTO(mediaItems);
		pageData.page = page;
		pageData.pageSize = 100;
		pageData.queryHash = queryHash;
		pageData.mediaCount = mediaItems.length;
		pageData.totalCount = mediaItems.length;
		return pageData;
	}

	test.each([PlexMediaType.TvShow, PlexMediaType.MusicArtist])('Should re-request cached %s pages when current library is affected', async (type) => {
		// Arrange
		const libraryId = 17;
		const mediaOverviewStore = useMediaOverviewStore();
		const libraryStore = useLibraryStore();
		const library = generatePlexLibrary({
			seed: new Seed(4817),
			plexServerId: 1,
			type,
			partialData: {
				id: libraryId,
			},
		});
		libraryStore.updateLibrary(library);
		mediaOverviewStore.libraryId = libraryId;

		const initialPage = createMediaPage(1, 'initial-query-hash', type);
		const refreshedPage = createMediaPage(1, 'refreshed-query-hash', type);
		initialPage.mediaList.forEach((item) => item.comparisonId = 3);
		refreshedPage.mediaList.forEach((item) => item.comparisonId = 1);
		let requestCount = 0;
		mock.onGet(new RegExp('/api/PlexMedia')).reply(() => {
			requestCount++;
			return [200, generateResultDTO(requestCount === 1 ? initialPage : refreshedPage)];
		});

		await subscribeSpyTo(mediaOverviewStore.requestMediaPage(1)).onComplete();
		const notification: LibraryComparisonCompletedDTO = {
			affectedLibraryIds: [214, libraryId],
			mediaType: type,
			completedAt: new Date().toISOString(),
		};

		// Act
		const result = subscribeSpyTo(mediaOverviewStore.refreshCurrentMediaDataWhenComparisonCompleted(notification));
		await result.onComplete();

		// Assert
		expect(result.receivedComplete()).toEqual(true);
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(2);
		expect(mediaOverviewStore.queryHash).toBe('refreshed-query-hash');
		expect(mediaOverviewStore.getMediaItems).toEqual(refreshedPage.mediaList);
	});

	test.each([PlexMediaType.TvShow, PlexMediaType.MusicArtist])('Should ignore %s comparison completion when current library is not affected', async (type) => {
		// Arrange
		const libraryId = 17;
		const mediaOverviewStore = useMediaOverviewStore();
		const libraryStore = useLibraryStore();
		const library = generatePlexLibrary({
			seed: new Seed(4818),
			plexServerId: 1,
			type,
			partialData: {
				id: libraryId,
			},
		});
		libraryStore.updateLibrary(library);
		mediaOverviewStore.libraryId = libraryId;

		const initialPage = createMediaPage(1, 'initial-query-hash', type);
		mock.onGet(new RegExp('/api/PlexMedia')).reply(200, generateResultDTO(initialPage));

		await subscribeSpyTo(mediaOverviewStore.requestMediaPage(1)).onComplete();
		const notification: LibraryComparisonCompletedDTO = {
			affectedLibraryIds: [214, 18],
			mediaType: type,
			completedAt: '2026-10-09T00:00:00Z',
		};

		// Act
		const result = subscribeSpyTo(mediaOverviewStore.refreshCurrentMediaDataWhenComparisonCompleted(notification));
		await result.onComplete();

		// Assert
		expect(result.receivedComplete()).toEqual(true);
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(1);
		expect(mediaOverviewStore.queryHash).toBe('initial-query-hash');
		expect(mediaOverviewStore.getMediaItems).toEqual(initialPage.mediaList);
	});
	test('Should ignore a different media family completion even when the Music library id is affected', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(4819), plexServerId: 1, type: PlexMediaType.MusicArtist, partialData: { id: 17 },
		}));
		store.libraryId = 17;
		const initialPage = createMediaPage(1, 'music-query', PlexMediaType.MusicArtist);
		mock.onGet('/api/PlexMedia').reply(200, generateResultDTO(initialPage));
		await subscribeSpyTo(store.requestMediaPage(1)).onComplete();

		// Act
		await subscribeSpyTo(store.refreshCurrentMediaDataWhenComparisonCompleted({
			affectedLibraryIds: [17], mediaType: PlexMediaType.Movie, completedAt: '2026-10-09T00:00:00Z',
		})).onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia')).toHaveLength(1);
		expect(store.getMediaItems).toEqual(initialPage.mediaList);
	});
});
