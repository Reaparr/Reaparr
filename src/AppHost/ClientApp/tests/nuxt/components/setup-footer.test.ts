import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import type { VueWrapper } from '@vue/test-utils';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import SetupFooter from '@/components/Views/Setup/SetupFooter.vue';
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

const languageSelectStub = defineComponent({
	name: 'LanguageSelect',
	emits: ['update:modelValue'],
	template: '<button data-cy="setup-language" @click="$emit(\'update:modelValue\', { code: \'fr-FR\' })" />',
});

const backgroundToggleStub = defineComponent({
	name: 'AnimatedBackgroundToggleButton',
	emits: ['click'],
	template: '<button data-cy="setup-background" @click="$emit(\'click\')" />',
});

const navigationButtonStub = defineComponent({
	emits: ['click'],
	template: '<button @click="$emit(\'click\')"><slot /></button>',
});

describe('SetupFooter committed shared settings intents', () => {
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

	test('Should capture language and background choices only when setup controls are committed', async () => {
		// Arrange
		wrapper = await mountSuspended(SetupFooter, {
			props: { maxPages: 6 },
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QCol: passthrough,
					QRow: passthrough,
					'q-separator': passthrough,
					LanguageSelect: languageSelectStub,
					AnimatedBackgroundToggleButton: backgroundToggleStub,
					NavigationPreviousButton: navigationButtonStub,
					NavigationNextButton: navigationButtonStub,
					NavigationFinishSetupButton: navigationButtonStub,
					ConfirmButton: navigationButtonStub,
				},
			},
		});

		// Assert: mounting/setup hydration does not represent a user choice.
		expect(capture).not.toHaveBeenCalled();

		// Act
		await wrapper.get('[data-cy="setup-language"]').trigger('click');
		await wrapper.get('[data-cy="setup-background"]').trigger('click');

		// Assert
		expect(capture).toHaveBeenCalledTimes(2);
		expect(capture).toHaveBeenNthCalledWith(1, FrontendTelemetryEvent.FrontendSetupInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.Language,
		});
		expect(capture).toHaveBeenNthCalledWith(2, FrontendTelemetryEvent.FrontendSetupInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.General,
		});
	});
});
