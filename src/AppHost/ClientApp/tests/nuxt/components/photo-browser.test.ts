import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { defineComponent, h, nextTick, reactive, type Component } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { flushPromises } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useGlobalStore, useLibraryStore, usePhotoBrowsingStore, useSettingsStore } from '@store';
import { mockPhotoMetadata } from '../helpers/photo-metadata';
import { LibrarySyncJobStatus, PlexMediaType, SortDirection, SortType, ViewMode } from '@dto';
import { generateLibrarySyncJobQueue, generatePlexLibrary, generateResultDTO, Seed } from '@mock';
import PhotoBrowser from '@/components/Photos/PhotoBrowser.vue';
import messages from '@/lang/en-US.json';
import { createDefaultMediaOverviewSort, toggleMediaOverviewSort } from '@composables';

const titleDescending = toggleMediaOverviewSort(createDefaultMediaOverviewSort(), SortType.Title);

const navigation = vi.hoisted(() => ({
	route: { path: '/photos', fullPath: '/photos', query: {} as Record<string, string>, params: {} },
	push: vi.fn(), replace: vi.fn(), back: vi.fn(),
}));
mockNuxtImport('useRoute', () => () => navigation.route);
mockNuxtImport('useRouter', () => () => ({ push: navigation.push, replace: navigation.replace, back: navigation.back }));

describe('PhotoBrowser - unified route context', () => {
	let { mock } = baseVars();
	let pinia = createPinia();
	const mounted: Array<{ unmount: () => void }> = [];

	beforeAll(() => baseSetup());
	beforeEach(async () => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		mockPhotoMetadata();
		navigation.route = reactive({ path: '/photos', fullPath: '/photos', query: {} as Record<string, string>, params: {} });
		navigation.push.mockReset().mockResolvedValue(undefined);
		navigation.replace.mockReset().mockResolvedValue(undefined);
		navigation.back.mockReset();
		await subscribeSpyTo(usePhotoBrowsingStore().setup()).onComplete();
		useGlobalStore().pageReadyObservable.next(false);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		usePhotoBrowsingStore().$reset();
		mock.restore();
		vi.restoreAllMocks();
	});
	async function render(libraryId = 0, albumId?: number, overviewStub: boolean | Component = true, barStub: boolean | Component = true) {
		useSettingsStore().displaySettings.photoAlbumViewMode = ViewMode.Table;
		const wrapper = await mountSuspended(PhotoBrowser, {
			props: { libraryId, albumId },
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					MediaOverviewBar: barStub,
					PhotoOverviewList: overviewStub,
					DownloadConfirmation: true,
					RefreshModeDialog: true,
					PhotoAssetDialog: true,
					MediaOverviewRefresh: false,
					QLoadingOverlay: true,
				},
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	test('Should activate and load the requested photo view when application setup becomes ready', async () => {
		// Arrange
		navigation.route.fullPath = '/photos?view=assets';
		navigation.route.query = { view: 'assets' };
		mock.onGet('/api/PlexPhoto/assets').reply(200, generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 0, totalBytes: 0,
		}));
		await render();
		const photoStore = usePhotoBrowsingStore();
		expect(photoStore.active).toBe(false);
		expect(mock.history.get.some((request) => request.url === '/api/PlexPhoto/assets')).toBe(false);

		// Act
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();

		// Assert
		expect(photoStore.active).toBe(true);
		expect(photoStore.sort).toEqual(createDefaultMediaOverviewSort());
		expect(photoStore.assets.loaded).toBe(true);
		expect(photoStore.assets.error).toBe(false);
		expect(mock.history.get.filter((request) => request.url === '/api/PlexPhoto/assets')).toHaveLength(1);
	});
	test.each([
		{ wireSort: `${SortType.Title}:asc`, expectedSort: createDefaultMediaOverviewSort() },
		{ wireSort: `${SortType.Title}:desc`, expectedSort: titleDescending },
		{ wireSort: 'title:asc', expectedSort: createDefaultMediaOverviewSort() },
		{ wireSort: 'sortIndex:asc', expectedSort: createDefaultMediaOverviewSort() },
	])('Should parse generated and legacy photo sort routes into the typed photo context ($wireSort)', async ({ wireSort, expectedSort }) => {
		// Arrange
		navigation.route.fullPath = `/photos?view=assets&sort=${wireSort}`;
		navigation.route.query = { view: 'assets', sort: wireSort };
		mock.onGet('/api/PlexPhoto/assets').reply(200, generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 0, totalBytes: 0,
		}));
		await render();

		// Act
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();

		// Assert
		expect(usePhotoBrowsingStore().sort).toEqual(expectedSort);
	});
	test('uses album asset capabilities when a stale folder view is present in the route', async () => {
		// Arrange
		navigation.route.path = '/photos/4/details/9';
		navigation.route.fullPath = `/photos/4/details/9?view=folders&sort=${SortType.MediaSize}:asc`;
		navigation.route.query = { view: 'folders', sort: `${SortType.MediaSize}:asc` };
		mock.onGet('/api/PlexPhoto/libraries/4/albums/9/assets').reply(200, generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 0, totalBytes: 0,
		}));
		await render(4, 9);

		// Act
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();

		// Assert
		expect(usePhotoBrowsingStore().active).toBe(true);
		expect(usePhotoBrowsingStore().view).toBe('assets');
		expect(usePhotoBrowsingStore().sort).toEqual({ field: SortType.MediaSize, sort: SortDirection.Asc });
	});

	test.each(['', `${SortType.Title}:ASC`, ` ${SortType.Title}:asc`, `${SortType.Title}:asc `, `${SortType.Title}:no-sort`])('Should reject malformed photo sort route value %s', async (sort) => {
		// Arrange
		navigation.route.fullPath = `/photos?sort=${encodeURIComponent(sort)}`;
		navigation.route.query = { sort };

		// Act
		const wrapper = await render();

		// Assert
		expect(wrapper.find('[data-cy="photo-invalid-route"]').exists()).toBe(true);
		expect(usePhotoBrowsingStore().active).toBe(false);
	});
	test('Should reject a known photo sort omitted from the scoped metadata response', async () => {
		// Arrange
		navigation.route.fullPath = `/photos/4?view=assets&sort=${SortType.Year}:asc`;
		navigation.route.query = { view: 'assets', sort: `${SortType.Year}:asc` };
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(1404),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, isEnabled: true },
		}));
		const wrapper = await render(4);

		// Act
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();

		// Assert
		expect(wrapper.find('[data-cy="photo-invalid-route"]').exists()).toBe(true);
		expect(usePhotoBrowsingStore().active).toBe(true);
		expect(mock.history.get.some((request) => request.url === '/api/PlexPhoto/assets')).toBe(false);
	});
	test('Should serialize the typed toolbar sort when updating the photo route', async () => {
		// Arrange
		const sortBar = defineComponent({
			name: 'MediaOverviewBar',
			emits: ['photo-sort'],
			setup(_, { emit }) {
				return () => h('button', { 'data-cy': 'test-sort', onClick: () => emit('photo-sort', titleDescending) });
			},
		});
		const wrapper = await render(0, undefined, true, sortBar);

		// Act
		await wrapper.get('[data-cy="test-sort"]').trigger('click');

		// Assert
		expect(navigation.push).toHaveBeenCalledWith({ query: { sort: `${SortType.Title}:desc` } });
	});
	test('resets an unsupported asset sort when switching to folder view', async () => {
		// Arrange
		navigation.route.fullPath = `/photos/4?view=assets&sort=${SortType.MediaSize}:asc`;
		navigation.route.query = { view: 'assets', sort: `${SortType.MediaSize}:asc` };
		const viewBar = defineComponent({
			name: 'MediaOverviewBar',
			emits: ['photo-view'],
			setup(_, { emit }) {
				return () => h('button', { 'data-cy': 'test-folder-view', onClick: () => emit('photo-view', 'folders') });
			},
		});
		const wrapper = await render(4, undefined, true, viewBar);
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="test-folder-view"]').trigger('click');

		// Assert
		expect(navigation.push).toHaveBeenCalledWith({ query: { view: 'folders' } });
	});
	test('resets an unsupported folder sort when clearing search', async () => {
		// Arrange
		navigation.route.fullPath = `/photos/4?view=folders&q=sun&sort=${SortType.MediaSize}:asc`;
		navigation.route.query = { view: 'folders', q: 'sun', sort: `${SortType.MediaSize}:asc` };
		const searchBar = defineComponent({
			name: 'MediaOverviewBar',
			emits: ['photo-search'],
			setup(_, { emit }) {
				return () => h('button', { 'data-cy': 'test-clear-search', onClick: () => emit('photo-search', '') });
			},
		});
		const wrapper = await render(4, undefined, true, searchBar);
		mounted.push(wrapper);

		// Act
		await wrapper.get('[data-cy="test-clear-search"]').trigger('click');

		// Assert
		expect(navigation.replace).toHaveBeenCalledWith({ query: { view: 'folders' } });
	});

	test.each([undefined, 'albums'])('Should redirect an albumless library to folders for view %s', async (view) => {
		// Arrange
		navigation.route.path = '/photos/4';
		navigation.route.fullPath = `/photos/4?sort=${SortType.Title}:desc${view ? `&view=${view}` : ''}`;
		navigation.route.query = { sort: `${SortType.Title}:desc`, ...(view ? { view } : {}) };
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(1204),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, photoAlbumCount: 0 },
		}));
		await render(4);

		// Act
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();

		// Assert
		expect(navigation.replace).toHaveBeenCalledWith({ query: { sort: `${SortType.Title}:desc`, view: 'folders' } });
		expect(mock.history.get.some((request) => request.url === '/api/PlexPhoto/albums')).toBe(false);
	});

	test('Should retain albums until a refresh removes the last album', async () => {
		// Arrange
		navigation.route.path = '/photos/4';
		navigation.route.fullPath = '/photos/4';
		const library = generatePlexLibrary({
			seed: new Seed(1205),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, photoAlbumCount: 1, isEnabled: true },
		});
		useLibraryStore().updateLibrary(library);
		mock.onGet('/api/PlexPhoto/albums').reply(200, generateResultDTO({
			items: [], page: 1, pageSize: 100, totalCount: 1, totalAssetBytes: 0,
		}));
		await render(4);
		useGlobalStore().pageReadyObservable.next(true);
		await nextTick();
		await flushPromises();
		expect(usePhotoBrowsingStore().view).toBe('albums');
		expect(usePhotoBrowsingStore().albums.loaded).toBe(true);
		expect(navigation.replace).not.toHaveBeenCalledWith({ query: { view: 'folders' } });

		// Act
		useLibraryStore().updateLibrary({ ...library, photoAlbumCount: 0 });
		await nextTick();
		await flushPromises();

		// Assert
		expect(navigation.replace).toHaveBeenCalledWith({ query: { view: 'folders' } });
	});

	test.each([undefined, 9])('Should show album breadcrumbs and retain the library return link for album %s', async (albumId) => {
		// Arrange
		useLibraryStore().updateLibrary(generatePlexLibrary({
			seed: new Seed(1206),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, title: 'Travel photos', photoAlbumCount: 1 },
		}));
		const wrapper = await render(4, albumId);

		// Act
		if (albumId) usePhotoBrowsingStore().mode = 'album';
		await nextTick();
		const breadcrumbs = wrapper.get('[data-cy="photo-album-breadcrumbs"]');

		// Assert
		expect(breadcrumbs.text()).toContain('Photos');
		expect(breadcrumbs.text()).toContain('Travel photos');
		expect(breadcrumbs.get('a[href="/photos"]').text()).toBe('Photos');
		if (albumId) expect(breadcrumbs.get('a[href="/photos/4"]').text()).toBe('Travel photos');
		expect(wrapper.find('[data-cy="photo-folder-breadcrumbs"]').exists()).toBe(false);
	});

	test('Should accept a legacy page query while canonicalizing it away without rejecting its folder', async () => {
		// Arrange
		navigation.route.path = '/photos/4';
		navigation.route.fullPath = '/photos/4?view=folders&folderId=9&folderPage=2&assetPage=3';
		navigation.route.query = { view: 'folders', folderId: '9', folderPage: '2', assetPage: '3' };

		// Act
		const wrapper = await render(4);

		// Assert
		expect(wrapper.find('[data-cy="photo-invalid-route"]').exists()).toBe(false);
		expect(navigation.replace).toHaveBeenCalledWith({ query: { view: 'folders', folderId: '9' } });
	});

	test('Should link folder ancestors without linking the current folder and preserve the selected sort', async () => {
		// Arrange
		navigation.route.path = '/photos/4';
		navigation.route.fullPath = `/photos/4?view=folders&folderId=3&sort=${SortType.Title}:desc`;
		navigation.route.query = { view: 'folders', folderId: '3', sort: `${SortType.Title}:desc` };
		const wrapper = await render(4);
		const photoStore = usePhotoBrowsingStore();
		photoStore.mode = 'library';
		photoStore.view = 'folders';
		photoStore.breadcrumbs = [
			{ id: 1, title: 'Pets', relativePath: '' },
			{ id: 2, title: 'Summer', relativePath: 'Summer' },
			{ id: 3, title: 'Day one', relativePath: 'Summer/Day one' },
		];

		// Act
		await nextTick();
		const breadcrumbs = wrapper.get('[data-cy="photo-folder-breadcrumbs"]');
		const links = breadcrumbs.findAll('a[href]');
		const destinations = links.map((link) => new URL(link.attributes('href')!, 'http://localhost'));

		// Assert
		expect(links.map((link) => link.text())).toEqual(['Photo roots', 'Pets', 'Summer']);
		expect(breadcrumbs.text()).toContain('Day one');
		expect(destinations.map((url) => url.pathname)).toEqual(['/photos/4', '/photos/4', '/photos/4']);
		expect(destinations.map((url) => url.searchParams.get('folderId'))).toEqual([null, '1', '2']);
		expect(destinations.map((url) => url.searchParams.get('sort'))).toEqual([`${SortType.Title}:desc`, `${SortType.Title}:desc`, `${SortType.Title}:desc`]);
	});

	test.each([LibrarySyncJobStatus.Completed, LibrarySyncJobStatus.Failed, LibrarySyncJobStatus.Cancelled])(
		'Should replace cached media with the shared processing screen and restore the overview after %s',
		async (status) => {
			// Arrange
			const libraryStore = useLibraryStore();
			libraryStore.updateLibrary(generatePlexLibrary({
				seed: new Seed(1207),
				plexServerId: 1,
				type: PlexMediaType.PhotoAlbum,
				partialData: { id: 4, title: 'Travel photos', photoAlbumCount: 1, syncedAt: '2026-01-01T00:00:00Z', isEnabled: true },
			}));
			const wrapper = await render(4);
			expect(wrapper.findComponent({ name: 'PhotoOverviewList' }).exists()).toBe(true);
			const queue = generateLibrarySyncJobQueue({
				plexLibraryId: 4, plexServerId: 1, status: LibrarySyncJobStatus.Processing, completedAt: null,
			});

			// Act
			libraryStore.updateSyncQueue(queue);
			await nextTick();

			// Assert
			expect(wrapper.get('[data-cy="refresh-library-container"]').text()).toContain('Travel photos');
			expect(wrapper.findComponent({ name: 'PhotoOverviewList' }).exists()).toBe(false);
			expect(wrapper.findComponent({ name: 'MediaOverviewBar' }).exists()).toBe(false);
			expect(wrapper.find('[data-cy="photo-album-breadcrumbs"]').exists()).toBe(false);
			expect(wrapper.find('[data-cy="media-overview-library-sync-processing-banner"]').exists()).toBe(false);

			// Act
			libraryStore.updateSyncQueue({ ...queue, status, completedAt: '2026-01-02T00:00:00Z' });
			await nextTick();

			// Assert
			expect(wrapper.find('[data-cy="refresh-library-container"]').exists()).toBe(false);
			expect(wrapper.findComponent({ name: 'PhotoOverviewList' }).exists()).toBe(true);
			expect(wrapper.findComponent({ name: 'MediaOverviewBar' }).exists()).toBe(true);
		},
	);

	test.each([null, '2026-01-01T00:00:00Z'])('Should match queued movie and TV presentation for syncedAt %s', async (syncedAt) => {
		// Arrange
		const libraryStore = useLibraryStore();
		libraryStore.updateLibrary(generatePlexLibrary({
			seed: new Seed(1208),
			plexServerId: 1,
			type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, title: 'Travel photos', photoAlbumCount: 1, syncedAt, isEnabled: true },
		}));
		const wrapper = await render(4);

		// Act
		libraryStore.updateSyncQueue(generateLibrarySyncJobQueue({
			plexLibraryId: 4, plexServerId: 1, status: LibrarySyncJobStatus.Queued, completedAt: null,
		}));
		await nextTick();

		// Assert
		const queuedSurface = syncedAt ? 'banner' : 'panel';
		expect(wrapper.get(`[data-cy="media-overview-library-sync-queued-${queuedSurface}"]`).text()).toContain('Refresh queued');
		expect(wrapper.findComponent({ name: 'PhotoOverviewList' }).exists()).toBe(syncedAt !== null);
		expect(wrapper.findComponent({ name: 'MediaOverviewBar' }).exists()).toBe(syncedAt !== null);
	});

	test('Should restore focus through the rendered overview target after closing the viewer', async () => {
		// Arrange
		const focusItem = vi.fn(async () => true);
		const focusFallback = vi.fn();
		const overviewStub = defineComponent({
			name: 'PhotoOverviewList',
			setup(_, { expose }) {
				expose({ focusItem, focusFallback });
				return () => h('section', { 'data-cy': 'photo-overview-list' });
			},
		});
		await render(4, undefined, overviewStub);
		const photoStore = usePhotoBrowsingStore();

		// Act
		photoStore.focusIndex = 7;
		await nextTick();
		await flushPromises();

		// Assert
		expect(focusItem).toHaveBeenCalledWith(7);
		expect(focusFallback).not.toHaveBeenCalled();
		expect(photoStore.focusIndex).toBeNull();
	});
});
