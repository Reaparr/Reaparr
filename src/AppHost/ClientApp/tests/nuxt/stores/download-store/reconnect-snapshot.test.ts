import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadStatus, PlexMediaType, type DownloadPatchMessagePackDTO, type ServerDownloadProgressDTO } from '@dto';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { DownloadPaths } from '@api-urls';
import { useDownloadStore } from '@store';

const url = DownloadPaths.getAllDownloadTasksEndpoint();
function queue(status = DownloadStatus.Queued, ids = ['current', 'removed']): ServerDownloadProgressDTO[] {
	return [{ id: 7, downloadableTasksCount: ids.length, downloads: ids.map((id) => ({
		id, title: id, status, mediaType: PlexMediaType.Movie, children: [], percentage: 0,
		dataReceived: 0, dataTotal: 100, downloadSpeed: 0, timeRemaining: 0,
	})) }];
}
function patch(sequence: number, status: DownloadStatus): DownloadPatchMessagePackDTO {
	return { serverId: 7, sequence, deletedIds: [], upserts: [{
		id: 'current', parentId: '', status, percentage: 75, dataReceived: 75, dataTotal: 100, downloadSpeed: 0, timeRemaining: 0,
	}] };
}

describe('DownloadStore reconnect snapshots', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should restore missed deletions and keep new-process patches received during reconnect', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO(queue()));
		await subscribeSpyTo(store.setup()).onComplete();
		store.updateDownloadPatch(patch(100, DownloadStatus.Downloading));
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);

		// Act
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(patch(1, DownloadStatus.Completed));
		held.resolve([200, generateResultDTO(queue(DownloadStatus.Paused, ['current']))]);
		await reconnect.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Completed },
		]);
		expect(store.latestPatchSequenceByServer[7]).toBe(1);
		store.updateDownloadPatch(patch(2, DownloadStatus.Stopped));
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Stopped);
	});

	test('Should ignore older snapshot responses and finalizers while retaining patches for the latest request', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO(queue()));
		await subscribeSpyTo(store.setup()).onComplete();
		const old = Promise.withResolvers<[number, unknown]>();
		const current = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).replyOnce(() => old.promise).onGet(url).replyOnce(() => current.promise);

		// Act
		const first = subscribeSpyTo(store.fetchDownloadList());
		const second = subscribeSpyTo(store.fetchDownloadList());
		store.updateDownloadPatch(patch(1, DownloadStatus.Completed));
		old.resolve([200, generateResultDTO(queue(DownloadStatus.Paused))]);
		await first.onComplete();
		store.updateDownloadPatch(patch(2, DownloadStatus.Stopped));
		current.resolve([200, generateResultDTO(queue())]);
		await second.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Stopped);
		expect(store.latestPatchSequenceByServer[7]).toBe(2);
	});

	test('Should retain the queue and live patches after a failed reconnect without retrying automatically', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO(queue()));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		mock.resetHistory();

		// Act
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(patch(1, DownloadStatus.Paused));
		const unknown = patch(2, DownloadStatus.Downloading);
		unknown.upserts[0]!.id = 'created-during-outage';
		store.updateDownloadPatch(unknown);
		held.resolve([503, generateFailedResultDTO({ statusCode: 503 })]);
		await reconnect.onComplete();

		// Assert
		expect(reconnect.getLastValue()?.isSuccess).toBe(false);
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Paused }, { id: 'removed', status: DownloadStatus.Queued },
		]);
		expect(mock.history.get).toHaveLength(1);
		mock.onGet(url).reply(200, generateResultDTO(queue(DownloadStatus.Completed, ['current'])));
		await subscribeSpyTo(store.reconcileAfterReconnect()).onComplete();
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Completed },
		]);
	});

	test('Should reject an old-session snapshot and buffered patches after reset', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO(queue()));
		await subscribeSpyTo(store.setup()).onComplete();
		const old = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).replyOnce(() => old.promise).onGet(url).reply(200, generateResultDTO(queue(DownloadStatus.Paused, ['current'])));
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(patch(100, DownloadStatus.Completed));

		// Act
		store.$reset();
		await subscribeSpyTo(store.fetchDownloadList()).onComplete();
		old.resolve([200, generateResultDTO(queue(DownloadStatus.Downloading))]);
		await reconnect.onComplete();
		store.updateDownloadPatch(patch(1, DownloadStatus.Stopped));

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Stopped },
		]);
		expect(store.latestPatchSequenceByServer[7]).toBe(1);
	});

	test('Should apply a buffered patch to a new task supplied by the snapshot without another lookup', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO(queue()));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		mock.resetHistory();
		const created = patch(1, DownloadStatus.Downloading);
		created.upserts[0]!.id = 'created-during-outage';

		// Act
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(created);
		held.resolve([200, generateResultDTO(queue(DownloadStatus.Queued, ['created-during-outage']))]);
		await reconnect.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'created-during-outage', status: DownloadStatus.Downloading },
		]);
		expect(mock.history.get).toHaveLength(1);
	});
});
