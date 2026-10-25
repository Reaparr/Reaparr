import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import { DOMWrapper, flushPromises } from '@vue/test-utils';
import { baseSetup } from '@services-test-base';
import { PhotoItemKind, PlexMediaType, SortType, ViewMode } from '@dto';
import { useLibraryStore, useMediaOverviewStore, usePhotoBrowsingStore, useSettingsStore } from '@store';
import { createDefaultMediaOverviewSort, toggleMediaOverviewSort } from '@composables';
import { generatePlexLibrary, generateSupportedSorts, Seed } from '@mock';
import messages from '@/lang/en-US.json';
import MediaOverviewBar from '@/components/MediaOverview/MediaOverviewBar.vue';

const i18n = () => createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } });

describe('MediaOverviewBar - photo mode', () => {
	let pinia = createPinia();
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
	});

	function setSortMetadata(mediaType: PlexMediaType) {
		const store = useMediaOverviewStore();
		store.metadataList = {
			mediaCount: 0,
			countryCount: 0,
			genreCount: 0,
			qualityCount: 0,
			roleCount: 0,
			countries: [],
			genres: [],
			qualities: [],
			roles: [],
			supportedSorts: generateSupportedSorts(mediaType),
		};
		return store;
	}

	test('derives sort menu choices from metadata capabilities for each media family', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.MusicArtist;
		const mediaOverviewStore = setSortMetadata(PlexMediaType.MusicArtist);
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { libraryId: 0 },
			global: {
				plugins: [pinia, i18n()],
				stubs: { teleport: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="media-overview-sort-btn"]').trigger('click');
		await flushPromises();

		// Assert
		const audioOption = wrapper.find(`[data-cy="sort-option-${SortType.AudioQuality}-btn"]`);
		expect(audioOption.exists()).toBe(true);
		expect(audioOption.text()).toContain('Audio Quality');
		expect(wrapper.find(`[data-cy="sort-option-${SortType.VideoQuality}-btn"]`).exists()).toBe(false);
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		mediaOverviewStore.metadataList = {
			...mediaOverviewStore.metadataList,
			supportedSorts: generateSupportedSorts(PlexMediaType.Movie),
		};
		expect(mediaOverviewStore.getSortOptions().map((option) => option.field)).not.toContain(SortType.AudioQuality);
		expect(mediaOverviewStore.getSortOptions().map((option) => option.field)).toContain(SortType.VideoQuality);
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.PhotoAlbum;
		const photoStore = usePhotoBrowsingStore();
		const returnedPhotoSorts = [SortType.MediaSize, SortType.Title];
		photoStore.supportedSorts = returnedPhotoSorts;
		const photoFields = mediaOverviewStore.getSortOptions(photoStore.supportedSorts).map((option) => option.field);
		expect(photoFields).toEqual(returnedPhotoSorts);
	});

	test('drives photo view and title sort route events through the shared dropdowns', async () => {
		// Arrange
		const photoStore = usePhotoBrowsingStore();
		photoStore.view = 'albums';
		photoStore.supportedSorts = [SortType.MediaSize, SortType.AddedAt, SortType.Title];
		const defaultSort = createDefaultMediaOverviewSort();
		photoStore.sort = defaultSort;
		const wrapper = await mountSuspended(MediaOverviewBar, {
			attachTo: document.body,
			props: { photoMode: true, libraryId: 4 },
			global: {
				plugins: [pinia, i18n()],
				stubs: { teleport: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();
		await wrapper.get('[data-cy="view-mode-assets-btn"]').trigger('click');
		await wrapper.get('[data-cy="media-overview-sort-btn"]').trigger('click');
		await flushPromises();
		const visibleSortOptions = wrapper.findAll('[data-cy^="sort-option-"]')
			.filter((option) => option.isVisible())
			.map((option) => option.attributes('data-cy'));
		expect(visibleSortOptions).toEqual([
			`sort-option-${SortType.MediaSize}-btn`,
			`sort-option-${SortType.AddedAt}-btn`,
			`sort-option-${SortType.Title}-btn`,
		]);
		await wrapper.get(`[data-cy="sort-option-${SortType.Title}-btn"]`).trigger('click');

		// Assert
		expect(wrapper.emitted('photo-view')).toEqual([['assets']]);
		expect(wrapper.emitted('photo-sort')).toEqual([[toggleMediaOverviewSort(defaultSort, SortType.Title)]]);
		expect(wrapper.find(`[data-cy="sort-option-${SortType.Year}-btn"]`).exists()).toBe(false);
		expect(wrapper.find(`[data-cy="sort-option-${SortType.UpdatedAt}-btn"]`).exists()).toBe(false);
		expect(wrapper.find(`[data-cy="sort-option-${SortType.Duration}-btn"]`).exists()).toBe(false);
		expect(wrapper.find(`[data-cy="sort-option-${SortType.MediaSize}-btn"]`).exists()).toBe(true);
	});
	test('clears a non-default photo sort through the shared sort menu', async () => {
		// Arrange
		const photoStore = usePhotoBrowsingStore();
		photoStore.view = 'assets';
		const defaultSort = createDefaultMediaOverviewSort();
		photoStore.sort = toggleMediaOverviewSort(defaultSort, SortType.Title);
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { photoMode: true, libraryId: 4 },
			global: {
				plugins: [pinia, i18n()],
				stubs: { teleport: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="media-overview-sort-btn"]').trigger('click');
		await wrapper.get('[data-cy="sort-clear-btn"]').trigger('click');

		// Assert
		expect(wrapper.emitted('photo-sort')).toEqual([[defaultSort]]);
	});

	test('renders shared photo selection actions and emits download and clear commands', async () => {
		// Arrange
		const photoStore = usePhotoBrowsingStore();
		photoStore.selectedSelectors = [{
			id: 10,
			plexServerId: 1,
			plexLibraryId: 4,
			kind: PhotoItemKind.Asset,
			type: PlexMediaType.PhotoImage,
			title: 'Selected image',
			stale: false,
		}];
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { photoMode: true, libraryId: 4, refreshDisabled: true },
			global: {
				plugins: [pinia, i18n()],
				stubs: { teleport: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="media-overview-bar-download-button"]').trigger('click');
		await wrapper.get('[data-cy="media-overview-bar-clear-selection-button"]').trigger('click');

		// Assert
		expect(wrapper.emitted('photo-download')).toHaveLength(1);
		expect(wrapper.emitted('photo-clear-selection')).toHaveLength(1);
		expect(wrapper.get('[data-cy="media-overview-refresh-library-btn"]').attributes('disabled')).toBeDefined();
	});

	test('changes photo presentation without changing the catalog view or another family preference', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.photoAlbumViewMode = ViewMode.Poster;
		settingsStore.displaySettings.movieViewMode = ViewMode.Poster;
		usePhotoBrowsingStore().view = 'folders';
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { photoMode: true, libraryId: 4 },
			global: { plugins: [pinia, i18n()], stubs: { teleport: true, QMediaTypeIcon: true } },
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();
		await wrapper.get('[data-cy="view-mode-table-btn"]').trigger('click');

		// Assert
		expect(settingsStore.displaySettings.photoAlbumViewMode).toBe(ViewMode.Table);
		expect(settingsStore.displaySettings.movieViewMode).toBe(ViewMode.Poster);
		expect(usePhotoBrowsingStore().view).toBe('folders');
		expect(wrapper.emitted('photo-view')).toBeUndefined();
	});

	test('offers one aggregate album/assets catalog choice alongside presentation choices', async () => {
		// Arrange
		const wrapper = await mountSuspended(MediaOverviewBar, {
			attachTo: document.body,
			props: { photoMode: true, libraryId: 0 },
			global: { plugins: [pinia, i18n()], stubs: { teleport: false, QMediaTypeIcon: true } },
		});
		mounted.push(wrapper);
		const menu = new DOMWrapper(document.body);

		// Act
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();
		await menu.get('[data-cy="view-mode-table-btn"]').trigger('click');
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();

		// Assert
		expect(menu.find('[data-cy="view-mode-albums-btn"]').exists()).toBe(true);
		expect(menu.find('[data-cy="view-mode-assets-btn"]').exists()).toBe(true);
		expect(menu.find('[data-cy="view-mode-folders-btn"]').exists()).toBe(false);
	});
	test.each([0, 2])('Should offer Albums only when the library has albums count %s', async (photoAlbumCount) => {
		// Arrange
		const library = generatePlexLibrary({
			seed: new Seed(1206),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, photoAlbumCount },
		});
		useLibraryStore().updateLibrary(library);
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { photoMode: true, libraryId: library.id },
			global: { plugins: [pinia, i18n()], stubs: { teleport: true, QMediaTypeIcon: true } },
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();

		// Assert
		expect(wrapper.find('[data-cy="view-mode-albums-btn"]').exists()).toBe(photoAlbumCount > 0);
		expect(wrapper.find('[data-cy="view-mode-folders-btn"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="view-mode-assets-btn"]').exists()).toBe(true);
	});

	test('keeps ordinary family view changes on the generic settings path', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
		useMediaOverviewStore().libraryId = 0;
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { libraryId: 0 },
			global: {
				plugins: [pinia, i18n()],
				stubs: { teleport: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="change-view-mode-btn"]').trigger('click');
		await flushPromises();
		await wrapper.get('[data-cy="view-mode-table-btn"]').trigger('click');

		// Assert
		expect(settingsStore.displaySettings.movieViewMode).toBe(ViewMode.Table);
		expect(wrapper.emitted('photo-view')).toBeUndefined();
	});
	test('keeps album Back inside the shared toolbar without hiding the view dropdown', async () => {
		// Arrange
		usePhotoBrowsingStore().mode = 'album';
		const wrapper = await mountSuspended(MediaOverviewBar, {
			props: { photoMode: true, libraryId: 4 },
			global: { plugins: [pinia, i18n()], stubs: { teleport: true, QMediaTypeIcon: true } },
		});
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="photo-album-back"]').trigger('click');

		// Assert
		expect(wrapper.emitted('action')).toEqual([['back']]);
		expect(wrapper.find('[data-cy="change-view-mode-btn"]').exists()).toBe(true);
	});
});
