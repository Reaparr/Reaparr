import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { baseSetup } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { PlexMediaType, VideoQuality, type DownloadMediaDTO, type PlexMediaDTO } from '@dto';
import { useMediaOverviewStore } from '@store';
import { listenMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus, useMediaOverviewCommandsBus } from '@composables/event-bus';
import OtherVideoMediaList from '@/components/MediaOverview/OtherVideoMediaList.vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

describe('OtherVideoMediaList - individual video selection', () => {
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
					{ id: 10, plexApiMediaId: 100, plexApiPartId: 101, fileName: 'part-1.mp4', duration: 6000, size: 1024, videoResolution: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac' },
					{ id: 11, plexApiMediaId: 100, plexApiPartId: 102, fileName: 'part-2.mp4', duration: 6000, size: 1024, videoResolution: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac' },
					{ id: 30, plexApiMediaId: 200, plexApiPartId: 201, fileName: 'alternative.mkv', duration: 12000, size: 4096, videoResolution: VideoQuality.UHD_4K, videoCodec: 'hevc', audioCodec: 'flac' },
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

	test('Should select one owning video while retaining every original file and the existing automatic choice', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();

		// Act
		await wrapper.get('[data-cy="other-video-root-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		const rows = wrapper.findAll('.other-video-files__item');
		expect(rows[0]?.text()).toContain('part-1.mp4');
		expect(rows[0]?.text()).toContain('part-2.mp4');
		expect(rows[1]?.text()).toContain('alternative.mkv');
		expect(wrapper.get('[data-cy="other-video-selected-count"]').text()).toBe('1 selected');
		expect(commands[0]).toMatchObject([{
			type: PlexMediaType.OtherVideos, mediaIds: [20], plexLibraryId: 5, plexServerId: 3, qualities: [],
		}]);
		expect(commands[0]).toHaveLength(1);
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

		// Act
		await wrapper.setProps({ mediaItem: { ...video(), id: 40, plexLibraryId: 6, plexServerId: 7 } });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="other-video-selected-count"]').text()).toBe('0 selected');
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
		expect(wrapper.get('[data-cy="other-video-files-empty"]').attributes('role')).toBe('status');
		expect(wrapper.find('[data-cy="other-video-root-checkbox"]').exists()).toBe(false);
		expect(commands).toEqual([]);
	});
});
