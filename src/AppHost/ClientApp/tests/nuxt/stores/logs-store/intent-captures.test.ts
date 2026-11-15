import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup } from '@services-test-base';
import { useLogsStore } from '@store';
import { LogSeverity, SortDirection } from '@dto';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('LogsStore telemetry intents', () => {
	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should capture deliberate log toolbar intents without dynamic log text', () => {
		// Arrange
		const store = useLogsStore();

		// Act
		store.setSearch('secret log search');
		store.setSelectedLevels([LogSeverity.Error]);
		store.toggleSortDirection();
		store.clearSearch();
		store.clearLogs();

		// Assert
		expect(store.sortDirection).toBe(SortDirection.Desc);
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendLogInteraction, { action: FrontendTelemetryAction.Search, has_query: true }],
			[FrontendTelemetryEvent.FrontendLogInteraction, { action: FrontendTelemetryAction.SetLevel, level_count: 1 }],
			[FrontendTelemetryEvent.FrontendLogInteraction, { action: FrontendTelemetryAction.Sort, sort_direction: SortDirection.Desc }],
			[FrontendTelemetryEvent.FrontendLogInteraction, { action: FrontendTelemetryAction.ClearSearch }],
			[FrontendTelemetryEvent.FrontendLogInteraction, { action: FrontendTelemetryAction.ClearLogs }],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain('secret log search');
	});
});
