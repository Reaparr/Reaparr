import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { ref } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import type { Router } from 'vue-router';
import type { CaptureResult, CapturedNetworkRequest, PostHogConfig } from 'posthog-js';
import { mockNuxtImport } from '@nuxt/test-utils/runtime';

import type { BrowserTelemetryBootstrapDTO } from '@dto';
import { useAuthenticationStore, useGlobalStore } from '@store';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { TelemetryPaths } from '@api-urls';
import posthogPlugin, {
	maskReplayAttribute,
	maskReplayNetworkRequest,
	sanitizeCapture,
	stripUrlMetadata,
} from '@/plugins/2.posthog.client';
import type { ITelemetry } from '@composables/telemetry/ITelemetry';

const posthogMock = vi.hoisted(() => {
	const client = {
		__loaded: true,
		get_distinct_id: vi.fn<() => string>(),
		identify: vi.fn<(id: string) => void>(),
		capture: vi.fn(),
		captureException: vi.fn(),
		opt_in_capturing: vi.fn(),
		opt_out_capturing: vi.fn(),
		register: vi.fn(),
		reset: vi.fn(),
		set_config: vi.fn<(config: Partial<PostHogConfig>) => void>(),
		startExceptionAutocapture: vi.fn(),
		stopExceptionAutocapture: vi.fn(),
		startSessionRecording: vi.fn(),
		stopSessionRecording: vi.fn(),
	};
	return { client, provide: vi.fn<(name: string, telemetry: ITelemetry) => void>() };
});
mockNuxtImport('usePostHog', () => () => posthogMock.client);

type PluginHook = (error: Error, ...args: unknown[]) => void;

type PluginApp = {
	$router: Router;
	hook: (name: string, callback: PluginHook) => void;
	provide: (name: string, telemetry: ITelemetry) => void;
};
const bootstrap: BrowserTelemetryBootstrapDTO = {
	apiHost: 'https://telemetry.example.test',
	projectToken: 'phc_test',
	distinctId: '5e2c5c48-aa0f-4f21-8c65-94e72b2bd389',
	release: '0.41.0-test',
	environment: 'test',
	runtimeMode: 'docker',
	runtimeIdentifier: 'linux-x64',
	platform: 'Linux',
};

function runPlugin(router: Router, hooks: Map<string, PluginHook>): void {
	const app: PluginApp = {
		$router: router,
		provide: posthogMock.provide,
		hook(name, callback) {
			hooks.set(name, callback);
		},
	};
	const plugin = posthogPlugin as unknown as ((nuxtApp: PluginApp) => void) | { setup: (nuxtApp: PluginApp) => void };
	if (typeof plugin === 'function') {
		plugin(app);
	} else {
		plugin.setup(app);
	}
}

describe('PostHog browser plugin', () => {
	let { appConfig, mock } = baseVars();

	beforeAll(() => {
		({ appConfig } = baseSetup());
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		vi.clearAllMocks();
		posthogMock.client.__loaded = true;
		posthogMock.client.get_distinct_id.mockReturnValue(bootstrap.distinctId);
	});

	test('Should keep replay URL and payload metadata private in native hooks', () => {
		expect(stripUrlMetadata('https://user:password@example.test/path?token=secret#fragment'))
			.toBe('https://example.test/path');
		expect(maskReplayAttribute('href', 'https://user:password@example.test/path?q=secret#fragment'))
			.toBe('https://example.test/path');
		expect(maskReplayAttribute('style', 'background:url("https://example.test/image.png?q=secret#fragment")'))
			.toBe('background:url("https://example.test/image.png")');

		const maskedNetwork = maskReplayNetworkRequest({
			name: 'https://user:password@example.test/api?q=secret#fragment',
			entryType: 'resource',
			startTime: 0,
			duration: 0,
			requestHeaders: { Authorization: 'Bearer secret' },
			responseHeaders: { 'Set-Cookie': 'secret' },
			requestBody: 'secret',
			responseBody: 'secret',
		} satisfies CapturedNetworkRequest);
		expect(maskedNetwork.name).toBe('https://example.test/api');
		expect(maskedNetwork).not.toHaveProperty('requestHeaders');
		expect(maskedNetwork).not.toHaveProperty('responseHeaders');
		expect(maskedNetwork).not.toHaveProperty('requestBody');
		expect(maskedNetwork).not.toHaveProperty('responseBody');

		const exception = sanitizeCapture({
			uuid: '00000000-0000-4000-8000-000000000001',
			event: '$exception',
			properties: {
				token: bootstrap.projectToken,
				$exception_list: [{
					type: 'Error',
					value: 'browser token should remain for diagnosis',
					stacktrace: { frames: [{ filename: 'app.ts', lineno: 1, colno: 1 }] },
				}],
				$exception_level: 'error',
				request: { body: 'raw API response' },
				response: { headers: { Authorization: 'Bearer secret' } },
				$current_url: 'https://user:password@example.test/page?q=secret#fragment',
				$referrer: 'https://referrer-user:password@example.test/from?q=secret#fragment',
			},
			$set_once: {
				$initial_referrer: 'https://user:password@example.test/initial?q=secret#fragment',
				$initial_current_url: 'https://user:password@example.test/start?q=secret#fragment',
			},
		} satisfies CaptureResult);
		expect(exception.properties).toMatchObject({
			token: bootstrap.projectToken,
			$exception_list: [{
				type: 'Error',
				value: 'browser token should remain for diagnosis',
				stacktrace: { frames: [{ filename: 'app.ts', lineno: 1, colno: 1 }] },
			}],
			$exception_level: 'error',
			$current_url: 'https://example.test/page',
			$referrer: 'https://example.test/from',
		});
		expect(exception.properties).not.toHaveProperty('request');
		expect(exception.properties).not.toHaveProperty('response');
		expect(exception.$set_once).toMatchObject({
			$initial_referrer: 'https://example.test/initial',
			$initial_current_url: 'https://example.test/start',
		});

		const snapshot = sanitizeCapture({
			uuid: '00000000-0000-4000-8000-000000000002',
			event: '$snapshot',
			properties: {
				token: bootstrap.projectToken,
				$snapshot_data: [{
					type: 4,
					data: {
						href: 'https://user:password@example.test/replay?q=secret#fragment',
					},
				}],
			},
		} satisfies CaptureResult);
		expect(snapshot.properties.token).toBe(bootstrap.projectToken);
		expect(snapshot.properties?.$snapshot_data).toEqual([{
			type: 4,
			data: {
				href: 'https://user:password@example.test/replay?q=secret#fragment',
			},
		}]);
	});

	test('Should initialize from public bootstrap before login and continue through logout', async () => {
		// Arrange
		const globalStore = useGlobalStore();
		globalStore.config = appConfig;
		mock.onGet(TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())
			.reply(200, generateResultDTO(bootstrap));
		const currentRoute = ref({ path: '/login' });
		let afterEachHook: ((to: { path: string }, from: { path: string }, failure?: unknown) => void) | undefined;
		const router = {
			currentRoute,
			afterEach: vi.fn((callback: NonNullable<typeof afterEachHook>) => {
				afterEachHook = callback;
			}),
		} as unknown as Router;
		const hooks = new Map<string, PluginHook>();

		// Act
		runPlugin(router, hooks);
		expect(posthogMock.client.identify).not.toHaveBeenCalled();
		expect(posthogMock.client.capture).not.toHaveBeenCalled();
		await vi.waitFor(() => expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId));
		const authenticationStore = useAuthenticationStore();
		authenticationStore.isLoggedIn = true;
		authenticationStore.isLoggedIn = false;
		// A backend 401 invokes the global session reset; telemetry must stay active.
		await useGlobalStore().resetSession();
		const originalError = new Error('browser exception');
		hooks.get('vue:error')?.(originalError);
		afterEachHook?.({ path: '/settings' }, { path: '/login' });
		afterEachHook?.({ path: '/settings' }, { path: '/settings' });
		afterEachHook?.({ path: '/details' }, { path: '/settings' }, new Error('navigation failed'));
		afterEachHook?.({ path: '/login' }, { path: '/details' });

		// Assert
		expect(mock.history.get.filter((request) => request.url === TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())).toHaveLength(1);
		expect(posthogMock.client.opt_out_capturing).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.opt_in_capturing).toHaveBeenCalledExactlyOnceWith({ captureEventName: false });
		expect(posthogMock.client.startSessionRecording).toHaveBeenCalledOnce();
		expect(posthogMock.client.startExceptionAutocapture).toHaveBeenCalledOnce();
		expect(posthogMock.client.reset).not.toHaveBeenCalled();
		expect(posthogMock.client.stopSessionRecording).not.toHaveBeenCalled();
		expect(posthogMock.client.stopExceptionAutocapture).not.toHaveBeenCalled();
		expect(posthogMock.client.capture).toHaveBeenCalledTimes(3);
		expect(posthogMock.client.capture.mock.calls.map(([event]) => event)).toEqual(['$pageview', '$pageview', '$pageview']);
		expect(posthogMock.client.captureException).toHaveBeenCalledExactlyOnceWith(originalError);
		expect(posthogMock.client.set_config).toHaveBeenCalledWith(expect.objectContaining({
			autocapture: true,
			capture_pageview: false,
			capture_pageleave: true,
			capture_exceptions: true,
			disable_session_recording: false,
			rageclick: true,
			capture_dead_clicks: true,
			capture_heatmaps: true,
		}));
		expect(posthogMock.client.set_config).toHaveBeenCalledWith({
			tracing_headers: [new URL(appConfig.baseUrl).hostname],
		});
	});

	test('Should reset a different persisted identity only when the public bootstrap arrives', async () => {
		// Arrange
		posthogMock.client.get_distinct_id.mockReturnValue('previous-account');
		useGlobalStore().config = appConfig;
		mock.onGet(TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())
			.reply(200, generateResultDTO(bootstrap));

		// Act
		runPlugin({ currentRoute: ref({ path: '/login' }), afterEach: vi.fn() } as unknown as Router, new Map());
		expect(posthogMock.client.reset).not.toHaveBeenCalled();
		await vi.waitFor(() => expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId));

		// Assert
		expect(posthogMock.client.reset).toHaveBeenCalledOnce();
		expect(posthogMock.client.reset.mock.invocationCallOrder[0])
			.toBeLessThan(posthogMock.client.opt_in_capturing.mock.invocationCallOrder[0]!);
	});

	test('Should preserve the persisted identity when the plugin reloads', async () => {
		// Arrange
		useGlobalStore().config = appConfig;
		mock.onGet(TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())
			.reply(200, generateResultDTO(bootstrap));

		// Act
		runPlugin({ currentRoute: ref({ path: '/login' }), afterEach: vi.fn() } as unknown as Router, new Map());
		await vi.waitFor(() => expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId));
		vi.clearAllMocks();
		posthogMock.client.__loaded = true;
		posthogMock.client.get_distinct_id.mockReturnValue(bootstrap.distinctId);
		runPlugin({ currentRoute: ref({ path: '/login' }), afterEach: vi.fn() } as unknown as Router, new Map());
		await vi.waitFor(() => expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId));

		// Assert
		expect(posthogMock.client.reset).not.toHaveBeenCalled();
		expect(posthogMock.client.startSessionRecording).toHaveBeenCalledOnce();
	});

	test('Should leave an unconfigured module client inactive without requesting bootstrap', () => {
		// Arrange
		posthogMock.client.__loaded = false;
		useGlobalStore().config = appConfig;
		const hooks = new Map<string, PluginHook>();

		// Act
		runPlugin({ currentRoute: ref({ path: '/' }), afterEach: vi.fn() } as unknown as Router, hooks);
		const [, telemetry] = posthogMock.provide.mock.calls[0]!;
		telemetry.generalSettingsChanged();
		telemetry.diagnosticsRequested();

		// Assert
		expect(mock.history.get.filter((request) => request.url === TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())).toHaveLength(0);
		expect(posthogMock.client.capture).not.toHaveBeenCalled();
		expect(posthogMock.client.opt_in_capturing).not.toHaveBeenCalled();
		expect(posthogMock.client.set_config).not.toHaveBeenCalled();
	});

	test('Should keep the application nonbreaking when public bootstrap fails', async () => {
		// Arrange
		useGlobalStore().config = appConfig;
		mock.onGet(TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())
			.reply(503, generateFailedResultDTO({ statusCode: 503 }));

		// Act
		runPlugin({ currentRoute: ref({ path: '/login' }), afterEach: vi.fn() } as unknown as Router, new Map());
		await vi.waitFor(() => expect(mock.history.get.filter((request) => request.url === TelemetryPaths.getBrowserTelemetryBootstrapEndpoint())).toHaveLength(1));

		// Assert
		expect(posthogMock.client.opt_out_capturing).toHaveBeenCalledOnce();
		expect(posthogMock.client.identify).not.toHaveBeenCalled();
		expect(posthogMock.client.opt_in_capturing).not.toHaveBeenCalled();
		expect(posthogMock.client.startSessionRecording).not.toHaveBeenCalled();
		expect(posthogMock.client.startExceptionAutocapture).not.toHaveBeenCalled();
		expect(posthogMock.client.capture).not.toHaveBeenCalled();
		expect(posthogMock.client.captureException).not.toHaveBeenCalled();
	});
});
