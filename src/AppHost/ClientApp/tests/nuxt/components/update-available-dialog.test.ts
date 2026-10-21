import { afterEach, describe, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App } from 'vue';
import { createI18n } from 'vue-i18n';
import { Quasar } from 'quasar';
import UpdateAvailableDialog from '@components/Dialogs/UpdateAvailableDialog.vue';
import messages from '@/lang/en-US.json';

const { updateStore } = vi.hoisted(() => ({
	updateStore: {
		newestVersion: '1.2.3',
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
	const releaseNotes = updateStore.releaseNotes;

	afterEach(() => {
		app?.unmount();
		updateStore.releaseNotes = releaseNotes;
		updateStore.newestVersion = '1.2.3';
	});

	async function renderDialog() {
		const container = document.createElement('div');
		app = createApp(UpdateAvailableDialog)
			.use(Quasar)
			.use(createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages }, missingWarn: false, fallbackWarn: false }));
		app.mount(container);
		await nextTick();
		return container;
	}

	test('Should render release notes as markdown', async () => {
		// Arrange / Act
		const container = await renderDialog();

		// Assert
		expect(container.querySelector('.update-dialog__notes h1')?.textContent).toBe('Fixed');
		expect(container.querySelector('.update-dialog__notes p')?.textContent).toBe('Release details.');
	});

	test('Should show the supplied target version when release notes are unavailable', async () => {
		// Arrange
		updateStore.newestVersion = '1.2.4';
		updateStore.releaseNotes = [];

		// Act
		const container = await renderDialog();

		// Assert
		expect(container.querySelector('.update-dialog__title-versions')?.textContent).toContain('1.2.4');
		expect(container.querySelector('.update-dialog__title-versions')?.textContent).not.toContain('?');
		expect(container.querySelector('.update-dialog__notes')).toBeNull();
	});
});
