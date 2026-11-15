import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { debugApi } from '@api';
import { DebugPaths } from '@api-urls';
import { useLogsStore } from '@store';
import { TelemetryDiagnosticOrigin, type BaseResultDTO } from '@dto';

describe('LogsStore.sendTelemetryDiagnostics()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	async function createStore() {
		mock.onGet(DebugPaths.getAllLogsEndpoint()).reply(200, generateResultDTO([]));
		const store = useLogsStore();
		await subscribeSpyTo(store.setup()).onComplete();
		return store;
	}

	function acceptedResult(): BaseResultDTO {
		return {
			isSuccess: true,
			statusCode: 200,
			errors: [],
			successes: [],
		};
	}

	function bodyOf(data: unknown): unknown {
		return typeof data === 'string' ? JSON.parse(data) : data;
	}

	test('Should not capture telemetry during store setup', async () => {
		// Arrange
		const store = await createStore();

		// Act
		const diagnostic = store.telemetryDiagnostic;

		// Assert
		expect(diagnostic).toEqual({ pending: false, backend: null, frontend: null });
		expect(mock.history.post).toHaveLength(0);
	});

	test('Should send backend then frontend diagnostics with exact origin-only bodies when both are accepted', async () => {
		// Arrange
		const requestBodies: unknown[] = [];
		mock.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint()).reply((request) => {
			requestBodies.push(bodyOf(request.data));
			return [200, acceptedResult()];
		});
		const store = await createStore();

		// Act
		const result = subscribeSpyTo(store.sendTelemetryDiagnostics());
		await result.onComplete();

		// Assert
		expect(result.getValues()).toEqual([true, true]);
		expect(requestBodies).toEqual([
			{ origin: TelemetryDiagnosticOrigin.Backend },
			{ origin: TelemetryDiagnosticOrigin.Frontend },
		]);
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: true, frontend: true });
	});

	test('Should reset previous origin statuses before a new deliberate run', async () => {
		// Arrange
		mock.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint()).reply(200, acceptedResult());
		const store = await createStore();
		await subscribeSpyTo(store.sendTelemetryDiagnostics()).onComplete();
		const backendResponse = new Subject<BaseResultDTO>();
		const frontendResponse = new Subject<BaseResultDTO>();
		vi.spyOn(debugApi, 'sendTelemetryDiagnosticEndpoint').mockImplementation(({ origin }) =>
			origin === TelemetryDiagnosticOrigin.Backend ? backendResponse : frontendResponse,
		);

		// Act
		const secondRun = subscribeSpyTo(store.sendTelemetryDiagnostics());

		// Assert
		expect(store.telemetryDiagnostic).toEqual({ pending: true, backend: null, frontend: null });
		secondRun.unsubscribe();
	});

	test('Should attempt frontend after a failed backend diagnostic', async () => {
		// Arrange
		mock.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.replyOnce(503, generateFailedResultDTO({ statusCode: 503 }))
			.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.replyOnce(200, acceptedResult());
		const store = await createStore();

		// Act
		const result = subscribeSpyTo(store.sendTelemetryDiagnostics());
		await result.onComplete();

		// Assert
		expect(result.getValues()).toEqual([false, true]);
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: false, frontend: true });
		expect(mock.history.post).toHaveLength(2);
	});

	test('Should retain backend success when the frontend diagnostic fails', async () => {
		// Arrange
		mock.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.replyOnce(200, acceptedResult())
			.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.replyOnce(503, generateFailedResultDTO({ statusCode: 503 }));
		const store = await createStore();

		// Act
		const result = subscribeSpyTo(store.sendTelemetryDiagnostics());
		await result.onComplete();

		// Assert
		expect(result.getValues()).toEqual([true, false]);
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: true, frontend: false });
		expect(mock.history.post).toHaveLength(2);
	});

	test('Should attempt both origins and expose failed statuses for transport errors', async () => {
		// Arrange
		mock.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.networkErrorOnce()
			.onPost(DebugPaths.sendTelemetryDiagnosticEndpoint())
			.networkErrorOnce();
		const store = await createStore();

		// Act
		const result = subscribeSpyTo(store.sendTelemetryDiagnostics());
		await result.onComplete();

		// Assert
		expect(result.getValues()).toEqual([false, false]);
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: false, frontend: false });
		expect(mock.history.post).toHaveLength(2);
	});

	test('Should update each origin as it completes and prevent duplicate pending runs', async () => {
		// Arrange
		const backendResponse = new Subject<BaseResultDTO>();
		const frontendResponse = new Subject<BaseResultDTO>();
		const request = vi.spyOn(debugApi, 'sendTelemetryDiagnosticEndpoint').mockImplementation(({ origin }) =>
			origin === TelemetryDiagnosticOrigin.Backend ? backendResponse : frontendResponse,
		);
		const store = await createStore();

		// Act
		const firstRun = subscribeSpyTo(store.sendTelemetryDiagnostics());
		const duplicateRun = subscribeSpyTo(store.sendTelemetryDiagnostics());
		expect(store.telemetryDiagnostic).toEqual({ pending: true, backend: null, frontend: null });
		expect(request).toHaveBeenCalledTimes(1);
		backendResponse.next(acceptedResult());
		backendResponse.complete();
		expect(store.telemetryDiagnostic).toEqual({ pending: true, backend: true, frontend: null });
		expect(request).toHaveBeenCalledWith({ origin: TelemetryDiagnosticOrigin.Frontend });
		frontendResponse.next(acceptedResult());
		frontendResponse.complete();
		await firstRun.onComplete();
		await duplicateRun.onComplete();

		// Assert
		expect(duplicateRun.getValues()).toEqual([]);
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: true, frontend: true });
		expect(request).toHaveBeenCalledTimes(2);
	});

	test('Should clear pending state and stop the sequence when the caller unsubscribes', async () => {
		// Arrange
		const backendResponse = new Subject<BaseResultDTO>();
		const frontendResponse = new Subject<BaseResultDTO>();
		const request = vi.spyOn(debugApi, 'sendTelemetryDiagnosticEndpoint').mockImplementation(({ origin }) =>
			origin === TelemetryDiagnosticOrigin.Backend ? backendResponse : frontendResponse,
		);
		const store = await createStore();

		// Act
		const run = subscribeSpyTo(store.sendTelemetryDiagnostics());
		run.unsubscribe();

		// Assert
		expect(store.telemetryDiagnostic).toEqual({ pending: false, backend: null, frontend: null });
		expect(request).toHaveBeenCalledTimes(1);
		expect(request).not.toHaveBeenCalledWith({ origin: TelemetryDiagnosticOrigin.Frontend });
	});
});
