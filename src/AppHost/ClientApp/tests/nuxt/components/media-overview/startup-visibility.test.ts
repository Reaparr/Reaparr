import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generatePlexLibrary, generatePlexMediaSlims, generatePlexMediaStatisticsDTO, generateResultDTO, generateSettingsModel, generateSupportedSorts, Seed } from '@mock';
import { PlexMediaType, ViewMode, type SettingsModelDTO } from '@dto';
import { useAccountStore, useGlobalStore, useLibraryStore, useMediaOverviewStore, useSettingsStore } from '@store';
import type { ResultDTO } from '@interfaces';
import MediaOverview from '@/components/MediaOverview/MediaOverview.vue';
import messages from '@/lang/en-US.json';

const slotShell = defineComponent({ template: '<div><slot /></div>' });
const mediaTable = defineComponent({ template: '<div data-cy="ready-media-table" />' });
const stubs = {
	MediaOverviewRefresh: true, MediaOverviewBar: true, QBanner: slotShell, QRow: slotShell,
	QCol: slotShell, QAlert: slotShell, BaseButton: true, MediaTable: mediaTable, PosterTable: true,
	AlphabetNavigation: true, MediaSelectionDialog: true, MediaComparisonDetailsDialog: true,
	MediaOptionsDialog: true, QLoadingOverlay: true, DownloadConfirmation: true, RefreshModeDialog: true,
};

function savedVisibilitySettings() {
	const settings = generateSettingsModel({ config: { seed: 45046 } });
	settings.generalSettings.hideMediaFromOwnedServers = true;
	settings.generalSettings.hideMediaFromOfflineServers = true;
	settings.displaySettings.allOverviewViewMode = PlexMediaType.Movie;
	settings.displaySettings.movieViewMode = ViewMode.Table;
	return settings;
}

describe('MediaOverview startup visibility', () => {
	let { mock } = baseVars();
	let wrapper: VueWrapper | undefined;
	let pinia: Pinia;
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => wrapper?.unmount());

	function mockVisibleMedia() {
		const movies = generatePlexMediaSlims({
			config: { seed: 45046, movieCount: 1 },
			partialData: { id: 101, title: 'Visible movie', type: PlexMediaType.Movie, plexServerId: 1, plexLibraryId: 1 },
		});
		mock.onGet('/api/PlexMedia').reply(200, generateResultDTO(generatePlexMediaStatisticsDTO(movies)));
		mock.onGet('/api/PlexLibrary/0/metadata').reply((request) => [200, generateResultDTO({
			mediaCount: 1, roleCount: 0, countryCount: 0, genreCount: 1, qualityCount: 0,
			roles: [], countries: [], genres: [{ id: 33, name: 'Visible genre' }], qualities: [],
			supportedSorts: generateSupportedSorts(request.params.mediaType as PlexMediaType),
		})]);
		mock.onGet('/api/PlexLibrary/0/metadata-filter').reply(200, generateResultDTO({
			roles: [], countries: [], genres: [33], qualities: [],
		}));
	}

	test('Should initialize media and metadata only after saved visibility settings and page setup are ready', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const mediaStore = useMediaOverviewStore();
		const ready = useGlobalStore().pageReadyObservable;
		ready.next(false);
		let finishSettings!: (response: [number, ResultDTO<SettingsModelDTO>]) => void;
		const settingsResponse = new Promise<[number, ResultDTO<SettingsModelDTO>]>((resolve) => {
			finishSettings = resolve;
		});
		mock.onGet('/api/Settings').reply(() => settingsResponse);
		mockVisibleMedia();
		const settingsSetup = subscribeSpyTo(settingsStore.setup());
		wrapper = await mountSuspended(MediaOverview, { props: { libraryId: 0 }, global: {
			plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })], stubs,
		} });
		await flushPromises();
		expect(mock.history.get.map((request) => request.url)).toEqual(['/api/Settings']);

		// Act: loaded settings alone do not bypass the app's remaining setup work.
		finishSettings([200, generateResultDTO(savedVisibilitySettings())]);
		await settingsSetup.onComplete();
		expect(mock.history.get.map((request) => request.url)).toEqual(['/api/Settings']);
		ready.next(true);
		await flushPromises();

		// Assert
		expect(mock.history.get.filter((request) => request.url !== '/api/Settings').map((request) => ({
			url: request.url, mediaType: request.params.mediaType,
			owned: request.params.filterOwnedMedia, offline: request.params.filterOfflineMedia,
		})).sort((left, right) => left.url!.localeCompare(right.url!))).toEqual([
			{ url: '/api/PlexLibrary/0/metadata', mediaType: PlexMediaType.Movie, owned: true, offline: true },
			{ url: '/api/PlexLibrary/0/metadata-filter', mediaType: PlexMediaType.Movie, owned: true, offline: true },
			{ url: '/api/PlexMedia', mediaType: PlexMediaType.Movie, owned: true, offline: true },
		]);
		expect(mediaStore.getMediaItems.map((item) => ({ id: item.id, title: item.title }))).toEqual([{ id: 101, title: 'Visible movie' }]);
		expect(mediaStore.metadataList.genres).toEqual([{ id: 33, name: 'Visible genre' }]);
		expect(mediaStore.availableGenreIds).toEqual([33]);
		expect(wrapper.find('[data-cy="ready-media-table"]').exists()).toBe(true);
	});
	test('renders the inaccessible-library banner between the overview bar and media content', async () => {
		// Arrange
		const library = generatePlexLibrary({ seed: new Seed(45047), plexServerId: 1, type: PlexMediaType.Movie, partialData: { id: 45047 } });
		useLibraryStore().updateLibrary(library);
		useGlobalStore().pageReadyObservable.next(false);
		const accountStore = useAccountStore();
		accountStore.accounts = [];
		const mediaStore = useMediaOverviewStore();
		mediaStore.libraryId = library.id;
		mediaStore.itemsLength = 1;
		mediaStore.loading = false;
		const layout = defineComponent({ template: '<div><header><slot name="header" /></header><main><slot /></main></div>' });
		const wrapperStubs = { ...stubs, MediaOverviewLayout: layout, MediaOverviewBar: { template: '<div data-cy="overview-bar" />' } };

		// Act
		wrapper = await mountSuspended(MediaOverview, { props: { libraryId: library.id }, global: {
			plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })], stubs: wrapperStubs,
		} });
		await flushPromises();

		// Assert
		const bar = wrapper.get('[data-cy="overview-bar"]');
		const banner = wrapper.get('[data-cy="media-overview-inaccessible-library-banner"]');
		const content = wrapper.get('main');
		expect(banner.element.closest('.media-overview-bar')).toBeNull();
		expect(bar.element.compareDocumentPosition(banner.element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
		expect(banner.element.compareDocumentPosition(content.element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
	});
});
