import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia } from 'pinia';
import type { Pinia } from 'pinia';
import { of, Subject } from 'rxjs';
import { nextTick, reactive } from 'vue';
import type { PlexMediaDTO } from '@dto';
import { PlexMediaType } from '@dto';
import { baseSetup } from '@services-test-base';
import { useDialogStore, useDownloadStore, useMediaStore, useSettingsStore } from '@store';
import { resetMediaOverviewCommandsBus, sendMediaOverviewDownloadCommand } from '@/composables/event-bus';
import ConfirmationSection from '@/components/Views/Settings/ConfirmationSection.vue';
import MusicArtistDetails from '@/pages/music/[id]/details/[artistId].vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

const { route } = vi.hoisted(() => ({
	route: { params: { id: '4', artistId: '12' } },
}));

mockNuxtImport('useRoute', () => () => route);

function artist(children: PlexMediaDTO[] = []): PlexMediaDTO {
	return {
		addedAt: '2024-01-01T00:00:00Z',
		childCount: children.length,
		children,
		comparisonId: 0,
		duration: 0,
		grandChildCount: 0,
		hasArt: false,
		hasTheme: false,
		hasThumb: false,
		id: 12,
		mediaData: [],
		mediaSize: 0,
		plexApiMetaDataKey: 12,
		plexApiRatingKey: 12,
		plexLibraryId: 4,
		plexServerId: 2,
		qualities: [],
		rating: 0,
		searchTitle: 'Artist',
		sortIndex: 1,
		studio: '',
		summary: '',
		title: 'Artist',
		tvShowId: 0,
		tvShowSeasonId: 0,
		type: PlexMediaType.MusicArtist,
		year: 2024,
	};
}

describe('Music artist details page', () => {
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
		route.params = reactive({ id: '4', artistId: '12' });
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
		resetMediaOverviewCommandsBus();
	});

	async function render() {
		const wrapper = await mountSuspended(MusicArtistDetails, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QPage: { template: '<main><slot /></main>' },
					QScroll: { template: '<section><slot /></section>' },
					MediaOverviewBar: true, MediaPosterImage: true, DownloadConfirmation: true,
					QFileSize: true, QDateTime: true,
				},
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	test('Should render an empty artist and retain historical dates while hiding an unknown date', async () => {
		// Arrange
		const item = artist();
		item.originallyAvailableAt = '1912-01-02T00:00:00Z';
		item.updatedAt = '0001-01-01T00:00:00';
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValue(of(item));

		// Act
		const wrapper = await render();

		// Assert
		expect(wrapper.get('h1').text()).toBe('Artist');
		expect(wrapper.get('[data-cy="music-media-list-empty"]').text()).toBe('No albums found');
		expect(wrapper.text()).toContain('Originally available');
		expect(wrapper.findAll('dt').map((label) => label.text())).not.toContain('Updated');
	});

	test('Should discard an old response after direct navigation to another artist', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const second = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(second);
		const wrapper = await render();

		// Act
		route.params.artistId = '13';
		await nextTick();
		first.next(artist());
		await nextTick();

		// Assert
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.find('[data-cy="music-media-list-empty"]').exists()).toBe(false);

		// Act
		second.next({ ...artist(), id: 13, title: 'Current Artist' });
		await nextTick();

		// Assert
		expect(wrapper.get('h1').text()).toBe('Current Artist');
	});

	test('Should clear the old artist and reject a response belonging to another library', async () => {
		// Arrange
		const second = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(of(artist())).mockReturnValueOnce(second);
		const wrapper = await render();
		expect(wrapper.get('h1').text()).toBe('Artist');

		// Act
		route.params.id = '5';
		await nextTick();
		second.next(artist());
		await nextTick();

		// Assert
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.get('[data-cy="music-details-error"]').text()).toContain('Unable to load');
		expect(wrapper.find('[data-cy="music-media-list-empty"]').exists()).toBe(false);
	});

	test('Should recover from a failed request through the visible retry control', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const retry = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(retry);
		const wrapper = await render();

		// Act
		first.error(new Error('Unavailable'));
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="music-media-list-empty"]').exists()).toBe(false);
		expect(wrapper.get('[data-cy="music-details-error"]').text()).toContain('Unable to load');

		// Act
		await wrapper.get('button').trigger('click');
		retry.next(artist());
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="music-details-error"]').exists()).toBe(false);
		expect(wrapper.get('h1').text()).toBe('Artist');
	});

	test('Should reject invalid direct-navigation IDs without requesting media', async () => {
		// Arrange
		route.params.artistId = 'not-an-id';
		const request = vi.spyOn(useMediaStore(), 'getMediaDataDetailById');

		// Act
		const wrapper = await render();

		// Assert
		expect(request).not.toHaveBeenCalled();
		expect(wrapper.get('[data-cy="music-details-error"]').text()).toContain('Invalid music library or artist.');
		expect(wrapper.find('[data-cy="music-media-list-empty"]').exists()).toBe(false);
		expect(wrapper.get('button').text()).toContain('Back');
	});
	test.each([
		[PlexMediaType.MusicArtist, 'ask-download-music-artist-confirmation'],
		[PlexMediaType.MusicAlbum, 'ask-download-music-album-confirmation'],
		[PlexMediaType.MusicTrack, 'ask-download-music-track-confirmation'],
	] as const)('Should use the distinct %s confirmation control in the detail download consumer', async (type, control) => {
		// Arrange
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValue(of(artist()));
		const open = vi.spyOn(useDialogStore(), 'openMediaConfirmationDownloadDialog');
		const download = vi.spyOn(useDownloadStore(), 'downloadMedia').mockImplementation(() => undefined);
		await render();
		const settings = await mountSuspended(ConfirmationSection, { global: {
			plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
			stubs: { QSection: { template: '<section><slot /></section>' }, HelpGroup: { template: '<div><slot /></div>' }, HelpRow: { template: '<div><slot /></div>' } },
		} });
		mounted.push(settings);
		const command = [{ type, mediaIds: [12], plexServerId: 2, plexLibraryId: 4, qualities: [], keepCompletedInDownloadFolder: false }];
		sendMediaOverviewDownloadCommand(command);
		expect(open).toHaveBeenCalledExactlyOnceWith(command);
		open.mockClear();

		// Act
		await settings.get(`[data-cy="${control}"]`).trigger('click');
		sendMediaOverviewDownloadCommand(command);

		// Assert
		expect(open).not.toHaveBeenCalled();
		expect(download).toHaveBeenCalledExactlyOnceWith({ downloadMedias: command, customDestinationFolderPath: '', destinationFolderPathId: null });
		expect(useSettingsStore().confirmationSettings.askDownloadMovieConfirmation).toBe(true);
		expect(useSettingsStore().confirmationSettings.askDownloadTvShowConfirmation).toBe(true);
	});
});
