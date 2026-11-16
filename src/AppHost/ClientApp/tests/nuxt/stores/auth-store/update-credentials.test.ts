import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { AuthenticationPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { useAuthenticationStore } from '@store';

import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AuthenticationStore.updateCredentials()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should retain a failed save and draft then refresh credentials after a corrected retry', async () => {
		// Arrange
		const store = useAuthenticationStore();
		const original = { userName: 'ExistingUser22', password: 'original-password-mask', isDefaultCredentials: false };
		const updated = { userName: 'RecoveredUser22', password: 'updated-password-mask', isDefaultCredentials: false };
		const failure = generateFailedResultDTO({
			statusCode: 400,
			errors: [{ message: 'Username contains an invalid character', reasons: [], metadata: {} }],
		});
		let saved = false;
		mock.onGet(AuthenticationPaths.getAppCredentials()).reply(() => [200, generateResultDTO(saved ? updated : original)]);
		mock.onPut(AuthenticationPaths.updateCredentialsEndpoint()).replyOnce(400, failure);
		mock.onPut(AuthenticationPaths.updateCredentialsEndpoint()).reply(() => {
			saved = true;
			return [200, generateResultDTO(null)];
		});
		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.refreshCredentials()).onComplete();
		store.username = 'Invalid Username';
		store.password = 'Updated-password1!';
		store.confirmPassword = store.password;

		// Act
		const failedSave = subscribeSpyTo(store.updateCredentials());
		await failedSave.onComplete();

		// Assert
		expect(failedSave.getLastValue().isSuccess).toBe(false);
		expect(failedSave.getLastValue().errors).toEqual(failure.errors);
		expect(store.currentUsername).toBe(original.userName);
		expect(store.currentPassword).toBe(original.password);
		expect(store.username).toBe('Invalid Username');
		expect(store.password).toBe('Updated-password1!');
		expect(store.confirmPassword).toBe('Updated-password1!');

		// Act
		store.username = updated.userName;
		const retry = subscribeSpyTo(store.updateCredentials());
		await retry.onComplete();

		// Assert
		expect(retry.getLastValue().isSuccess).toBe(true);
		expect(store.currentUsername).toBe(updated.userName);
		expect(store.username).toBe(updated.userName);
		expect(store.currentPassword).toBe(updated.password);
		expect(store.password).toBe(updated.password);
		expect(store.confirmPassword).toBe('');
		expect(store.hasUsernameChanged).toBe(false);
		expect(store.hasPasswordChanged).toBe(false);
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendAuthenticationInteraction, {
				action: FrontendTelemetryAction.Update, username_changed: true, password_changed: true,
			}],
			[FrontendTelemetryEvent.FrontendAuthenticationInteraction, {
				action: FrontendTelemetryAction.Update, username_changed: true, password_changed: true,
			}],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain(original.password);
		expect(JSON.stringify(capture.mock.calls)).not.toContain(updated.password);
		expect(JSON.stringify(capture.mock.calls)).not.toContain(updated.userName);
	});

	test('Should emit no edit intention or write when credentials are unchanged', async () => {
		// Arrange
		const store = useAuthenticationStore();
		await subscribeSpyTo(store.setup()).onComplete();

		// Act
		const result = subscribeSpyTo(store.updateCredentials());
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toBeNull();
		expect(capture).not.toHaveBeenCalled();
		expect(mock.history.put).toEqual([]);
	});
});
