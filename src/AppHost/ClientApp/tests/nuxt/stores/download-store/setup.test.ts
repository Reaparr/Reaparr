import { describe, beforeAll, test, expect, beforeEach } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { subscribeSpyTo, baseSetup, getAxiosMock, baseVars } from '@services-test-base';
import { generateResultDTO } from '@mock';
import { DownloadPaths } from '@api/api-paths';
import { StoreNames, type ISetupResult } from '@interfaces';
import { useDownloadStore } from '@store';

describe('DownloadStore.setup()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should return success and complete when setup is run', async () => {
		// Arrange
		const downloadStore = useDownloadStore();
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		mock.onGet(DownloadPaths.getDownloadTaskByGuidEndpoint('*')).reply(200, generateResultDTO([]));
		const setupResult: ISetupResult = {
			isSuccess: true,
			name: StoreNames.DownloadStore,
		};

		// Act
		const result = subscribeSpyTo(downloadStore.setup());
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toEqual(setupResult);
		expect(result.receivedComplete()).toEqual(true);
	});
});
