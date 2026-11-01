import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexServerConnectionPaths } from '@api-urls';
import { generateFailedResultDTO, generatePlexServerConnection, generateResultDTO } from '@mock';
import type { PlexServerStatusDTO } from '@dto';
import { useServerConnectionStore } from '@store';

describe('ServerConnectionStore.checkServerConnection()', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	async function setupConnections() {
		const connections = [1, 2].map((id) => generatePlexServerConnection({
			id, plexServerId: id, partialData: { latestConnectionStatus: null, url: `http://connection-${id}.test:32400` },
		}));
		mock.onGet(PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint()).reply(200, generateResultDTO(connections));
		const store = useServerConnectionStore();
		await subscribeSpyTo(store.setup()).onComplete();
		return store;
	}

	function status(id: number, isSuccessful: boolean): PlexServerStatusDTO {
		return {
			id, plexServerId: 1, plexServerConnectionId: 1, isSuccessful,
			lastChecked: '2026-10-20T12:00:00Z', statusCode: isSuccessful ? 200 : 503,
			statusMessage: isSuccessful ? 'Completed' : 'Unavailable',
		};
	}

	test('Should publish first successful and later failed checks without refreshing or changing other connections', async () => {
		// Arrange
		const store = await setupConnections();
		const before = { ...store.getServerConnection(1)! };
		const control = { ...store.getServerConnection(2)! };
		const connected = status(101, true);
		const disconnected = status(102, false);
		const checkPath = PlexServerConnectionPaths.checkConnectionStatusByIdEndpoint(1);
		mock.onGet(checkPath).replyOnce(200, generateResultDTO(connected))
			.onGet(checkPath).reply(200, generateResultDTO(disconnected));
		expect(store.isServerConnected(1)).toBe(false);

		// Act
		const first = subscribeSpyTo(store.checkServerConnection(1));
		await first.onComplete();

		// Assert
		expect(first.getLastValue()).toEqual(connected);
		expect(store.getServerConnection(1)).toEqual({ ...before, latestConnectionStatus: connected });
		expect(store.isServerConnected(1)).toBe(true);
		expect(store.getConnectionLoading(1)).toBe(false);

		// Act
		await subscribeSpyTo(store.checkServerConnection(1)).onComplete();

		// Assert
		expect(store.getServerConnection(1)).toEqual({ ...before, latestConnectionStatus: disconnected });
		expect(store.isServerConnected(1)).toBe(false);
		expect(store.getServerConnection(2)).toEqual(control);
		expect(store.getConnectionLoading(1)).toBe(false);
		expect(mock.history.get.map((request) => request.url)).toEqual([
			PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint(), checkPath, checkPath,
		]);
	});

	test.each([200, 500])('Should preserve the previous status and clear loading when the check request fails with HTTP %i', async (code) => {
		// Arrange
		const store = await setupConnections();
		store.getServerConnection(1)!.latestConnectionStatus = status(100, true);
		const before = { ...store.getServerConnection(1)! };
		mock.onGet(PlexServerConnectionPaths.checkConnectionStatusByIdEndpoint(1))
			.reply(code, generateFailedResultDTO());

		// Act
		const request = store.checkServerConnection(1);
		expect(store.getConnectionLoading(1)).toBe(true);
		const result = subscribeSpyTo(request);
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toBeNull();
		expect(store.getServerConnection(1)).toEqual(before);
		expect(store.isServerConnected(1)).toBe(true);
		expect(store.getConnectionLoading(1)).toBe(false);
	});

	test('Should not restore a removed connection or overwrite another row when a check finishes after removal', async () => {
		// Arrange
		const store = await setupConnections();
		const control = { ...store.getServerConnection(2)! };
		const checked = status(103, true);
		const response = Promise.withResolvers<[number, unknown]>();
		mock.onGet(PlexServerConnectionPaths.checkConnectionStatusByIdEndpoint(1)).reply(() => response.promise);

		// Act
		const result = subscribeSpyTo(store.checkServerConnection(1));
		expect(store.getConnectionLoading(1)).toBe(true);
		store.serverConnections.splice(0, 1);
		response.resolve([200, generateResultDTO(checked)]);
		await result.onComplete();

		// Assert
		expect(result.getLastValue()).toEqual(checked);
		expect(store.serverConnections).toEqual([control]);
		expect(store.getServerConnection(1)).toBeUndefined();
		expect(store.getConnectionLoading(1)).toBe(false);
	});
});
