import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { createI18n } from 'vue-i18n';
import { baseSetup } from '@services-test-base';
import { generatePlexMedia, generatePlexLibrary, generatePlexServer, Seed } from '@mock';
import { PlexMediaComparisonState, PlexMediaType } from '@dto';
import { MediaMetaDataTypes } from '@enums';
import { useDialogStore, useLibraryStore, useMediaOverviewStore, useServerStore, useSettingsStore } from '@store';
import MediaComparisonStateButton from '@/components/Common/MediaComparisonStateButton.vue';
import MediaPoster from '@/components/MediaOverview/PosterTable/MediaPoster.vue';
import MediaQTable from '@/components/MediaOverview/MediaTable/MediaQTable.vue';
import MediaFilterMenu from '@/components/MediaOverview/MediaFilterMenu.vue';
import messages from '@/lang/en-US.json';

const menuShell = defineComponent({ template: '<div><slot /></div>' });

describe('Music comparison overview surfaces', () => {
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
	});
	function plugins() {
		return [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })];
	}
	function item(type: PlexMediaType, comparisonId = 3) {
		return generatePlexMedia({
			config: { seed: 624 }, partialData: {
				id: 100, type, comparisonId, plexLibraryId: 4, plexServerId: 2,
				title: 'Comparison root', hasThumb: false, children: [],
			},
		});
	}

	test.each([3, 5])('Should open actual artist-scoped comparison from Music poster state %s', async (comparisonId) => {
		// Arrange
		const mediaItem = item(PlexMediaType.MusicArtist, comparisonId);
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await mountSuspended(MediaPoster, { props: { mediaItem }, global: { plugins: plugins() } });
		mounted.push(wrapper);

		// Act
		await wrapper.get(`[data-cy="comparison-chip-${comparisonId === 3 ? 'Missing' : 'Partial'}"]`).trigger('click');

		// Assert
		expect(wrapper.find('.mdi-music-note-off-outline').exists()).toBe(comparisonId === 3);
		expect(wrapper.find('.mdi-video-off-outline').exists()).toBe(false);
		expect(open).toHaveBeenCalledExactlyOnceWith(mediaItem);
	});

	test.each([0, 1, 2])('Should keep Music poster state %s status-only', async (comparisonId) => {
		// Arrange
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await mountSuspended(MediaPoster, {
			props: { mediaItem: item(PlexMediaType.MusicArtist, comparisonId) }, global: { plugins: plugins() },
		});
		mounted.push(wrapper);

		// Act
		await wrapper.findComponent(MediaComparisonStateButton).get('button').trigger('click');

		// Assert
		expect(open).not.toHaveBeenCalled();
	});

	test.each([PlexMediaType.Movie, PlexMediaType.TvShow])('Should preserve %s poster upgrade details behavior', async (type) => {
		// Arrange
		const mediaItem = item(type, 4);
		const open = vi.spyOn(useDialogStore(), 'openMediaComparisonDetailsDialog');
		const wrapper = await mountSuspended(MediaPoster, { props: { mediaItem }, global: { plugins: plugins() } });
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="comparison-chip-HigherQuality"]').trigger('click');

		// Assert
		expect(open).toHaveBeenCalledExactlyOnceWith(mediaItem);
	});

	test.each([PlexMediaType.PhotoAlbum, PlexMediaType.OtherVideos])('Should not render comparison on a %s poster', async (type) => {
		// Arrange / Act
		const wrapper = await mountSuspended(MediaPoster, { props: { mediaItem: item(type) }, global: { plugins: plugins() } });
		mounted.push(wrapper);

		// Assert
		expect(wrapper.findComponent(MediaComparisonStateButton).exists()).toBe(false);
	});

	test.each([
		[PlexMediaType.MusicArtist, false, true],
		[PlexMediaType.PhotoImage, false, false],
		[PlexMediaType.OtherVideos, true, false],
		[PlexMediaType.Movie, true, true],
		[PlexMediaType.Episode, true, true],
	] as const)('Should gate table columns for %s without introducing unsupported quality or comparison', async (type, quality, comparison) => {
		// Arrange / Act
		const wrapper = await mountSuspended(MediaQTable, {
			props: { rows: [item(type)], selection: null }, global: { plugins: plugins() },
		});
		mounted.push(wrapper);

		// Assert
		const headers = wrapper.get('thead').text();
		expect(headers.includes('Quality')).toBe(quality);
		expect(headers.includes('Comparison state')).toBe(comparison);
		expect(wrapper.findComponent(MediaComparisonStateButton).exists()).toBe(comparison);
		expect(wrapper.find('.mdi-music-note-off-outline').exists()).toBe(type === PlexMediaType.MusicArtist);
	});

	test.each([false, true])('Should offer Music states without upgrades for owned=%s', async (owned) => {
		// Arrange
		const seed = new Seed(624);
		useSettingsStore().displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		useLibraryStore().updateLibrary(generatePlexLibrary({ seed, plexServerId: 2, type: PlexMediaType.MusicArtist, partialData: { id: 4 } }));
		useServerStore().servers.push(generatePlexServer({ id: 2, config: { seed: 624 }, partialData: { owned } }));
		useMediaOverviewStore().libraryId = 4;
		const wrapper = await mountSuspended(MediaFilterMenu, {
			global: { plugins: plugins(), stubs: { QMenu: menuShell } },
		});
		mounted.push(wrapper);

		expect(wrapper.find(`[data-cy="media-filter-menu-category-${MediaMetaDataTypes.Quality}"]`).exists()).toBe(false);
		// Act
		await wrapper.get(`[data-cy="media-filter-menu-category-${MediaMetaDataTypes.ComparisonState}"]`).trigger('click');

		// Assert
		const states = wrapper.findAllComponents(MediaComparisonStateButton).map((button) => button.props('comparisonState'));
		expect(states).toEqual(expect.arrayContaining([
			PlexMediaComparisonState.NotCompared, PlexMediaComparisonState.Owned,
			PlexMediaComparisonState.Pending, PlexMediaComparisonState.Partial,
		]));
		expect(states.includes(PlexMediaComparisonState.Missing)).toBe(!owned);
		expect(states).toHaveLength(owned ? 4 : 5);
	});
});
