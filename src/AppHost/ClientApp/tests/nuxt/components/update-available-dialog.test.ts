import { afterEach, describe, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App } from 'vue';
import { createI18n } from 'vue-i18n';
import { Quasar } from 'quasar';
import UpdateAvailableDialog from '@components/Dialogs/UpdateAvailableDialog.vue';

const { updateStore } = vi.hoisted(() => ({
	updateStore: {
		releaseNotes: [{ version: '1.2.3', releaseDate: '2026-10-04T00:00:00Z', notes: '# Fixed\n\nRelease details.', isDevRelease: false }],
		hasUpdateAvailable: true,
		isApplyingUpdate: false,
		isDownloading: false,
		downloadProgress: 0,
		downloadAndApplyUpdate: vi.fn(),
	},
}));

vi.mock('@store', async (importOriginal) => ({
	...await importOriginal<object>(),
	useUpdateStore: () => updateStore,
}));

vi.mock('@store/globalStore', () => ({
	useGlobalStore: () => ({ version: '1.0.0', isDockerMode: true }),
}));

vi.mock('@components/Common/QCardDialog.vue', () => ({
	default: { template: '<section><slot name="title"/><slot/><slot name="actions"/></section>' },
}));

describe('UpdateAvailableDialog', () => {
	let app: App;

	afterEach(() => {
		app?.unmount();
	});

	test('Should render release notes as markdown', async () => {
		// Arrange / Act
		const container = document.createElement('div');
		app = createApp(UpdateAvailableDialog)
			.use(Quasar)
			.use(createI18n({ legacy: false, missingWarn: false, fallbackWarn: false }));
		app.mount(container);
		await nextTick();

		// Assert
		expect(container.querySelector('.update-dialog__notes h1')?.textContent).toBe('Fixed');
		expect(container.querySelector('.update-dialog__notes p')?.textContent).toBe('Release details.');
	});
});
