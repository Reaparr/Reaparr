import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { HubConnectionState } from '@microsoft/signalr';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import {
	JobStatus,
	JobTypes,
	MessageTypes,
	NotificationLevel,
	PlexMediaType,
} from '@dto';
import {
	generateJobStatusUpdate,
	generateLibrarySyncProgress,
	generateLibrarySyncProgressItem,
} from '@mock';
import {
	useBackgroundJobsStore,
	useLibraryStore,
	useNotificationsStore,
	useSignalrStore,
} from '@store';

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

describe('SignalrStore message routing', () => {
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
			value: { downloads: [], patchSequenceByServer: {} },
		});
		setActivePinia(createPinia());
		hubConnections.clear();
		hubConnections.set('progress', createHub());
		hubConnections.set('download', createHub());
		hubConnections.set('notifications', createHub());
	});

	test('Should route LibraryProgress message to library store update method', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const libraryStore = useLibraryStore();
		const progress = generateLibrarySyncProgress({
			libraryId: 3,
			type: PlexMediaType.Movie,
			received: 10,
			total: 100,
			items: [generateLibrarySyncProgressItem(PlexMediaType.Movie, { received: 10, total: 100 })],
		});
		const updateLibraryProgressSpy = vi.spyOn(libraryStore, 'updateLibraryProgress');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('progress', MessageTypes.LibraryProgress, progress);

		// Assert
		expect(updateLibraryProgressSpy).toHaveBeenCalledTimes(1);
		expect(updateLibraryProgressSpy).toHaveBeenCalledWith(progress);
	});

	test('Should route JobStatusUpdate message to background jobs store update method', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const backgroundJobsStore = useBackgroundJobsStore();
		const update = generateJobStatusUpdate({
			jobType: JobTypes.InspectPlexServerJob,
			jobStatus: JobStatus.Started,
			data: { plexServerIds: [2, 3] },
		});
		const setStatusJobUpdateSpy = vi.spyOn(backgroundJobsStore, 'setStatusJobUpdate');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('progress', MessageTypes.JobStatusUpdate, update);

		// Assert
		expect(setStatusJobUpdateSpy).toHaveBeenCalledTimes(1);
		expect(setStatusJobUpdateSpy).toHaveBeenCalledWith(update);
	});

	test('Should route Notification message to notifications store update method', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const notificationsStore = useNotificationsStore();
		const notification = {
			id: 44,
			message: 'Test notification',
			level: NotificationLevel.Information,
			hidden: false,
			createdAt: '2026-03-10T13:00:00.000Z',
		};
		const setNotificationSpy = vi.spyOn(notificationsStore, 'setNotification');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('notifications', MessageTypes.Notification, notification);

		// Assert
		expect(setNotificationSpy).toHaveBeenCalledTimes(1);
		expect(setNotificationSpy).toHaveBeenCalledWith(notification);
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
