import { describe, beforeAll, beforeEach, afterEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO, generateSettingsModel } from '@mock';
import { SettingsPaths } from '@api-urls';
import { useSettingsStore } from '@store';

describe('SettingsStore autosave regressions', () => {
	let { mock } = baseVars();
	const { config: initialConfig } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		vi.useFakeTimers();
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	test('Should only send one autosave request after setup is called twice', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const settings = generateSettingsModel({ config: initialConfig });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		await subscribeSpyTo(settingsStore.setup()).onComplete();
		await subscribeSpyTo(settingsStore.setup()).onComplete();

		// Act
		settingsStore.generalSettings.firstTimeSetup = !settingsStore.generalSettings.firstTimeSetup;
		await vi.advanceTimersByTimeAsync(600);

		// Assert
		expect(mock.history.put.filter((request) => request.url === SettingsPaths.updateUserSettingsEndpoint())).toHaveLength(1);
		expect(JSON.parse(mock.history.put[0]!.data).generalSettings.firstTimeSetup).toBe(!settings.generalSettings.firstTimeSetup);
		expect(settingsStore.generalSettings.firstTimeSetup).toBe(!settings.generalSettings.firstTimeSetup);
	});

	test('Should keep autosave working after reset', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const settings = generateSettingsModel({ config: initialConfig });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		await subscribeSpyTo(settingsStore.setup()).onComplete();
		settingsStore.$reset();

		// Act
		settingsStore.generalSettings.firstTimeSetup = false;
		await vi.advanceTimersByTimeAsync(600);

		// Assert
		expect(mock.history.put.filter((request) => request.url === SettingsPaths.updateUserSettingsEndpoint())).toHaveLength(1);
		expect(JSON.parse(mock.history.put[0]!.data).generalSettings.firstTimeSetup).toBe(false);
		expect(settingsStore.generalSettings.firstTimeSetup).toBe(false);
	});

	test('Should restore autosave after reset and setup are run again', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const settings = generateSettingsModel({ config: initialConfig });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		await subscribeSpyTo(settingsStore.setup()).onComplete();
		settingsStore.$reset();
		await subscribeSpyTo(settingsStore.setup()).onComplete();

		// Act
		settingsStore.generalSettings.firstTimeSetup = !settingsStore.generalSettings.firstTimeSetup;
		await vi.advanceTimersByTimeAsync(600);

		// Assert
		expect(mock.history.put.filter((request) => request.url === SettingsPaths.updateUserSettingsEndpoint())).toHaveLength(1);
		expect(JSON.parse(mock.history.put[0]!.data).generalSettings.firstTimeSetup).toBe(!settings.generalSettings.firstTimeSetup);
		expect(settingsStore.generalSettings.firstTimeSetup).toBe(!settings.generalSettings.firstTimeSetup);
	});

	test('Should rebase a queued autosave onto the preceding schedule response', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const settings = generateSettingsModel({ config: initialConfig });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(settingsStore.setup()).onComplete();
		const pending: { body: typeof settings; resolve: (reply: [number, unknown]) => void }[] = [];
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => {
			const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
			pending.push({ body: JSON.parse(request.data), resolve });
			return promise;
		});
		const schedule = { enabled: true, days: { Monday: { '09:00': 100 } } };
		const scheduleSave = subscribeSpyTo(settingsStore.saveDownloadSchedule(schedule));
		await vi.waitFor(() => expect(pending).toHaveLength(1));
		settingsStore.dateTimeSettings.timeZone = 'Europe/Amsterdam';
		await vi.advanceTimersByTimeAsync(600);

		// Act
		pending[0]!.resolve([200, generateResultDTO(pending[0]!.body)]);
		await scheduleSave.onComplete();
		await vi.waitFor(() => expect(pending).toHaveLength(2));

		// Assert
		expect(pending[1]!.body.downloadManagerSettings.downloadSchedule).toEqual(schedule);
		expect(pending[1]!.body.dateTimeSettings.timeZone).toBe('Europe/Amsterdam');
		pending[1]!.resolve([200, generateResultDTO(pending[1]!.body)]);
		await vi.waitFor(() => expect(settingsStore.dateTimeSettings.timeZone).toBe('Europe/Amsterdam'));
	});

	test('Should keep the edit-time baseline when a save completes before debounce expires', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const settings = generateSettingsModel({ config: initialConfig });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(settingsStore.setup()).onComplete();
		const pending: { body: typeof settings; resolve: (reply: [number, unknown]) => void }[] = [];
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => {
			const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
			pending.push({ body: JSON.parse(request.data), resolve });
			return promise;
		});
		const schedule = { enabled: true, days: { Tuesday: { '10:00': 200 } } };
		const scheduleSave = subscribeSpyTo(settingsStore.saveDownloadSchedule(schedule));
		await vi.waitFor(() => expect(pending).toHaveLength(1));
		settingsStore.dateTimeSettings.timeZone = 'Europe/Amsterdam';

		// Act
		pending[0]!.resolve([200, generateResultDTO(pending[0]!.body)]);
		await scheduleSave.onComplete();
		await vi.advanceTimersByTimeAsync(600);
		await vi.waitFor(() => expect(pending).toHaveLength(2));

		// Assert
		expect(pending[1]!.body.downloadManagerSettings.downloadSchedule).toEqual(schedule);
		expect(pending[1]!.body.dateTimeSettings.timeZone).toBe('Europe/Amsterdam');
		pending[1]!.resolve([200, generateResultDTO(pending[1]!.body)]);
	});
});
