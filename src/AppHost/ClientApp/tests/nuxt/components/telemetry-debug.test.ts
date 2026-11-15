import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { flushPromises } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { DebugPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { TelemetryDiagnosticOrigin } from '@dto';
import { useLogsStore } from '@store';
import TelemetryDebug from '@/pages/debug-pages/telemetry.vue';
import messages from '@/lang/en-US.json';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

const diagnosticEndpoint = DebugPaths.sendTelemetryDiagnosticEndpoint();

describe('Telemetry diagnostics page', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let wrapper: { unmount(): void } | undefined;
	function diagnosticRequests() {
		return mock.history.post.filter((request) => request.url === diagnosticEndpoint);
	}

	function requestOrigins(): string[] {
		const requests = diagnosticRequests();
		return requests.map((request) => JSON.parse(request.data as string).origin);
	}

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
		capture.mockClear();
	});
	afterEach(() => wrapper?.unmount());

	async function renderPage() {
		const page = await mountSuspended(TelemetryDebug, {
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { QPage: { template: '<main><slot /></main>' } },
			},
		});
		wrapper = page;
		return page;
	}

	test('Should not send a diagnostic request when the page mounts', async () => {
		// Arrange
		const page = await renderPage();

		// Act
		await flushPromises();

		// Assert
		expect(diagnosticRequests()).toHaveLength(0);
		expect(page.get('[data-cy="telemetry-send-test-event"]').text()).toContain('Send test event');
	});

	test('Should attempt both origins and retain independent success and failure statuses', async () => {
		// Arrange
		const failed = generateFailedResultDTO({ statusCode: 503 });
		const accepted = generateResultDTO(null);
		mock.onPost(diagnosticEndpoint)
			.replyOnce(503, failed)
			.onPost(diagnosticEndpoint)
			.replyOnce(200, accepted);
		const page = await renderPage();

		// Act
		await page.get('[data-cy="telemetry-send-test-event"]').trigger('click');
		await vi.waitFor(() => expect(diagnosticRequests()).toHaveLength(2));
		await flushPromises();

		// Assert
		expect(requestOrigins()).toEqual([
			TelemetryDiagnosticOrigin.Backend,
			TelemetryDiagnosticOrigin.Frontend,
		]);
		expect(page.get('[data-cy="telemetry-backend-status"]').text()).toContain('Diagnostic event was not accepted for sending');
		expect(page.get('[data-cy="telemetry-frontend-status"]').text()).toContain('Diagnostic event accepted for sending');
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendDiagnosticRequested, {
			action: FrontendTelemetryAction.Inspect,
		});
	});

	test('Should disable submission while a diagnostic run is pending', async () => {
		// Arrange
		const accepted = generateResultDTO(null);
		const firstResponse = Promise.withResolvers<[number, typeof accepted]>();
		mock.onPost(diagnosticEndpoint)
			.replyOnce(() => firstResponse.promise)
			.onPost(diagnosticEndpoint)
			.reply(200, accepted);
		const page = await renderPage();
		const button = page.get('[data-cy="telemetry-send-test-event"]');

		// Act
		await button.trigger('click');
		await vi.waitFor(() => expect(button.attributes('disabled')).toBeDefined());
		await button.trigger('click');

		// Assert
		expect(diagnosticRequests()).toHaveLength(1);
		firstResponse.resolve([200, accepted]);
		await vi.waitFor(() => expect(diagnosticRequests()).toHaveLength(2));
		await vi.waitFor(() => expect(button.attributes('disabled')).toBeUndefined());
	});

	test('Should retry both origins on a later deliberate click', async () => {
		// Arrange
		const accepted = generateResultDTO(null);
		mock.onPost(diagnosticEndpoint).reply(200, accepted);
		const page = await renderPage();
		const button = page.get('[data-cy="telemetry-send-test-event"]');

		// Act
		await button.trigger('click');
		await vi.waitFor(() => expect(diagnosticRequests()).toHaveLength(2));
		await vi.waitFor(() => expect(button.attributes('disabled')).toBeUndefined());
		await button.trigger('click');
		await vi.waitFor(() => expect(diagnosticRequests()).toHaveLength(4));
		await flushPromises();

		// Assert
		expect(requestOrigins()).toEqual([
			TelemetryDiagnosticOrigin.Backend,
			TelemetryDiagnosticOrigin.Frontend,
			TelemetryDiagnosticOrigin.Backend,
			TelemetryDiagnosticOrigin.Frontend,
		]);
		expect(page.get('[data-cy="telemetry-backend-status"]').text()).toContain('Diagnostic event accepted for sending');
		expect(page.get('[data-cy="telemetry-frontend-status"]').text()).toContain('Diagnostic event accepted for sending');
	});

	test('Should cancel the pending diagnostic sequence when leaving the page', async () => {
		// Arrange
		const accepted = generateResultDTO(null);
		const firstResponse = Promise.withResolvers<[number, typeof accepted]>();
		mock.onPost(diagnosticEndpoint).replyOnce(() => firstResponse.promise);
		const page = await renderPage();

		// Act
		await page.get('[data-cy="telemetry-send-test-event"]').trigger('click');
		await vi.waitFor(() => expect(diagnosticRequests()).toHaveLength(1));
		page.unmount();
		wrapper = undefined;
		firstResponse.resolve([200, accepted]);
		await flushPromises();

		// Assert
		expect(diagnosticRequests()).toHaveLength(1);
		expect(useLogsStore().telemetryDiagnostic.pending).toBe(false);
	});
});
