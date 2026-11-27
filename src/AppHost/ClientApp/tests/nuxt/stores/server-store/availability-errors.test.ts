import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO } from '@mock';
import { useAlertStore, useLibraryStore, useServerStore } from '@store';

describe('Availability change rejection feedback', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test.each(['server', 'library'] as const)('Should show the backend explanation when disabling a %s is rejected', async (owner) => {
		// Arrange
		const message = `Complete or remove all downloads before disabling this ${owner}.`;
		const failure = generateFailedResultDTO({ statusCode: 409, errors: [{ message, reasons: [], metadata: {} }] });
		const path = owner === 'server' ? '/api/PlexServer/7/set-server-enabled' : '/api/PlexLibrary/7/set-library-enabled';
		mock.onPut(path).reply(409, failure);
		const server = useServerStore();
		const library = useLibraryStore();
		await subscribeSpyTo(server.setup()).onComplete();
		await subscribeSpyTo(library.setup()).onComplete();

		// Act
		const result = owner === 'server'
			? subscribeSpyTo(server.setServerEnabled(7, false))
			: subscribeSpyTo(library.setLibraryEnabled(7, false));
		await result.onComplete();

		// Assert
		expect(useAlertStore().alerts.map((alert) => alert.text)).toEqual([message]);
		expect(mock.history.put[0]?.handleErrorsLocally).toBe(true);
		expect(mock.history.put[0]?.data).toBe(JSON.stringify({ isEnabled: false }));
	});
});
