import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { HubConnectionState } from '@microsoft/signalr';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { MessageTypes, RefreshDataType } from '@dto';
import { useDownloadStore, useSignalrStore } from '@store';

type HubName = 'progress' | 'download' | 'notifications';

interface MockHub {
	state: HubConnectionState;
	handlers: Map<string, (data: unknown) => void>;
	on: ReturnType<typeof vi.fn>;
	start: ReturnType<typeof vi.fn>;
}

const { hubConnections } = vi.hoisted(() => ({
	hubConnections: new Map<HubName, MockHub>(),
}));

vi.mock('cypress-signalr-mock', () => ({
	useCypressSignalRMock: (hubName: string) => hubConnections.get(hubName as HubName),
}));

describe('SignalrStore refresh notifications', () => {
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
		vi.useFakeTimers();
		hubConnections.clear();
		hubConnections.set('progress', createHub());
		hubConnections.set('download', createHub());
		hubConnections.set('notifications', createHub());
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	test('Should request download list when RefreshNotification message is DownloadTasks', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const fetchDownloadListSpy = vi.spyOn(downloadStore, 'fetchDownloadList');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);

		// Assert
		expect(fetchDownloadListSpy).toHaveBeenCalledTimes(1);
	});

	test('Should not request download list when RefreshNotification message is not DownloadTasks', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const fetchDownloadListSpy = vi.spyOn(downloadStore, 'fetchDownloadList');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.PlexLibrary);

		// Assert
		expect(fetchDownloadListSpy).toHaveBeenCalledTimes(0);
	});

	test('Should emit refresh notification when RefreshNotification message matches getter filter', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const refreshSpy = subscribeSpyTo(signalrStore.getRefreshNotification(RefreshDataType.DownloadTasks));

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);
		await vi.advanceTimersByTimeAsync(250);

		// Assert
		expect(refreshSpy.getFirstValue()).toEqual(RefreshDataType.DownloadTasks);
	});

	test('Should debounce duplicate refresh notifications', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const refreshSpy = subscribeSpyTo(signalrStore.getRefreshNotification(RefreshDataType.PlexLibrary));

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.PlexLibrary);
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.PlexLibrary);
		await vi.advanceTimersByTimeAsync(250);

		// Assert
		expect(refreshSpy.getValues()).toEqual([RefreshDataType.PlexLibrary]);
	});
});

function createHub(state = HubConnectionState.Disconnected) {
	const handlers = new Map<string, (data: unknown) => void>();

	return {
		state,
		handlers,
		on: vi.fn((event: string, callback: (data: unknown) => void) => {
			handlers.set(event, callback);
		}),
		start: vi.fn(async () => Promise.resolve()),
	};
}

function emitHubMessage(hubName: string, messageType: MessageTypes, payload: unknown): void {
	const hub = hubConnections.get(hubName as HubName);
	const handler = hub?.handlers?.get(messageType);
	if (!handler)
		throw new Error(`No handler registered for hub '${hubName}' and message '${messageType}'`);

	handler(payload);
}
