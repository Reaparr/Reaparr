import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { reactive, nextTick } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { flushPromises } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generatePlexAccount, generatePlexLibrary, generatePlexServer, generateResultDTO, Seed } from '@mock';
import { PhotoAssetKind, PlexMediaType, ViewMode, type PhotoAssetPageDTO } from '@dto';
import { useAccountStore, useGlobalStore, useLibraryStore, usePhotoBrowsingStore, useServerStore, useSettingsStore } from '@store';
import PhotoDetails from '@/pages/photos/[id]/details/[albumId].vue';
import messages from '@/lang/en-US.json';
import { mockPhotoMetadata } from '../helpers/photo-metadata';

const routeState = vi.hoisted(() => ({
	params: { id: '4', albumId: '20' },
	query: {} as Record<string, string>,
	path: '/photos/4/details/20',
	fullPath: '/photos/4/details/20',
}));
mockNuxtImport('useRoute', () => () => routeState);

describe('Photos album route - bounded member browsing', () => {
	let { mock } = baseVars();
	let pinia = createPinia();
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(async () => {
		mock = getAxiosMock();
		mockPhotoMetadata();
		pinia = createPinia();
		setActivePinia(pinia);
		useSettingsStore().displaySettings.photoAlbumViewMode = ViewMode.Table;
		routeState.params = reactive({ id: '4', albumId: '20' });
		routeState.query = {};
		routeState.fullPath = '/photos/4/details/20';
		vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(960);
		vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(600);
		const library = generatePlexLibrary({
			seed: new Seed(625), plexServerId: 3, type: PlexMediaType.PhotoAlbum,
			partialData: { id: 4, isEnabled: true, syncedAt: '2026-10-20T00:00:00Z' },
		});
		useLibraryStore().updateLibrary(library);
		const server = generatePlexServer({ id: 3, config: { seed: 626 } });
		server.name = 'Camera server';
		useServerStore().servers = [server];
		useAccountStore().accounts = [generatePlexAccount({
			id: 1,
			plexServers: [server],
			plexLibraries: [library],
			partialData: { isEnabled: false },
			config: { seed: 627 },
		})];
		await subscribeSpyTo(usePhotoBrowsingStore().setup()).onComplete();
		useGlobalStore().pageReadyObservable.next(true);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		usePhotoBrowsingStore().$reset();
		mock.restore();
		vi.restoreAllMocks();
	});

	function page(title: string): PhotoAssetPageDTO {
		return {
			items: [{
				id: 31, plexServerId: 3, plexLibraryId: 4,
				plexServerName: 'Camera server', plexLibraryTitle: 'Travel photos',
				title, assetKind: PhotoAssetKind.Image, hasThumbnail: false,
				plexApiRatingKey: 31, plexApiMetaDataKey: 31, originalFilename: 'original.jpg',
				container: 'jpg', size: 2048, width: 1920, height: 1080, duration: null,
				relativeFolderPath: 'Summer', sourceRootTitle: 'Camera',
			}],
			page: 1, pageSize: 100, totalCount: 1, totalBytes: 2048,
		};
	}

	async function render() {
		const wrapper = await mountSuspended(PhotoDetails, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QPage: { template: '<main><slot /></main>' },
					DownloadConfirmation: true, PhotoAssetDialog: true, RefreshModeDialog: true,
					MediaOverviewRefresh: true, PhotoThumbnail: true, QFileSize: true,
				},
			},
		});
		mounted.push(wrapper);
		await flushPromises();
		return wrapper;
	}

	test('Should browse explicit album members without requesting generic embedded album contents', async () => {
		// Arrange
		mock.onGet(/\/api\/PlexPhoto\/libraries\/4\/albums\/20\/assets/).reply(200, generateResultDTO(page('Beach image')));
		// Act
		const wrapper = await render();
		// Assert
		expect(wrapper.text()).toContain('Beach image');
		expect(wrapper.text()).toContain('Camera server');
		expect(wrapper.get('[data-cy="photo-album-back"]').attributes('aria-label')).toBe('Back');
		expect(mock.history.get.some((request) => request.url?.includes('/api/PlexMedia'))).toBe(false);
	});

	test('Should reject invalid route ids before fetching a catalog', async () => {
		// Arrange
		routeState.params.albumId = 'not-an-id';
		// Act
		const wrapper = await render();
		// Assert
		expect(wrapper.get('[data-cy="photo-album-invalid-route"]').text()).toContain('Invalid photo library or album');
		expect(mock.history.get).toHaveLength(0);
	});

	test('Should show a scope rejection and recover through the visible member retry action', async () => {
		// Arrange
		mock.onGet(/\/api\/PlexPhoto\/libraries\/4\/albums\/20\/assets/).replyOnce(404, {
			isSuccess: false, value: null, statusCode: 404, errors: [{ message: 'Album not found', metadata: {}, reasons: [] }], successes: [],
		}).onGet(/\/api\/PlexPhoto\/libraries\/4\/albums\/20\/assets/).reply(200, generateResultDTO(page('Recovered image')));
		const wrapper = await render();
		expect(wrapper.text()).toContain('Photo catalog could not be loaded');
		// Act
		const retry = wrapper.findAll('button').find((button) => button.text().includes('Retry'));
		expect(retry).toBeDefined();
		await retry!.trigger('click');
		await flushPromises();
		// Assert
		expect(wrapper.text()).toContain('Recovered image');
		expect(wrapper.text()).not.toContain('Photo catalog could not be loaded');
	});

	test('Should discard an old album response after route navigation while retaining the new album members', async () => {
		// Arrange
		const oldResponse = Promise.withResolvers<[number, unknown]>();
		mock.onGet(/\/api\/PlexPhoto\/libraries\/4\/albums\/20\/assets/).reply(() => oldResponse.promise);
		mock.onGet(/\/api\/PlexPhoto\/libraries\/4\/albums\/30\/assets/).reply(200, generateResultDTO(page('Current album image')));
		const wrapper = await render();
		// Act
		routeState.params.albumId = '30';
		await nextTick();
		await flushPromises();
		oldResponse.resolve([200, generateResultDTO(page('Obsolete album image'))]);
		await flushPromises();
		// Assert
		expect(wrapper.text()).toContain('Current album image');
		expect(wrapper.text()).not.toContain('Obsolete album image');
	});
});
