import { gunzipSync, strFromU8 } from 'fflate';
import { route } from '@fixtures';
import { AuthenticationPaths, DebugPaths, TelemetryPaths } from '@api-urls';
import { TelemetryDiagnosticOrigin, type BrowserTelemetryBootstrapDTO, type UserClaimsDTO } from '@dto';
import { generateFailedResultDTO, generateResultDTO } from '@mock';

const isolatedNativeBuild = Cypress.env('RP_KEY') === 'phc_cypress';
type NativeCapture = { event: string; properties: Record<string, unknown> };
const partialSnapshotCompressionVersion = '2024-10';

function asRecord(value: unknown): Record<string, unknown> | undefined {
	if (!value || typeof value !== 'object' || Array.isArray(value)) return undefined;
	return value as Record<string, unknown>;
}

async function decodePartialSnapshotJson(value: string): Promise<unknown> {
	const trimmed = value.trimStart();
	if (trimmed.startsWith('{') || trimmed.startsWith('[')) return JSON.parse(trimmed);
	const binaryBytes = Uint8Array.from(value, (character) => character.charCodeAt(0));
	const encoded = binaryBytes[0] === 0x1F && binaryBytes[1] === 0x8B
		? binaryBytes
		: Uint8Array.from(atob(value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4)), (character) => character.charCodeAt(0));
	const bytes = encoded;
	const text = strFromU8(bytes[0] === 0x1F && bytes[1] === 0x8B ? gunzipSync(bytes) : bytes);
	return JSON.parse(text);
}

async function decodePartialSnapshotEvents(value: unknown): Promise<unknown> {
	if (!Array.isArray(value)) return value;
	return Promise.all(value.map(async (event) => {
		const record = asRecord(event);
		if (!record || record.cv !== partialSnapshotCompressionVersion) return event;
		if (record.type === 2 && typeof record.data === 'string') {
			return { ...record, data: await decodePartialSnapshotJson(record.data) };
		}
		if (record.type !== 3) return event;
		const snapshotData = asRecord(record.data);
		if (!snapshotData) return event;
		const data = { ...snapshotData };
		const fields = data.source === 0
			? ['texts', 'attributes', 'removes', 'adds']
			: data.source === 8
				? ['adds', 'removes']
				: [];
		await Promise.all(fields.map(async (field) => {
			const fieldValue = data[field];
			if (typeof fieldValue === 'string') data[field] = await decodePartialSnapshotJson(fieldValue);
		}));
		return { ...record, data };
	}));
}

function collectSnapshotNodes(value: unknown, nodes: Array<Record<string, unknown>>): void {
	if (Array.isArray(value)) {
		value.forEach((item) => collectSnapshotNodes(item, nodes));
		return;
	}
	const record = asRecord(value);
	if (!record) return;
	const attributes = asRecord(record.attributes);
	if (typeof record.tagName === 'string' && typeof record.id === 'number' && attributes) {
		nodes.push(record);
	}
	Object.values(record).forEach((child) => collectSnapshotNodes(child, nodes));
}

function loginInputEvidence(captures: NativeCapture[]): { fixtureNodes: Array<Record<string, unknown>>; mutations: Array<Record<string, unknown>> } {
	const fixtureNodes: Array<Record<string, unknown>> = [];
	const mutations: Array<Record<string, unknown>> = [];
	for (const capture of captures) {
		if (capture.event !== '$snapshot' || !Array.isArray(capture.properties.$snapshot_data)) continue;
		const events = capture.properties.$snapshot_data;
		events.forEach((eventValue) => {
			const event = asRecord(eventValue);
			if (!event) return;
			collectSnapshotNodes(event.data, fixtureNodes);
			const data = asRecord(event.data);
			if (event.type === 3 && data?.source === 5) mutations.push(data);
		});
	}
	return {
		fixtureNodes: fixtureNodes.filter((node) => {
			const attributes = asRecord(node.attributes);
			return String(node.tagName).toLowerCase() === 'input'
				&& !!attributes
				&& Object.hasOwn(attributes, 'data-cy');
		}),
		mutations,
	};
}

async function decodeNativeCaptures(body: unknown): Promise<NativeCapture[]> {
	let value = body;
	let bytes: Uint8Array | undefined;
	if (typeof body === 'string') {
		if (body.startsWith('data=')) {
			bytes = Uint8Array.from(atob(new URLSearchParams(body).get('data')!), (character) => character.charCodeAt(0));
		} else if (body.charCodeAt(0) === 0x1F) {
			bytes = Uint8Array.from(body, (character) => character.charCodeAt(0));
		} else {
			value = JSON.parse(body);
		}
	} else if (body instanceof ArrayBuffer) {
		bytes = new Uint8Array(body);
	} else if (ArrayBuffer.isView(body)) {
		bytes = new Uint8Array(body.buffer, body.byteOffset, body.byteLength);
	} else {
		const bufferPayload = asRecord(body);
		const bufferData = bufferPayload?.data;
		if (bufferPayload?.type === 'Buffer' && Array.isArray(bufferData)) {
			bytes = new Uint8Array(bufferData as number[]);
		}
	}
	if (bytes) {
		const text = strFromU8(bytes[0] === 0x1F && bytes[1] === 0x8B ? gunzipSync(bytes) : bytes);
		value = JSON.parse(text);
	}
	let captures: NativeCapture[];
	if (Array.isArray(value)) {
		captures = value as NativeCapture[];
	} else {
		const batchPayload = asRecord(value);
		const batch = batchPayload?.batch;
		captures = Array.isArray(batch)
			? batch as NativeCapture[]
			: [value as NativeCapture];
	}
	return Promise.all(captures.map(async (capture) => {
		if (capture.event !== '$snapshot' || !Array.isArray(capture.properties.$snapshot_data)) return capture;
		return {
			...capture,
			properties: {
				...capture.properties,
				$snapshot_data: await decodePartialSnapshotEvents(capture.properties.$snapshot_data),
			},
		};
	}));
}

function installNativeReceiver(): NativeCapture[] {
	const captures: NativeCapture[] = [];
	cy.on('window:before:load', (window) => {
		Object.defineProperty(window.navigator, 'webdriver', { value: false });
	});
	cy.intercept({
		url: /^https:\/\/(?:[^/]+\.)?posthog\.com\/|^https:\/\/posthog\.isolated\.test\//,
		middleware: true,
	}, (request) => request.destroy()).as('nativeDirect');
	cy.intercept('GET', '**/e/**', (request) => {
		throw new Error(`Unhandled native resource: ${request.url}`);
	});
	cy.intercept('GET', '**/e/array/**/config*', {
		body: {
			hasFeatureFlags: false,
			autocapture_opt_out: false,
			heatmaps: true,
			captureDeadClicks: true,
			capturePerformance: { web_vitals: true },
			sessionRecording: { minimumDurationMilliseconds: 0 },
		},
		headers: { 'access-control-allow-origin': '*', 'content-type': 'application/json' },
	}).as('nativeConfig');
	cy.intercept('GET', '**/e/array/**/config.js*', { statusCode: 404, body: '' });
	for (const script of [
		'lazy-recorder', 'tracing-headers', 'exception-autocapture', 'dead-clicks-autocapture',
		'web-vitals', 'web-vitals-with-attribution', 'web-vitals-soft-navs', 'web-vitals-with-attribution-soft-navs',
	]) {
		cy.readFile(`node_modules/posthog-js/dist/${script}.js`, 'utf8').then((source) => {
			cy.intercept('GET', `**/e/static/**/${script}.js*`, {
				body: source,
				headers: { 'content-type': 'application/javascript', 'access-control-allow-origin': '*' },
			}).as(script === 'exception-autocapture' ? 'nativeExceptionAutocapture' : `native-${script}`);
		});
	}
	cy.intercept('OPTIONS', '**/e/**', {
		statusCode: 204,
		headers: {
			'access-control-allow-origin': '*',
			'access-control-allow-methods': 'GET,POST,OPTIONS',
			'access-control-allow-headers': 'content-type,authorization,x-posthog-distinct-id,x-posthog-session-id,x-posthog-window-id',
		},
	});
	cy.intercept('POST', '**/e/**', async (request) => {
		const events = await decodeNativeCaptures(request.body);
		events.forEach((event) => expect(event.properties.token, 'isolated native project').to.equal('phc_cypress'));
		if (events.some((event) => event.event === '$pageview')) {
			expect(new URL(request.url).pathname, 'first-party pageview capture path').to.be.oneOf(['/e/submit/', '/e/submit-batch/']);
		}
		captures.push(...events);
		request.reply({
			body: { status: 1 },
			headers: { 'access-control-allow-origin': '*', 'content-type': 'application/json' },
		});
	}).as('nativeCapture');
	return captures;
}

function installEmptyNativeReceiver(): string[] {
	const requests: string[] = [];
	cy.intercept({
		url: /^https:\/\/(?:[^/]+\.)?posthog\.com\/|^https:\/\/posthog\.isolated\.test\//,
		middleware: true,
	}, (request) => {
		requests.push(request.url);
		request.destroy();
	}).as('emptyNativeDirect');
	for (const method of ['GET', 'POST', 'OPTIONS'] as const) {
		cy.intercept(method, '**/e/**', (request) => {
			requests.push(request.url);
			request.reply({ statusCode: 404, body: '' });
		}).as(`emptyNative${method}`);
	}
	return requests;
}

describe('Telemetry diagnostics', () => {
	let nativeCaptures: NativeCapture[] = [];
	let emptyNativeRequests: string[] = [];

	beforeEach(() => {
		cy.basePageSetup({ seed: 678, plexAccountCount: 0, plexServerCount: 0 });
		if (isolatedNativeBuild) {
			nativeCaptures = installNativeReceiver();
			emptyNativeRequests = [];
		} else {
			emptyNativeRequests = installEmptyNativeReceiver();
		}
	});

	afterEach(() => {
		if (!isolatedNativeBuild) return;
		// Finish this document's native unload delivery while its receivers are still installed.
		cy.window().then((window) => {
			window.location.href = 'about:blank';
		});
		cy.window().its('location.href').should('eq', 'about:blank');
		cy.get('@nativeDirect.all').should('have.length', 0);
	});

	it('Should show independent outcomes, recover on a deliberate retry, and never send on page load', () => {
		const requests: unknown[] = [];
		cy.intercept('POST', `**${DebugPaths.sendTelemetryDiagnosticEndpoint()}`, (request) => {
			requests.push(request.body);
			const failed = requests.length === 1;
			request.reply({
				statusCode: failed ? 503 : 200,
				delay: 300,
				body: failed ? generateFailedResultDTO({ statusCode: 503 }) : generateResultDTO(null),
			});
		}).as('diagnostic');

		cy.visit(route('/debug-pages/telemetry'));
		cy.getCy('telemetry-diagnostic-page').should('be.visible');
		cy.getCy('telemetry-backend-status').should('contain.text', 'Not run yet');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Not run yet');
		cy.then(() => expect(requests).to.deep.equal([]));

		cy.getCy('telemetry-send-test-event').click().should('be.disabled');
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 503);
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.getCy('telemetry-backend-status').should('contain.text', 'Diagnostic event was not accepted for sending');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.getCy('alert-dialog').should('be.visible');
		cy.getCy('close-alert-dialog').click();
		cy.getCy('alert-dialog').should('not.exist');
		cy.getCy('telemetry-send-test-event').should('not.be.disabled').click();
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.getCy('telemetry-backend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.then(() => expect(requests).to.deep.equal([
			{ origin: TelemetryDiagnosticOrigin.Backend },
			{ origin: TelemetryDiagnosticOrigin.Frontend },
			{ origin: TelemetryDiagnosticOrigin.Backend },
			{ origin: TelemetryDiagnosticOrigin.Frontend },
		]));

		cy.reload();
		cy.getCy('telemetry-diagnostic-page').should('be.visible');
		cy.getCy('telemetry-backend-status').should('contain.text', 'Not run yet');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Not run yet');
		cy.then(() => expect(requests).to.have.length(4));
		if (!isolatedNativeBuild) {
			cy.then(() => expect(emptyNativeRequests, 'unconfigured native telemetry requests').to.deep.equal([]));
		}
	});

	(isolatedNativeBuild ? it : it.skip)('Should embed the loaded Quasar stylesheet in native replay snapshots', () => {
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			body: generateResultDTO<BrowserTelemetryBootstrapDTO>({
				apiHost: 'https://posthog.isolated.test', projectToken: 'phc_cypress',
				distinctId: 'c8b4b3fa-a606-477a-9079-79833ea61558', release: 'isolated-test',
				environment: 'test', runtimeMode: 'docker', runtimeIdentifier: 'browser', platform: 'browser',
			}),
		});
		cy.visit(route('/'));
		cy.getCy('account-selector-btn').should('be.visible');
		cy.getCy('account-selector-btn').click();
		cy.document().should((document) => {
			expect([...document.styleSheets].some((sheet) => [...sheet.cssRules].some((rule) => rule.cssText.includes('.q-layout'))), 'Quasar stylesheet loaded and readable').to.equal(true);
		});
		cy.wrap(null, { timeout: 30000 }).should(() => {
			const nodes: Array<Record<string, unknown>> = [];
			for (const capture of nativeCaptures) {
				if (capture.event === '$snapshot') collectSnapshotNodes(capture.properties.$snapshot_data, nodes);
			}
			expect(nodes.some((node) => {
				const css = asRecord(node.attributes)?._cssText;
				return typeof css === 'string' && css.includes('.q-layout');
			}), 'Quasar layout CSS embedded in emitted full snapshot').to.equal(true);
		});
	});

	(isolatedNativeBuild ? it : it.skip)('Should retain native network debug data without credential values', () => {
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			body: generateResultDTO<BrowserTelemetryBootstrapDTO>({ apiHost: 'https://posthog.isolated.test', projectToken: 'phc_cypress', distinctId: 'c8b4b3fa-a606-477a-9079-79833ea61558', release: 'isolated-test', environment: 'test', runtimeMode: 'docker', runtimeIdentifier: 'browser', platform: 'browser' }),
		});
		cy.intercept('POST', '**/debug-network?*', {
			body: { result: 'debug-result', accessToken: 'network-credential-sentinel' },
			headers: { 'content-type': 'application/json', 'x-debug-response': 'response-value' },
		});
		cy.visit(route('/').replace('localhost', 'reaparr.localhost'));
		cy.getCy('account-selector-btn').should('be.visible').click();
		cy.wait('@native-lazy-recorder');
		cy.window().then((window) => {
			const request = new window.XMLHttpRequest();
			request.open('POST', '/debug-network?search=movie&token=network-credential-sentinel');
			request.setRequestHeader('content-type', 'application/json');
			request.setRequestHeader('authorization', 'Bearer network-credential-sentinel');
			request.setRequestHeader('x-debug-request', 'request-value');
			request.send(JSON.stringify({ search: 'exact movie search', password: 'network-credential-sentinel' }));
		});
		cy.wrap(null, { timeout: 30000 }).should(() => {
			const requests: unknown[] = [];
			for (const capture of nativeCaptures) {
				if (capture.event !== '$snapshot' || !Array.isArray(capture.properties.$snapshot_data)) continue;
				for (const event of capture.properties.$snapshot_data) {
					const data = asRecord(asRecord(event)?.data);
					const payload = asRecord(data?.payload) ?? data;
					if (Array.isArray(payload?.requests)) requests.push(...payload.requests);
				}
			}
			const request = requests.map(asRecord).find((entry) => String(entry?.name).includes('/debug-network'));
			const serialized = JSON.stringify(request ?? {});
			expect(serialized).to.contain('exact movie search');
			expect(serialized).to.contain('debug-result');
			expect(serialized).to.contain('request-value');
			expect(serialized).to.contain('response-value');
			expect(serialized).not.to.contain('network-credential-sentinel');
			const pageview = nativeCaptures.find((capture) => capture.event === '$pageview');
			expect(pageview?.properties.$current_url).to.contain('/');
			expect(pageview?.properties.$geoip_disable).to.equal(false);
		});
	});

	(isolatedNativeBuild ? it : it.skip)('Should capture a dead click after the native observation window', () => {
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			body: generateResultDTO<BrowserTelemetryBootstrapDTO>({
				apiHost: 'https://posthog.isolated.test', projectToken: 'phc_cypress',
				distinctId: 'c8b4b3fa-a606-477a-9079-79833ea61558', release: 'isolated-test',
				environment: 'test', runtimeMode: 'docker', runtimeIdentifier: 'browser', platform: 'browser',
			}),
		});
		cy.visit(route('/'));
		cy.getCy('account-selector-btn').should('be.visible');
		cy.wait('@native-dead-clicks-autocapture');
		cy.document().then((document) => {
			const probe = document.createElement('button');
			probe.type = 'button';
			probe.textContent = 'Dead click probe';
			probe.style.cssText = 'position:fixed;top:80px;left:20px;z-index:10000;user-select:none';
			probe.dataset.cy = 'native-dead-click-probe';
			document.body.append(probe);
		});
		cy.getCy('account-selector-btn').click();
		cy.wrap(null, { timeout: 30000 }).should(() => {
			expect(nativeCaptures.some((event) => event.event === '$autocapture'), 'native collectors are active').to.equal(true);
		});
		cy.getCy('native-dead-click-probe').then((probe) => {
			const element = probe[0];
			if (!element) throw new Error('native dead click probe was not found');
			cy.clock(Date.now(), ['Date', 'setTimeout', 'clearTimeout']).then((clock) => {
				for (const type of ['mousedown', 'mouseup', 'click']) {
					element.dispatchEvent(new MouseEvent(type, { bubbles: true, button: 0, detail: 1 }));
				}
				// Keep the native observation window free of unrelated asynchronous page mutations.
				clock.tick(6000);
				clock.restore();
			});
		});
		cy.wrap(null, { timeout: 15000 }).should(() => {
			const event = nativeCaptures.find((capture) => capture.event === '$dead_click');
			expect(event?.properties.distinct_id, 'native dead click identity').to.equal('c8b4b3fa-a606-477a-9079-79833ea61558');
			expect(event?.properties.$session_id, 'native dead click session').to.be.a('string').and.not.equal('');
		});
	});
	(isolatedNativeBuild ? it : it.skip)('Should preserve native identity and session across an authenticated reload', () => {
		const identity = 'c8b4b3fa-a606-477a-9079-79833ea61558';
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			body: generateResultDTO<BrowserTelemetryBootstrapDTO>({ apiHost: 'https://posthog.isolated.test', projectToken: 'phc_cypress', distinctId: identity, release: 'isolated-test', environment: 'test', runtimeMode: 'docker', runtimeIdentifier: 'browser', platform: 'browser' }),
			headers: { 'cache-control': 'no-store' },
		}).as('reloadBootstrap');
		cy.visit(route('/'));
		cy.getCy('account-selector-btn').should('be.visible').click();
		let originalSession = '';
		cy.wrap(null, { timeout: 15000 }).should(() => {
			const pageviews = nativeCaptures.filter((event) => event.event === '$pageview');
			expect(pageviews).to.have.length(1);
			expect(pageviews[0]?.properties.distinct_id).to.equal(identity);
			expect(pageviews[0]?.properties.$session_id).to.be.a('string').and.not.equal('');
			originalSession = String(pageviews[0]?.properties.$session_id);
		});
		cy.wait('@reloadBootstrap');
		cy.reload();
		cy.wait('@reloadBootstrap');
		cy.getCy('account-selector-btn').should('be.visible');
		cy.wrap(null, { timeout: 15000 }).should(() => {
			const pageviews = nativeCaptures.filter((event) => event.event === '$pageview');
			expect(pageviews).to.have.length(2);
			pageviews.forEach((event) => {
				expect(event.properties.distinct_id).to.equal(identity);
				expect(event.properties.$session_id).to.equal(originalSession);
			});
			expect(nativeCaptures.some((event) => event.event === '$pageleave')).to.equal(true);
		});
	});

	(isolatedNativeBuild ? it : it.skip)('Should preserve identity and masked replay input after logout', () => {
		const identity = 'c8b4b3fa-a606-477a-9079-79833ea61558';
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			body: generateResultDTO<BrowserTelemetryBootstrapDTO>({ apiHost: 'https://posthog.isolated.test', projectToken: 'phc_cypress', distinctId: identity, release: 'isolated-test', environment: 'test', runtimeMode: 'docker', runtimeIdentifier: 'browser', platform: 'browser' }),
		});
		let authenticated = true;
		cy.intercept('GET', AuthenticationPaths.authenticationStatusEndpoint(), (request) => request.reply({
			statusCode: authenticated ? 200 : 401,
			body: authenticated ? generateResultDTO<UserClaimsDTO>({ isLoggedIn: true, userName: 'admin', claims: ['Admin'] }) : generateFailedResultDTO({ statusCode: 401 }),
		}));
		cy.intercept('POST', AuthenticationPaths.appUserLogOutEndpoint(), (request) => {
			authenticated = false;
			request.reply({ body: generateResultDTO(null) });
		}).as('logout');
		cy.visit(route('/'));
		cy.getCy('account-selector-btn').should('be.visible').click();
		let session = '';
		cy.wrap(null, { timeout: 15000 }).should(() => {
			const pageview = nativeCaptures.find((event) => event.event === '$pageview');
			expect(pageview?.properties.$session_id).to.be.a('string').and.not.equal('');
			session = String(pageview?.properties.$session_id);
		});
		cy.contains('Logout').click();
		cy.wait('@logout');
		cy.getCy('login-username-input').should('be.visible').click().type('post-logout-input-secret').blur();
		cy.wrap(null, { timeout: 30000 }).should(() => {
			const { mutations } = loginInputEvidence(nativeCaptures);
			expect(mutations.some((mutation) => mutation.text === '*'.repeat('post-logout-input-secret'.length)), 'native masked input after logout').to.equal(true);
			expect(JSON.stringify(nativeCaptures.filter((event) => event.event === '$snapshot'))).not.to.contain('post-logout-input-secret');
			const pageviews = nativeCaptures.filter((event) => event.event === '$pageview');
			expect(pageviews.at(-1)?.properties.$current_url).to.contain('/login');
			pageviews.forEach((event) => {
				expect(event.properties.distinct_id).to.equal(identity);
				expect(event.properties.$session_id).to.equal(session);
			});
		});
	});

	(isolatedNativeBuild ? it : it.skip)('Should collect masked native signals from public bootstrap before login', () => {
		let authenticated = false;
		const expectedExceptionMessage = 'isolated-native-exception-secret';
		cy.on('uncaught:exception', (error) => error.message.includes(expectedExceptionMessage) ? false : undefined);
		const bootstrap: BrowserTelemetryBootstrapDTO = {
			apiHost: 'https://posthog.isolated.test',
			projectToken: 'phc_cypress',
			distinctId: 'c8b4b3fa-a606-477a-9079-79833ea61558',
			release: 'isolated-test',
			environment: 'test',
			runtimeMode: 'docker',
			runtimeIdentifier: 'browser',
			platform: 'browser',
		};
		cy.basePageSetup({ seed: 679, isLoggedIn: false, plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', TelemetryPaths.getBrowserTelemetryBootstrapEndpoint(), {
			statusCode: 200,
			body: generateResultDTO(bootstrap),
			headers: { 'cache-control': 'no-store' },
		}).as('nativeBootstrap');
		cy.intercept('GET', AuthenticationPaths.authenticationStatusEndpoint(), (request) => request.reply({
			statusCode: authenticated ? 200 : 401,
			body: authenticated
				? generateResultDTO<UserClaimsDTO>({
						isLoggedIn: true, userName: 'admin', claims: ['Admin'],
					})
				: generateFailedResultDTO({ statusCode: 401 }),
		})).as('nativeAuthentication');
		cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), (request) => {
			authenticated = true;
			request.reply({ body: generateResultDTO(null) });
		}).as('nativeLogin');
		cy.intercept('POST', AuthenticationPaths.appUserLogOutEndpoint(), (request) => {
			authenticated = false;
			request.reply({ body: generateResultDTO(null) });
		}).as('nativeLogout');
		cy.visit(route('/login'));
		cy.wait('@nativeAuthentication');
		cy.wait('@nativeBootstrap');
		cy.url().should('eq', route('/login'));
		cy.getCy('login-submit-button').should('be.visible');
		cy.wait('@nativeConfig');
		cy.wait('@nativeExceptionAutocapture');
		cy.window().then((window) => {
			const expected = new Error(expectedExceptionMessage);
			window.setTimeout(() => {
				throw expected;
			}, 0);
		});
		cy.wrap(null, { timeout: 15000 }).should(() => {
			const pageviews = nativeCaptures.filter((event) => event.event === '$pageview' && String(event.properties.$current_url).endsWith('/login'));
			expect(pageviews, 'fresh login startup pageview').to.have.length(1);
			expect(pageviews[0]?.properties.$current_url, 'login startup pageview').to.contain('/login');
			expect(pageviews[0]?.properties.distinct_id, 'public bootstrap identity').to.equal(bootstrap.distinctId);
		});
		let snapshotsBeforeCredentials = 0;
		cy.then(() => {
			snapshotsBeforeCredentials = nativeCaptures.filter((event) => event.event === '$snapshot').length;
		});
		cy.getCy('login-username-input').type('login-username-secret');
		cy.getCy('login-password-input').type('login-password-secret');
		cy.getCy('login-submit-button').click();
		cy.wait('@nativeLogin');
		cy.url().should('eq', route('/'));
		cy.wait('@native-dead-clicks-autocapture');
		cy.document().then((document) => {
			const probe = document.createElement('div');
			probe.dataset.cy = 'native-interaction-probe';
			probe.dataset.probeSecret = 'native-attribute-secret';
			probe.setAttribute('role', 'button');
			probe.textContent = 'native-rendered-text-secret';
			probe.style.cssText = 'position:fixed;top:80px;left:20px;z-index:10000;cursor:pointer;user-select:none';
			const rageProbe = document.createElement('button');
			rageProbe.type = 'button';
			rageProbe.dataset.cy = 'native-rage-probe';
			rageProbe.textContent = 'rage probe';
			rageProbe.style.cssText = 'position:fixed;top:120px;left:20px;z-index:10000';
			document.body.append(probe, rageProbe);
		});
		cy.getCy('native-rage-probe').should('be.visible').click().click().click();
		cy.wrap(null, { timeout: 15000 }).should(() => {
			expect(nativeCaptures.some((event) => event.event === '$rageclick'), 'rage interaction completed before dead-click probe').to.equal(true);
		});
		cy.wrap(null, { timeout: 30000 }).should(() => {
			const interactions = nativeCaptures.filter((event) => event.event === '$autocapture');
			expect(interactions.some((event) => event.properties.$event_type === 'click'), 'native autocapture click').to.equal(true);
			interactions.forEach((event) => {
				expect(event.properties.distinct_id).to.equal(bootstrap.distinctId);
				expect(event.properties.$session_id).to.be.a('string').and.not.equal('');
			});
			expect(JSON.stringify(interactions), 'native autocapture masks text').not.to.contain('native-rendered-text-secret');
			expect(JSON.stringify(interactions), 'native autocapture masks attributes').not.to.contain('native-attribute-secret');
			const snapshots = nativeCaptures.filter((event) => event.event === '$snapshot');
			expect(snapshots.length, 'login replay snapshot').to.be.greaterThan(0);
			expect(snapshots.length, 'login credential replay snapshot').to.be.greaterThan(snapshotsBeforeCredentials);
			expect(JSON.stringify(snapshots), 'login username is masked').not.to.contain('login-username-secret');
			expect(JSON.stringify(snapshots), 'login password is masked').not.to.contain('login-password-secret');
			const { fixtureNodes, mutations } = loginInputEvidence(snapshots);
			expect(fixtureNodes.length, 'decoded login input fixture').to.be.greaterThan(0);
			const fixtureIdentifiers = fixtureNodes.map((node) => {
				const attributes = asRecord(node.attributes);
				return attributes?.['data-cy'];
			});
			expect(fixtureIdentifiers.some((value) => typeof value === 'string' && value.length > 0), 'login input fixture identifier').to.equal(true);
			const fixtureIds = new Set(fixtureNodes.flatMap((node) => typeof node.id === 'number' ? [node.id] : []));
			const inputValues = mutations
				.filter((mutation) => typeof mutation.id === 'number' && fixtureIds.has(mutation.id))
				.map((mutation) => mutation.text)
				.filter((value): value is string => typeof value === 'string');
			expect(inputValues.some((value) => /^\*+$/.test(value) && value.length === 'login-username-secret'.length), 'decoded username input is masked').to.equal(true);
			expect(inputValues.some((value) => /^\*+$/.test(value) && value.length === 'login-password-secret'.length), 'decoded password input is masked').to.equal(true);
			expect(inputValues.every((value) => !value.includes('login-username-secret') && !value.includes('login-password-secret')), 'decoded input omits credentials').to.equal(true);
			const exceptions = nativeCaptures.filter((event) => event.event === '$exception');
			expect(exceptions.some((event) => {
				const list = event.properties.$exception_list;
				return Array.isArray(list)
					&& list.some((exception) => asRecord(exception)?.value === expectedExceptionMessage);
			}), 'native original exception').to.equal(true);
			expect(nativeCaptures.some((event) => event.event === '$rageclick'), 'native rageclick').to.equal(true);
			expect(nativeCaptures.some((event) => event.event === '$$heatmap'), 'native heatmap').to.equal(true);
			const vitals = nativeCaptures.find((event) => event.event === '$web_vitals' && typeof event.properties.$web_vitals_FCP_value === 'number');
			expect(vitals?.properties.$web_vitals_FCP_value, 'native web vitals FCP').to.be.a('number').and.greaterThan(0);
		});
	});
});
