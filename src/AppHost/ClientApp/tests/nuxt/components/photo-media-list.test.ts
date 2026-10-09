import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { PlexMediaType, VideoQuality, type DownloadMediaDTO, type PlexMediaDTO } from '@dto';
import { useMediaOverviewStore } from '@store';
import { listenMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus, useMediaOverviewCommandsBus } from '@composables/event-bus';
import PhotoMediaList from '@/components/MediaOverview/PhotoMediaList.vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

describe('PhotoMediaList - owned asset selection', () => {
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

	function asset(id: number, parentId = 20, type = PlexMediaType.PhotoImage): PlexMediaDTO {
		return generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id, parentId, type, plexServerId: 3, plexLibraryId: 4,
				title: `Asset ${id}`, sortIndex: id, hasThumb: false, mediaSize: 1024,
				mediaData: [{
					id: id + 100, fileName: id === 22 ? 'holiday.mp4' : 'holiday.jpg', size: 1024,
					duration: id === 22 ? 6000 : 0, audioCodec: '', videoCodec: id === 22 ? 'h264' : '',
					videoResolution: VideoQuality.Unknown, plexApiMediaId: id, plexApiPartId: id,
				}],
			},
		});
	}

	function album(id = 20): PlexMediaDTO {
		return generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id, type: PlexMediaType.PhotoAlbum, plexServerId: 3, plexLibraryId: 4,
				title: `Album ${id}`, hasThumb: false, childCount: 2, children: [asset(21, id), asset(22, id)],
			},
		});
	}

	async function render(mediaItem = album()) {
		const wrapper = await mountSuspended(PhotoMediaList, {
			props: { mediaItem },
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { MediaPosterImage: true, QFileSize: true, QDuration: true },
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	function captureDownloads() {
		const commands: DownloadMediaDTO[][] = [];
		listenMediaOverviewDownloadCommand((command) => commands.push(command));
		return commands;
	}

	test('Should show partial selection and keep a video-file child in the Photos download family', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();

		// Act
		await wrapper.get('[data-cy="photo-asset-checkbox-22"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="photo-selected-count"]').text()).toBe('1 selected');
		expect(wrapper.get('[data-cy="photo-album-checkbox"]').attributes('aria-checked')).toBe('mixed');
		expect(wrapper.get('[data-cy="photo-asset-22"]').text()).toContain('holiday.mp4');
		expect(commands).toHaveLength(1);
		expect(commands[0]?.map((command) => ({ ids: command.mediaIds, type: command.type }))).toEqual([
			{ ids: [22], type: PlexMediaType.PhotoImage },
		]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(true);
	});

	test('Should give an explicitly selected album precedence over selected assets without duplicate downloads', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="photo-asset-checkbox-21"]').trigger('click');

		// Act
		await wrapper.get('[data-cy="photo-album-checkbox"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="photo-selected-count"]').text()).toBe('2 selected');
		expect(wrapper.get('[data-cy="photo-album-checkbox"]').attributes('aria-checked')).toBe('true');
		expect(wrapper.get('[data-cy="photo-asset-checkbox-21"]').attributes('aria-checked')).toBe('true');
		expect(wrapper.get('[data-cy="photo-asset-checkbox-22"]').attributes('aria-checked')).toBe('true');
		expect(commands[0]?.map((command) => ({ ids: command.mediaIds, type: command.type }))).toEqual([
			{ ids: [20], type: PlexMediaType.PhotoAlbum },
		]);
	});

	test('Should downgrade album selection to remaining assets when one child is unchecked and reset all selection', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="photo-album-checkbox"]').trigger('click');

		// Act
		await wrapper.get('[data-cy="photo-asset-checkbox-21"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="photo-selected-count"]').text()).toBe('1 selected');
		expect(wrapper.get('[data-cy="photo-album-checkbox"]').attributes('aria-checked')).toBe('mixed');
		expect(commands[0]?.flatMap((command) => command.mediaIds)).toEqual([22]);

		// Act
		await wrapper.get('[data-cy="photo-reset-selection"]').trigger('click');
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="photo-selected-count"]').text()).toBe('0 selected');
		expect(wrapper.get('[data-cy="photo-album-checkbox"]').attributes('aria-checked')).toBe('false');
		expect(commands).toHaveLength(1);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
	});

	test('Should clear selection when the displayed album props change without remounting', async () => {
		// Arrange
		const wrapper = await render();
		const commands = captureDownloads();
		await wrapper.get('[data-cy="photo-album-checkbox"]').trigger('click');

		// Act
		await wrapper.setProps({ mediaItem: album(30) });
		useMediaOverviewBarDownloadCommandBus().emit('download');

		// Assert
		expect(wrapper.get('[data-cy="photo-selected-count"]').text()).toBe('0 selected');
		expect(wrapper.get('[data-cy="photo-album-checkbox"]').attributes('aria-checked')).toBe('false');
		expect(wrapper.get('[data-cy="photo-asset-checkbox-22"]').attributes('aria-checked')).toBe('false');
		expect(commands).toEqual([]);
		expect(useMediaOverviewStore().downloadButtonVisible).toBe(false);
	});

	test('Should render only directly owned PhotoImage children and expose an empty state when there are none', async () => {
		// Arrange
		const mediaItem = album();
		mediaItem.children.push(asset(23, 99), asset(24, 20, PlexMediaType.OtherVideos));
		const wrapper = await render(mediaItem);

		// Assert
		expect(wrapper.find('[data-cy="photo-asset-23"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy="photo-asset-24"]').exists()).toBe(false);

		// Act
		await wrapper.setProps({ mediaItem: { ...mediaItem, children: [] } });

		// Assert
		expect(wrapper.get('[data-cy="photo-assets-empty"]').text()).toBe('No images or clips found in this album.');
		expect(wrapper.find('[data-cy="photo-asset-21"]').exists()).toBe(false);
		expect(mock.history.get).toHaveLength(0);
	});
});
