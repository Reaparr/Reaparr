import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { AuthenticationPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { useAuthenticationStore } from '@store';

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
		}));

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(false);
	});

	test('Should set isLoggedIn to false when endpoint response is unsuccessful', async () => {
		// Arrange
		const authenticationStore = useAuthenticationStore();
		authenticationStore.isLoggedIn = true;
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, {
			isSuccess: false,
			value: {
				claims: [],
				isLoggedIn: true,
				userName: 'guest',
			},
			statusCode: 401,
			errors: [],
			successes: [],
		});

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(false);
	});

	test('Should set isLoggedIn to true when status endpoint returns authenticated user', async () => {
		// Arrange
		const authenticationStore = useAuthenticationStore();
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO({
			claims: [],
			isLoggedIn: true,
			userName: 'jason',
		}));

		// Act
		await subscribeSpyTo(authenticationStore.status()).onComplete();

		// Assert
		expect(authenticationStore.isLoggedIn).toEqual(true);
	});
});
