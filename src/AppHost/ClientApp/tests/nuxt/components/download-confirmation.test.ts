import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { Subject } from 'rxjs';
import { createI18n } from 'vue-i18n';
import { baseSetup } from '@services-test-base';
import { useDownloadStore, useFolderPathStore } from '@store';
import { FolderType, PlexMediaType, type DownloadMediaDTO, type DownloadPreviewContainerDTO, type FolderPathDTO } from '@dto';
import DownloadConfirmation from '@/components/Dialogs/DownloadConfirmation.vue';
import messages from '@/lang/en-US.json';

const CardDialog = defineComponent({
	name: 'QCardDialog',
	emits: ['opened', 'closed'],
	setup(_, { slots, emit }) {
		return () => h('section', [
			slots['top-row']?.(), slots.default?.(), slots.actions?.({ close: () => emit('closed') }),
		]);
	},
});

const DirectoryBrowser = defineComponent({
	name: 'DirectoryBrowser',
	emits: ['confirm'],
	setup: () => () => h('div'),
});

function command(type: PlexMediaType): DownloadMediaDTO[] {
	return [{ type, mediaIds: [101], plexServerId: 3, plexLibraryId: 4, qualities: [], keepCompletedInDownloadFolder: false }];
}

function preview(title: string, type: PlexMediaType): DownloadPreviewContainerDTO {
	return {
		previews: [{ key: title, title, type, children: [], qualities: [], size: 1024 }],
		expanded: {}, totalSize: 1024,
	};
}

function folder(id: number, mediaType: PlexMediaType, folderType: FolderType, isDefault = false): FolderPathDTO {
	return { id, mediaType, folderType, isDefault, directory: `/destination/${id}`, displayName: `Folder ${id}`, isValid: true };
}

const families = [
	[PlexMediaType.Movie, PlexMediaType.Movie, FolderType.MovieFolder],
	[PlexMediaType.Episode, PlexMediaType.TvShow, FolderType.TvShowFolder],
	[PlexMediaType.MusicArtist, PlexMediaType.MusicArtist, FolderType.MusicFolder],
	[PlexMediaType.MusicAlbum, PlexMediaType.MusicArtist, FolderType.MusicFolder],
	[PlexMediaType.MusicTrack, PlexMediaType.MusicArtist, FolderType.MusicFolder],
	[PlexMediaType.PhotoAlbum, PlexMediaType.PhotoAlbum, FolderType.PhotosFolder],
	[PlexMediaType.PhotoImage, PlexMediaType.PhotoAlbum, FolderType.PhotosFolder],
	[PlexMediaType.OtherVideos, PlexMediaType.OtherVideos, FolderType.OtherVideosFolder],
] as const;

describe('DownloadConfirmation - family destinations and preview lifetime', () => {
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

	async function render(paths: FolderPathDTO[], responses: Subject<DownloadPreviewContainerDTO | null>[]) {
		const downloadStore = useDownloadStore();
		let responseIndex = 0;
		vi.spyOn(downloadStore, 'previewDownload').mockImplementation(() => responses[responseIndex++]!.asObservable());
		useFolderPathStore().$patch({ folderPaths: paths });
		const wrapper = await mountSuspended(DownloadConfirmation, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { QCardDialog: CardDialog, DirectoryBrowser, QFileSize: true, QMediaTypeIcon: true },
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	test.each(families)('Should use the persisted %s family default instead of the first or foreign destination', async (type, rootType, folderType) => {
		const response = new Subject<DownloadPreviewContainerDTO | null>();
		const wrapper = await render([
			folder(90, PlexMediaType.Games, FolderType.GamesVideosFolder, true),
			folder(20, rootType, folderType), folder(21, rootType, folderType, true),
		], [response]);

		wrapper.getComponent(CardDialog).vm.$emit('opened', command(type));
		await flushPromises();
		const submit = () => wrapper.get('[data-cy="download-confirmation-submit"]').findAll('button')[0]!;
		expect(submit().attributes('disabled')).toBeDefined();
		response.next(preview('Chosen media', type));
		await flushPromises();
		expect(wrapper.text()).toContain('/destination/21');
		expect(submit().attributes('disabled')).toBeUndefined();
		await submit().trigger('click');
		expect(wrapper.emitted('download')?.[0]?.[0]).toMatchObject({ destinationFolderPathId: 21, customDestinationFolderPath: '' });
	});

	test.each([PlexMediaType.MusicTrack, PlexMediaType.PhotoImage])('Should omit video quality controls from a %s preview', async (type) => {
		const response = new Subject<DownloadPreviewContainerDTO | null>();
		const wrapper = await render([], [response]);
		wrapper.getComponent(CardDialog).vm.$emit('opened', command(type));
		response.next(preview('Non-video asset', type));
		await flushPromises();
		expect(wrapper.text()).toContain('Non-video asset');
		expect(wrapper.text()).not.toContain('Quality');
	});

	test('Should keep Other Videos quality information and never borrow a Movie destination', async () => {
		const response = new Subject<DownloadPreviewContainerDTO | null>();
		const wrapper = await render([folder(10, PlexMediaType.Movie, FolderType.MovieFolder, true)], [response]);
		wrapper.getComponent(CardDialog).vm.$emit('opened', command(PlexMediaType.OtherVideos));
		response.next(preview('Personal video', PlexMediaType.OtherVideos));
		await flushPromises();
		expect(wrapper.text()).toContain('Quality');
		expect(wrapper.text()).toContain('Not set');
		expect(wrapper.text()).not.toContain('/destination/10');

		wrapper.getComponent(DirectoryBrowser).vm.$emit('confirm', {
			...folder(0, PlexMediaType.OtherVideos, FolderType.OtherVideosFolder), directory: '/custom/personal-videos',
		});
		await flushPromises();
		await wrapper.get('[data-cy="download-confirmation-submit"]').findAll('button')[0]!.trigger('click');
		expect(wrapper.emitted('download')?.[0]?.[0]).toMatchObject({ destinationFolderPathId: 0, customDestinationFolderPath: '/custom/personal-videos' });
	});

	test('Should show a failed preview, reject submission and retry the current selection', async () => {
		const failed = new Subject<DownloadPreviewContainerDTO | null>();
		const retried = new Subject<DownloadPreviewContainerDTO | null>();
		const wrapper = await render([], [failed, retried]);
		wrapper.getComponent(CardDialog).vm.$emit('opened', command(PlexMediaType.PhotoImage));
		failed.error(new Error('Preview unavailable'));
		await flushPromises();
		expect(wrapper.get('[data-cy="download-confirmation-error"]').text()).toContain('Unable to load the download preview');
		const submit = () => wrapper.get('[data-cy="download-confirmation-submit"]').findAll('button')[0]!;
		expect(submit().attributes('disabled')).toBeDefined();
		await submit().trigger('click');
		expect(wrapper.emitted('download')).toBeUndefined();

		await wrapper.get('[data-cy="download-confirmation-retry"]').trigger('click');
		expect(submit().attributes('disabled')).toBeDefined();
		retried.next(preview('Retried image', PlexMediaType.PhotoImage));
		await flushPromises();
		expect(wrapper.find('[data-cy="download-confirmation-error"]').exists()).toBe(false);
		expect(wrapper.text()).toContain('Retried image');
		expect(submit().attributes('disabled')).toBeUndefined();
	});

	test('Should ignore a cancelled stale preview after a different family selection reopens', async () => {
		const stale = new Subject<DownloadPreviewContainerDTO | null>();
		const current = new Subject<DownloadPreviewContainerDTO | null>();
		const wrapper = await render([
			folder(31, PlexMediaType.MusicArtist, FolderType.MusicFolder, true),
			folder(41, PlexMediaType.PhotoAlbum, FolderType.PhotosFolder, true),
		], [stale, current]);
		const dialog = wrapper.getComponent(CardDialog);
		dialog.vm.$emit('opened', command(PlexMediaType.MusicArtist));
		dialog.vm.$emit('closed');
		dialog.vm.$emit('opened', command(PlexMediaType.PhotoImage));
		current.next(preview('Current image', PlexMediaType.PhotoImage));
		stale.next(preview('Stale artist', PlexMediaType.MusicArtist));
		await flushPromises();
		expect(wrapper.text()).toContain('Current image');
		expect(wrapper.text()).not.toContain('Stale artist');
		expect(wrapper.text()).toContain('/destination/41');
		await wrapper.get('[data-cy="download-confirmation-submit"]').findAll('button')[0]!.trigger('click');
		expect(wrapper.emitted('download')?.[0]?.[0]).toMatchObject({ destinationFolderPathId: 41, downloadMedias: [{ type: PlexMediaType.PhotoImage }] });
	});
});
