import { afterEach, beforeAll, beforeEach, describe, expect, test, vi, type Mock } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { HubConnectionState } from '@microsoft/signalr';
import { flushPromises } from '@vue/test-utils';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { UpdatePaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { MessageTypes, RefreshDataType, type BaseResultDTO } from '@dto';
import { useSignalrStore, useUpdateStore } from '@store';

type HubName = 'progress' | 'download' | 'notifications' | 'logs';

interface MockHub {
	state: HubConnectionState;
	handlers: Record<string, (data: unknown) => void>;
	on: Mock<(event: string, callback: (data: unknown) => void) => void>;
	start: Mock<() => Promise<void>>;
}

const { hubConnections } = vi.hoisted(() => ({
	hubConnections: new Map<HubName, MockHub>(),
}));

vi.mock('cypress-signalr-mock', () => ({
	useCypressSignalRMock: (hubName: string) => hubConnections.get(hubName as HubName),
}));

describe('UpdateStore refresh notification consumer', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		vi.useFakeTimers();
		hubConnections.clear();
		hubConnections.set('progress', createHub());
		hubConnections.set('download', createHub());
		hubConnections.set('notifications', createHub());
		hubConnections.set('logs', createHub());
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	test('Should perform one update check and expose the returned release notes after one UpdateAvailable notification', async () => {
		// Arrange
		const releaseNotes = [{
			version: '1.0.1',
			releaseDate: '2026-10-18T00:00:00Z',
			notes: 'SignalR refresh regression coverage',
			isDevRelease: false,
		}];
		mock.onGet(UpdatePaths.checkForUpdateEndpoint())
			.replyOnce(200, generateResultDTO({
				currentVersion: '1.0.0',
				newestVersion: '1.0.0',
				isUpdateAvailable: false,
				releaseNotes: [],
			}))
			.onGet(UpdatePaths.checkForUpdateEndpoint())
			.reply(200, generateResultDTO({
				currentVersion: '1.0.0',
				newestVersion: '1.0.1',
				isUpdateAvailable: true,
				releaseNotes,
			}));
		const signalrStore = useSignalrStore();
		const updateStore = useUpdateStore();

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		await subscribeSpyTo(updateStore.setup()).onComplete();

		// Assert
		expect(mock.history.get.filter((request) => request.url === UpdatePaths.checkForUpdateEndpoint())).toHaveLength(1);
		expect(updateStore.isUpdateAvailable).toBe(false);
		expect(updateStore.newestVersion).toBe('1.0.0');
		expect(updateStore.releaseNotes).toEqual([]);

		// Act
		mock.resetHistory();
		emitHubMessage('notifications', MessageTypes.RefreshNotification, RefreshDataType.UpdateAvailable);
		await vi.advanceTimersByTimeAsync(250);
		await flushPromises();

		// Assert
		expect(mock.history.get.filter((request) => request.url === UpdatePaths.checkForUpdateEndpoint())).toHaveLength(1);
		expect(updateStore.isUpdateAvailable).toBe(true);
		expect(updateStore.newestVersion).toBe('1.0.1');
		expect(updateStore.releaseNotes).toEqual(releaseNotes);
	});

	test('Should retain the last successful update after a failed check and accept a later successful no-update', async () => {
		// Arrange
		const path = UpdatePaths.checkForUpdateEndpoint();
		const releaseNotes = [{
			version: '1.0.1', releaseDate: '2026-10-18T00:00:00Z',
			notes: 'Last successful release', isDevRelease: false,
		}];
		const failure: BaseResultDTO = {
			isSuccess: false, statusCode: 500, successes: [],
			errors: [{ message: 'GitHub unavailable', reasons: [], metadata: {} }],
		};
		mock.onGet(path).replyOnce(200, generateResultDTO({
			currentVersion: '1.0.0', newestVersion: '1.0.1', isUpdateAvailable: true, releaseNotes,
		})).onGet(path).replyOnce(500, failure).onGet(path).replyOnce(200, generateResultDTO({
			currentVersion: '1.0.0', newestVersion: '1.0.0', isUpdateAvailable: false, releaseNotes: [],
		}));
		const store = useUpdateStore();

		// Act
		await subscribeSpyTo(store.checkForUpdate()).onComplete();

		// Assert
		expect(store.isUpdateAvailable).toBe(true);
		expect(store.newestVersion).toBe('1.0.1');
		expect(store.releaseNotes).toEqual(releaseNotes);

		// Act
		const failedCheck = subscribeSpyTo(store.checkForUpdate());
		await failedCheck.onComplete();

		// Assert
		expect(failedCheck.getValues().map((result) => result.isSuccess)).toEqual([false]);
		expect(store.isUpdateAvailable).toBe(true);
		expect(store.newestVersion).toBe('1.0.1');
		expect(store.releaseNotes).toEqual(releaseNotes);

		// Act
		await subscribeSpyTo(store.checkForUpdate()).onComplete();

		// Assert
		expect(store.isUpdateAvailable).toBe(false);
		expect(store.newestVersion).toBe('1.0.0');
		expect(store.releaseNotes).toEqual([]);
		expect(mock.history.get.map((request) => request.url)).toEqual([path, path, path]);

		// Act
		store.$reset();

		// Assert
		expect(store.newestVersion).toBe('?');
		expect(store.isUpdateAvailable).toBe(false);
	});
});

function createHub(state = HubConnectionState.Disconnected): MockHub {
	const handlers: Record<string, (data: unknown) => void> = {};

	return {
		state,
		handlers,
		on: vi.fn((event: string, callback: (data: unknown) => void) => {
			handlers[event] = callback;
		}),
		start: vi.fn(async () => Promise.resolve()),
	};
}

function emitHubMessage(hubName: string, messageType: MessageTypes, payload: unknown): void {
	const hub = hubConnections.get(hubName as HubName);
	const handler = hub?.handlers[messageType];
	if (!handler)
		throw new Error(`No handler registered for hub '${hubName}' and message '${messageType}'`);

	handler(payload);
}
