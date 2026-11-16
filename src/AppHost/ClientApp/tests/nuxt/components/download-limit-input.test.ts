import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent, h } from 'vue';
import type { VueWrapper } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { baseSetup } from '@services-test-base';
import { useSettingsStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent, FrontendTelemetrySettingsModule } from '@composables/telemetry/events';
import DownloadLimitInput from '@/components/Form/DownloadLimitInput.vue';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

const InputNumber = defineComponent({
	props: { modelValue: { type: Number, default: 0 } },
	emits: ['update:modelValue'],
	setup(_, { emit }) {
		return () => h('button', {
			'data-cy': 'download-speed-limit-input',
			onClick: () => emit('update:modelValue', 2048),
		});
	},
});

describe('DownloadLimitInput telemetry intents', () => {
	let pinia: Pinia;
	let wrapper: VueWrapper | undefined;

	beforeAll(() => baseSetup());
	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
		capture.mockClear();
	});
	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
		vi.restoreAllMocks();
	});

	test('Should capture one structured server intent at the numeric commit boundary', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		settingsStore.serverSettings.data.push({
			machineIdentifier: 'machine-1',
			plexServerName: '',
			downloadSpeedLimit: 0,
			allowStreamDownloader: false,
		});
		wrapper = await mountSuspended(DownloadLimitInput, {
			props: { machineIdentifier: 'machine-1' },
			global: { plugins: [pinia], stubs: { InputNumber } },
		});

		// Act
		await wrapper.find('[data-cy="download-speed-limit-input"]').trigger('click');

		// Assert
		expect(settingsStore.getServerSettings('machine-1')?.downloadSpeedLimit).toBe(2048);
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendSettingsInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.Server,
			machine_identifier: 'machine-1',
		});
	});
});
