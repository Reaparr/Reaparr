import { describe, beforeAll, beforeEach, test, expect, vi, type Mock } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { HubConnectionState } from '@microsoft/signalr';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadStatus, MessageTypes, PlexMediaType } from '@dto';
import {
	generateDownloadPatchMessagePackDTO,
	toDownloadPatchMessagePackTuple,
} from '@mock';
import { useDownloadStore, useSignalrStore } from '@store';

type HubName = 'progress' | 'download' | 'notifications' | 'logs';

interface MockHub {
	state: HubConnectionState;
	handlers: Map<string, (data: unknown) => void>;
	on: ReturnType<typeof vi.fn>;
	start: ReturnType<typeof vi.fn>;
	off: Mock;
	stop: Mock;
}

const { hubConnections } = vi.hoisted(() => ({
	hubConnections: new Map<HubName, MockHub>(),
}));

vi.mock('cypress-signalr-mock', () => ({
	useCypressSignalRMock: (hubName: string) => hubConnections.get(hubName as HubName),
}));

describe('SignalrStore - Download MessagePack conversion', () => {
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

	test('Should convert MessagePack download patch tuple and forward patch when DownloadPatch message is received', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const patch = generateDownloadPatchMessagePackDTO({ sequence: 3456, upsertCount: 2, deletedCount: 2 });
		const tuplePayload = toDownloadPatchMessagePackTuple(patch);
		const updateDownloadPatchSpy = vi.spyOn(downloadStore, 'updateDownloadPatch');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('download', MessageTypes.DownloadPatch, tuplePayload);

		// Assert
		expect(updateDownloadPatchSpy).toHaveBeenCalledTimes(1);
		expect(updateDownloadPatchSpy).toHaveBeenCalledWith(patch);
	});

	test('Should convert percentage to number when MessagePack download patch tuple contains percentage as string', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const patch = generateDownloadPatchMessagePackDTO({ upsertCount: 1 });
		const tuplePayload = toDownloadPatchMessagePackTuple(patch, { percentageAsString: true });
		const updateDownloadPatchSpy = vi.spyOn(downloadStore, 'updateDownloadPatch');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('download', MessageTypes.DownloadPatch, tuplePayload);

		// Assert
		expect(updateDownloadPatchSpy).toHaveBeenCalledTimes(1);
		expect(updateDownloadPatchSpy.mock.calls[0]?.[0]?.upserts?.[0]?.percentage).toEqual(Number(patch.upserts[0]!.percentage));
		expect(typeof updateDownloadPatchSpy.mock.calls[0]?.[0]?.upserts?.[0]?.percentage).toEqual('number');
	});

	test('Should throw when MessagePack download patch tuple contains invalid upsert items', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const tuplePayload = [
			1,
			100,
			['invalid-upsert-entry'],
			[],
		];

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(() => emitHubMessage('download', MessageTypes.DownloadPatch, tuplePayload)).toThrow('Invalid DownloadPatch MessagePack tuple entry');
	});

	test.each([
		[
			'Should throw when MessagePack download patch tuple upsert contains extra properties',
			[
				'36a8519c-39b3-4ea3-ad07-6957d2f4d03a',
				'0219f4c1-e588-469e-b4b9-aef890fdf7c8',
				DownloadStatus.Downloading,
				'60.84',
				100,
				200,
				300,
				400,
				'unexpected-extra-property',
			],
		],
		[
			'Should throw when MessagePack download patch tuple upsert contains invalid property type',
			[
				123,
				'0219f4c1-e588-469e-b4b9-aef890fdf7c8',
				DownloadStatus.Downloading,
				'60.84',
				100,
				200,
				300,
				400,
			],
		],
	])('%s', async (_name, invalidEntry) => {
		// Arrange
		const signalrStore = useSignalrStore();
		const tuplePayload = [
			1,
			101,
			[invalidEntry],
			[],
		];

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(() => emitHubMessage('download', MessageTypes.DownloadPatch, tuplePayload)).toThrow('Invalid DownloadPatch MessagePack tuple entry');
	});

	test('Should throw when DownloadPatch message payload is not a MessagePack tuple', async () => {
		// Arrange
		const signalrStore = useSignalrStore();

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();

		// Assert
		expect(() => emitHubMessage('download', MessageTypes.DownloadPatch, { invalid: true })).toThrow('Invalid DownloadPatch MessagePack tuple payload');
	});

	test('Should convert MessagePack server download progress tuple and forward progress when ServerDownloadProgress message is received', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const updateServerDownloadProgressSpy = vi.spyOn(downloadStore, 'updateServerDownloadProgress');
		const progressTuple = [
			12,
			1,
			[
				[
					'7f8c0cec-43ca-4540-96ce-eef7107132cc',
					'Example Download',
					PlexMediaType.Movie,
					DownloadStatus.Downloading,
					'60.84',
					2345,
					6789,
					123,
					456,
					[],
				],
			],
		];

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('download', MessageTypes.ServerDownloadProgress, progressTuple);

		// Assert
		expect(updateServerDownloadProgressSpy).toHaveBeenCalledTimes(1);
		expect(updateServerDownloadProgressSpy.mock.calls[0]?.[0]).toEqual({
			id: 12,
			downloadableTasksCount: 1,
			downloads: [{
				id: '7f8c0cec-43ca-4540-96ce-eef7107132cc',
				title: 'Example Download',
				mediaType: PlexMediaType.Movie,
				status: DownloadStatus.Downloading,
				percentage: 60.84,
				dataReceived: 2345,
				dataTotal: 6789,
				downloadSpeed: 123,
				timeRemaining: 456,
				children: [],
			}],
		});
	});

	test('Should forward object server download progress unchanged when payload is already an object', async () => {
		// Arrange
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const progress = {
			id: 99,
			downloadableTasksCount: 0,
			downloads: [],
		};
		const updateServerDownloadProgressSpy = vi.spyOn(downloadStore, 'updateServerDownloadProgress');

		// Act
		await subscribeSpyTo(signalrStore.setup()).onComplete();
		emitHubMessage('download', MessageTypes.ServerDownloadProgress, progress);

		// Assert
		expect(updateServerDownloadProgressSpy).toHaveBeenCalledTimes(1);
		expect(updateServerDownloadProgressSpy).toHaveBeenCalledWith(progress);
	});
});

function createHub(state = HubConnectionState.Disconnected) {
	const handlers = new Map<string, (data: unknown) => void>();

	const hub: MockHub = {
		state,
		handlers,
		on: vi.fn((event: string, callback: (data: unknown) => void) => {
			handlers.set(event, callback);
		}),
		off: vi.fn((event: string) => { handlers.delete(event); }),
		start: vi.fn(async () => { hub.state = HubConnectionState.Connected; }),
		stop: vi.fn(async () => { hub.state = HubConnectionState.Disconnected; }),
	};
	return hub;
}

function emitHubMessage(hubName: string, messageType: MessageTypes, payload: unknown): void {
	const hub = hubConnections.get(hubName as HubName);
	const handler = hub?.handlers?.get(messageType);
	if (!handler)
		throw new Error(`No handler registered for hub '${hubName}' and message '${messageType}'`);

	handler(payload);
}
