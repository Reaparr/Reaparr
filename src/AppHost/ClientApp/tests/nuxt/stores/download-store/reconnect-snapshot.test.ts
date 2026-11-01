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
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		store.updateDownloadPatch(patch(100, DownloadStatus.Downloading));
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);

		// Act
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(patch(1, DownloadStatus.Completed));
		held.resolve([200, generateResultDTO({ downloads: queue(DownloadStatus.Paused, ['current']), patchSequenceByServer: {} })]);
		await reconnect.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Completed },
		]);
		expect(store.latestPatchSequenceByServer[7]).toBe(1);
		store.updateDownloadPatch(patch(2, DownloadStatus.Stopped));
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Stopped);
	});

	test('Should install a newer snapshot over an older buffered patch and reject delayed covered packets', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		const snapshot = queue(DownloadStatus.Completed, ['current']);
		snapshot[0]!.downloads[0]!.percentage = 100;
		const fetch = subscribeSpyTo(store.fetchDownloadList());
		store.updateDownloadPatch(patch(10, DownloadStatus.Downloading));

		// Act
		held.resolve([200, generateResultDTO({ downloads: snapshot, patchSequenceByServer: { 7: 10 } })]);
		await fetch.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Completed);
		expect(store.serverDownloads[0]!.downloads[0]!.percentage).toBe(100);
		store.updateDownloadPatch(patch(10, DownloadStatus.Paused));
		store.updateDownloadPatch(patch(9, DownloadStatus.Error));
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Completed);
		expect(store.serverDownloads[0]!.downloads[0]!.percentage).toBe(100);
		store.updateDownloadPatch(patch(11, DownloadStatus.Downloading));
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Downloading);
	});

	test('Should replay a newer buffered patch over an older snapshot using the snapshot watermark', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		const snapshot = queue(DownloadStatus.Paused, ['current']);
		snapshot[0]!.downloads[0]!.percentage = 10;
		const fetch = subscribeSpyTo(store.fetchDownloadList());
		store.updateDownloadPatch(patch(10, DownloadStatus.Completed));

		// Act
		held.resolve([200, generateResultDTO({ downloads: snapshot, patchSequenceByServer: { 7: 9 } })]);
		await fetch.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Completed);
		expect(store.serverDownloads[0]!.downloads[0]!.percentage).toBe(75);
		store.updateDownloadPatch(patch(10, DownloadStatus.Paused));
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Completed);
	});

	test('Should apply sequence floors independently for each server', async () => {
		// Arrange
		const store = useDownloadStore();
		const firstServer = queue()[0]!;
		const secondServer = queue()[0]!;
		secondServer.id = 8;
		secondServer.downloads[0]!.id = 'server-8';
		mock.onGet(url).reply(200, generateResultDTO({
			downloads: [firstServer, secondServer],
			patchSequenceByServer: { 7: 10, 8: 0 },
		}));
		await subscribeSpyTo(store.setup()).onComplete();
		const server8Patch = patch(1, DownloadStatus.Completed);
		server8Patch.serverId = 8;
		server8Patch.upserts[0]!.id = 'server-8';

		// Act
		store.updateDownloadPatch(patch(10, DownloadStatus.Error));
		store.updateDownloadPatch(server8Patch);

		// Assert
		expect(store.serverDownloads.find((server) => server.id === 7)!.downloads[0]!.status).toBe(DownloadStatus.Queued);
		expect(store.serverDownloads.find((server) => server.id === 8)!.downloads[0]!.status).toBe(DownloadStatus.Completed);
	});

	test('Should not refetch when a covered buffered patch targets a node removed by the snapshot', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		const fetch = subscribeSpyTo(store.fetchDownloadList());
		const removedPatch = patch(10, DownloadStatus.Completed);
		removedPatch.upserts[0]!.id = 'removed';
		store.updateDownloadPatch(removedPatch);
		const snapshot = queue(DownloadStatus.Queued, ['current']);

		// Act
		held.resolve([200, generateResultDTO({ downloads: snapshot, patchSequenceByServer: { 7: 10 } })]);
		await fetch.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id }) => id)).toEqual(['current']);
		expect(mock.history.get).toHaveLength(2);
	});

	test('Should ignore older snapshot responses and finalizers while retaining patches for the latest request', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const old = Promise.withResolvers<[number, unknown]>();
		const current = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).replyOnce(() => old.promise).onGet(url).replyOnce(() => current.promise);

		// Act
		const first = subscribeSpyTo(store.fetchDownloadList());
		const second = subscribeSpyTo(store.fetchDownloadList());
		store.updateDownloadPatch(patch(1, DownloadStatus.Completed));
		old.resolve([200, generateResultDTO({ downloads: queue(DownloadStatus.Paused), patchSequenceByServer: {} })]);
		await first.onComplete();
		store.updateDownloadPatch(patch(2, DownloadStatus.Stopped));
		current.resolve([200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} })]);
		await second.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Stopped);
		expect(store.latestPatchSequenceByServer[7]).toBe(2);
	});

	test('Should retain the queue and live patches after a failed reconnect without retrying automatically', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
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
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(DownloadStatus.Completed, ['current']), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.reconcileAfterReconnect()).onComplete();
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Completed },
		]);
	});

	test('Should reject an old-session snapshot and buffered patches after reset', async () => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const old = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).replyOnce(() => old.promise).onGet(url).reply(200, generateResultDTO({ downloads: queue(DownloadStatus.Paused, ['current']), patchSequenceByServer: {} }));
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(patch(100, DownloadStatus.Completed));

		// Act
		store.$reset();
		await subscribeSpyTo(store.fetchDownloadList()).onComplete();
		old.resolve([200, generateResultDTO({ downloads: queue(DownloadStatus.Downloading), patchSequenceByServer: {} })]);
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
		mock.onGet(url).reply(200, generateResultDTO({ downloads: queue(), patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet(url).reply(() => held.promise);
		mock.resetHistory();
		const created = patch(1, DownloadStatus.Downloading);
		created.upserts[0]!.id = 'created-during-outage';

		// Act
		const reconnect = subscribeSpyTo(store.reconcileAfterReconnect());
		store.updateDownloadPatch(created);
		held.resolve([200, generateResultDTO({ downloads: queue(DownloadStatus.Queued, ['created-during-outage']), patchSequenceByServer: {} })]);
		await reconnect.onComplete();

		// Assert
		expect(store.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'created-during-outage', status: DownloadStatus.Downloading },
		]);
		expect(mock.history.get).toHaveLength(1);
	});
});
