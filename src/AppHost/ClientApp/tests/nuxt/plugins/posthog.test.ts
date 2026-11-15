import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { ref } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import type { Router } from 'vue-router';
import type { CaptureResult, CapturedNetworkRequest, PostHogConfig } from 'posthog-js';
import { mockNuxtImport } from '@nuxt/test-utils/runtime';

import type { BrowserTelemetryBootstrapDTO } from '@dto';
import { baseSetup, baseVars } from '@services-test-base';
import { useAuthenticationStore, useGlobalStore } from '@store';
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
	let { appConfig } = baseVars();

	beforeAll(() => {
		({ appConfig } = baseSetup());
	});

	beforeEach(() => {
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
				$exception_message: 'browser token should remain for diagnosis',
				$exception_stack_trace: 'Error: browser token\n    at app.ts:1:1',
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
			$exception_message: 'browser token should remain for diagnosis',
			$exception_stack_trace: 'Error: browser token\n    at app.ts:1:1',
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

	test('Should activate the module client only after authenticated bootstrap and reset on logout', () => {
		const authenticationStore = useAuthenticationStore();
		const globalStore = useGlobalStore();
		globalStore.config = appConfig;

		const currentRoute = ref({ path: '/library' });
		let afterEachHook: ((to: { path: string }, from: { path: string }, failure?: unknown) => void) | undefined;
		const router = {
			currentRoute,
			afterEach: vi.fn((callback: NonNullable<typeof afterEachHook>) => {
				afterEachHook = callback;
			}),
		} as unknown as Router;
		const hooks = new Map<string, PluginHook>();
		runPlugin(router, hooks);
		expect(posthogMock.client.opt_out_capturing).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.reset).not.toHaveBeenCalled();
		hooks.get('vue:error')?.(new Error('unauthenticated browser error'));
		afterEachHook?.({ path: '/settings' }, { path: '/library' });
		expect(posthogMock.client.capture).not.toHaveBeenCalled();
		expect(posthogMock.client.captureException).not.toHaveBeenCalled();

		authenticationStore.telemetryBootstrap = bootstrap;
		expect(posthogMock.client.opt_in_capturing).not.toHaveBeenCalled();
		authenticationStore.isLoggedIn = true;
		expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId);
		expect(posthogMock.client.startSessionRecording).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.reset).not.toHaveBeenCalled();
		expect(posthogMock.client.capture).toHaveBeenCalledWith('$pageview', expect.objectContaining({
			$current_url: expect.any(String),
		}));
		const initialPageviews = posthogMock.client.capture.mock.calls.length;
		authenticationStore.telemetryBootstrap = { ...bootstrap };
		expect(posthogMock.client.identify).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.capture).toHaveBeenCalledTimes(initialPageviews);

		afterEachHook?.({ path: '/settings' }, { path: '/library' });
		expect(posthogMock.client.capture).toHaveBeenCalledTimes(initialPageviews + 1);
		afterEachHook?.({ path: '/settings' }, { path: '/settings' });
		afterEachHook?.({ path: '/details' }, { path: '/settings' }, new Error('navigation failed'));
		afterEachHook?.({ path: '/login' }, { path: '/settings' });
		expect(posthogMock.client.capture).toHaveBeenCalledTimes(initialPageviews + 1);
		const originalError = new Error('original browser exception');
		hooks.get('vue:error')?.(originalError);
		expect(posthogMock.client.captureException).toHaveBeenCalledWith(originalError);

		authenticationStore.isLoggedIn = false;
		expect(posthogMock.client.stopSessionRecording).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.stopExceptionAutocapture).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.opt_out_capturing).toHaveBeenCalledTimes(2);
		expect(posthogMock.client.reset).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.set_config).toHaveBeenLastCalledWith({ tracing_headers: [] });
		hooks.get('vue:error')?.(new Error('logged out browser error'));
		expect(posthogMock.client.captureException).toHaveBeenCalledTimes(1);

		authenticationStore.telemetryBootstrap = null;
		authenticationStore.telemetryBootstrap = bootstrap;
		authenticationStore.isLoggedIn = true;
		expect(posthogMock.client.reset).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.identify).toHaveBeenCalledTimes(2);
		expect(posthogMock.client.opt_in_capturing).toHaveBeenNthCalledWith(1, { captureEventName: false });
		expect(posthogMock.client.opt_in_capturing).toHaveBeenNthCalledWith(2, { captureEventName: false });
		expect(posthogMock.client.startSessionRecording).toHaveBeenCalledTimes(2);
		expect(posthogMock.client.set_config.mock.calls.every(([config]) => !('api_host' in config) && !('token' in config))).toBe(true);
	});

	test('Should reset a different persisted identity only when authenticated capture becomes eligible', () => {
		// Arrange
		posthogMock.client.get_distinct_id.mockReturnValue('previous-account');
		useGlobalStore().config = appConfig;
		const authenticationStore = useAuthenticationStore();
		runPlugin({ currentRoute: ref({ path: '/library' }), afterEach: vi.fn() } as unknown as Router, new Map());
		expect(posthogMock.client.reset).not.toHaveBeenCalled();

		// Act
		authenticationStore.telemetryBootstrap = bootstrap;
		authenticationStore.isLoggedIn = true;

		// Assert
		expect(posthogMock.client.reset).toHaveBeenCalledTimes(1);
		expect(posthogMock.client.identify).toHaveBeenCalledExactlyOnceWith(bootstrap.distinctId);
		expect(posthogMock.client.reset.mock.invocationCallOrder[0])
			.toBeLessThan(posthogMock.client.opt_in_capturing.mock.invocationCallOrder[0]!);
		expect(posthogMock.client.capture).toHaveBeenCalledExactlyOnceWith('$pageview', expect.any(Object));
	});

	test('Should leave an unconfigured module client inactive', () => {
		posthogMock.client.__loaded = false;
		const hooks = new Map<string, PluginHook>();
		runPlugin({ currentRoute: ref({ path: '/' }), afterEach: vi.fn() } as unknown as Router, hooks);
		const authenticationStore = useAuthenticationStore();
		authenticationStore.telemetryBootstrap = bootstrap;
		authenticationStore.isLoggedIn = true;
		const [, telemetry] = posthogMock.provide.mock.calls[0]!;
		telemetry.generalSettingsChanged();
		telemetry.diagnosticsRequested();
		expect(posthogMock.client.capture).not.toHaveBeenCalled();
		expect(posthogMock.client.opt_in_capturing).not.toHaveBeenCalled();
	});
});
