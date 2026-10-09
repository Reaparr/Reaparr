import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia } from 'pinia';
import type { Pinia } from 'pinia';
import type { DownloadMediaDTO, PlexMediaDTO, PlexMediaDataDTO } from '@dto';
import { PlexMediaComparisonState, PlexMediaType, VideoQuality } from '@dto';
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
	return {
		audioCodec: 'flac',
		duration: 180000,
		fileName: 'track.flac',
		id: 0,
		plexApiMediaId: 0,
		plexApiPartId: 0,
		size: 1000,
		videoCodec: '',
		videoResolution: VideoQuality.Unknown,
		...overrides,
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
							original({ id: 703, plexApiMediaId: 71, plexApiPartId: 3, fileName: 'alternate.mp3', audioCodec: 'mp3', size: 700 }),
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

	test('Should prefer artist then album then tracks without duplicate descendants', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
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
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[0]).toMatchObject([{ type: PlexMediaType.MusicTrack, mediaIds: [8] }]);
		expect(commands[0]).toHaveLength(1);
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(wrapper.get('[data-cy="music-track-checkbox-7-8"]').attributes('aria-checked')).toBe('true');
	});

	test('Should keep equal album and track IDs independent and promote all tracks to their album', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');

		// Assert
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('mixed');
		expect(wrapper.get('[data-cy="music-track-checkbox-7-8"]').attributes('aria-checked')).toBe('false');

		// Act
		await wrapper.get('[data-cy="music-track-checkbox-7-8"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="music-album-checkbox-7"]').attributes('aria-checked')).toBe('true');
		expect(commands[0]).toMatchObject([{ type: PlexMediaType.MusicAlbum, mediaIds: [7] }]);
		expect(commands[0]).toHaveLength(1);
	});

	test('Should group multipart originals and send the representative part for a track choice', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		expect(wrapper.text()).toContain('disc-a.flac');
		expect(wrapper.text()).toContain('disc-b.flac');
		expect(wrapper.text()).toContain('alternate.mp3');
		expect(wrapper.text()).toContain('flac');
		expect(wrapper.text()).toContain('mp3');

		// Act
		await wrapper.get('[data-cy="music-track-original-7-7-70"]').trigger('click');
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands[0]).toMatchObject([{
			type: PlexMediaType.MusicTrack,
			mediaIds: [7],
			qualities: [{ mediaId: 7, dataId: 701, mediaDataType: PlexMediaType.MusicTrack, quality: VideoQuality.Unknown }],
		}]);
	});

	test('Should propagate only selected descendant originals through album and artist requests', async () => {
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
		await wrapper.get('[data-cy="music-track-original-7-7-71"]').trigger('click');
		await wrapper.get('[data-cy="music-track-original-7-8-80"]').trigger('click');

		// Act
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-album-checkbox-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-artist-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.find('[data-cy="music-track-7-90"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy="music-album-91"]').exists()).toBe(false);
		expect(commands[0]).toMatchObject([{ type: PlexMediaType.MusicTrack, qualities: [{ mediaId: 7, dataId: 703 }] }]);
		expect(commands[1]).toMatchObject([{
			type: PlexMediaType.MusicAlbum,
			qualities: [{ mediaId: 7, dataId: 703 }, { mediaId: 8, dataId: 801 }],
		}]);
		expect(commands[2]).toMatchObject([{
			type: PlexMediaType.MusicArtist,
			qualities: [{ mediaId: 7, dataId: 703 }, { mediaId: 8, dataId: 801 }],
		}]);
	});

	test('Should use automatic originals and clear an explicit choice for a replacement root', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');
		await wrapper.get('[data-cy="music-track-original-7-7-70"]').trigger('click');
		await wrapper.get('[data-cy="music-track-original-7-7-automatic"]').trigger('click');
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');
		await wrapper.get('[data-cy="music-track-original-7-7-70"]').trigger('click');
		const replacement = artistFixture();
		replacement.id = 200;
		replacement.children[0]!.parentId = 200;
		await wrapper.setProps({ mediaItem: replacement });
		await wrapper.get('[data-cy="music-album-7"] .q-item').trigger('click');

		// Act
		await wrapper.get('[data-cy="music-track-checkbox-7-7"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(commands).toMatchObject([[{ qualities: [] }], [{ qualities: [] }]]);
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
		await wrapper.get('[data-cy="music-comparison-track-7-7"]').trigger('click');
		await wrapper.get('[data-cy="music-comparison-track-7-8"]').trigger('click');

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
		expect(wrapper.get('[data-cy="music-comparison-track-7-7"] .q-icon').classes()).toContain('mdi-music-note-off-outline');
		expect(wrapper.get('[data-cy="music-track-checkbox-7-7"]').attributes('aria-checked')).toBe('false');

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
});
