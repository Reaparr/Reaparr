import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { of } from 'rxjs';
import { baseSetup, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO } from '@mock';
import { plexLibraryApi } from '@api';
import { useLibraryStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('LibraryStore telemetry intents', () => {
	beforeAll(() => baseSetup());

	beforeEach(() => {
		setActivePinia(createPinia());
		capture.mockClear();
		vi.restoreAllMocks();
	});

	test('Should capture enabled and destination choices without library or folder labels', async () => {
		// Arrange
		const store = useLibraryStore();
		const failed = () => of(generateFailedResultDTO({ statusCode: 400 }));
		vi.spyOn(plexLibraryApi, 'setLibraryEnabledEndpoint').mockImplementation(failed);
		vi.spyOn(plexLibraryApi, 'setPlexLibraryDefaultDestinationByIdEndpoint').mockImplementation(failed);

		// Act
		await subscribeSpyTo(store.setLibraryEnabled(11, false)).onComplete();
		store.updateDefaultDestination(11, 22);

		// Assert
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendLibraryInteraction, {
				action: FrontendTelemetryAction.Disable,
				library_id: 11,
				is_enabled: false,
			}],
			[FrontendTelemetryEvent.FrontendLibraryInteraction, {
				action: FrontendTelemetryAction.SetDestination,
				library_id: 11,
				folder_path_id: 22,
			}],
		]);
	});
});
