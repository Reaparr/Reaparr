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
	redactUrlCredentials,
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
		unregister: vi.fn(),
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
	releaseVersion: '0.41.0-test',
	releaseChannel: 'dev',
	runtimeEnvironment: 'Production',
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

	test('Should retain diagnostic URLs and payloads while redacting credentials', () => {
		// Arrange / Act
		const url = redactUrlCredentials('https://user:password@example.test/path?q=movie&token=secret#details');
		expect(url).toBe('https://example.test/path?q=movie&token=%5Bredacted%5D#details');
		expect(maskReplayAttribute('href', 'https://user:password@example.test/path?q=movie#fragment')).toBe('https://example.test/path?q=movie#fragment');
		const maskedNetwork = maskReplayNetworkRequest({
			name: 'https://user:password@example.test/api?q=movie#fragment', entryType: 'resource', startTime: 0, duration: 12,
			requestHeaders: { Authorization: 'Bearer secret', 'Content-Type': 'application/json' },
			responseHeaders: { 'Set-Cookie': 'secret', 'X-Request-ID': 'request-123' },
			requestBody: JSON.stringify({ search: 'exact search', password: 'secret', nested: { apiKey: 'secret', page: 2 } }),
			responseBody: JSON.stringify({ result: 'found', accessToken: 'secret' }),
		} satisfies CapturedNetworkRequest);
		// Assert
		expect(maskedNetwork).toMatchObject({ name: 'https://example.test/api?q=movie#fragment', duration: 12,
			requestHeaders: { Authorization: '[redacted]', 'Content-Type': 'application/json' },
			responseHeaders: { 'Set-Cookie': '[redacted]', 'X-Request-ID': 'request-123' },
		});
		expect(JSON.parse(maskedNetwork!.requestBody!)).toEqual({ search: 'exact search', password: '[redacted]', nested: { apiKey: '[redacted]', page: 2 } });
		expect(JSON.parse(maskedNetwork!.responseBody!)).toEqual({ result: 'found', accessToken: '[redacted]' });
		expect(JSON.stringify(maskedNetwork)).not.toContain('secret');
		const exception = sanitizeCapture({ uuid: '00000000-0000-4000-8000-000000000001', event: '$exception', properties: {
			token: bootstrap.projectToken, request: { body: { search: 'movie', password: 'secret' } },
			response: { headers: { Authorization: 'Bearer secret', 'Content-Type': 'application/json' }, data: { found: true } },
			$current_url: 'https://user:password@example.test/page?q=movie#fragment',
		}, $set_once: { $initial_current_url: 'https://example.test/start?q=movie#details' } } satisfies CaptureResult);
		expect(exception.properties).toMatchObject({ token: bootstrap.projectToken,
			request: { body: { search: 'movie', password: '[redacted]' } },
			response: { headers: { Authorization: '[redacted]' }, data: { found: true } },
			$current_url: 'https://example.test/page?q=movie#fragment',
		});
		expect(exception.$set_once).toEqual({ $initial_current_url: 'https://example.test/start?q=movie#details' });

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

	test('Should preserve SVG paint and local references without retaining remote URL credentials', () => {
		// Arrange
		const style = 'fill:url(#gradient);background:url("https://user:password@example.test/image.png?token=secret#private")';
		const svgUse = document.createElementNS('http://www.w3.org/2000/svg', 'use');

		// Act / Assert
		expect(maskReplayAttribute('fill', '#fff')).toBe('#fff');
		expect(maskReplayAttribute('stroke', '#123456')).toBe('#123456');
		expect(maskReplayAttribute('href', '#logo', svgUse)).toBe('#logo');
		expect(maskReplayAttribute('xlink:href', '#gradient', svgUse)).toBe('#gradient');
		expect(maskReplayAttribute('href', '#private')).toContain('#private');
		expect(maskReplayAttribute('style', style)).toBe('fill:url(#gradient);background:url("https://example.test/image.png?token=%5Bredacted%5D#private")');
		expect(maskReplayAttribute('style', `filter:url(${window.location.href.split('#', 1)[0]}#gradient)`)).toBe('filter:url(#gradient)');
		expect(maskReplayAttribute('xlink:href', 'https://user:password@example.test/icons.svg?token=secret#logo')).toBe('https://example.test/icons.svg?token=%5Bredacted%5D#logo');
	});

	test('Should exclude telemetry traffic and preserve safe encoded URL parameters', () => {
		// Arrange
		const request = { name: '/base/e/s/', entryType: 'resource', startTime: 0, duration: 1 } satisfies CapturedNetworkRequest;
		// Act / Assert
		expect(maskReplayNetworkRequest(request)).toBeNull();
		const url = new URL(redactUrlCredentials('https://example.test/?q=one&q=two&redirect=https%3A%2F%2Fexample.test%2F%3Ftoken%3Dsecret#token=secret&tab=details'));
		expect(url.searchParams.getAll('q')).toEqual(['one', 'two']);
		expect(url.searchParams.get('redirect')).toBe('https://example.test/?token=%5Bredacted%5D');
		expect(new URLSearchParams(url.hash.slice(1)).get('token')).toBe('[redacted]');
		expect(new URLSearchParams(url.hash.slice(1)).get('tab')).toBe('details');
		expect(url.href).not.toContain('secret');
		const formRequest = maskReplayNetworkRequest({ ...request, name: '/api/debug', requestBody: 'search=movie&password=secret' });
		expect(formRequest?.requestBody).toBe('search=movie&password=%5Bredacted%5D');
		const textResponse = maskReplayNetworkRequest({ ...request, name: '/api/debug', responseBody: 'ordinary failure details' });
		expect(textResponse?.responseBody).toBe('ordinary failure details');
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
		expect(posthogMock.client.unregister).toHaveBeenCalledWith('environment');
		expect(posthogMock.client.unregister).toHaveBeenCalledWith('release');
		expect(posthogMock.client.register).toHaveBeenCalledWith(expect.objectContaining({ release_channel: 'dev', runtime_environment: 'Production', release_version: bootstrap.releaseVersion }));
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
		expect(posthogMock.client.register).toHaveBeenCalledWith(expect.objectContaining({ $geoip_disable: false }));
		expect(posthogMock.client.set_config).toHaveBeenCalledWith(expect.objectContaining({ session_recording: expect.objectContaining({ recordBody: true, recordHeaders: true }) }));
		const recording = posthogMock.client.set_config.mock.calls
			.map(([config]) => config.session_recording)
			.find((config) => config?.canvasCapture);
		const background = document.createElement('canvas');
		background.classList.add('vanta-canvas');
		const privateCanvas = document.createElement('canvas');
		expect(recording?.canvasCapture?.maskRegionsFn?.(background)).toEqual([]);
		expect(recording?.canvasCapture?.maskRegionsFn?.(privateCanvas)).toBeNull();
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

	test.each([
		['/e/i/v0/e/', '/e/submit/'],
		['/client/e/i/v0/e/', '/client/e/submit/'],
		['/e/e/', '/e/submit-batch/'],
		['/e/s/', '/e/s/'],
		['/e/static/recorder.js', '/e/static/recorder.js'],
		['/e/i/v0/e/other', '/e/i/v0/e/other'],
	])('Should map only event capture paths (%s)', (path, expectedPath) => {
		// Arrange
		const url = new URL(path + '?retry_count=1&compression=gzip', 'https://reaparr.example.test');
		runPlugin({ afterEach: vi.fn() } as unknown as Router, new Map());
		const rewrite = posthogMock.client.set_config.mock.calls[0]![0].rewriteRequestPath!;

		// Act
		const rewrittenUrl = rewrite(url);

		// Assert
		expect(rewrittenUrl.href).toBe(`https://reaparr.example.test${expectedPath}?retry_count=1&compression=gzip`);
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
