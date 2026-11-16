import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { of } from 'rxjs';
import { baseSetup, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO } from '@mock';
import { plexServerApi, plexServerConnectionApi } from '@api';
import { useServerConnectionStore } from '@store';
import type { CreatePlexServerConnectionEndpointRequest, UpdatePlexServerConnectionEndpointRequest } from '@dto';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('ServerConnectionStore telemetry intents', () => {
	beforeAll(() => baseSetup());

	beforeEach(() => {
		setActivePinia(createPinia());
		capture.mockClear();
		vi.restoreAllMocks();
	});

	test('Should capture connection CRUD and preferred actions without connection URLs', async () => {
		// Arrange
		const store = useServerConnectionStore();
		const failed = () => of(generateFailedResultDTO({ statusCode: 400 }));
		const create: CreatePlexServerConnectionEndpointRequest = {
			address: 'private.example.test',
			plexServerId: 7,
			port: 32400,
			protocol: 'https',
			url: 'https://private.example.test:32400',
		};
		const update: UpdatePlexServerConnectionEndpointRequest = { ...create, id: 11 };
		vi.spyOn(plexServerConnectionApi, 'createPlexServerConnectionEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerConnectionApi, 'updatePlexServerConnectionEndpoint').mockImplementation(failed);
		vi.spyOn(plexServerConnectionApi, 'deletePlexServerConnectionById').mockImplementation(failed);
		vi.spyOn(plexServerApi, 'setPreferredPlexServerConnectionEndpoint').mockImplementation(failed);

		// Act
		await subscribeSpyTo(store.createServerConnection(create)).onComplete();
		await subscribeSpyTo(store.updateServerConnection(update)).onComplete();
		await subscribeSpyTo(store.deleteServerConnection(11)).onComplete();
		await subscribeSpyTo(store.setPreferredPlexServerConnection(7, 11)).onComplete();

		// Assert
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendConnectionInteraction, {
				action: FrontendTelemetryAction.Create,
				server_id: 7,
			}],
			[FrontendTelemetryEvent.FrontendConnectionInteraction, {
				action: FrontendTelemetryAction.Update,
				server_id: 7,
				connection_id: 11,
			}],
			[FrontendTelemetryEvent.FrontendConnectionInteraction, {
				action: FrontendTelemetryAction.Delete,
				connection_id: 11,
			}],
			[FrontendTelemetryEvent.FrontendConnectionInteraction, {
				action: FrontendTelemetryAction.SetPreferred,
				server_id: 7,
				connection_id: 11,
			}],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain('private.example.test');
		expect(JSON.stringify(capture.mock.calls)).not.toContain('https://');
	});
});
