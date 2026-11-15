import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { defineComponent } from 'vue';
import { of } from 'rxjs';
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import type { VueWrapper } from '@vue/test-utils';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import EnableServersSection from '@/components/Views/Settings/EnableServersSection.vue';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';
import messages from '@/lang/en-US.json';

const { capture, serverStore } = vi.hoisted(() => ({
	capture: vi.fn(),
	serverStore: {
		getDisabledServers: [{ id: 42 }],
		getServerName: vi.fn(() => 'Test server'),
		setServerEnabled: vi.fn(),
	},
}));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});
mockNuxtImport('useServerStore', () => () => serverStore);

const passthrough = defineComponent({
	template: '<div><slot /></div>',
});

const iconButtonStub = defineComponent({
	name: 'IconSquareButton',
	emits: ['click'],
	template: '<button data-cy="enable-server" @click="$emit(\'click\')" />',
});

describe('EnableServersSection committed settings intents', () => {
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
		serverStore.getServerName.mockClear();
		serverStore.setServerEnabled.mockImplementation((serverId: number, isEnabled: boolean) => {
			capture(FrontendTelemetryEvent.FrontendServerInteraction, {
				action: isEnabled ? FrontendTelemetryAction.Enable : FrontendTelemetryAction.Disable,
				server_id: serverId,
				is_enabled: isEnabled,
			});
			return of({ isSuccess: true });
		});
	});

	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
		mock.restore();
	});

	test('Should capture one server enable intent when a disabled server is enabled', async () => {
		// Arrange
		wrapper = await mountSuspended(EnableServersSection, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QSection: passthrough,
					QAlert: passthrough,
					'q-list': passthrough,
					'q-item': passthrough,
					'q-item-section': passthrough,
					'q-item-label': passthrough,
					IconSquareButton: iconButtonStub,
				},
			},
		});

		// Act
		await wrapper.get('[data-cy="enable-server"]').trigger('click');

		// Assert
		expect(serverStore.setServerEnabled).toHaveBeenCalledExactlyOnceWith(42, true);
		expect(capture).toHaveBeenCalledExactlyOnceWith(FrontendTelemetryEvent.FrontendServerInteraction, {
			action: FrontendTelemetryAction.Enable,
			server_id: 42,
			is_enabled: true,
		});
	});
});
