import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { of } from 'rxjs';
import { baseSetup, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO } from '@mock';
import { plexServerApi } from '@api';
import { useServerStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('ServerStore telemetry intents', () => {
	beforeAll(() => baseSetup());

	beforeEach(() => {
		setActivePinia(createPinia());
		capture.mockClear();
		vi.restoreAllMocks();
	});

	test('Should capture server configuration and control actions without alias values', async () => {
		// Arrange
		const store = useServerStore();
		const failed = () => of(generateFailedResultDTO({ statusCode: 400 }));
		vi.spyOn(plexServerApi, 'setServerAlias').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'setServerEnabledEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'setServerOwnedEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'deletePlexServerEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'pausePlexServerDownloadsEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'resumePlexServerDownloadsEndpoint').mockImplementation(failed);

		// Act
		await subscribeSpyTo(store.setServerAlias(7, 'private server')).onComplete();
		await subscribeSpyTo(store.setServerEnabled(7, false)).onComplete();
		await subscribeSpyTo(store.setServerOwned(7, true)).onComplete();
		await subscribeSpyTo(store.deleteServer(7)).onComplete();
		await subscribeSpyTo(store.setServerPaused(7, true)).onComplete();
		await subscribeSpyTo(store.setServerPaused(7, false)).onComplete();

		// Assert
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.SetAlias,
				server_id: 7,
			}],
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.Disable,
				server_id: 7,
				is_enabled: false,
			}],
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.SetOwned,
				server_id: 7,
				owned: true,
			}],
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.Delete,
				server_id: 7,
			}],
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.Pause,
				server_id: 7,
				paused: true,
			}],
			[FrontendTelemetryEvent.FrontendServerInteraction, {
				action: FrontendTelemetryAction.Resume,
				server_id: 7,
				paused: false,
			}],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain('private server');
	});
});
