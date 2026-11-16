import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import type { VueWrapper } from '@vue/test-utils';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { useSettingsStore } from '@store';
import NetworkSection from '@/components/Views/Settings/NetworkSection.vue';
import { FrontendTelemetryAction, FrontendTelemetryEvent, FrontendTelemetrySettingsModule } from '@composables/telemetry/events';
import messages from '@/lang/en-US.json';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

const passthrough = defineComponent({
	template: '<div><slot /></div>',
});

const inputStub = defineComponent({
	name: 'QInput',
	props: {
		modelValue: { type: [String, Number], default: '' },
	},
	emits: ['update:modelValue', 'change', 'blur'],
	template: '<input :value="modelValue" />',
});

describe('NetworkSection committed settings intents', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let wrapper: VueWrapper | undefined;

	beforeAll(() => {
		baseSetup();
	});

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

	test('Should not capture an intent when a network field loses focus without an edit', async () => {
		// Arrange
		wrapper = await mountSuspended(NetworkSection, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QSection: passthrough,
					HelpGroup: passthrough,
					HelpRow: passthrough,
					QInput: inputStub,
					'q-toggle': passthrough,
				},
			},
		});
		const input = wrapper.findComponent(inputStub);

		// Act
		await input.vm.$emit('blur');

		// Assert
		expect(capture).not.toHaveBeenCalled();
	});

	test('Should capture one network intent when an edited field commits its value', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		wrapper = await mountSuspended(NetworkSection, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QSection: passthrough,
					HelpGroup: passthrough,
					HelpRow: passthrough,
					QInput: inputStub,
					'q-toggle': passthrough,
				},
			},
		});
		const input = wrapper.findComponent(inputStub);

		// Act
		await input.vm.$emit('update:modelValue', 'https://edited.example');
		await input.vm.$emit('change');

		// Assert
		expect(settingsStore.networkSettings.reverseProxyUrl).toBe('https://edited.example');
		expect(capture).toHaveBeenCalledExactlyOnceWith(FrontendTelemetryEvent.FrontendSettingsInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.Network,
		});
	});
});
