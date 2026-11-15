import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { useAccountDialogStore } from '@store';
import type { GeneratePlexTokenResponse } from '@dto';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AccountDialogStore.generateToken() telemetry', () => {
	let { mock } = baseVars();

	beforeAll(() => baseSetup());

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should capture one generation intention and exclude token and verification code values', async () => {
		// Arrange
		const store = useAccountDialogStore();
		store.id = 42;
		const response = generateResultDTO<GeneratePlexTokenResponse>({
			isUnAuthorized: false,
			needsVerificationCode: false,
			plexAuthToken: 'secret-generated-token',
		});
		mock.onGet(PlexAccountPaths.generatePlexTokenEndpoint(42)).reply(200, response);

		// Act
		await subscribeSpyTo(store.generateToken()).onComplete();

		// Assert
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendAccountInteraction, {
			action: FrontendTelemetryAction.GenerateToken,
			account_id: 42,
		});
		expect(JSON.stringify(capture.mock.calls)).not.toContain('secret-generated-token');
		expect(JSON.stringify(capture.mock.calls)).not.toContain('123456');

		// Act: the verification-code retry is part of the same generation intention.
		capture.mockClear();
		await subscribeSpyTo(store.generateToken('123456')).onComplete();

		// Assert
		expect(capture).not.toHaveBeenCalled();
	});
});
