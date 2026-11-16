import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { NotificationPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { NotificationLevel, type BaseResultDTO, type NotificationDTO } from '@dto';
import { useNotificationsStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

const success: BaseResultDTO = { isSuccess: true, statusCode: 200, errors: [], successes: [] };

function notification(id: number): NotificationDTO {
	return { id, level: NotificationLevel.Information, createdAt: '2026-10-19T00:00:00Z', message: `Notification ${id}`, hidden: false };
}

describe('NotificationsStore telemetry intents', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should capture a deliberate notification dismissal with stable target context', async () => {
		// Arrange
		const store = useNotificationsStore();
		store.notifications = [notification(7)];
		mock.onPatch(NotificationPaths.setNotificationVisibilityEndpoint()).reply(200, success);

		// Act
		await subscribeSpyTo(store.hideNotification(7)).onComplete();

		// Assert
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendNotificationInteraction, {
			action: FrontendTelemetryAction.Dismiss,
			notification_id: 7,
		});
		expect(JSON.stringify(capture.mock.calls)).not.toContain('Notification 7');
	});

	test('Should capture a deliberate clear-all notification action without forwarding rows', async () => {
		// Arrange
		const store = useNotificationsStore();
		store.notifications = [notification(8), notification(9)];
		mock.onDelete(NotificationPaths.clearAllNotificationsEndpoint()).reply(200, generateResultDTO({ count: 2 }));

		// Act
		await subscribeSpyTo(store.clearAllNotifications()).onComplete();

		// Assert
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendNotificationInteraction, {
			action: FrontendTelemetryAction.ClearAll,
		});
		expect(JSON.stringify(capture.mock.calls)).not.toContain('Notification 8');
		expect(JSON.stringify(capture.mock.calls)).not.toContain('Notification 9');
	});
});
