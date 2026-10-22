import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { HubConnectionState } from '@microsoft/signalr';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useSignalrStore, useLibraryStore, useDownloadStore } from '@store';
import { PlexLibraryPaths } from '@api/api-paths';
import { DownloadStatus, LibrarySyncJobStatus, LogSeverity, MessageTypes, PlexMediaType, type ServerDownloadProgressDTO } from '@dto';
import { generateLibrarySyncJobQueue, generateResultDTO } from '@mock';
import { flushPromises } from '@vue/test-utils';

type HubName = 'progress' | 'download' | 'notifications' | 'logs';

type ReconnectedHandler = () => void;

interface MockHub {
	state: HubConnectionState;
	handlers: Map<string, (data: unknown) => void>;
	on: (event: string, callback: (data: unknown) => void) => void;
	off: (event: string) => void;
	onreconnected: (callback: ReconnectedHandler) => void;
	reconnected?: ReconnectedHandler;
	start: () => Promise<void>;
	stop: () => Promise<void>;
}
const { hubConnections } = vi.hoisted(() => ({
	hubConnections: new Map<HubName, MockHub>(),
}));

vi.mock('cypress-signalr-mock', () => ({
	useCypressSignalRMock: (hubName: string) => hubConnections.get(hubName as HubName),
}));

describe('SignalrStore connections', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		mock.onGet('/api/Download').reply(200, {
			isSuccess: true,
			errors: [],
			successes: [],
			statusCode: 200,
			value: [],
		});
		setActivePinia(createPinia());
		hubConnections.clear();
		hubConnections.set('progress', createHub());
		hubConnections.set('download', createHub());
		hubConnections.set('notifications', createHub());
		hubConnections.set('logs', createHub());
	});

	test('Should start all hub connections when setup is run and hubs are disconnected', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const progressHub = hubConnections.get('progress')!;
		const downloadHub = hubConnections.get('download')!;
		const notificationsHub = hubConnections.get('notifications')!;

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(progressHub.start).toHaveBeenCalledTimes(1);
		expect(downloadHub.start).toHaveBeenCalledTimes(1);
		expect(notificationsHub.start).toHaveBeenCalledTimes(1);
	});

	test('Should not start already connected hub connections when setup is run', async () => {
		// Arrange
		hubConnections.set('progress', createHub(HubConnectionState.Connected));
		hubConnections.set('download', createHub(HubConnectionState.Connected));
		hubConnections.set('notifications', createHub(HubConnectionState.Connected));
		const signalrStore = useSignalrStore();
		const progressHub = hubConnections.get('progress')!;
		const downloadHub = hubConnections.get('download')!;
		const notificationsHub = hubConnections.get('notifications')!;

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(progressHub.start).toHaveBeenCalledTimes(0);
		expect(downloadHub.start).toHaveBeenCalledTimes(0);
		expect(notificationsHub.start).toHaveBeenCalledTimes(0);
	});

	test('Should refresh library sync status once after the notification hub reconnects', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const libraryStore = useLibraryStore();
		const notificationsHub = hubConnections.get('notifications')!;
		const queue = generateLibrarySyncJobQueue({ plexLibraryId: 11, plexServerId: 1, status: LibrarySyncJobStatus.Queued });
		mock.onGet(PlexLibraryPaths.getLibrarySyncStatusEndpoint()).reply(200, generateResultDTO([queue]));

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		notificationsHub.reconnected?.();
		await flushPromises();

		// Assert
		expect(mock.history.get.filter((request) => request.url === PlexLibraryPaths.getLibrarySyncStatusEndpoint())).toHaveLength(1);
		expect(libraryStore.getLibrarySyncQueue(11)).toEqual(queue);
	});

	test('Should restore the download queue on reconnect and ignore a replaced connection callback', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const initial = downloadQueue(DownloadStatus.Downloading);
		mock.onGet('/api/Download').reply(200, generateResultDTO(initial));
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		await subscribeSpyTo(downloadStore.setup()).onComplete();
		const oldDownloadHub = hubConnections.get('download')!;
		mock.onGet('/api/Download').reply(200, generateResultDTO(downloadQueue(DownloadStatus.Paused)));
		mock.resetHistory();

		// Act
		oldDownloadHub.reconnected?.();
		await flushPromises();

		// Assert
		expect(downloadStore.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Paused);
		expect(mock.history.get.filter((request) => request.url === '/api/Download')).toHaveLength(1);
		await signalrStore.resetSession();
		for (const name of hubConnections.keys()) hubConnections.set(name, createHub());
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		mock.resetHistory();
		oldDownloadHub.reconnected?.();
		await flushPromises();
		expect(mock.history.get.filter((request) => request.url === '/api/Download')).toHaveLength(0);
		expect(downloadStore.serverDownloads[0]!.downloads[0]!.status).toBe(DownloadStatus.Paused);
		await signalrStore.resetSession();
	});

	test('Should cancel an old reconnect lookup and reject its retained event handler after session teardown', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		mock.onGet('/api/Download').reply(200, generateResultDTO(downloadQueue(DownloadStatus.Queued)));
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		await subscribeSpyTo(downloadStore.setup()).onComplete();
		const oldHub = hubConnections.get('download')!;
		const oldPatchHandler = oldHub.handlers.get(MessageTypes.DownloadPatch)!;
		const held = Promise.withResolvers<[number, unknown]>();
		mock.onGet('/api/Download').replyOnce(() => held.promise);
		oldHub.reconnected?.();
		await flushPromises();
		const oldSignal = mock.history.get.at(-1)!.signal;

		// Act
		await signalrStore.resetSession();
		downloadStore.$reset();
		for (const name of hubConnections.keys()) hubConnections.set(name, createHub());
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		mock.onGet('/api/Download').reply(200, generateResultDTO(downloadQueue(DownloadStatus.Paused)));
		await subscribeSpyTo(downloadStore.fetchDownloadList()).onComplete();
		oldPatchHandler([7, 999, [], ['current']]);
		held.resolve([200, generateResultDTO([])]);
		await flushPromises();

		// Assert
		expect(oldSignal?.aborted).toBe(true);
		expect(downloadStore.serverDownloads[0]!.downloads.map(({ id, status }) => ({ id, status }))).toEqual([
			{ id: 'current', status: DownloadStatus.Paused },
		]);
		expect(downloadStore.latestPatchSequenceByServer).toEqual({});
		await signalrStore.resetSession();
	});
	test('Should share one connection setup when setup is requested concurrently or repeatedly', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadHub = hubConnections.get('download')!;
		let finishStart!: () => void;
		const starting = new Promise<void>((resolve) => {
			finishStart = resolve;
		});
		vi.mocked(downloadHub.start).mockImplementation(async () => {
			await starting;
			downloadHub.state = HubConnectionState.Connected;
		});

		// Act
		const first = subscribeSpyTo(signalrStore.setup());
		const second = subscribeSpyTo(signalrStore.setup());
		await flushPromises();
		finishStart();
		await Promise.all([first.onComplete(), second.onComplete()]);
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(first.getLastValue()?.isSuccess).toBe(true);
		expect(second.getLastValue()?.isSuccess).toBe(true);
		for (const hub of hubConnections.values()) {
			expect(hub.start).toHaveBeenCalledTimes(1);
			expect(hub.stop).not.toHaveBeenCalled();
			expect(hub.state).toBe(HubConnectionState.Connected);
		}
	});

	test('Should stop old hubs and deliver only new session events after reset and setup', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		const oldHubs = [...hubConnections.values()];
		const oldLogHub = hubConnections.get('logs')!;
		const oldNotificationHub = hubConnections.get('notifications')!;
		const oldEvents = subscribeSpyTo(signalrStore.getLogEvents());
		const event = { sequence: 1, timestamp: '2026-10-18T10:00:00Z', level: LogSeverity.Information, message: 'New session log' };

		// Act
		await signalrStore.resetSession();
		for (const name of hubConnections.keys()) {
			hubConnections.set(name, createHub());
		}
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		const currentEvents = subscribeSpyTo(signalrStore.getLogEvents());
		oldLogHub.handlers.get('LogEvent')?.(event);
		hubConnections.get('logs')!.handlers.get('LogEvent')?.(event);
		oldNotificationHub.reconnected?.();

		// Assert
		expect(oldEvents.receivedComplete()).toBe(true);
		expect(oldEvents.getValues()).toEqual([]);
		expect(currentEvents.getValues()).toEqual([event]);
		expect(mock.history.get.filter((request) => request.url === PlexLibraryPaths.getLibrarySyncStatusEndpoint())).toHaveLength(0);
		for (const hub of oldHubs) {
			expect(hub.stop).toHaveBeenCalledTimes(1);
			expect(hub.state).toBe(HubConnectionState.Disconnected);
			expect(hub.handlers.size).toBe(0);
		}
		await signalrStore.resetSession();
	});

	test('Should stop a pending setup before creating connections for the next session', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const oldHubs = [...hubConnections.values()];
		const downloadHub = hubConnections.get('download')!;
		let finishStart!: () => void;
		const starting = new Promise<void>((resolve) => {
			finishStart = resolve;
		});
		vi.mocked(downloadHub.start).mockImplementation(async () => {
			await starting;
			downloadHub.state = HubConnectionState.Connected;
		});
		const first = subscribeSpyTo(signalrStore.setup());
		await flushPromises();

		// Act
		const reset = signalrStore.resetSession();
		for (const name of hubConnections.keys()) {
			hubConnections.set(name, createHub());
		}
		const next = subscribeSpyTo(signalrStore.setup());
		finishStart();
		await Promise.all([first.onComplete(), reset, next.onComplete()]);

		// Assert
		expect(next.getLastValue()?.isSuccess).toBe(true);
		expect(oldHubs.map((hub) => hub.state)).toEqual(oldHubs.map(() => HubConnectionState.Disconnected));
		expect([...hubConnections.values()].map((hub) => hub.state)).toEqual(oldHubs.map(() => HubConnectionState.Connected));
		await signalrStore.resetSession();
	});
	test.each(['logs', 'download'] as const)('Should retry failed %s hub teardown before starting another session', async (hubName) => {
		// Arrange
		const signalrStore = useSignalrStore();
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		const oldHub = hubConnections.get(hubName)!;
		vi.mocked(oldHub.stop).mockRejectedValueOnce(new Error('Hub stop failed'));

		// Act
		await expect(signalrStore.resetSession()).rejects.toThrow('Hub stop failed');
		for (const name of hubConnections.keys()) {
			hubConnections.set(name, createHub());
		}
		const result = subscribeSpyTo(signalrStore.setup());
		await result.onComplete();

		// Assert
		expect(result.getLastValue()?.isSuccess).toBe(true);
		expect(oldHub.stop).toHaveBeenCalledTimes(2);
		expect(oldHub.state).toBe(HubConnectionState.Disconnected);
		expect(oldHub.handlers.size).toBe(0);
		await signalrStore.resetSession();
	});
});

function createHub(state = HubConnectionState.Disconnected): MockHub {
	const hub: MockHub = {
		state,
		handlers: new Map<string, (data: unknown) => void>(),
		on: vi.fn((event, callback) => { hub.handlers.set(event, callback); }),
		off: vi.fn((event) => { hub.handlers.delete(event); }),
		onreconnected: (callback) => {
			hub.reconnected = callback;
		},
		start: vi.fn(async () => { hub.state = HubConnectionState.Connected; }),
		stop: vi.fn(async () => { hub.state = HubConnectionState.Disconnected; }),
	};

	return hub;
}

function downloadQueue(status: DownloadStatus): ServerDownloadProgressDTO[] {
	return [{ id: 7, downloadableTasksCount: 1, downloads: [{
		id: 'current', title: 'Current movie', status, mediaType: PlexMediaType.Movie, children: [], percentage: 0,
		dataReceived: 0, dataTotal: 100, downloadSpeed: 0, timeRemaining: 0,
	}] }];
}
