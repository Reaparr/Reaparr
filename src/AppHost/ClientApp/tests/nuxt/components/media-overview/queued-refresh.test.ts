import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { flushPromises } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h, nextTick } from 'vue';
import { createI18n } from 'vue-i18n';
import { baseSetup } from '@services-test-base';
import { generateLibrarySyncJobQueue, generatePlexLibrary, generatePlexServer, Seed } from '@mock';
import { LibrarySyncJobStatus, PlexMediaType } from '@dto';
import { LibrarySyncWaitingReason } from '@enums';
import { useDialogStore, useLibraryStore, useServerStore } from '@store';
import MediaOverviewRefresh from '@/components/MediaOverview/MediaOverviewRefresh.vue';
import SyncServerMediaDialog from '@/components/Dialogs/SyncServerMediaDialog.vue';
import messages from '@/lang/en-US.json';
import { Subject } from 'rxjs';

const passthrough = defineComponent({
	inheritAttrs: false,
	props: {
		cy: { type: String, default: undefined },
		justify: { type: String, default: '' },
		align: { type: String, default: '' },
		noWrap: { type: Boolean, default: false },
		cols: { type: [String, Number], default: undefined },
		textAlign: { type: String, default: '' },
	},
	setup(props, { attrs, slots }) {
		return () => h('div', {
			...attrs,
			class: [attrs.class, props.noWrap ? 'no-wrap' : '', props.cols ? `col-${props.cols}` : '', props.textAlign ? `text-${props.textAlign}` : ''],
			'data-cy': props.cy,
		}, slots.default?.());
	},
});

const buttonStub = defineComponent({
	inheritAttrs: false,
	props: {
		cy: { type: String, default: '' },
		label: { type: String, default: '' },
		ariaLabel: { type: String, default: '' },
		icon: { type: String, default: '' },
		tooltipText: { type: String, default: '' },
		size: { type: String, default: '' },
		loading: { type: Boolean, default: false },
	},
	template: '<button v-bind="$attrs" :disabled="loading" :aria-label="ariaLabel" :data-icon="icon" :data-size="size" :data-tooltip="tooltipText" :data-cy="cy"><slot />{{ label }}</button>',
});

describe('MediaOverviewRefresh queued presentation', () => {
	let pinia: Pinia;
	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		vi.restoreAllMocks();
	});

	function setupQueuedLibrary(syncedAt: string | null) {
		const libraryStore = useLibraryStore();
		const serverStore = useServerStore();
		const processingLibrary = generatePlexLibrary({
			seed: new Seed(1801),
			plexServerId: 7,
			type: PlexMediaType.Movie,
			partialData: { id: 701, title: 'Movies', syncedAt: new Date().toISOString() },
		});
		const queuedLibrary = generatePlexLibrary({
			seed: new Seed(1802),
			plexServerId: 7,
			type: PlexMediaType.Movie,
			partialData: { id: 702, title: 'Queued Movies', syncedAt },
		});
		libraryStore.updateLibrary(processingLibrary);
		libraryStore.updateLibrary(queuedLibrary);
		const processingQueue = generateLibrarySyncJobQueue({
			plexLibraryId: processingLibrary.id,
			plexServerId: 7,
			status: LibrarySyncJobStatus.Processing,
		});
		const queuedQueue = generateLibrarySyncJobQueue({
			plexLibraryId: queuedLibrary.id,
			plexServerId: 7,
			status: LibrarySyncJobStatus.Queued,
		});
		libraryStore.updateSyncQueue(processingQueue);
		libraryStore.updateSyncQueue(queuedQueue);
		vi.spyOn(libraryStore, 'getLibrarySyncWaitingReason').mockReturnValue(LibrarySyncWaitingReason.Busy);
		vi.spyOn(libraryStore, 'getProcessingLibrarySyncQueue').mockReturnValue(processingQueue);
		vi.spyOn(serverStore, 'getServerName').mockReturnValue('My Plex Server');
		return { libraryStore, queuedLibrary, processingLibrary };
	}

	async function render(libraryId: number, variant: 'panel' | 'banner', mode: 'processing' | 'queued' = 'queued') {
		return mountSuspended(MediaOverviewRefresh, {
			props: { libraryId, mode, variant },
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QAlert: passthrough,
					QCol: passthrough,
					QIcon: passthrough,
					QProgressBar: passthrough,
					QRow: passthrough,
					BaseButton: buttonStub,
					CancelButton: buttonStub,
				},
			},
		});
	}

	test('renders a first-sync queued panel without progress or execution controls', async () => {
		const { queuedLibrary } = setupQueuedLibrary(null);
		const wrapper = await render(queuedLibrary.id, 'panel');

		expect(wrapper.find('[data-cy="media-overview-library-sync-queued-panel"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="media-overview-library-sync-queued-message"]').text()).toContain(
			'Waiting for Movies to finish refreshing on My Plex Server.',
		);
		expect(wrapper.find('[data-cy="media-overview-library-sync-queued-panel"]').attributes('role')).toBe('status');
		expect(wrapper.find('[data-cy*="progress"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy*="eta"]').exists()).toBe(false);
	});

	test('renders a previously synced queued banner with activity and cancellation actions', async () => {
		const { queuedLibrary } = setupQueuedLibrary(new Date().toISOString());
		const dialogStore = useDialogStore();
		const openActivity = vi.spyOn(dialogStore, 'openSyncServerMediaDialog');
		const wrapper = await render(queuedLibrary.id, 'banner');

		expect(wrapper.find('[data-cy="media-overview-library-sync-queued-banner"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="view-library-sync-activity-button"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="cancel-queued-library-sync-button"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="media-overview-library-sync-queued-banner"]').attributes('aria-live')).toBe('polite');

		await wrapper.get('[data-cy="view-library-sync-activity-button"]').trigger('click');
		expect(openActivity).toHaveBeenCalledWith(7);
	});
	test('renders a processing banner with progress and cancellation action', async () => {
		const { processingLibrary } = setupQueuedLibrary(new Date().toISOString());
		const wrapper = await render(processingLibrary.id, 'banner', 'processing');

		expect(wrapper.find('[data-cy="media-overview-library-sync-processing-banner"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="media-overview-library-sync-processing-progress"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="cancel-library-sync-button"]').exists()).toBe(true);
		expect(wrapper.find('[data-cy="media-overview-library-sync-processing-banner"]').attributes('aria-live')).toBe('polite');
	});
	test('cancels only queued library rows from the activity dialog and keeps processing rows intact', async () => {
		// Arrange
		const libraryStore = useLibraryStore();
		const serverStore = useServerStore();
		const queuedLibrary = generatePlexLibrary({
			seed: new Seed(1810), plexServerId: 7, type: PlexMediaType.Movie, partialData: { id: 710, title: 'Queued Movies' },
		});
		const processingLibrary = generatePlexLibrary({
			seed: new Seed(1811), plexServerId: 7, type: PlexMediaType.Movie, partialData: { id: 711, title: 'Processing Movies' },
		});
		libraryStore.updateLibrary(queuedLibrary);
		libraryStore.updateLibrary(processingLibrary);
		const queuedQueue = generateLibrarySyncJobQueue({ plexLibraryId: queuedLibrary.id, plexServerId: 7, status: LibrarySyncJobStatus.Queued });
		const processingQueue = generateLibrarySyncJobQueue({ plexLibraryId: processingLibrary.id, plexServerId: 7, status: LibrarySyncJobStatus.Processing });
		libraryStore.syncQueues = [queuedQueue, processingQueue];
		serverStore.servers = [generatePlexServer({ seed: new Seed(1810), id: 7 })];
		vi.spyOn(serverStore, 'getServerName').mockReturnValue('My Plex Server');
		vi.spyOn(libraryStore, 'getLibrarySyncWaitingReason').mockReturnValue(LibrarySyncWaitingReason.Busy);
		vi.spyOn(libraryStore, 'getProcessingLibrarySyncQueue').mockReturnValue(libraryStore.syncQueues[1]!);
		const cancelResponse = new Subject<{ isSuccess: boolean }>();
		const cancel = vi.spyOn(libraryStore, 'cancelLibrarySync').mockReturnValue(cancelResponse as never);
		const cardDialog = defineComponent({
			name: 'QCardDialog',
			setup(_, { slots }) {
				return () => h('section', [slots.default?.(), slots.actions?.({ close: () => undefined })]);
			},
		});
		const passthroughTree = defineComponent({
			name: 'q-tree',
			props: { nodes: { type: Array, default: () => [] } },
			setup(props, { slots }) {
				return () => h('div', (props.nodes as Array<{ children: unknown[] }>).flatMap((server) => server.children.map((node, index) => h('div', { key: index }, slots['default-header']?.({ node })))));
			},
		});
		const wrapper = await mountSuspended(SyncServerMediaDialog, { global: {
			plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
			stubs: { QCardDialog: cardDialog, 'q-tree': passthroughTree, QRow: passthrough, QCol: passthrough, QMediaTypeIcon: true, QProgressBar: true, HideButton: buttonStub, IconButton: buttonStub },
		} });
		useDialogStore().openSyncServerMediaDialog();
		await flushPromises();
		await nextTick();

		// Act
		const queuedCancel = wrapper.get(`[data-cy="sync-server-media-dialog-cancel-library-${queuedLibrary.id}"]`);
		await queuedCancel.trigger('click');
		await flushPromises();

		// Assert
		expect(wrapper.find(`[data-cy="sync-server-media-dialog-cancel-library-${processingLibrary.id}"]`).exists()).toBe(false);
		expect(queuedCancel.attributes('data-icon')).toBe('mdi-close');
		expect(queuedCancel.attributes('data-tooltip')).toBe('Cancel queued refresh');
		expect(queuedCancel.attributes('aria-label')).toBe('Cancel queued refresh');
		expect(queuedCancel.attributes('data-size')).toBe('sm');
		expect(queuedCancel.element.parentElement?.parentElement?.classList.contains('no-wrap')).toBe(true);
		expect(queuedCancel.element.parentElement?.classList.contains('col-1')).toBe(true);
		expect(queuedCancel.element.parentElement?.classList.contains('text-right')).toBe(true);
		expect(cancel).toHaveBeenCalledExactlyOnceWith(queuedLibrary.id);
		expect(queuedCancel.attributes('disabled')).toBeDefined();
		cancelResponse.next({ isSuccess: true });
		cancelResponse.complete();
		await flushPromises();
		wrapper.unmount();
	});
});
