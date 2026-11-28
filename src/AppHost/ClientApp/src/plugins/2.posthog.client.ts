import type { CaptureResult, CapturedNetworkRequest, PostHog } from 'posthog-js';
import type { Router } from 'vue-router';
import { watch } from 'vue';
import { telemetryApi } from '@api';
import { useGlobalStore } from '@store';
import type { BrowserTelemetryBootstrapDTO } from '@dto';
import { defineNuxtPlugin } from '#app';
import { createTelemetry } from '@composables/telemetry/createTelemetry';
// Bundle the version-pinned recorder adapter instead of downloading an unpatched recorder.
import 'posthog-js/dist/reaparr-recorder.mjs';

const exceptionAutocaptureConfig = {
	capture_unhandled_errors: true,
	capture_unhandled_rejections: true,
	capture_console_errors: false,
};

const collectorConfig = {
	autocapture: true,
	capture_pageview: false,
	capture_pageleave: true,
	capture_exceptions: true,
	disable_session_recording: false,
	rageclick: true,
	capture_dead_clicks: true,
	capture_heatmaps: true,
};

const replayUrlAttributes: Record<string, true> = {
	href: true,
	src: true,
	action: true,
	poster: true,
	formaction: true,
	background: true,
};
const replayUrlPropertyNames: Record<string, true> = {
	currenturl: true,
	initialcurrenturl: true,
	sessionentryurl: true,
	referrer: true,
	url: true,
	urlfull: true,
	href: true,
	src: true,
	pageurl: true,
	requesturl: true,
	responseurl: true,
	externalclickurl: true,
	stylesheeturl: true,
	imageurl: true,
};
const credentialPropertyNames: Record<string, true> = {
	authorization: true, proxyauthorization: true, apikey: true, password: true, confirmpassword: true, currentpassword: true, newpassword: true,
	cookie: true, cookies: true, setcookie: true, credential: true, credentials: true, secret: true, token: true, accesstoken: true, refreshtoken: true,
	authtoken: true, authenticationtoken: true, plexauthtoken: true, xplextoken: true, xapikey: true, sid: true, sessiontoken: true,
};

function normalizedPropertyName(name: string): string {
	return name.replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
}

function looksLikeUrl(value: string): boolean {
	const trimmed = value.trim();
	return /^(?:https?:)?\/\//i.test(trimmed)
		|| trimmed.startsWith('/')
		|| trimmed.startsWith('?')
		|| trimmed.startsWith('#');
}

/** Redact credential parameters without dropping repeated diagnostic values or nested URLs. */
function redactParameters(parameters: URLSearchParams): void {
	for (const key of [...parameters.keys()]) {
		if (credentialPropertyNames[normalizedPropertyName(key)]) {
			parameters.set(key, '[redacted]');
			continue;
		}
		const values = parameters.getAll(key);
		if (!values.some(looksLikeUrl)) continue;
		parameters.delete(key);
		for (const value of values) parameters.append(key, looksLikeUrl(value) ? redactUrlCredentials(value) : value);
	}
}

/** Retain diagnostic URL metadata while removing userinfo and credential parameter values. */
export function redactUrlCredentials(value: string): string {
	const trimmed = value.trim();
	if (!trimmed || !looksLikeUrl(trimmed)) return value;

	try {
		const baseUrl = typeof window === 'undefined' ? 'http://localhost/' : window.location.href;
		const url = new URL(trimmed, baseUrl);
		if (!['http:', 'https:'].includes(url.protocol)) return value;
		url.username = '';
		url.password = '';
		redactParameters(url.searchParams);
		if (url.hash.includes('=')) {
			const fragment = new URLSearchParams(url.hash.slice(1));
			redactParameters(fragment);
			url.hash = fragment.toString();
		}
		return url.href;
	} catch {
		return value.split(/[?#]/, 1)[0] ?? value;
	}
}

function maskStyleUrls(value: string): string {
	return value.replace(/url\(\s*(['"]?)([^'"\s)]+)\1\s*\)/gi, (_match, quote: string, url: string) => {
		// rrweb resolves CSS fragment references against the document before this hook runs.
		if (/^#[a-zA-Z_][\w:.-]*$/.test(url)) return `url(${quote}${url}${quote})`;
		if (typeof window !== 'undefined' && URL.canParse(url, window.location.href)) {
			const reference = new URL(url, window.location.href);
			if (reference.href === `${window.location.href.split('#', 1)[0]}${reference.hash}` && /^#[a-zA-Z_][\w:.-]*$/.test(reference.hash)) {
				return `url(${quote}${reference.hash}${quote})`;
			}
		}
		return `url(${quote}${redactUrlCredentials(url)}${quote})`;
	});
}

/** Keep local SVG references intact; strip private metadata from navigation and resource URLs. */
export function maskReplayAttribute(name: string, value: string, element?: Element): string {
	const attribute = name.toLowerCase();
	if (attribute === 'style') return maskStyleUrls(value);
	if (element?.namespaceURI === 'http://www.w3.org/2000/svg') {
		if (/^(?:href|xlink:href)$/.test(attribute) && /^#[a-zA-Z_][\w:.-]*$/.test(value)) return value;
	}
	if (replayUrlAttributes[attribute] || attribute === 'xlink:href') return redactUrlCredentials(value);
	return value;
}

/** Keep native debugging content; credentials are redacted before replay leaves the browser. */
export function maskReplayNetworkRequest(data: CapturedNetworkRequest): CapturedNetworkRequest | null {
	const url = new URL(data.name, window.location.href);
	if (/\/e\/(?:i|s|e|batch|capture|flags|decide|array|static)(?:\/|$)/.test(url.pathname)) return null;
	const masked = { ...data, name: redactUrlCredentials(data.name) };
	for (const key of ['requestHeaders', 'responseHeaders'] as const) {
		if (masked[key]) masked[key] = sanitizeValue(masked[key], '') as CapturedNetworkRequest[typeof key];
	}
	for (const key of ['requestBody', 'responseBody'] as const) {
		const body = masked[key];
		if (!body) continue;
		try {
			masked[key] = JSON.stringify(sanitizeValue(JSON.parse(body), ''));
		} catch {
			// Opaque authentication payloads cannot safely be projected field by field.
			if (/authentication|login|token/i.test(url.pathname)) masked[key] = '[redacted]';
			else if (body.includes('=')) {
				const form = new URLSearchParams(body);
				redactParameters(form);
				masked[key] = form.toString();
			}
		}
	}
	return masked;
}

function sanitizeValue(value: unknown, propertyName: string): unknown {
	if (typeof value === 'string') {
		const normalizedName = normalizedPropertyName(propertyName);
		return replayUrlPropertyNames[normalizedName] || normalizedName.endsWith('url') || normalizedName.endsWith('referrer')
			? redactUrlCredentials(value)
			: value;
	}
	if (Array.isArray(value)) return value.map((item) => sanitizeValue(item, propertyName));
	if (!value || typeof value !== 'object') return value;

	const normalizedName = normalizedPropertyName(propertyName);
	if (normalizedName === 'snapshotdata') return value;

	const sanitized: Record<string, unknown> = {};
	for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
		if (credentialPropertyNames[normalizedPropertyName(key)]) {
			sanitized[key] = '[redacted]';
			continue;
		}
		sanitized[key] = sanitizeValue(child, key);
	}
	return sanitized;
}

/** Retain diagnostic properties and original exception context while redacting credential fields. */
export function sanitizeCapture(result: CaptureResult): CaptureResult {
	return {
		...result,
		properties: {
			...(sanitizeValue(result.properties, '') as CaptureResult['properties']),
			// The native public project key is a required transport field, not a user credential.
			token: result.properties.token,
		},
		$set: result.$set ? sanitizeValue(result.$set, '') as CaptureResult['$set'] : result.$set,
		$set_once: result.$set_once ? sanitizeValue(result.$set_once, '') as CaptureResult['$set_once'] : result.$set_once,
	};
}

function getBackendHostname(baseUrl: string): string | undefined {
	try {
		return new URL(baseUrl).hostname || undefined;
	} catch {
		return undefined;
	}
}

function capturePageview(client: PostHog): void {
	const properties: Record<string, string> = {
		$current_url: redactUrlCredentials(window.location.href),
	};
	if (document.referrer) properties.$referrer = redactUrlCredentials(document.referrer);
	client.capture('$pageview', properties);
}

export default defineNuxtPlugin({
	name: 'reaparr-telemetry',
	dependsOn: ['posthog-client'],
	setup(nuxtApp) {
		const moduleClient = usePostHog();
		nuxtApp.provide('telemetry', createTelemetry(moduleClient?.__loaded ? moduleClient : undefined));
		if (!moduleClient?.__loaded) return;
		const client = moduleClient;
		// Stored consent from an earlier document must not admit anonymous startup events.
		client.opt_out_capturing();

		const router = nuxtApp.$router as Router;
		const globalStore = useGlobalStore();
		let activeIdentity: string | undefined;

		// Native collectors remain opt-out until the public startup bootstrap supplies the existing UUID.
		// Keep captured DOM text/attributes masked while retaining structural interaction metadata.
		client.set_config({
			// Install in the browser: Nuxt serializes the module's clientConfig as runtime data.
			rewriteRequestPath: (url) => {
				url.pathname = url.pathname
					.replace(/\/e\/i\/v0\/e\/$/, '/e/submit/')
					.replace(/\/e\/e\/$/, '/e/submit-batch/');
				return url;
			},
			before_send: sanitizeCapture,
			mask_all_text: true,
			mask_all_element_attributes: true,
			session_recording: {
				// Canvas pixels bypass DOM masking; only the decorative background is safe to capture.
				captureCanvas: { recordCanvas: true, canvasFps: 4, canvasQuality: '0.9' },
				canvasCapture: {
					maskRegionsFn: (canvas) => canvas.classList.contains('vanta-canvas') ? [] : null,
				},
				maskAllInputs: true,
				maskCapturedNetworkRequestFn: maskReplayNetworkRequest,
				maskAttributeFn: maskReplayAttribute,
				recordHeaders: true,
				recordBody: true,
				captureJsonLd: false,
				collectFonts: false,
				inlineStylesheet: true,
				// Quasar's combined stylesheet exceeds rrweb's default 10,000-rule budget.
				inlineStylesheetBudgetRules: 20000,
			},
		});

		function enableCapture(bootstrap: BrowserTelemetryBootstrapDTO): void {
			if (client.get_distinct_id() !== bootstrap.distinctId) client.reset();
			client.unregister('environment');
			client.unregister('release');
			client.register({
				release_version: bootstrap.releaseVersion,
				runtime_mode: bootstrap.runtimeMode,
				runtime_identifier: bootstrap.runtimeIdentifier,
				release_channel: bootstrap.releaseChannel,
				runtime_environment: bootstrap.runtimeEnvironment,
				$process_person_profile: true,
				$geoip_disable: false,
			});
			client.opt_in_capturing({ captureEventName: false });
			client.identify(bootstrap.distinctId);
			client.set_config(collectorConfig);
			const backendHostname = getBackendHostname(globalStore.config.baseUrl);
			client.set_config({ tracing_headers: backendHostname ? [backendHostname] : [] });
			client.startExceptionAutocapture(exceptionAutocaptureConfig);
			client.startSessionRecording();
			activeIdentity = bootstrap.distinctId;
			capturePageview(client);
		}

		let bootstrapRequestStarted = false;
		watch(
			() => globalStore.config.baseUrl,
			(baseUrl) => {
				if (!baseUrl || bootstrapRequestStarted) return;
				bootstrapRequestStarted = true;
				telemetryApi.getBrowserTelemetryBootstrapEndpoint({ handleErrorsLocally: true }).subscribe({
					next: (result) => {
						if (result.isSuccess && result.value) enableCapture(result.value);
					},
					error: () => undefined,
				});
			},
			{ immediate: true, flush: 'sync' },
		);

		router.afterEach((to, from, failure) => {
			if (!activeIdentity || failure || to.path === from.path) return;
			capturePageview(client);
		});
		nuxtApp.hook('vue:error', (error) => {
			if (activeIdentity) client.captureException(error);
		});
	},
});
