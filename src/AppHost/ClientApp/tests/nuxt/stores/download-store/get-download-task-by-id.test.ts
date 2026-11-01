import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadPaths } from '@api/api-paths';
import { generateResultDTO, Seed, generateServerDownloadProgress } from '@mock';
import { useDownloadStore } from '@store';

describe('DownloadStore.getDownloadTaskById()', () => {
	let { mock, config } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should return the queued download task when the task exists in the loaded server downloads', async () => {
		// Arrange
		config = {
			seed: 263,
			plexServerCount: 1,
			movieDownloadTask: 1,
		};
		const downloadStore = useDownloadStore();
		const serverDownloads = [generateServerDownloadProgress({
			plexServerId: 1,
			plexLibraryId: -1,
			config,
			seed: new Seed(config.seed!),
		})];
		const queuedDownloadTask = serverDownloads[0]!.downloads[0]!;
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: serverDownloads, patchSequenceByServer: {} }));

		// Act
		await subscribeSpyTo(downloadStore.setup()).onComplete();
		const result = downloadStore.getDownloadTaskById(queuedDownloadTask.id);

		// Assert
		expect(result).toEqual(queuedDownloadTask);
		expect(result?.status).toEqual('Queued');
	});
});
