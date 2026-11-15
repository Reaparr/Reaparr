import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { AuthenticationPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { useAuthenticationStore } from '@store';
import type { BrowserTelemetryBootstrapDTO } from '@dto';

const bootstrap: BrowserTelemetryBootstrapDTO = {
	apiHost: 'https://telemetry.example.test',
	projectToken: 'phc_test',
	distinctId: '5e2c5c48-aa0f-4f21-8c65-94e72b2bd389',
	release: '0.41.0-test',
	environment: 'development',
	runtimeMode: 'docker',
	runtimeIdentifier: 'linux-x64',
	platform: 'Linux',
};

describe('AuthenticationStore.status()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should set isLoggedIn to false when status endpoint returns unauthenticated user', async () => {
		// Arrange
		const authenticationStore = useAuthenticationStore();
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO({
			claims: [],
			isLoggedIn: false,
			userName: 'guest',
			telemetry: bootstrap,
		}));

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(false);
		expect(authenticationStore.telemetryBootstrap).toBeNull();
	});

	test('Should set isLoggedIn to false when endpoint response is unsuccessful', async () => {
		// Arrange
		const authenticationStore = useAuthenticationStore();
		authenticationStore.isLoggedIn = true;
		authenticationStore.telemetryBootstrap = bootstrap;
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, {
			isSuccess: false,
			value: {
				claims: [],
				isLoggedIn: true,
				userName: 'guest',
				telemetry: bootstrap,
			},
			statusCode: 401,
			errors: [],
			successes: [],
		});

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(false);
		expect(authenticationStore.telemetryBootstrap).toBeNull();
	});

	test('Should set isLoggedIn to true when status endpoint returns authenticated user', async () => {
		// Arrange
		const authenticationStore = useAuthenticationStore();
		authenticationStore.telemetryBootstrap = bootstrap;
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO({
			claims: [],
			isLoggedIn: true,
			userName: 'jason',
		}));

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(true);
		expect(authenticationStore.telemetryBootstrap).toBeNull();
	});

	test('Should retain trusted bootstrap only from a successful authenticated status response', async () => {
		// Arrange
		const store = useAuthenticationStore();
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO({
			claims: [],
			isLoggedIn: true,
			userName: 'test-user',
			telemetry: bootstrap,
		}));

		// Act
		const result = subscribeSpyTo(store.status());
		await result.onComplete();

		// Assert
		expect(result.getLastValue().isSuccess).toBe(true);
		expect(store.isLoggedIn).toBe(true);
		expect(store.telemetryBootstrap).toEqual(bootstrap);
	});
});
