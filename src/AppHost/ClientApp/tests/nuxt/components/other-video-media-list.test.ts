import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { AudioQuality, PlexMediaType, VideoQuality, type DownloadMediaDTO, type PlexMediaDTO } from '@dto';
import { useMediaOverviewStore } from '@store';
import { listenMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus, useMediaOverviewCommandsBus } from '@composables/event-bus';
import OtherVideoMediaList from '@/components/MediaOverview/OtherVideoMediaList.vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

describe('OtherVideoMediaList - individual video selection', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		useMediaOverviewCommandsBus().reset();
		useMediaOverviewBarDownloadCommandBus().reset();
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		useMediaOverviewCommandsBus().reset();
		useMediaOverviewBarDownloadCommandBus().reset();
	});

	function video(): PlexMediaDTO {
		return generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id: 20, type: PlexMediaType.OtherVideos, plexServerId: 3, plexLibraryId: 5,
				title: 'Individual video', hasThumb: false, children: [],
				mediaData: [
					{ id: 10, plexApiMediaId: 100, plexApiPartId: 101, fileName: 'part-1.mp4', duration: 6000, size: 1024, videoQuality: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac', audioQuality: AudioQuality.None },
					{ id: 11, plexApiMediaId: 100, plexApiPartId: 102, fileName: 'part-2.mp4', duration: 6000, size: 1024, videoQuality: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac', audioQuality: AudioQuality.None },
					{ id: 30, plexApiMediaId: 200, plexApiPartId: 201, fileName: 'alternative.mkv', duration: 12000, size: 4096, videoQuality: VideoQuality.UHD_4K, videoCodec: 'hevc', audioCodec: 'flac', audioQuality: AudioQuality.None },
				],
				qualities: [
					{ mediaId: 20, dataId: 10, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.FullHD, audioQuality: AudioQuality.None },
					{ mediaId: 20, dataId: 30, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.UHD_4K, audioQuality: AudioQuality.None },
				],
			},
		});
	}
	async function render(mediaItem = video()) {
		const wrapper = await mountSuspended(OtherVideoMediaList, {
			props: { mediaItem },
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

	test('Should replace one explicit source with another source', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		const mediaTable = wrapper.findComponent({ name: 'MediaQTable' });
		// Assert
		expect(wrapper.text()).toContain('Full HD (1080p)');
		expect(wrapper.text()).toContain('4K (2160p)');
		mediaTable.vm.$emit('selection', { indexKey: 20, keys: [30], allSelected: null });

		// Act
		useMediaOverviewBarDownloadCommandBus().emit('download');

		expect(commands[0]![0]!.selectionKind).toBeNull();
		expect(commands[0]?.[0]?.qualities).toEqual([
			{ mediaId: 20, dataId: 30, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.UHD_4K, audioQuality: AudioQuality.None },
		]);

		// Act
		mediaTable.vm.$emit('selection', { indexKey: 20, keys: [10], allSelected: null });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		expect(commands[1]![0]!.selectionKind).toBeNull();
		expect(commands[1]?.[0]?.qualities).toEqual([
			{ mediaId: 20, dataId: 10, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.FullHD, audioQuality: AudioQuality.None },
		]);
	});

	test('Should remove a deselected video from the download action', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="other-video-root-checkbox"]').trigger('click');

		// Act
		await wrapper.get('[data-cy="other-video-root-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="other-video-root-checkbox"]').attributes('aria-checked')).toBe('false');
		expect(commands).toEqual([]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
	});

	test('Should clear the selection before another video or library replaces the current item', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="other-video-root-checkbox"]').trigger('click');
		wrapper.findComponent({ name: 'MediaQTable' }).vm.$emit('selection', { indexKey: 20, keys: [30], allSelected: null });

		// Act
		await wrapper.setProps({ mediaItem: { ...video(), id: 40, plexLibraryId: 6, plexServerId: 7 } });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.find('[data-cy="other-video-selected-count"]').exists()).toBe(false);
		expect(wrapper.get('[data-cy="other-video-root-checkbox"]').attributes('aria-checked')).toBe('false');
		expect(commands).toEqual([]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
	});

	test('Should expose missing originals without offering an empty download', async () => {
		// Arrange
		const wrapper = await render({ ...video(), mediaData: [] });
		const commands = captureDownloads();

		// Act
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="other-video-files-empty"]').text()).toBe('No original files available.');
		expect(wrapper.find('[data-cy="other-video-root-checkbox"]').exists()).toBe(false);
		expect(commands).toEqual([]);
	});

	test('Should discard a selected source and prevent downloading when the current video loses all files', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="other-video-root-checkbox"]').trigger('click');
		wrapper.findComponent({ name: 'MediaQTable' }).vm.$emit('selection', { indexKey: 20, keys: [30], allSelected: null });

		// Act
		await wrapper.setProps({ mediaItem: { ...video(), mediaData: [] } });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.find('[data-cy="other-video-files-empty"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="other-video-root-checkbox"]').exists()).toBe(false);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
		expect(commands).toEqual([]);
		expect(mock.history.get).toHaveLength(0);
	});
});
