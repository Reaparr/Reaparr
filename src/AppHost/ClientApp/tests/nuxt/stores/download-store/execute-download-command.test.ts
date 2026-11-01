import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadActions, DownloadStatus, PlexMediaType, type ServerDownloadProgressDTO } from '@dto';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { DownloadPaths } from '@api-urls';
import { useDownloadStore } from '@store';

const commands = [
	{ name: 'Delete', action: DownloadActions.Delete, ids: ['retained'], url: DownloadPaths.deleteDownloadTaskEndpoint() },
	{ name: 'Clear selected', action: DownloadActions.Clear, ids: ['retained'], url: DownloadPaths.clearCompletedDownloadTasksByDownloadTaskIdEndpoint() },
	{ name: 'Clear server', action: DownloadActions.Clear, ids: [], url: DownloadPaths.clearCompletedDownloadTasksByServerIdEndpoint(7) },
];

function queue(): ServerDownloadProgressDTO[] {
	return [{ id: 7, downloadableTasksCount: 1, downloads: [{
		id: 'retained', title: 'Completed movie', mediaType: PlexMediaType.Movie, status: DownloadStatus.Completed,
		children: [], percentage: 100, dataReceived: 100, dataTotal: 100, downloadSpeed: 0, timeRemaining: 0,
	}] }];
}

describe('DownloadStore.executeDownloadCommand()', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test.each(commands)('Should preserve the failure and queue when $name fails', async ({ action, ids, url }) => {
		// Arrange
		const store = useDownloadStore();
		const retained = queue();
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: retained, patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const failure = generateFailedResultDTO({ statusCode: 503, errors: [{ message: 'Mutation unavailable', reasons: [], metadata: {} }] });
		mock.onDelete(url).reply(503, failure);
		// A successful refresh must never replace the failed mutation result.
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		mock.resetHistory();

		// Act
		const result = subscribeSpyTo(store.executeDownloadCommand(action, ids, 7));
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toMatchObject({ isSuccess: false, statusCode: 503, errors: failure.errors });
		expect(store.serverDownloads).toEqual(retained);
		expect(mock.history.get).toHaveLength(0);
	});

	test.each(commands)('Should refresh the visible queue after $name succeeds', async ({ action, ids, url }) => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		mock.onDelete(url).reply(200, generateResultDTO({ count: 1 }));
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		mock.resetHistory();

		// Act
		const result = subscribeSpyTo(store.executeDownloadCommand(action, ids, 7));
		await result.onComplete();

		// Assert
		expect(result.getLastValue()?.isSuccess).toBe(true);
		expect(store.serverDownloads).toEqual([]);
		expect(mock.history.get).toHaveLength(1);
	});
});
