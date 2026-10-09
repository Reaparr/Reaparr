import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia } from 'pinia';
import type { Pinia } from 'pinia';
import type { DownloadMediaDTO, PlexMediaDTO } from '@dto';
import { PlexMediaType } from '@dto';
import { baseSetup } from '@services-test-base';
import MusicMediaList from '@/components/MediaOverview/MusicMediaList.vue';
import { useMediaOverviewStore } from '@store';
import { listenMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus, useMediaOverviewCommandsBus } from '@composables/event-bus';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

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

function artistFixture(): PlexMediaDTO {
	return media({
		id: 100,
		title: 'Artist',
		type: PlexMediaType.MusicArtist,
		children: [
			media({
				id: 7,
				title: 'Album One',
				type: PlexMediaType.MusicAlbum,
				children: [
					media({ id: 7, parentId: 7, title: 'Overlapping Track', sortIndex: 1 }),
					media({ id: 8, parentId: 7, title: 'Track Two', sortIndex: 2 }),
				],
			}),
			media({
				id: 9,
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
});
