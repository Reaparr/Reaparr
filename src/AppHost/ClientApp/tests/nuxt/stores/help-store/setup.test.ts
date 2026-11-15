import { describe, beforeAll, test, expect, beforeEach, vi } from 'vitest';

import { createPinia, setActivePinia } from 'pinia';
import { subscribeSpyTo, baseSetup, baseVars } from '@services-test-base';
import { StoreNames, type ISetupResult } from '@interfaces';
import { useHelpStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('HelpStore.setup()', () => {
	baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should return success and complete when setup is run', async () => {
		// Arrange
		const helpStore = useHelpStore();

		const setupResult: ISetupResult = {
			isSuccess: true,
			name: StoreNames.HelpStore,
		};

		// Act
		const result = subscribeSpyTo(helpStore.setup());
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toEqual(setupResult);
		expect(result.receivedComplete()).toEqual(true);
	});

	test('Should capture help opening without forwarding translated help content', () => {
		// Arrange
		const helpStore = useHelpStore();

		// Act
		helpStore.openHelpDialog({ label: 'sensitive-label', title: 'sensitive-title', text: 'sensitive-help' });

		// Assert
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendHelpInteraction, {
			action: FrontendTelemetryAction.Open,
		});
		expect(JSON.stringify(capture.mock.calls)).not.toContain('sensitive-help');
	});
});
