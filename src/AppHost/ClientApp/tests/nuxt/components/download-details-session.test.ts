import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h, type PropType } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { Subject } from 'rxjs';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generateDownloadTask, generateResultDTO } from '@mock';
import { downloadApi } from '@api';
import { useDownloadStore } from '@store';
import { DownloadStatus, DownloadTaskType, NotificationLevel, type DownloadTaskDTO, type DownloadTaskLogDTO, type ErrorDTO } from '@dto';
import type { ResultDTO } from '@interfaces';
import DownloadDetailsDialog from '@/components/Dialogs/DownloadDetailsDialog/DownloadDetailsDialog.vue';
import messages from '@/lang/en-US.json';

const CardDialog = defineComponent({
	name: 'QCardDialog',
	props: { loading: Boolean },
	emits: ['opened', 'closed'],
	setup(props, { slots }) {
		return () => h('section', { 'aria-busy': props.loading }, [
			slots.title?.(), props.loading ? null : slots.default?.(),
		]);
	},
});
const Panel = defineComponent({ setup: (_, { slots }) => () => h('div', slots.default?.()) });
const Overview = defineComponent({
	name: 'OverviewTabContent',
	props: { errors: { type: Array as PropType<ErrorDTO[]>, required: true } },
	setup: (props) => () => h('div', props.errors.map((error) => h('p', error.message))),
});
const Logs = defineComponent({
	name: 'DownloadDetailsDialogLogsTabContent',
	props: { initialLogs: { type: Array as PropType<DownloadTaskLogDTO[]>, required: true } },
	setup: (props) => () => h('div', { 'data-testid': 'logs' }, props.initialLogs.map((log) => h('p', log.message))),
});

function log(id: number, message: string): DownloadTaskLogDTO {
	return { id, message, logLevel: NotificationLevel.Information, status: DownloadStatus.Downloading, createdAt: '2026-11-04T10:00:00Z' };
}

function failure(message: string): ResultDTO<DownloadTaskDTO> {
	return { isSuccess: false, statusCode: 404, errors: [{ message, metadata: {}, reasons: [] }], successes: [] };
}

describe('DownloadDetailsDialog - opening request ownership', () => {
	let { config } = baseVars();
	let pinia: Pinia;
	let first: DownloadTaskDTO;
	let second: DownloadTaskDTO;
	const mounted: Array<{ unmount: () => void }> = [];

	beforeAll(() => baseSetup());
	beforeEach(() => {
		config = { seed: 116 };
		getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		first = generateDownloadTask({
			id: 'first', plexServerId: 3, plexLibraryId: 31, type: DownloadTaskType.Movie, config,
			partial: { fullTitle: 'First download', plexServerId: 3, plexLibraryId: 31, createdAt: '2026-11-04T10:00:00Z' },
		});
		second = generateDownloadTask({
			id: 'second', plexServerId: 4, plexLibraryId: 42, type: DownloadTaskType.Episode, config,
			partial: { fullTitle: 'Second download', plexServerId: 4, plexLibraryId: 42, createdAt: '2026-11-04T10:00:00Z' },
		});
		vi.spyOn(useDownloadStore(), 'getDownloadTaskById').mockReturnValue(null);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.restoreAllMocks();
	});

	async function render(details: Subject<ResultDTO<DownloadTaskDTO>>[], logs: Subject<ResultDTO<DownloadTaskLogDTO[]>>[]) {
		vi.spyOn(downloadApi, 'getDownloadTaskByGuidEndpoint').mockImplementation(() => {
			const response = details.shift();
			if (!response) throw new Error('Unexpected detail request');
			return response.asObservable();
		});
		const logRequests = vi.spyOn(downloadApi, 'getDownloadTaskLogsByDownloadTaskIdEndpoint').mockImplementation(() => {
			const response = logs.shift();
			if (!response) throw new Error('Unexpected log request');
			return response.asObservable();
		});
		const wrapper = await mountSuspended(DownloadDetailsDialog, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QCardDialog: CardDialog, QTabPanels: Panel, QTabPanel: Panel, QTabs: true, QTab: true,
					QMediaTypeIcon: true, OverviewTabContent: Overview, DownloadDetailsDialogLogsTabContent: Logs,
				},
			},
		});
		mounted.push(wrapper);
		return { wrapper, dialog: wrapper.getComponent(CardDialog), logRequests };
	}

	test('Should retain the newer details and matching log scope when an older opening responds last', async () => {
		// Arrange
		const stale = new Subject<ResultDTO<DownloadTaskDTO>>();
		const current = new Subject<ResultDTO<DownloadTaskDTO>>();
		const currentLogs = new Subject<ResultDTO<DownloadTaskLogDTO[]>>();
		const { wrapper, dialog, logRequests } = await render([stale, current], [currentLogs]);

		// Act
		dialog.vm.$emit('opened', first.id);
		dialog.vm.$emit('closed');
		dialog.vm.$emit('opened', second.id);
		current.next(generateResultDTO(second));
		currentLogs.next(generateResultDTO([log(20, 'Second download log')]));
		stale.next(generateResultDTO(first));
		await flushPromises();

		// Assert
		expect(wrapper.get('[data-cy="download-details-dialog-title"]').text()).toBe(second.fullTitle);
		expect(wrapper.get('[data-testid="logs"]').text()).toBe('Second download log');
		expect(dialog.attributes('aria-busy')).toBe('false');
		expect(logRequests.mock.calls).toEqual([[second.id, {
			type: second.downloadTaskType, plexLibraryId: second.plexLibraryId, plexServerId: second.plexServerId, take: 50,
		}]]);
	});

	test('Should retain the newer initial logs when a closed opening returns logs last', async () => {
		// Arrange
		const firstDetails = new Subject<ResultDTO<DownloadTaskDTO>>();
		const secondDetails = new Subject<ResultDTO<DownloadTaskDTO>>();
		const staleLogs = new Subject<ResultDTO<DownloadTaskLogDTO[]>>();
		const currentLogs = new Subject<ResultDTO<DownloadTaskLogDTO[]>>();
		const { wrapper, dialog } = await render([firstDetails, secondDetails], [staleLogs, currentLogs]);

		// Act
		dialog.vm.$emit('opened', first.id);
		firstDetails.next(generateResultDTO(first));
		dialog.vm.$emit('closed');
		dialog.vm.$emit('opened', second.id);
		secondDetails.next(generateResultDTO(second));
		currentLogs.next(generateResultDTO([log(21, 'Second newest'), log(20, 'Second older')]));
		staleLogs.next(generateResultDTO([log(99, 'Wrong first download log')]));
		await flushPromises();

		// Assert
		expect(wrapper.get('[data-cy="download-details-dialog-title"]').text()).toBe(second.fullTitle);
		expect(wrapper.get('[data-testid="logs"]').findAll('p').map((row) => row.text())).toEqual(['Second newest', 'Second older']);
	});

	test('Should preserve current loading after a stale failure and recover a failed opening on reopen', async () => {
		// Arrange
		const stale = new Subject<ResultDTO<DownloadTaskDTO>>();
		const failed = new Subject<ResultDTO<DownloadTaskDTO>>();
		const recovered = new Subject<ResultDTO<DownloadTaskDTO>>();
		const recoveredLogs = new Subject<ResultDTO<DownloadTaskLogDTO[]>>();
		const { wrapper, dialog } = await render([stale, failed, recovered], [recoveredLogs]);

		// Act
		dialog.vm.$emit('opened', first.id);
		dialog.vm.$emit('closed');
		dialog.vm.$emit('opened', second.id);
		stale.next(failure('Stale error'));
		await flushPromises();

		// Assert
		expect(dialog.attributes('aria-busy')).toBe('true');
		expect(wrapper.text()).not.toContain('Stale error');
		failed.next(failure('Current task unavailable'));
		await flushPromises();
		expect(dialog.attributes('aria-busy')).toBe('false');
		expect(wrapper.text()).toContain('Current task unavailable');

		dialog.vm.$emit('closed');
		dialog.vm.$emit('opened', second.id);
		recovered.next(generateResultDTO(second));
		recoveredLogs.next(generateResultDTO([log(22, 'Recovered download log')]));
		await flushPromises();
		expect(wrapper.get('[data-cy="download-details-dialog-title"]').text()).toBe(second.fullTitle);
		expect(wrapper.get('[data-testid="logs"]').text()).toBe('Recovered download log');
		expect(wrapper.text()).not.toContain('Current task unavailable');
	});

	test('Should keep a closed dialog cleared when its pending detail response arrives', async () => {
		// Arrange
		const pending = new Subject<ResultDTO<DownloadTaskDTO>>();
		const { wrapper, dialog, logRequests } = await render([pending], []);

		// Act
		dialog.vm.$emit('opened', first.id);
		dialog.vm.$emit('closed');
		pending.next(generateResultDTO(first));
		await flushPromises();

		// Assert
		expect(wrapper.find('[data-cy="download-details-dialog-title"]').exists()).toBe(false);
		expect(dialog.attributes('aria-busy')).toBe('true');
		expect(logRequests).not.toHaveBeenCalled();
	});
});
