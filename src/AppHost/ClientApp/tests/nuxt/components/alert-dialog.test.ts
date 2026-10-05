import { afterEach, expect, test } from 'vitest';
import { createApp, defineComponent, h, nextTick, Suspense, type App } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { createI18n } from 'vue-i18n';
import { Quasar } from 'quasar';
import AlertDialog from '@components/Dialogs/AlertDialog.vue';
import { useAlertStore, useGlobalStore } from '@store';
import messages from '@/lang/en-US.json';

let app: App | undefined;
const container = document.createElement('div');

afterEach(() => {
	app?.unmount();
});

test('Should report retained startup failures and later failures together without selection', async () => {
	// Arrange
	const pinia = createPinia();
	setActivePinia(pinia);
	const alertStore = useAlertStore();
	alertStore.showApiError({
		method: 'GET',
		url: 'http://localhost:3030/api/Authentication/status',
		statusCode: 500,
		message: 'Startup failure <script>not executable</script>',
	});
	const startup = defineComponent({
		async setup() {
			return () => h(AlertDialog, { alert: alertStore.alerts[0]! });
		},
	});
	app = createApp({ render: () => h(Suspense, null, { default: () => h(startup) }) });
	app.use(pinia);
	app.use(Quasar);
	app.use(createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } }));

	// Act
	app.mount(container);
	await nextTick();
	await nextTick();

	// Assert
	await expect.poll(() => document.querySelector('[data-cy="alert-dialog"]')?.textContent)
		.toContain('Startup failure <script>not executable</script>');
	const dialog = document.querySelector('[data-cy="alert-dialog"]');
	expect(dialog?.textContent).toContain('The affected action may not have completed');
	expect(dialog?.textContent).toContain('HTTP 4xx means the request was rejected; HTTP 5xx means the server could not complete it');
	expect(dialog?.textContent).toContain('it does not retry failed requests');
	expect(dialog?.textContent).toContain('Nothing is submitted automatically');
	expect(dialog?.querySelector('script')).toBeNull();
	expect(dialog?.querySelector('[role="radio"]')).toBeNull();
	const initialUrl = new URL(dialog!.querySelector<HTMLAnchorElement>('[data-cy="report-api-error"]')!.href);
	expect(initialUrl.searchParams.get('template')).toBe('BUG-REPORT.yml');
	expect(initialUrl.searchParams.has('version')).toBe(false);
	expect(initialUrl.searchParams.has('os')).toBe(false);
	expect(initialUrl.searchParams.has('body')).toBe(false);
	expect(initialUrl.searchParams.has('checks')).toBe(false);
	expect(initialUrl.searchParams.has('screenshot')).toBe(false);
	const globalStore = useGlobalStore();
	globalStore.setAppVersion('v0.31.0');
	globalStore.setAppPlatform('Windows');

	// New failures must join the report without selecting them or reopening the dialog.
	alertStore.showApiError({
		method: 'POST',
		url: 'http://localhost:3030/api/PlexLibrary/sync',
		statusCode: 409,
		message: 'Library sync failed',
	});
	for (let index = 2; index < 10; index++) {
		alertStore.showApiError({
			method: 'GET',
			url: `http://localhost:3030/api/failure-${index}?detail=${'%'.repeat(200)}`,
			statusCode: 503,
			message: '診'.repeat(400),
		});
	}
	alertStore.showApiError({
		method: 'GET',
		url: 'http://localhost:3030/api/overflow',
		statusCode: 500,
		message: 'Omitted failure',
	});
	await expect.poll(() => new URL(dialog!.querySelector<HTMLAnchorElement>('[data-cy="report-api-error"]')!.href)
		.searchParams.get('logs')).toContain('Only the first 10 distinct failures are shown.');
	const reportLink = dialog?.querySelector<HTMLAnchorElement>('[data-cy="report-api-error"]');
	const reportUrl = new URL(reportLink!.href);
	expect(reportUrl.searchParams.get('title')).toBe('[BUG] - Reaparr: Something went wrong');
	expect(reportUrl.searchParams.get('version')).toBe('v0.31.0');
	expect(reportUrl.searchParams.get('os')).toBe('Windows');
	expect(reportUrl.searchParams.get('description')).toContain('Backend requests failed');
	expect(reportUrl.searchParams.get('reproduce-steps')).toContain('Page at report time:');
	const logs = reportUrl.searchParams.get('logs');
	expect(logs).toContain('Captured frontend request failures (not backend logs):');
	expect(logs).toContain('GET http://localhost:3030/api/Authentication/status\nHTTP 500\nStartup failure <script>not executable');
	expect(logs).toContain('POST http://localhost:3030/api/PlexLibrary/sync\nHTTP 409\nLibrary sync failed');
	for (let index = 2; index < 10; index++) {
		expect(logs).toContain(`GET http://localhost:3030/api/failure-${index}?detail=`);
	}
	expect(logs).not.toContain('Omitted failure');
	expect(reportLink!.href.length).toBeLessThan(8000);

	// Deployment mode is not the server OS, and "latest" is not an exact version.
	globalStore.setAppPlatform('docker');
	globalStore.setAppVersion('latest');
	await nextTick();
	const dockerUrl = new URL(reportLink!.href);
	expect(dockerUrl.searchParams.has('version')).toBe(false);
	expect(dockerUrl.searchParams.has('os')).toBe(false);
	expect(dockerUrl.searchParams.get('description')).toContain('Deployment: docker');
});
