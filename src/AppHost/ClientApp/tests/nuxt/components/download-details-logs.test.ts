import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h } from 'vue';
import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generateDownloadTask, generateFailedResultDTO, generateResultDTO } from '@mock';
import { DownloadPaths } from '@api-urls';
import { DownloadStatus, DownloadTaskType, NotificationLevel, type DownloadTaskLogDTO } from '@dto';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';
import DownloadDetailsLogs from '@/components/Dialogs/DownloadDetailsDialog/Tabs/DownloadDetailsDialogLogsTabContent.vue';
import type * as Composables from '@composables';
import messages from '@/lang/en-US.json';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});
vi.mock('@composables', async (importOriginal) => ({
	...await importOriginal<typeof Composables>(),
	showSuccessNotification: vi.fn(),
}));
const Confirmation = defineComponent({
	emits: ['confirm'],
	setup(_, { emit }) {
		return () => h('button', { onClick: () => emit('confirm') }, 'Confirm log deletion');
	},
});

describe('DownloadDetailsLogs - confirmed deletion intent', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let wrapper: VueWrapper | undefined;

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		capture.mockClear();
	});
	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
		mock.restore();
	});

	test.each([true, false])('Should capture intent before the response and only once when deletion success is %s', async (success) => {
		// Arrange
		const task = generateDownloadTask({
			id: '00000000-0000-4000-8000-000000000101',
			plexServerId: 3,
			plexLibraryId: 4,
			type: DownloadTaskType.Movie,
			config: { seed: 55301 },
			partial: { plexServerId: 3, plexLibraryId: 4 },
		});
		const initialLogs: DownloadTaskLogDTO[] = [{
			id: 1, createdAt: '2026-01-01T00:00:00Z', logLevel: NotificationLevel.Error,
			message: 'credential-bearing operational log', status: DownloadStatus.Error,
		}];
		const query = { type: task.downloadTaskType, plexLibraryId: 4, plexServerId: 3 };
		const endpoint = DownloadPaths.deleteAllDownloadTaskLogsByDownloadTaskIdEndpoint(task.id, query).split('?')[0]!;
		const response = Promise.withResolvers<[number, unknown]>();
		mock.onDelete(endpoint, { params: query }).reply(() => response.promise);
		wrapper = await mountSuspended(DownloadDetailsLogs, {
			props: { downloadTaskId: task.id, downloadTask: task, initialLogs },
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { ConfirmationDialog: Confirmation },
			},
		});
		expect(capture).not.toHaveBeenCalled();
		expect(mock.history.delete).toHaveLength(0);

		// Act
		await wrapper.getComponent(Confirmation).trigger('click');
		await flushPromises();

		// Assert: an accepted confirmation is observable while the request is still pending.
		expect(capture).toHaveBeenCalledExactlyOnceWith(FrontendTelemetryEvent.FrontendLogInteraction, {
			action: FrontendTelemetryAction.ClearLogs, download_task_id: task.id, log_count: 1,
		});
		expect(mock.history.delete.map((request) => ({ url: request.url, params: request.params })))
			.toEqual([{ url: endpoint, params: query }]);
		expect(wrapper.emitted('logs-deleted')).toBeUndefined();

		// Act
		response.resolve([200, success ? generateResultDTO(1) : generateFailedResultDTO()]);
		await flushPromises();

		// Assert: neither success nor rejection creates another intention.
		expect(capture).toHaveBeenCalledTimes(1);
		expect(wrapper.emitted('logs-deleted')).toEqual(success ? [[]] : undefined);
	});
});
