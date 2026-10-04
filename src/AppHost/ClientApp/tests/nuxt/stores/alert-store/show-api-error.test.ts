import { describe, beforeAll, beforeEach, afterEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import type { IApiError } from '@interfaces';
import { useAlertStore } from '@store';

const apiError = (overrides: Partial<IApiError> = {}): IApiError => ({
	method: 'GET',
	url: 'http://localhost:3030/api/Movies?libraryId=1',
	statusCode: 500,
	code: 'MOVIE_LOOKUP_FAILED',
	message: 'Movie lookup failed',
	...overrides,
});

describe('AlertStore.showApiError()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(async () => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		await subscribeSpyTo(useAlertStore().setup()).onComplete();
	});

	afterEach(() => {
		mock.restore();
	});

	test('Should merge only failures with the same method, full URL, status, and code', () => {
		// Arrange
		const alertStore = useAlertStore();
		const failures = [
			apiError(),
			apiError({ method: 'POST' }),
			apiError({ url: 'http://localhost:3030/api/Movies?libraryId=2' }),
			apiError({ statusCode: 400 }),
			apiError({ code: 'DIFFERENT_CODE' }),
		];

		// Act
		failures.forEach((failure) => alertStore.showApiError(failure));
		alertStore.showApiError(apiError({ message: 'A repeated failure with different display text' }));

		// Assert
		expect(alertStore.alerts).toHaveLength(1);
		expect(alertStore.alerts[0]?.apiErrors).toEqual(failures);
		expect(alertStore.alerts[0]?.hasOmittedApiErrors).toBe(false);
	});

	test('Should retain the first ten distinct failures and flag later distinct failures as omitted', () => {
		// Arrange
		const alertStore = useAlertStore();
		const firstTen = Array.from({ length: 10 }, (_, index) => apiError({
			url: `http://localhost:3030/api/Movies/${index}`,
			message: `Failure ${index}`,
		}));

		// Act
		firstTen.forEach((failure) => alertStore.showApiError(failure));
		alertStore.showApiError(apiError({
			url: 'http://localhost:3030/api/Movies/10',
			message: 'Omitted failure',
		}));

		// Assert
		expect(alertStore.alerts).toHaveLength(1);
		expect(alertStore.alerts[0]?.apiErrors).toEqual(firstTen);
		expect(alertStore.alerts[0]?.hasOmittedApiErrors).toBe(true);
	});

	test('Should not flag omission when a duplicate arrives after the list reaches capacity', () => {
		// Arrange
		const alertStore = useAlertStore();
		const firstTen = Array.from({ length: 10 }, (_, index) => apiError({
			url: `http://localhost:3030/api/Movies/${index}`,
		}));
		firstTen.forEach((failure) => alertStore.showApiError(failure));

		// Act
		alertStore.showApiError(firstTen[4]!);

		// Assert
		expect(alertStore.alerts[0]?.apiErrors).toEqual(firstTen);
		expect(alertStore.alerts[0]?.hasOmittedApiErrors).toBe(false);
	});

	test('Should clear the aggregate on removal and reopen it for a repeated failure', () => {
		// Arrange
		const alertStore = useAlertStore();
		const failure = apiError();
		alertStore.showApiError(failure);

		// Act
		alertStore.removeAlert(-1);
		alertStore.showApiError(failure);

		// Assert
		expect(alertStore.alerts).toHaveLength(1);
		expect(alertStore.alerts[0]?.id).toBe(-1);
		expect(alertStore.alerts[0]?.apiErrors).toEqual([failure]);
		expect(alertStore.alerts[0]?.hasOmittedApiErrors).toBe(false);
	});

	test('Should clear aggregate state on reset and accept the next failure', () => {
		// Arrange
		const alertStore = useAlertStore();
		for (let index = 0; index < 11; index += 1) {
			alertStore.showApiError(apiError({ url: `http://localhost:3030/api/Movies/${index}` }));
		}

		// Act
		alertStore.$reset();
		alertStore.showApiError(apiError());

		// Assert
		expect(alertStore.alerts).toHaveLength(1);
		expect(alertStore.alerts[0]?.apiErrors).toEqual([apiError()]);
		expect(alertStore.alerts[0]?.hasOmittedApiErrors).toBe(false);
	});

	test('Should coexist with legacy alerts without replacing their local feedback', () => {
		// Arrange
		const alertStore = useAlertStore();
		alertStore.showAlert({ id: 0, title: 'Validation failed', text: 'Fix the highlighted field' });

		// Act
		alertStore.showApiError(apiError());
		alertStore.showApiError(apiError({ statusCode: 400 }));

		// Assert
		expect(alertStore.alerts).toHaveLength(2);
		expect(alertStore.alerts.find((alert) => alert.id !== -1)).toMatchObject({
			title: 'Validation failed',
			text: 'Fix the highlighted field',
		});
		expect(alertStore.alerts.find((alert) => alert.id === -1)?.apiErrors).toHaveLength(2);
	});
});
