import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import type { VueWrapper } from '@vue/test-utils';
import { baseSetup } from '@services-test-base';
import { useSettingsStore } from '@store';
import MediaOptionsDialog from '@/components/Dialogs/MediaOptionsDialog.vue';
import { FrontendTelemetryAction, FrontendTelemetryEvent, FrontendTelemetrySettingsModule } from '@composables/telemetry/events';
import messages from '@/lang/en-US.json';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

const cardDialogStub = defineComponent({
	name: 'QCardDialogStub',
	emits: ['opened', 'closed'],
	template: '<div><slot name="title" /><slot /></div>',
});

const passthrough = defineComponent({
	template: '<div><slot /></div>',
});

const toggleStub = defineComponent({
	name: 'QToggleStub',
	props: { modelValue: { type: Boolean, default: false } },
	emits: ['update:modelValue'],
	template: '<button v-bind="$attrs" @click="$emit(\'update:modelValue\', !modelValue)" />',
});

describe('MediaOptionsDialog close visibility event', () => {
	let pinia: Pinia;
	let wrapper: VueWrapper | undefined;
	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		pinia = createPinia();
		setActivePinia(pinia);
		capture.mockClear();
	});

	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
	});

	async function render() {
		wrapper = await mountSuspended(MediaOptionsDialog, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QCardDialog: cardDialogStub,
					HelpRow: passthrough,
					'q-toggle': toggleStub,
				},
			},
		});
		return wrapper;
	}

	test('Should emit visibilityChanged when an owned or offline setting changes', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const rendered = await render();
		const cardDialog = rendered.findComponent(cardDialogStub);
		cardDialog.vm.$emit('opened');
		settingsStore.generalSettings.hideMediaFromOwnedServers = true;
		await rendered.findAllComponents(toggleStub)[0]!.vm.$emit('update:modelValue', true);
		// Act
		cardDialog.vm.$emit('closed');

		// Assert
		expect(rendered.emitted('closed')).toEqual([[true, true]]);
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendSettingsInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.General,
		});
	});

	test('Should capture each committed general toggle immediately', async () => {
		// Arrange
		const rendered = await render();
		const toggles = rendered.findAllComponents(toggleStub);

		// Act
		for (const toggle of toggles)
			await toggle.vm.$emit('update:modelValue', true);

		// Assert
		expect(capture).toHaveBeenCalledTimes(3);
		expect(capture.mock.calls.map(([, payload]) => payload)).toEqual([
			{ action: FrontendTelemetryAction.Update, settings_module: FrontendTelemetrySettingsModule.General },
			{ action: FrontendTelemetryAction.Update, settings_module: FrontendTelemetrySettingsModule.General },
			{ action: FrontendTelemetryAction.Update, settings_module: FrontendTelemetrySettingsModule.General },
		]);
	});

	test('Should not mark poster-only changes as visibility changes', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const rendered = await render();
		const cardDialog = rendered.findComponent(cardDialogStub);
		cardDialog.vm.$emit('opened');
		settingsStore.generalSettings.useLowQualityPosterImages = true;
		await rendered.findAllComponents(toggleStub)[2]!.vm.$emit('update:modelValue', true);
		// Act
		cardDialog.vm.$emit('closed');
		// Assert
		expect(rendered.emitted('closed')).toEqual([[true, false]]);
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendSettingsInteraction, {
			action: FrontendTelemetryAction.Update,
			settings_module: FrontendTelemetrySettingsModule.General,
		});
	});
});
