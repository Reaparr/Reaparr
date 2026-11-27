import type { CaptureResult, CapturedNetworkRequest, PostHog } from 'posthog-js';
import type { Router } from 'vue-router';
import { watch } from 'vue';
import { telemetryApi } from '@api';
import { useGlobalStore } from '@store';
import type { BrowserTelemetryBootstrapDTO } from '@dto';
import { defineNuxtPlugin } from '#app';
import { createTelemetry } from '@composables/telemetry/createTelemetry';

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
const rawPayloadPropertyNames: Record<string, true> = {
	authorization: true,
	apikey: true,
	body: true,
	config: true,
	cookie: true,
	cookies: true,
	credential: true,
	credentials: true,
	data: true,
	error: true,
	headers: true,
	password: true,
	payload: true,
	request: true,
	requestbody: true,
	requestheaders: true,
	response: true,
	responsebody: true,
	responseheaders: true,
	secret: true,
	token: true,
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

/** Strip credentials, query strings and fragments while retaining the URL origin/path. */
export function stripUrlMetadata(value: string): string {
	const trimmed = value.trim();
	if (!trimmed || !looksLikeUrl(trimmed)) return value;

	try {
		const baseUrl = typeof window === 'undefined' ? 'http://localhost/' : window.location.href;
		const url = new URL(trimmed, baseUrl);
		if (!['http:', 'https:'].includes(url.protocol)) return value;
		url.username = '';
		url.password = '';
		url.search = '';
		url.hash = '';
		return url.origin + url.pathname;
	} catch {
		return value.split(/[?#]/, 1)[0] ?? value;
	}
}

function maskStyleUrls(value: string): string {
	return value.replace(/url\(\s*(['"]?)([^'"\s)]+)\1\s*\)/gi, (_match, quote: string, url: string) => {
		return `url(${quote}${stripUrlMetadata(url)}${quote})`;
	});
}

/** Native rrweb attribute hook for links, stylesheet/image sources and inline CSS URLs. */
export function maskReplayAttribute(name: string, value: string): string {
	if (name.toLowerCase() === 'style') return maskStyleUrls(value);
	if (replayUrlAttributes[name.toLowerCase()] || looksLikeUrl(value)) return stripUrlMetadata(value);
	return value;
}

/** Native replay network hook; network bodies and headers are never retained. */
export function maskReplayNetworkRequest(data: CapturedNetworkRequest): CapturedNetworkRequest {
	const masked = { ...data };
	delete masked.requestHeaders;
	delete masked.responseHeaders;
	delete masked.requestBody;
	delete masked.responseBody;
	masked.name = stripUrlMetadata(masked.name);
	return masked;
}

function sanitizeValue(value: unknown, propertyName: string): unknown {
	if (typeof value === 'string') {
		const normalizedName = normalizedPropertyName(propertyName);
		return replayUrlPropertyNames[normalizedName] || normalizedName.endsWith('url') || normalizedName.endsWith('referrer')
			? stripUrlMetadata(value)
			: value;
	}
	if (Array.isArray(value)) return value.map((item) => sanitizeValue(item, propertyName));
	if (!value || typeof value !== 'object') return value;

	const normalizedName = normalizedPropertyName(propertyName);
	if (normalizedName === 'snapshotdata') return value;

	const sanitized: Record<string, unknown> = {};
	for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
		if (rawPayloadPropertyNames[normalizedPropertyName(key)]) continue;
		sanitized[key] = sanitizeValue(child, key);
	}
	return sanitized;
}

/** Native analytics hook that keeps original exception text/stack but drops raw payload fields. */
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
		$current_url: stripUrlMetadata(window.location.href),
	};
	if (document.referrer) properties.$referrer = stripUrlMetadata(document.referrer);
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
				maskAllInputs: true,
				maskCapturedNetworkRequestFn: maskReplayNetworkRequest,
				maskAttributeFn: maskReplayAttribute,
				recordHeaders: false,
				recordBody: false,
				captureJsonLd: false,
				collectFonts: false,
				inlineStylesheet: false,
			},
		});

		function enableCapture(bootstrap: BrowserTelemetryBootstrapDTO): void {
			if (client.get_distinct_id() !== bootstrap.distinctId) client.reset();
			client.register({
				environment: bootstrap.environment,
				release: bootstrap.release,
				runtime_mode: bootstrap.runtimeMode,
				runtime_identifier: bootstrap.runtimeIdentifier,
				platform: bootstrap.platform,
				$process_person_profile: true,
				$geoip_disable: true,
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
