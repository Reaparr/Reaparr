import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { reactive, nextTick } from 'vue';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { of, Subject } from 'rxjs';
import { baseSetup } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { PlexMediaType, type PlexMediaDTO } from '@dto';
import { useMediaStore } from '@store';
import OtherVideoDetails from '@/pages/other-videos/[libraryId]/details/[videoId].vue';
import { createI18n } from 'vue-i18n';
import messages from '@/lang/en-US.json';

const routeState = vi.hoisted(() => ({ params: { libraryId: '5', videoId: '20' } }));
mockNuxtImport('useRoute', () => () => routeState);

describe('Other Videos root detail - request lifetime', () => {
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
		routeState.params = reactive({ libraryId: '5', videoId: '20' });
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
	});

	function video(id = 20): PlexMediaDTO {
		return generatePlexMedia({
			config: { seed: 625 },
			partialData: {
				id, type: PlexMediaType.OtherVideos, plexLibraryId: 5, plexServerId: 3,
				title: `Video ${id}`, summary: 'Independent video summary', children: [], mediaData: [], hasThumb: false,
				originallyAvailableAt: '1960-12-25T00:00:00Z', addedAt: '2026-01-02T00:00:00Z', updatedAt: '0001-01-01T00:00:00',
			},
		});
	}
	async function render() {
		const wrapper = await mountSuspended(OtherVideoDetails, {
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

	test('Should discard the previous video response after direct navigation', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const second = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(second);
		const wrapper = await render();
		expect(wrapper.get('[data-cy="other-video-details-loading"]').text()).toBe('Loading video…');

		// Act
		routeState.params.videoId = '30';
		await nextTick();
		first.next(video());
		await nextTick();

		// Assert
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.find('[data-cy="other-video-details-loading"]').exists()).toBe(true);

		// Act
		second.next(video(30));
		await nextTick();

		// Assert
		expect(wrapper.get('h1').text()).toBe('Video 30');
		expect(wrapper.text()).toContain('Independent video summary');
		expect(wrapper.text()).toContain('Originally available');
		expect(wrapper.findAll('dt').map((label) => label.text())).not.toContain('Updated');
		expect(wrapper.find('[data-cy="other-video-files-empty"]').exists()).toBe(true);
	});

	test('Should reject Movie data rather than render a coerced Other Video', async () => {
		// Arrange
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValue(of({ ...video(), type: PlexMediaType.Movie }));

		// Act
		const wrapper = await render();

		// Assert
		expect(wrapper.get('[data-cy="other-video-details-error"]').text()).toBe('Unable to load this video.');
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.find('[data-cy="other-video-root-checkbox"]').exists()).toBe(false);
	});

	test('Should reject old ownership when only the library route changes', async () => {
		// Arrange
		const second = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(of(video())).mockReturnValueOnce(second);
		const wrapper = await render();
		expect(wrapper.get('h1').text()).toBe('Video 20');

		// Act
		routeState.params.libraryId = '6';
		await nextTick();
		second.next(video());
		await nextTick();

		// Assert
		expect(wrapper.find('h1').exists()).toBe(false);
		expect(wrapper.get('[data-cy="other-video-details-error"]').text()).toBe('Unable to load this video.');
	});

	test('Should recover through the retry button after a failed root request', async () => {
		// Arrange
		const first = new Subject<PlexMediaDTO>();
		const retry = new Subject<PlexMediaDTO>();
		vi.spyOn(useMediaStore(), 'getMediaDataDetailById').mockReturnValueOnce(first).mockReturnValueOnce(retry);
		const wrapper = await render();

		// Act
		first.error(new Error('Unavailable'));
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="other-video-details-loading"]').exists()).toBe(false);
		expect(wrapper.get('[data-cy="other-video-details-error"]').text()).toBe('Unable to load this video.');

		// Act
		await wrapper.get('button').trigger('click');
		retry.next(video());
		await nextTick();

		// Assert
		expect(wrapper.find('[data-cy="other-video-details-error"]').exists()).toBe(false);
		expect(wrapper.get('h1').text()).toBe('Video 20');
	});

	test('Should reject an invalid library before any request and offer a back action', async () => {
		// Arrange
		routeState.params.libraryId = '0';
		const request = vi.spyOn(useMediaStore(), 'getMediaDataDetailById');

		// Act
		const wrapper = await render();

		// Assert
		expect(request).not.toHaveBeenCalled();
		expect(wrapper.get('[data-cy="other-video-details-error"]').text()).toBe('Invalid video library or video.');
		expect(wrapper.find('[data-cy="other-video-details-loading"]').exists()).toBe(false);
		expect(wrapper.get('button').text()).toContain('Back');
	});
});
