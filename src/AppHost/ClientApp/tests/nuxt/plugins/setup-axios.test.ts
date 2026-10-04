import Log from 'consola';
import Axios from 'axios';
import { describe, beforeAll, beforeEach, afterEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { Router } from 'vue-router';
import type { BaseResultDTO } from '@dto';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useAlertStore } from '@store';
import { axiosObservable, apiCheckPipe } from '@/types/api/base/ApiPipes';
import { setupAxios } from '@/plugins/3.boot.client';

const notificationMocks = vi.hoisted(() => ({
	showErrorNotification: vi.fn(),
}));

vi.mock('@composables/notification', () => notificationMocks);

describe('setupAxios()', () => {
	let { mock, appConfig } = baseVars();
	let interceptorId: number | undefined;
	let observerInterceptorId: number | undefined;
	let router: Router;
	const translate = (key: string) => `translated:${key}`;

	beforeAll(() => {
		({ appConfig } = baseSetup());
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		notificationMocks.showErrorNotification.mockClear();
		router = { push: vi.fn() } as unknown as Router;
		const useSpy = vi.spyOn(Axios.interceptors.response, 'use');
		setupAxios(appConfig, router, translate);
		interceptorId = useSpy.mock.results[0]?.value;
		useSpy.mockRestore();
	});

	afterEach(() => {
		if (interceptorId !== undefined) {
			Axios.interceptors.response.eject(interceptorId);
		}
		if (observerInterceptorId !== undefined) {
			Axios.interceptors.response.eject(observerInterceptorId);
			observerInterceptorId = undefined;
		}
		mock.restore();
		vi.restoreAllMocks();
	});

	test.each([
		[400, 'The request was invalid'],
		[500, 'The backend failed'],
	])('Should report backend HTTP %i details while preserving the caller result', async (status, message) => {
		// Arrange
		const alertStore = useAlertStore();
		mock.onGet('/api/ErrorPipeline').reply(status, {
			isSuccess: false,
			statusCode: status,
			errors: [{ message, reasons: [], metadata: {} }],
			successes: [],
		});

		// Act
		const result = subscribeSpyTo(axiosObservable<BaseResultDTO>({ method: 'GET', url: '/api/ErrorPipeline' }).pipe(apiCheckPipe));
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toMatchObject({ isSuccess: false, statusCode: status });
		expect(alertStore.alerts).toHaveLength(1);
		expect(alertStore.alerts[0]?.apiErrors).toEqual([
			expect.objectContaining({
				method: 'GET',
				url: 'http://localhost:3030/api/ErrorPipeline',
				statusCode: status,
				message,
			}),
		]);
	});

	test('Should redirect a 401 rejection without opening global feedback', async () => {
		// Arrange
		const alertStore = useAlertStore();
		mock.onGet('/api/Secure').reply(401, { message: 'Unauthorized' });

		// Act
		const caught = await Axios.get('/api/Secure').catch((error) => error);

		// Assert
		expect(caught.response.status).toBe(401);
		expect(router.push).toHaveBeenCalledWith('/login');
		expect(alertStore.alerts).toEqual([]);
		expect(notificationMocks.showErrorNotification).not.toHaveBeenCalled();
	});

	test.each([
		['network failure', () => mock.onGet('/api/Offline').networkError()],
		['timeout', () => mock.onGet('/api/Offline').timeout()],
	])('Should show only a translated connectivity toast for a %s', async (_name, arrangeFailure) => {
		// Arrange
		const alertStore = useAlertStore();
		arrangeFailure();

		// Act
		await Axios.get('/api/Offline').catch((error) => error);

		// Assert
		expect(notificationMocks.showErrorNotification).toHaveBeenCalledWith(
			'translated:components.alert-dialog.connection-failed',
		);
		expect(alertStore.alerts).toEqual([]);
	});

	test('Should keep intentional cancellation silent', async () => {
		// Arrange
		const alertStore = useAlertStore();
		const controller = new AbortController();
		controller.abort();

		// Act
		const caught = await Axios.get('/api/Cancelled', { signal: controller.signal }).catch((error) => error);

		// Assert
		expect(Axios.isCancel(caught)).toBe(true);
		expect(alertStore.alerts).toEqual([]);
		expect(notificationMocks.showErrorNotification).not.toHaveBeenCalled();
	});

	test('Should leave a 2xx failed domain result to local handling', async () => {
		// Arrange
		const alertStore = useAlertStore();
		const domainFailure = {
			isSuccess: false,
			statusCode: 200,
			errors: [{ message: 'Local validation failed', reasons: [], metadata: {} }],
			successes: [],
		};
		mock.onPost('/api/Validate').reply(200, domainFailure);

		// Act
		const result = subscribeSpyTo(axiosObservable<BaseResultDTO>({ method: 'POST', url: '/api/Validate' }).pipe(apiCheckPipe));
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toEqual(domainFailure);
		expect(alertStore.alerts).toEqual([]);
		expect(notificationMocks.showErrorNotification).not.toHaveBeenCalled();
	});

	test.each([
		['an external origin', 'https://example.com/api/Failure'],
		['a backend non-API path', '/health'],
	])('Should exclude HTTP failures from %s', async (_name, url) => {
		// Arrange
		const alertStore = useAlertStore();
		mock.onGet(url).reply(500, { message: 'Excluded failure' });

		// Act
		const caught = await Axios.get(url).catch((error) => error);

		// Assert
		expect(caught.response.status).toBe(500);
		expect(alertStore.alerts).toEqual([]);
		expect(notificationMocks.showErrorNotification).not.toHaveBeenCalled();
	});

	test('Should reject with the same error object after global reporting', async () => {
		// Arrange
		mock.onDelete('/api/Failure').reply(500, { message: 'Delete failed' });
		let interceptorError: unknown;
		const observerId = Axios.interceptors.response.use(undefined, (error) => {
			interceptorError = error;
			return Promise.reject(error);
		});
		observerInterceptorId = observerId;

		// Act
		const caught = await Axios.delete('/api/Failure').catch((error) => error);
		Axios.interceptors.response.eject(observerId);
		observerInterceptorId = undefined;

		// Assert
		expect(caught).toBe(interceptorError);
		expect(caught.response.status).toBe(500);
	});

	test('Should preserve API error logging after the interceptor reports the failure', async () => {
		// Arrange
		const logSpy = vi.spyOn(Log, 'error').mockImplementation(() => undefined);
		mock.onGet('/api/LoggedFailure').reply(500, { message: 'Logged failure' });

		// Act
		const result = subscribeSpyTo(axiosObservable<BaseResultDTO>({ method: 'GET', url: '/api/LoggedFailure' }).pipe(apiCheckPipe));
		await result.onComplete();

		// Assert
		expect(logSpy).toHaveBeenCalledWith('Error in API call', expect.anything());
		expect(useAlertStore().alerts).toHaveLength(1);
	});
});
