import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import type { VueWrapper } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { JobStatus, JobTypes } from '@dto';
import { generateJobStatusUpdate } from '@factories';
import { useBackgroundJobsStore, useDialogStore } from '@store';
import BackgroundActivityToggleButton from '@/components/Buttons/BackgroundActivityToggleButton.vue';
import messages from '@/lang/en-US.json';

const passthrough = defineComponent({
	inheritAttrs: false,
	template: '<div v-bind="$attrs"><slot /></div>',
});

const dropdownStub = defineComponent({
	inheritAttrs: false,
	template: '<div v-bind="$attrs"><slot name="label" /><slot /></div>',
});

const badgeStub = defineComponent({
	props: ['value'],
	template: '<div>{{ value }}</div>',
});

const itemSectionStub = defineComponent({
	emits: ['click'],
	template: '<button @click="$emit(\'click\')"><slot /></button>',
});

describe('BackgroundActivityToggleButton', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let wrapper: VueWrapper | undefined;

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
		mock.restore();
	});

	async function render() {
		return mountSuspended(BackgroundActivityToggleButton, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					'q-btn-dropdown': dropdownStub,
					'q-list': passthrough,
					'q-item': passthrough,
					'q-item-section': itemSectionStub,
					QGlowBadge: badgeStub,
					QSpinnerOval: passthrough,
				},
			},
		});
	}

	function publish(status: JobStatus, data: Record<string, number[]> = {}) {
		useBackgroundJobsStore().setStatusJobUpdate(generateJobStatusUpdate({
			jobType: JobTypes.CheckAllConnectionsStatusByPlexServerJob,
			jobStatus: status,
			data: { plexServersWithConnectionIds: data },
		}));
	}

	test('shows overall activity for a startup update with unknown server ids', async () => {
		const backgroundJobsStore = useBackgroundJobsStore();
		backgroundJobsStore.setStatusJobUpdate(generateJobStatusUpdate({
			jobType: JobTypes.CheckAllConnectionsStatusByPlexServerJob,
			jobStatus: JobStatus.Started,
			data: {},
		}));

		wrapper = await render();
		const openDialog = vi.spyOn(useDialogStore(), 'openCheckServerConnectionsDialog');

		expect(wrapper.find('[data-cy="background-activity-button-badge"]').text()).toBe('1');
		expect(wrapper.find('[data-cy="background-activity-button-loading"]').exists()).toBe(true);
		expect(wrapper.find(`[data-cy="${JobTypes.CheckAllConnectionsStatusByPlexServerJob}activity-button"]`).exists()).toBe(true);
		await wrapper.findComponent(itemSectionStub).trigger('click');
		expect(openDialog).toHaveBeenCalledExactlyOnceWith({ plexServersWithConnectionIds: {} });
	});

	test('opens the filtered dialog when live activity includes known server ids', async () => {
		wrapper = await render();
		const dialogStore = useDialogStore();
		const openDialog = vi.spyOn(dialogStore, 'openCheckServerConnectionsDialog');
		publish(JobStatus.Started, { 12: [34, 35] });
		await wrapper.vm.$nextTick();

		await wrapper.findComponent(itemSectionStub).trigger('click');

		expect(openDialog).toHaveBeenCalledExactlyOnceWith({
			plexServersWithConnectionIds: { 12: [34, 35] },
		});
	});

	test.each([JobStatus.Completed, JobStatus.Failed, JobStatus.Cancelled])('clears activity on %s', async (terminalStatus) => {
		wrapper = await render();
		publish(JobStatus.Started, { 12: [34] });
		await wrapper.vm.$nextTick();
		expect(wrapper.find('[data-cy="background-activity-button-loading"]').exists()).toBe(true);

		publish(terminalStatus);
		await wrapper.vm.$nextTick();

		expect(wrapper.find('[data-cy="background-activity-button-badge"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy="background-activity-button-loading"]').exists()).toBe(false);
		expect(wrapper.find(`[data-cy="${JobTypes.CheckAllConnectionsStatusByPlexServerJob}activity-button"]`).exists()).toBe(false);
	});

	test('ignores unrelated job updates', async () => {
		wrapper = await render();
		useBackgroundJobsStore().setStatusJobUpdate(generateJobStatusUpdate({
			jobType: JobTypes.InspectPlexServerJob,
			jobStatus: JobStatus.Started,
			data: { plexServerIds: [12] },
		}));
		await wrapper.vm.$nextTick();

		expect(wrapper.find('[data-cy="background-activity-button-badge"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy="background-activity-button-loading"]').exists()).toBe(false);
		expect(wrapper.find('[data-cy="no-background-activity-button"]').exists()).toBe(true);
	});
});
