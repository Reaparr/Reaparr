import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { subscribeSpyTo, baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generateResultDTO, generateSettingsModel } from '@mock';
import { SettingsPaths } from '@api-urls';
import { useSettingsStore } from '@store';

describe('SettingsStore.refreshSettings() network settings', () => {
	// eslint-disable-next-line prefer-const
	let { mock, config } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should preserve network settings reactivity and apply the backend URL after refresh', async () => {
		// Arrange
		const settingsStore = useSettingsStore();
		const initialSettings = generateSettingsModel({ config });
		initialSettings.networkSettings.reverseProxyUrl = 'https://old.example.com';
		settingsStore.setSettingsState(initialSettings);
		const networkSettingsRef = settingsStore.networkSettings;

		const updatedSettings = generateSettingsModel({ config });
		updatedSettings.networkSettings.reverseProxyUrl = 'https://reaparr.example.nl';
		updatedSettings.networkSettings.basePath = '/reaparr';
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(updatedSettings));

		// Act
		const result = subscribeSpyTo(settingsStore.refreshSettings());
		await result.onComplete();

		// Assert
		expect(networkSettingsRef).toBe(settingsStore.networkSettings);
		expect(networkSettingsRef.reverseProxyUrl).toBe('https://reaparr.example.nl');
		expect(networkSettingsRef.basePath).toBe('/reaparr');
	});
});
