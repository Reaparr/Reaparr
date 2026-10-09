import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { reactive, nextTick } from 'vue';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { PlexMediaType, type PlexMediaDTO } from '@dto';
import { useMediaStore } from '@store';
import PhotoDetails from '@/pages/photos/[id]/details/[albumId].vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

const routeState = vi.hoisted(() => ({ params: { id: '4', albumId: '20' } }));
mockNuxtImport('useRoute', () => () => routeState);

describe('Photos album detail - request lifetime', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		routeState.params = reactive({ id: '4', albumId: '20' });
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
	});

	function album(id: number) {
		return generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id, type: PlexMediaType.PhotoAlbum, plexLibraryId: 4, plexServerId: 3,
				title: `Photo album ${id}`, summary: 'A real album summary', children: [], childCount: 0,
				addedAt: '2026-01-02T00:00:00Z', updatedAt: null, hasThumb: false,
			},
		});
	}

	async function render() {
		const wrapper = await mountSuspended(PhotoDetails, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QPage: { template: '<main><slot /></main>' },
					MediaOverviewBar: true, MediaPosterImage: true, DownloadConfirmation: true,
					QFileSize: true, QDateTime: true,
				},
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	test('Should discard an old response after direct navigation to another album', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const second = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(second);
		const wrapper = await render();
		expect(wrapper.get('[data-cy="photo-details-loading"]').text()).toBe('Loading photo album…');

		// Act
		routeState.params.albumId = '30';
		await nextTick();
		first.next(album(20));
		await nextTick();

		// Assert
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.find('[data-cy="photo-details-loading"]').exists()).toBe(true);

		// Act
		second.next(album(30));
		await nextTick();

		// Assert
		expect(wrapper.get('h1').text()).toBe('Photo album 30');
		expect(wrapper.text()).toContain('A real album summary');
		expect(wrapper.get('[data-cy="photo-assets-empty"]').text()).toBe('No images or clips found in this album.');
		expect(wrapper.text()).not.toContain('Photo album 20');
	});

	test('Should stop loading on a failed request and recover through the visible retry control', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const retry = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(retry);
		const wrapper = await render();

		// Act
		first.error(new Error('Unavailable'));
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="photo-details-loading"]').exists()).toBe(false);
		expect(wrapper.get('[data-cy="photo-details-error"]').text()).toBe('Unable to load this photo album.');

		// Act
		await wrapper.get('button').trigger('click');
		retry.next(album(20));
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="photo-details-error"]').exists()).toBe(false);
		expect(wrapper.get('h1').text()).toBe('Photo album 20');
	});

	test('Should reject invalid route ids before fetching and display an actionable error', async () => {
		// Arrange
		routeState.params.albumId = 'not-an-id';
		const request = vi.spyOn(useMediaStore(), 'getMediaDataDetailById');

		// Act
		const wrapper = await render();

		// Assert
		expect(wrapper.get('[data-cy="photo-details-error"]').text()).toBe('Invalid photo library or album.');
		expect(wrapper.find('[data-cy="photo-details-loading"]').exists()).toBe(false);
		expect(request).not.toHaveBeenCalled();
		expect(wrapper.get('button').text()).toContain('Back');
		expect(mock.history.get).toHaveLength(0);
	});
});
