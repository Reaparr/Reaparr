import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h } from 'vue';
import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { Subject } from 'rxjs';
import { createI18n } from 'vue-i18n';
import { baseSetup } from '@services-test-base';
import { useDialogStore, useLibraryStore, useMediaStore, useServerStore } from '@store';
import {
	PlexMediaComparisonState, PlexMediaType, VideoQuality,
	type PlexMediaComparisonDetailsDTO, type PlexMediaComparisonDetailsRowDTO, type PlexMediaSlimDTO,
} from '@dto';
import MediaComparisonDetailsDialog from '@/components/Dialogs/MediaComparisonDetailsDialog.vue';
import MediaVideoQuality from '@/components/MediaOverview/MediaVideoQuality.vue';
import messages from '@/lang/en-US.json';

const CardDialog = defineComponent({
	name: 'QCardDialog',
	emits: ['opened', 'closed'],
	setup(_, { slots, emit }) {
		return () => h('section', [
			slots.title?.(), slots.default?.(), slots.actions?.(),
			h('button', { 'data-cy': 'dialog-close-button', onClick: () => emit('closed') }, 'Close'),
		]);
	},
});

function artist(type = PlexMediaType.MusicArtist, comparisonId = 5): PlexMediaSlimDTO {
	return {
		id: 999, title: 'Selected artist', type, comparisonId,
		plexServerId: 1, plexLibraryId: 2, plexApiRatingKey: 999, plexApiMetaDataKey: 999,
		childCount: 0, grandChildCount: 0, duration: 0, sortIndex: 0, year: 0,
		searchTitle: 'selected artist', hasThumb: false, mediaSize: 0, qualities: [],
		addedAt: '2024-01-01T00:00:00Z',
	};
}

function row(overrides: Partial<PlexMediaComparisonDetailsRowDTO>): PlexMediaComparisonDetailsRowDTO {
	return {
		plexMediaId: 7, title: '', type: PlexMediaType.MusicAlbum,
		plexServerId: 3, plexLibraryId: 4, state: PlexMediaComparisonState.Missing,
		ownedQuality: VideoQuality.None, remoteQuality: VideoQuality.None, children: [],
		...overrides,
	};
}

function musicRows(): PlexMediaComparisonDetailsRowDTO[] {
	return [
		row({ title: 'Partial album', state: PlexMediaComparisonState.Partial, children: [
			row({ title: 'Owned track', type: PlexMediaType.MusicTrack, state: PlexMediaComparisonState.Owned }),
			row({ title: 'Missing track', type: PlexMediaType.MusicTrack, plexMediaId: 8 }),
		] }),
		row({ title: 'Missing album elsewhere', plexServerId: 5, plexLibraryId: 6, children: [
			row({ title: 'Same ID elsewhere', type: PlexMediaType.MusicTrack, plexMediaId: 8, plexServerId: 5, plexLibraryId: 6 }),
		] }),
		row({ title: 'Owned album', state: PlexMediaComparisonState.Owned, children: [
			row({ title: 'Another owned track', type: PlexMediaType.MusicTrack, state: PlexMediaComparisonState.Owned }),
		] }),
		row({ title: 'Empty missing album' }),
	];
}

function details(rows = musicRows(), type = PlexMediaType.MusicArtist): PlexMediaComparisonDetailsDTO {
	return { plexMediaId: 999, type, state: PlexMediaComparisonState.Partial, rows };
}

function keyFor(wrapper: VueWrapper, title: string): string {
	const titleNode = wrapper.findAll('[data-cy^="media-comparison-details-title-"]').find((node) => node.text() === title)!;
	return titleNode.attributes('data-cy')!.replace('media-comparison-details-title-', '');
}

async function expandAll(wrapper: VueWrapper) {
	let collapsed = wrapper.find('tr[aria-expanded="false"]');
	while (collapsed.exists()) {
		await collapsed.get('button[data-pc-group-section="rowactionbutton"]').trigger('click');
		collapsed = wrapper.find('tr[aria-expanded="false"]');
	}
}

function submit(wrapper: VueWrapper) {
	return wrapper.get('[data-cy="media-comparison-details-dialog-download-button"]');
}

function checkbox(wrapper: VueWrapper, title: string) {
	return wrapper.get(`[data-cy="media-comparison-details-select-${keyFor(wrapper, title)}"]`);
}

function action(wrapper: VueWrapper, title: string) {
	return wrapper.get(`[data-cy="media-comparison-details-download-${keyFor(wrapper, title)}"]`);
}

const expectedTracks = [
	{ type: PlexMediaType.MusicTrack, mediaIds: [8], plexServerId: 3, plexLibraryId: 4, qualities: [], keepCompletedInDownloadFolder: false },
	{ type: PlexMediaType.MusicTrack, mediaIds: [8], plexServerId: 5, plexLibraryId: 6, qualities: [], keepCompletedInDownloadFolder: false },
];

describe('MediaComparisonDetailsDialog - missing Music leaves and request lifetime', () => {
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

	async function render(responses: Subject<PlexMediaComparisonDetailsDTO>[], phone = false) {
		let responseIndex = 0;
		const request = vi.spyOn(useMediaStore(), 'getMediaComparisonDetails').mockImplementation(() => responses[responseIndex++]!.asObservable());
		const download = vi.spyOn(useDialogStore(), 'openMediaConfirmationDownloadDialog').mockImplementation(() => undefined);
		vi.spyOn(useServerStore(), 'getServerName').mockImplementation((id) => `Server ${id}`);
		vi.spyOn(useLibraryStore(), 'getLibraryName').mockImplementation((id) => `Library ${id}`);
		const wrapper = await mountSuspended(MediaComparisonDetailsDialog, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { QCardDialog: CardDialog },
			},
		});
		const screen = wrapper.vm.$q.screen;
		const originalScreen = { lt: { ...screen.lt }, gt: { ...screen.gt } };
		Object.assign(screen.lt, { md: phone });
		Object.assign(screen.gt, { sm: !phone });
		await flushPromises();
		mounted.push({ unmount: () => {
			Object.assign(screen.lt, originalScreen.lt);
			Object.assign(screen.gt, originalScreen.gt);
			wrapper.unmount();
		} });
		return { wrapper, request, download, dialog: wrapper.getComponent(CardDialog) };
	}

	test.each([1, 5])('Should download only remote Missing track leaves from owned/remote artist state %s', async (comparisonId) => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, request, download } = await render([response]);
		dialog.vm.$emit('opened', artist(PlexMediaType.MusicArtist, comparisonId));
		response.next(details());
		await flushPromises();
		await expandAll(wrapper);

		// Act
		await checkbox(wrapper, 'Partial album').trigger('click');
		await submit(wrapper).trigger('click');
		await action(wrapper, 'Partial album').trigger('click');
		await action(wrapper, 'Missing track').trigger('click');
		await wrapper.get('[data-cy="media-comparison-details-select-all"]').trigger('click');
		await submit(wrapper).trigger('click');

		// Assert
		expect(request).toHaveBeenCalledWith(999, PlexMediaType.MusicArtist);
		expect(download.mock.calls.map(([commands]) => commands)).toEqual([
			[expectedTracks[0]], [expectedTracks[0]], [expectedTracks[0]], expectedTracks,
		]);
		expect(wrapper.text()).toContain('Owned album');
		expect(wrapper.text()).toContain('Owned track');
		expect(wrapper.text()).toContain('Server 3');
		expect(wrapper.text()).toContain('Library 6');
		expect(checkbox(wrapper, 'Owned track').attributes('aria-disabled')).toBe('true');
		expect(checkbox(wrapper, 'Owned album').attributes('aria-disabled')).toBe('true');
		expect(checkbox(wrapper, 'Empty missing album').attributes('aria-disabled')).toBe('true');
		expect(wrapper.find(`[data-cy="media-comparison-details-download-${keyFor(wrapper, 'Owned track')}"]`).exists()).toBe(false);
		expect(wrapper.findAllComponents(MediaVideoQuality)).toHaveLength(0);
		expect(wrapper.text()).not.toContain('Current quality');
		expect(wrapper.text()).not.toContain('Available quality');
		expect(wrapper.findAll('thead th')).toHaveLength(3);
		expect(wrapper.findAll('.mdi-music-note-off-outline')).toHaveLength(4);
		expect(wrapper.find('.mdi-video-off-outline').exists()).toBe(false);
	});

	test('Should deduplicate ancestor/descendant selections while preserving equal IDs at different locations', async () => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, download } = await render([response]);
		dialog.vm.$emit('opened', artist());
		response.next(details());
		await flushPromises();
		await expandAll(wrapper);
		const titles = ['Partial album', 'Owned track', 'Missing track', 'Missing album elsewhere', 'Same ID elsewhere'];

		// Act
		wrapper.getComponent({ name: 'QTreeTable' }).vm.$emit('selected', Object.fromEntries(titles.map((title) => [keyFor(wrapper, title), { checked: true, partialChecked: false }])));
		await flushPromises();
		await submit(wrapper).trigger('click');
		await checkbox(wrapper, 'Missing track').trigger('click');
		await submit(wrapper).trigger('click');

		// Assert
		expect(download.mock.calls.map(([commands]) => commands)).toEqual([expectedTracks, [expectedTracks[1]]]);
	});

	test('Should keep phone location/actions without Music video-quality metadata', async () => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, download } = await render([response], true);
		dialog.vm.$emit('opened', artist());
		response.next(details());
		await flushPromises();
		await expandAll(wrapper);

		// Act
		await action(wrapper, 'Partial album').trigger('click');
		await checkbox(wrapper, 'Missing track').trigger('click');
		await submit(wrapper).trigger('click');

		// Assert
		expect(download.mock.calls.map(([commands]) => commands)).toEqual([[expectedTracks[0]], [expectedTracks[0]]]);
		expect(wrapper.text()).toContain('Server 3');
		expect(wrapper.text()).toContain('Library 4');
		expect(wrapper.findAllComponents(MediaVideoQuality)).toHaveLength(0);
		expect(wrapper.text()).not.toContain('quality');
		expect(wrapper.findAll('thead th')).toHaveLength(1);
	});

	test('Should reject Owned-only and empty Music rows even when a selected event is injected', async () => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, download } = await render([response]);
		dialog.vm.$emit('opened', artist());
		response.next(details(musicRows().slice(2)));
		await flushPromises();
		await expandAll(wrapper);

		// Act
		wrapper.getComponent({ name: 'QTreeTable' }).vm.$emit('selected', { [keyFor(wrapper, 'Owned album')]: { checked: true, partialChecked: false } });
		await flushPromises();
		await submit(wrapper).trigger('click');

		// Assert
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		expect(download).not.toHaveBeenCalled();
		expect(wrapper.findAll('[data-cy^="media-comparison-details-download-"]')).toHaveLength(0);
		expect(wrapper.get('[data-cy="media-comparison-details-select-all"]').attributes('aria-disabled')).toBe('true');
	});

	test.each([false, true])('Should disable pending/failed submission and recover by retry (phone: %s)', async (phone) => {
		// Arrange
		const failed = new Subject<PlexMediaComparisonDetailsDTO>();
		const retry = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, request, download } = await render([failed, retry], phone);
		dialog.vm.$emit('opened', artist());
		await flushPromises();

		// Act
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		failed.error(new Error('Comparison unavailable'));
		await flushPromises();
		expect(wrapper.get('[data-cy="media-comparison-details-error"]').text()).toContain('Unable to load comparison details');
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		await submit(wrapper).trigger('click');
		await wrapper.get('[data-cy="media-comparison-details-retry"]').trigger('click');
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		retry.next(details());
		await flushPromises();
		await checkbox(wrapper, 'Partial album').trigger('click');
		await submit(wrapper).trigger('click');

		// Assert
		expect(request).toHaveBeenCalledTimes(2);
		expect(request).toHaveBeenLastCalledWith(999, PlexMediaType.MusicArtist);
		expect(wrapper.find('[data-cy="media-comparison-details-error"]').exists()).toBe(false);
		expect(download).toHaveBeenCalledExactlyOnceWith([expectedTracks[0]]);
	});

	test('Should treat a completed request without details as failure rather than empty success', async () => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, download } = await render([response]);
		dialog.vm.$emit('opened', artist());

		// Act
		response.complete();
		await flushPromises();

		// Assert
		expect(wrapper.find('[data-cy="media-comparison-details-error"]').exists()).toBe(true);
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		expect(download).not.toHaveBeenCalled();
	});

	test('Should cancel on close and ignore stale results when another artist reopens', async () => {
		// Arrange
		const stale = new Subject<PlexMediaComparisonDetailsDTO>();
		const current = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, request, download } = await render([stale, current]);
		dialog.vm.$emit('opened', artist());
		await flushPromises();

		// Act
		await wrapper.get('[data-cy="dialog-close-button"]').trigger('click');
		expect(stale.observed).toBe(false);
		dialog.vm.$emit('opened', { ...artist(), id: 1000, title: 'Current artist' });
		stale.next(details([row({ title: 'Stale album' })]));
		stale.error(new Error('Stale failure'));
		await flushPromises();
		expect(submit(wrapper).attributes('disabled')).toBeDefined();
		current.next(details([row({ title: 'Current missing track', type: PlexMediaType.MusicTrack, plexMediaId: 88 })]));
		await flushPromises();
		await action(wrapper, 'Current missing track').trigger('click');

		// Assert
		expect(request).toHaveBeenLastCalledWith(1000, PlexMediaType.MusicArtist);
		expect(wrapper.text()).toContain('Current artist');
		expect(wrapper.text()).toContain('Current missing track');
		expect(wrapper.text()).not.toContain('Stale album');
		expect(wrapper.find('[data-cy="media-comparison-details-error"]').exists()).toBe(false);
		expect(download).toHaveBeenCalledExactlyOnceWith([{ ...expectedTracks[0], mediaIds: [88] }]);
	});

	test.each([PlexMediaType.Movie, PlexMediaType.TvShow])('Should preserve %s selection, quality columns and row download controls', async (type) => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog, download } = await render([response]);
		const videoRow = row({ title: 'Video upgrade', type, state: PlexMediaComparisonState.HigherQuality, ownedQuality: VideoQuality.HD, remoteQuality: VideoQuality.FullHD });
		dialog.vm.$emit('opened', artist(type));
		response.next(details([videoRow], type));
		await flushPromises();

		// Act
		await wrapper.get('thead .q-checkbox').trigger('click');
		await submit(wrapper).trigger('click');
		await action(wrapper, 'Video upgrade').trigger('click');

		// Assert
		expect(wrapper.findAll('thead th')).toHaveLength(5);
		expect(wrapper.text()).toContain('Current quality');
		expect(wrapper.text()).toContain('Available quality');
		expect(wrapper.findAllComponents(MediaVideoQuality).map((quality) => quality.props('quality'))).toEqual([VideoQuality.HD, VideoQuality.FullHD]);
		expect(download.mock.calls.map(([commands]) => commands)).toEqual([
			[{ ...expectedTracks[0], type, mediaIds: [7] }], [{ ...expectedTracks[0], type, mediaIds: [7] }],
		]);
	});

	test.each([PlexMediaType.Movie, PlexMediaType.TvShow])('Should preserve phone %s quality metadata', async (type) => {
		// Arrange
		const response = new Subject<PlexMediaComparisonDetailsDTO>();
		const { wrapper, dialog } = await render([response], true);

		// Act
		dialog.vm.$emit('opened', artist(type));
		response.next(details([row({ title: 'Video row', type, ownedQuality: VideoQuality.HD, remoteQuality: VideoQuality.FullHD })], type));
		await flushPromises();

		// Assert
		expect(wrapper.text()).toContain('Current quality');
		expect(wrapper.text()).toContain('Available quality');
		expect(wrapper.findAllComponents(MediaVideoQuality)).toHaveLength(2);
	});
});
