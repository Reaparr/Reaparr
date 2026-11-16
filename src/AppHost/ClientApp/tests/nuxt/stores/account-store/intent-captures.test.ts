import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths } from '@api/api-paths';
import { generateFailedResultDTO } from '@mock';
import { useAccountStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AccountStore telemetry intents', () => {
	let { mock } = baseVars();

	beforeAll(() => baseSetup());

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should capture deliberate account access refresh with account context only', async () => {
		// Arrange
		const store = useAccountStore();
		mock.onGet(PlexAccountPaths.refreshPlexAccountAccessEndpoint(42)).reply(400, generateFailedResultDTO());

		// Act
		await subscribeSpyTo(store.reSyncAccount(42)).onComplete();

		// Assert
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendAccountInteraction, {
			action: FrontendTelemetryAction.Refresh,
			account_id: 42,
		});
	});
});
