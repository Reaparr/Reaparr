import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia } from 'pinia';
import type { Pinia } from 'pinia';
import type { VueWrapper } from '@vue/test-utils';
import type { DownloadMediaDTO, PlexMediaDTO, PlexMediaDataDTO } from '@dto';
import { AudioQuality, PlexMediaComparisonState, PlexMediaType, VideoQuality } from '@dto';
import { baseSetup } from '@services-test-base';
import MusicMediaList from '@/components/MediaOverview/MusicMediaList.vue';
import { useDialogStore, useMediaOverviewStore } from '@store';
import { listenMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus, useMediaOverviewCommandsBus } from '@composables/event-bus';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';
import MediaComparisonStateButton from '@/components/Common/MediaComparisonStateButton.vue';

function media(overrides: Partial<PlexMediaDTO>): PlexMediaDTO {
	return {
		addedAt: '2024-01-01T00:00:00Z',
		childCount: 0,
		children: [],
		comparisonId: 0,
		duration: 0,
		grandChildCount: 0,
		hasArt: false,
		hasTheme: false,
		hasThumb: false,
		genres: [],
		id: 0,
		mediaData: [],
		mediaSize: 0,
		plexApiMetaDataKey: 0,
		plexApiRatingKey: 0,
		plexLibraryId: 4,
		plexServerId: 2,
		qualities: [],
		rating: 0,
		searchTitle: '',
		sortIndex: 0,
		studio: '',
		summary: '',
		title: '',
		tvShowId: 0,
		tvShowSeasonId: 0,
		type: PlexMediaType.MusicTrack,
		year: 0,
		...overrides,
	};
}

function original(overrides: Partial<PlexMediaDataDTO>): PlexMediaDataDTO {
	const { audioQuality: overrideAudioQuality, ...originalOverrides } = overrides;
	return {
		audioCodec: '',
		audioQuality: overrideAudioQuality ?? AudioQuality.Unknown,
		duration: 180000,
		fileName: 'track.flac',
		id: 0,
		plexApiMediaId: 0,
		plexApiPartId: 0,
		size: 1000,
		videoCodec: '',
		videoQuality: VideoQuality.None,
		...originalOverrides,
	};
}

function artistFixture(): PlexMediaDTO {
	return media({
		id: 100,
		title: 'Artist',
		type: PlexMediaType.MusicArtist,
		children: [
			media({
				id: 7,
				parentId: 100,
				title: 'Album One',
				type: PlexMediaType.MusicAlbum,
				children: [
					media({
						id: 7,
						parentId: 7,
						title: 'Overlapping Track',
						sortIndex: 1,
						mediaData: [
							original({ id: 701, plexApiMediaId: 70, plexApiPartId: 1, fileName: 'disc-a.flac', size: 1000 }),
							original({ id: 702, plexApiMediaId: 70, plexApiPartId: 2, fileName: 'disc-b.flac', size: 2000 }),
							original({ id: 703, plexApiMediaId: 71, plexApiPartId: 3, fileName: 'alternate.mp3', size: 700 }),
						],
					}),
					media({
						id: 8,
						parentId: 7,
						title: 'Track Two',
						sortIndex: 2,
						mediaData: [original({ id: 801, plexApiMediaId: 80, fileName: 'track-two.flac' })],
					}),
				],
			}),
			media({
				id: 9,
				parentId: 100,
				title: 'Empty Album',
				type: PlexMediaType.MusicAlbum,
				children: [],
			}),
		],
	});
}

function multiDiscArtistFixture(): PlexMediaDTO {
	const artist = artistFixture();
	const album = artist.children[0]!;
	Object.assign(album.children[0]!, { discNumber: 2, trackNumber: 1, sortIndex: 99 });
	Object.assign(album.children[1]!, { discNumber: 1, trackNumber: 1, sortIndex: 88 });
	album.children.push(
		media({ id: 10, parentId: 7, title: 'Disc One Track Two', discNumber: 1, trackNumber: 2, sortIndex: 77 }),
		media({ id: 11, parentId: 7, title: 'Unnumbered Track', discNumber: null, trackNumber: null, sortIndex: 66 }),
		media({ id: 12, parentId: 7, title: 'Missing Metadata Track', sortIndex: 55 }),
	);
	artist.children.push(media({
		id: 20,
		parentId: 100,
		title: 'Another Album',
		type: PlexMediaType.MusicAlbum,
		children: [
			media({ id: 21, parentId: 20, title: 'Another Disc One', discNumber: 1, trackNumber: 1 }),
			media({ id: 22, parentId: 20, title: 'Another Disc Two', discNumber: 2, trackNumber: 1 }),
		],
	}));
	return artist;
}

const trackTitles: Record<number, string> = {
	7: 'Overlapping Track',
	8: 'Track Two',
	10: 'Disc One Track Two',
	11: 'Unnumbered Track',
	12: 'Missing Metadata Track',
	21: 'Another Disc One',
	22: 'Another Disc Two',
};

function trackRow(wrapper: VueWrapper, trackId: number) {
	const title = trackTitles[trackId];
	const row = wrapper.findAll('tbody tr').find((candidate) => candidate.find('.row-title').text() === title);
	if (!row)
		throw new Error(`Track ${trackId} is not rendered`);
	return row;
}

function trackCheckbox(wrapper: VueWrapper, trackId: number) {
	return trackRow(wrapper, trackId).get('.q-checkbox');
}

function trackComparison(wrapper: VueWrapper, trackId: number) {
	return trackRow(wrapper, trackId).get('.media-comparison-state-button');
}

describe('MusicMediaList hierarchy selection', () => {
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
		useMediaOverviewCommandsBus().reset();
		useMediaOverviewBarDownloadCommandBus().reset();
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
		useMediaOverviewCommandsBus().reset();
		useMediaOverviewBarDownloadCommandBus().reset();
	});

	async function render(item = artistFixture()) {
		const wrapper = await mountSuspended(MusicMediaList, {
			props: { mediaItem: item },
			global: { plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })] },
		});
		mounted.push(wrapper);
		return wrapper;
	}
	function captureDownloads() {
		const commands: DownloadMediaDTO[][] = [];
		listenMediaOverviewDownloadCommand((command) => commands.push(command));
		return commands;
	}

	test('Should render supplied album record label and originally available date metadata', async () => {
		// Arrange
		const item = artistFixture();
		Object.assign(item.children[0]!, {
			recordLabel: 'Example Records',
			originallyAvailableAt: '2020-04-12T00:00:00Z',
		});

		// Act
		const wrapper = await render(item);

		// Assert
		const metadata = wrapper.get('[data-cy="music-album-7"] .music-album-row');
		expect(metadata.text()).toContain('Record label');
		expect(metadata.text()).toContain('Example Records');
		expect(metadata.text()).toContain('Release date');
		expect(metadata.text()).toContain('2020');
	});

	test('Should prefer artist then album then tracks without duplicate descendants', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-album-checkbox-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands.map((command) => command.map((item) => ({ type: item.type, ids: item.mediaIds })))).toEqual([
			[{ type: PlexMediaType.MusicTrack, ids: [7] }],
			[{ type: PlexMediaType.MusicAlbum, ids: [7] }],
			[{ type: PlexMediaType.MusicArtist, ids: [100] }],
		]);
		expect(wrapper.get('[data-cy="music-artist-checkbox"]').attributes('aria-checked')).toBe('true');
		expect(commands[2]?.[0]).toMatchObject({ plexLibraryId: 4, plexServerId: 2 });
	});

	test('Should preserve sibling tracks and omit an empty album after demoting a selected artist', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[0]).toMatchObject([{ type: PlexMediaType.MusicTrack, mediaIds: [8] }]);
		expect(commands[0]).toHaveLength(1);
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(trackCheckbox(wrapper, 8).attributes('aria-checked')).toBe('true');
	});

	test('Should keep equal album and track IDs independent and promote all tracks to their album', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');

		// Assert
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(trackCheckbox(wrapper, 8).attributes('aria-checked')).toBe('false');

		// Act
		await trackCheckbox(wrapper, 8).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('true');
		expect(commands[0]).toMatchObject([{ type: PlexMediaType.MusicAlbum, mediaIds: [7] }]);
		expect(commands[0]).toHaveLength(1);
	});

	test('Should render multipart tracks as standard table rows with automatic download selection', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		const row = trackRow(wrapper, 7);
		expect(row.text()).toContain('Overlapping Track');
		expect(row.findAll('td')).toHaveLength(10);
		expect(row.find('.q-checkbox').exists()).toBe(true);
		expect(row.find('.media-comparison-state-button').exists()).toBe(true);
		expect(row.find('button .mdi-download').exists()).toBe(true);
		expect(row.text()).not.toContain('disc-a.flac');
		expect(row.text()).not.toContain('alternate.mp3');
		expect(commands[0]).toMatchObject([{
			type: PlexMediaType.MusicTrack,
			mediaIds: [7],
			qualities: [],
		}]);
	});

	test('Should exclude foreign media from the table and use automatic descendant payloads', async () => {
		// Arrange
		const item = artistFixture();
		item.children[0]!.children.push(media({
			id: 90,
			parentId: 7,
			title: 'Foreign Track',
			plexServerId: 99,
			mediaData: [original({ id: 9001, plexApiMediaId: 900 })],
		}));
		item.children.push(media({
			id: 91,
			parentId: 100,
			title: 'Foreign Album',
			type: PlexMediaType.MusicAlbum,
			plexLibraryId: 99,
			children: [media({ id: 92, parentId: 91, title: 'Unrelated Track' })],
		}));
		const wrapper = await render(item);
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-album-checkbox-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.text()).not.toContain('Foreign Track');
		expect(wrapper.find('[data-cy="music-album-91"]').exists()).toBe(false);
		expect(commands).toEqual([
			[expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [7], qualities: [] })],
			[expect.objectContaining({ type: PlexMediaType.MusicAlbum, mediaIds: [7], qualities: [] })],
			[expect.objectContaining({ type: PlexMediaType.MusicArtist, mediaIds: [100], qualities: [] })],
		]);
	});

	test('Should reset automatic track selection for a replacement root', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		const replacement = artistFixture();
		replacement.id = 200;
		replacement.children[0]!.parentId = 200;
		await wrapper.setProps({ mediaItem: replacement });

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands).toEqual([
			[expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [7], qualities: [] })],
			[expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [7], qualities: [] })],
		]);
	});

	test('Should clear selection when the artist changes and expose an empty hierarchy', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');

		// Act
		await wrapper.setProps({
			mediaItem: media({ id: 101, title: 'Artist Without Albums', type: PlexMediaType.MusicArtist, children: [] }),
		});
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="music-media-list-empty"]').text()).toBe('No albums found');
		expect(commands).toEqual([]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
	});

	test('Should not offer downloads when every album is empty', async () => {
		// Arrange
		const item = artistFixture();
		item.children = [item.children[1]!];
		const wrapper = await render(item);
		const commands = captureDownloads();

		// Act
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="music-artist-checkbox"]').attributes('aria-disabled')).toBe('true');
		expect(wrapper.get('[data-cy="music-album-checkbox-9"]').attributes('aria-disabled')).toBe('true');
		expect(commands).toEqual([]);
	});
	test('Should display projected hierarchy states and open only the artist-scoped comparison', async () => {
		// Arrange
		const item = artistFixture();
		item.comparisonId = 5;
		item.children[0]!.comparisonId = 5;
		item.children[0]!.children[0]!.comparisonId = 3;
		item.children[0]!.children[1]!.comparisonId = 1;
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await render(item);
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await wrapper.get('[data-cy="music-comparison-album-7"]').trigger('click');
		await trackComparison(wrapper, 7).get('button').trigger('click');
		await trackComparison(wrapper, 8).get('button').trigger('click');

		// Assert
		expect(open).not.toHaveBeenCalled();
		expect(wrapper.findAllComponents(MediaComparisonStateButton).map((badge) => ({
			type: badge.props('mediaType'), state: badge.props('comparisonState'),
		}))).toEqual(expect.arrayContaining([
			{ type: PlexMediaType.MusicArtist, state: PlexMediaComparisonState.Partial },
			{ type: PlexMediaType.MusicAlbum, state: PlexMediaComparisonState.Partial },
			{ type: PlexMediaType.MusicTrack, state: PlexMediaComparisonState.Missing },
			{ type: PlexMediaType.MusicTrack, state: PlexMediaComparisonState.Owned },
		]));
		expect(trackComparison(wrapper, 7).get('button .q-icon').classes()).toContain('mdi-music-note-off-outline');
		expect(trackCheckbox(wrapper, 7).attributes('aria-checked')).toBe('false');

		// Act
		await wrapper.get('[data-cy="music-comparison-artist-100"]').trigger('click');

		// Assert
		expect(open).toHaveBeenCalledExactlyOnceWith(item);
	});

	test.each([0, 1, 2, -1])('Should keep artist state %s status-only', async (comparisonId) => {
		// Arrange
		const item = artistFixture();
		item.comparisonId = comparisonId;
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await render(item);

		// Act
		await wrapper.get('[data-cy="music-comparison-artist-100"]').trigger('click');

		// Assert
		expect(open).not.toHaveBeenCalled();
	});

	test('Should allow missing artist comparison even when no albums are projected', async () => {
		// Arrange
		const item = media({ id: 101, type: PlexMediaType.MusicArtist, comparisonId: 3, children: [] });
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await render(item);

		// Act
		await wrapper.get('[data-cy="music-comparison-artist-101"]').trigger('click');

		// Assert
		expect(wrapper.find('[data-cy="music-media-list-empty"]').exists()).toBe(true);
		expect(open).toHaveBeenCalledExactlyOnceWith(item);
	});

	test('Should render all album tracks in one table without Disc levels', async () => {
		// Arrange
		const wrapper = await render(multiDiscArtistFixture());

		// Act
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await wrapper.get('[data-cy="music-album-20"] .q-item').trigger('click');

		// Assert
		expect(wrapper.findAll('[data-cy^="music-disc-"]')).toHaveLength(0);
		const firstAlbumTable = wrapper.get('[data-cy="music-album-7"] .media-q-table-scroll');
		const secondAlbumTable = wrapper.get('[data-cy="music-album-20"] .media-q-table-scroll');
		expect(firstAlbumTable.findAll('tbody tr')).toHaveLength(5);
		expect(secondAlbumTable.findAll('tbody tr')).toHaveLength(2);
		expect(firstAlbumTable.findAll('tbody tr').map((row) => row.find('.row-title').text())).toEqual([
			'Track Two', 'Disc One Track Two', 'Overlapping Track', 'Missing Metadata Track', 'Unnumbered Track',
		]);
		expect(wrapper.findAll('tbody tr')).toHaveLength(7);
	});

	test('Should render the same table columns as movie and TV detail lists', async () => {
		// Arrange
		const wrapper = await render(multiDiscArtistFixture());

		// Act
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Assert
		const table = wrapper.get('[data-cy="music-album-7"] .media-q-table-scroll');
		const header = table.get('thead');
		expect(header.text()).toContain('Title');
		expect(header.text()).toContain('Comparison state');
		expect(header.text()).toContain('Quality');
		expect(header.text()).toContain('Year');
		expect(header.text()).toContain('Duration');
		expect(header.text()).toContain('Size');
		expect(header.text()).toContain('Added At');
		expect(header.text()).toContain('Updated At');
		expect(header.text()).toContain('Actions');
		expect(header.text()).not.toContain('Original');
		expect(header.findAll('th')).toHaveLength(10);
		expect(table.find('.music-track-originals').exists()).toBe(false);
		expect(table.find('.music-track-table__header').exists()).toBe(false);
	});

	test('Should keep canonical selection across repeated track numbers', async () => {
		// Arrange
		const wrapper = await render(multiDiscArtistFixture());
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await wrapper.get('[data-cy="music-album-20"] .q-item').trigger('click');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		await trackCheckbox(wrapper, 8).trigger('click');
		await trackCheckbox(wrapper, 21).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[0]).toEqual([
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [8], plexServerId: 2, plexLibraryId: 4, qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [7], plexServerId: 2, plexLibraryId: 4, qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [21], plexServerId: 2, plexLibraryId: 4, qualities: [] }),
		]);
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(trackCheckbox(wrapper, 22).attributes('aria-checked')).toBe('false');
		expect(wrapper.get('.music-media-list__root').text()).toContain('3');

		// Act
		await wrapper.get('[data-cy="music-album-checkbox-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[1]).toEqual([
			expect.objectContaining({ type: PlexMediaType.MusicAlbum, mediaIds: [7], qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [21], qualities: [] }),
		]);
		expect(commands[2]).toEqual([expect.objectContaining({
			type: PlexMediaType.MusicArtist, mediaIds: [100], qualities: [],
		})]);

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[3]).toEqual([
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [8], qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [10], qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [12], plexServerId: 2, plexLibraryId: 4, qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [11], plexServerId: 2, plexLibraryId: 4, qualities: [] }),
			expect.objectContaining({ type: PlexMediaType.MusicAlbum, mediaIds: [20], qualities: [] }),
		]);
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(wrapper.get('[data-cy="music-album-checkbox-20"]').attributes('aria-checked')).toBe('true');
	});

	test('Should derive refreshed tracks and reset selection for a replacement artist object', async () => {
		// Arrange
		const wrapper = await render(multiDiscArtistFixture());
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await trackCheckbox(wrapper, 7).trigger('click');
		const replacement = multiDiscArtistFixture();
		replacement.children[0]!.children[0]!.discNumber = 3;
		replacement.children[0]!.children[0]!.trackNumber = 9;

		// Act
		await wrapper.setProps({ mediaItem: replacement });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands).toEqual([]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
		expect(wrapper.findAll('[data-cy^="music-disc-"]')).toHaveLength(0);
		const table = wrapper.get('[data-cy="music-album-7"] .media-q-table-scroll');
		expect(table.findAll('tbody tr')).toHaveLength(5);
		expect(table.findAll('tbody tr').map((row) => row.find('.row-title').text())).toContain('Overlapping Track');

		// Act
		await trackCheckbox(wrapper, 7).trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands).toEqual([[expect.objectContaining({ type: PlexMediaType.MusicTrack, mediaIds: [7], qualities: [] })]]);
	});
});
