import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { nextTick } from 'vue';
import { createI18n } from 'vue-i18n';
import { cloneDeep } from 'lodash-es';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO, generateSettingsModel } from '@mock';
import { SettingsPaths } from '@api-urls';
import { PlexMediaType, ViewMode, type SettingsModelDTO } from '@dto';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import MediaOverviewBar from '@/components/MediaOverview/MediaOverviewBar.vue';
import ConfirmationSection from '@/components/Views/Settings/ConfirmationSection.vue';
import messages from '@/lang/en-US.json';

const families = [
	[PlexMediaType.MusicArtist, 'musicArtistViewMode'],
	[PlexMediaType.PhotoAlbum, 'photoAlbumViewMode'],
	[PlexMediaType.OtherVideos, 'otherVideosViewMode'],
] as const;
const confirmationControls = [
	['music-artist', 'askDownloadMusicArtistConfirmation'],
	['music-album', 'askDownloadMusicAlbumConfirmation'],
	['music-track', 'askDownloadMusicTrackConfirmation'],
	['photo-album', 'askDownloadPhotoAlbumConfirmation'],
	['photo-image', 'askDownloadPhotoImageConfirmation'],
	['other-videos', 'askDownloadOtherVideosConfirmation'],
] as const;
const slotShell = { template: '<div><slot /></div>' };

describe('Family preferences - controls and API persistence', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.useRealTimers();
	});
	function plugins() {
		return [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })];
	}
	async function renderBar() {
		const wrapper = await mountSuspended(MediaOverviewBar, { global: {
			plugins: plugins(), stubs: {
				MediaOverviewBarHeader: true, MediaOverviewSearchBar: true, QMenu: slotShell,
				VerticalButton: slotShell,
			},
		} });
		mounted.push(wrapper);
		return wrapper;
	}

	test.each(families)('Should autosave both %s view modes independently and rehydrate the visible control', async (type, field) => {
		// Arrange
		let persisted = generateSettingsModel({ config: { seed: 625 } });
		persisted.displaySettings.movieViewMode = ViewMode.Table;
		persisted.displaySettings.tvShowViewMode = ViewMode.Table;
		persisted.displaySettings.musicArtistViewMode = ViewMode.Poster;
		persisted.displaySettings.photoAlbumViewMode = ViewMode.Poster;
		persisted.displaySettings.otherVideosViewMode = ViewMode.Poster;
		persisted.displaySettings.allOverviewViewMode = type;
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(() => [200, generateResultDTO(cloneDeep(persisted))]);
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => {
			persisted = JSON.parse(request.data) as SettingsModelDTO;
			return [200, generateResultDTO(cloneDeep(persisted))];
		});
		const store = useSettingsStore();
		await subscribeSpyTo(store.setup()).onComplete();
		const wrapper = await renderBar();
		vi.useFakeTimers();

		// Act
		await wrapper.get('[data-cy="view-mode-table-btn"]').trigger('click');
		await vi.advanceTimersByTimeAsync(600);

		// Assert
		expect(persisted.displaySettings[field]).toBe(ViewMode.Table);
		expect(useMediaOverviewStore().showSelectionButton).toBe(true);
		for (const [, otherField] of families.filter(([, key]) => key !== field)) {
			expect(persisted.displaySettings[otherField]).toBe(ViewMode.Poster);
		}
		expect(persisted.displaySettings.movieViewMode).toBe(ViewMode.Table);
		expect(persisted.displaySettings.tvShowViewMode).toBe(ViewMode.Table);
		const tableSettings = cloneDeep(persisted);
		await wrapper.get('[data-cy="view-mode-poster-btn"]').trigger('click');
		await vi.advanceTimersByTimeAsync(600);
		expect(persisted.displaySettings[field]).toBe(ViewMode.Poster);
		expect(useMediaOverviewStore().showSelectionButton).toBe(false);
		expect(mock.history.put).toHaveLength(2);

		// Act
		persisted = tableSettings;
		wrapper.unmount();
		pinia = createPinia();
		setActivePinia(pinia);
		await subscribeSpyTo(useSettingsStore().setup()).onComplete();
		vi.useRealTimers();
		const reloaded = await renderBar();
		await nextTick();

		// Assert
		expect(useMediaOverviewStore().showSelectionButton).toBe(true);
		expect(reloaded.get('[data-cy="view-mode-table-btn"]').find('.mdi-check').exists()).toBe(true);
		expect(reloaded.get('[data-cy="view-mode-poster-btn"]').find('.mdi-check').exists()).toBe(false);
	});

	test.each(confirmationControls)('Should persist and reload only the %s confirmation control', async (control, field) => {
		// Arrange
		let persisted = generateSettingsModel({ config: { seed: 625 } });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(() => [200, generateResultDTO(cloneDeep(persisted))]);
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => {
			persisted = JSON.parse(request.data) as SettingsModelDTO;
			return [200, generateResultDTO(cloneDeep(persisted))];
		});
		const store = useSettingsStore();
		await subscribeSpyTo(store.setup()).onComplete();
		const original = cloneDeep(store.confirmationSettings);
		const wrapper = await mountSuspended(ConfirmationSection, { global: {
			plugins: plugins(), stubs: { QSection: slotShell, HelpGroup: slotShell, HelpRow: slotShell },
		} });
		mounted.push(wrapper);
		vi.useFakeTimers();

		// Act
		await wrapper.get(`[data-cy="ask-download-${control}-confirmation"]`).trigger('click');
		await vi.advanceTimersByTimeAsync(600);
		await subscribeSpyTo(store.refreshSettings()).onComplete();

		// Assert
		expect(mock.history.put).toHaveLength(1);
		expect(persisted.confirmationSettings).toEqual({ ...original, [field]: !original[field] });
		expect(store.confirmationSettings).toEqual(persisted.confirmationSettings);
		expect(wrapper.get(`[data-cy="ask-download-${control}-confirmation"]`).attributes('aria-checked')).toBe(String(!original[field]));
	});
});
