import { describe, beforeAll, beforeEach, afterEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia, disposePinia, type Pinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO, generateSettingsModel, generateFailedResultDTO } from '@mock';
import { SettingsPaths } from '@api-urls';
import { useSettingsStore } from '@store';
import type { DownloadScheduleDTO, SettingsModelDTO } from '@dto';
import { cloneDeep } from 'lodash-es';

describe('SettingsStore - Download schedule saving', () => {
	let { mock, config } = baseVars();
	let pinia: Pinia;
	beforeAll(() => baseSetup());
	beforeEach(() => {
		vi.useFakeTimers();
		config = { seed: 20261005 };
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		disposePinia(pinia);
		vi.useRealTimers();
	});

	test('Should serialize immutable policy intents and preserve newer local edits when an older save completes', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const pending: { body: SettingsModelDTO; resolve: (reply: [number, unknown]) => void }[] = [];
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => {
			const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
			pending.push({ body: JSON.parse(request.data), resolve });
			return promise;
		});
		const first: DownloadScheduleDTO = { enabled: true, days: { Monday: { '09:30': 100 } } };
		const second: DownloadScheduleDTO = { enabled: true, days: { Monday: { '09:30': 200 } } };

		// Act
		const firstSave = subscribeSpyTo(store.saveDownloadSchedule(first));
		first.days.Monday!['09:30'] = 999;
		const secondSave = subscribeSpyTo(store.saveDownloadSchedule(second));
		await vi.waitFor(() => expect(pending).toHaveLength(1));
		store.generalSettings.hideMediaFromOfflineServers = !settings.generalSettings.hideMediaFromOfflineServers;
		store.dateTimeSettings.timeZone = 'Europe/Amsterdam';
		pending[0]!.resolve([200, generateResultDTO(pending[0]!.body)]);
		await firstSave.onComplete();
		await vi.waitFor(() => expect(pending).toHaveLength(2));

		// Assert
		expect(pending[0]!.body.downloadManagerSettings.downloadSchedule.days).toEqual({ Monday: { '09:30': 100 } });
		expect(store.generalSettings.hideMediaFromOfflineServers).toBe(!settings.generalSettings.hideMediaFromOfflineServers);
		expect(pending[1]!.body.downloadManagerSettings.downloadSchedule.days).toEqual({ Monday: { '09:30': 200 } });
		expect(pending[1]!.body.generalSettings.hideMediaFromOfflineServers).toBe(!settings.generalSettings.hideMediaFromOfflineServers);
		expect(pending[1]!.body.dateTimeSettings.timeZone).toBe('Europe/Amsterdam');
		pending[1]!.resolve([200, generateResultDTO(pending[1]!.body)]);
		await secondSave.onComplete();
		expect(store.confirmedDownloadSchedule.days).toEqual({ Monday: { '09:30': 200 } });
		expect(store.generalSettings.hideMediaFromOfflineServers).toBe(!settings.generalSettings.hideMediaFromOfflineServers);
	});

	test('Should retain the save error without incidental retry when unrelated autosave succeeds', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const policy: DownloadScheduleDTO = { enabled: true, days: { Monday: { '09:30': 123 } } };
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).replyOnce(500, generateFailedResultDTO());
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);

		// Act
		await subscribeSpyTo(store.saveDownloadSchedule(policy)).onComplete();
		const failure = store.settingsSaveError;
		store.generalSettings.hasAgreedToDisclaimer = !settings.generalSettings.hasAgreedToDisclaimer;
		await vi.advanceTimersByTimeAsync(600);

		// Assert
		expect(mock.history.put).toHaveLength(2);
		expect(JSON.parse(mock.history.put[1]!.data).downloadManagerSettings.downloadSchedule).toEqual(settings.downloadManagerSettings.downloadSchedule);
		expect(store.confirmedDownloadSchedule).toEqual(settings.downloadManagerSettings.downloadSchedule);
		expect(store.settingsSaveError).toBe(failure);
		expect(store.settingsSaveState).toBe('error');
		await subscribeSpyTo(store.saveDownloadSchedule(policy)).onComplete();
		expect(store.confirmedDownloadSchedule).toEqual(policy);
		expect(store.settingsSaveError).toBeNull();
		expect(store.settingsSaveState).toBe('saved');
	});

	test('Should finish the owned PUT when the caller unsubscribes', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);
		const policy: DownloadScheduleDTO = { enabled: true, days: { Tuesday: { '17:30': 456 } } };
		const saved = cloneDeep(settings);
		saved.downloadManagerSettings.downloadSchedule = policy;

		// Act
		const caller = subscribeSpyTo(store.saveDownloadSchedule(policy));
		await vi.waitFor(() => expect(mock.history.put).toHaveLength(1));
		caller.unsubscribe();
		resolve([200, generateResultDTO(saved)]);

		// Assert
		await vi.waitFor(() => expect(store.confirmedDownloadSchedule).toEqual(policy));
		expect(store.settingsSaveState).toBe('saved');
	});

	test('Should ignore a late acknowledgement when the store has reset', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);
		const policy: DownloadScheduleDTO = { enabled: true, days: { Sunday: { '23:30': 789 } } };
		const saved = cloneDeep(settings);
		saved.downloadManagerSettings.downloadSchedule = policy;

		// Act
		const caller = subscribeSpyTo(store.saveDownloadSchedule(policy));
		await vi.waitFor(() => expect(mock.history.put).toHaveLength(1));
		store.$reset();
		resolve([200, generateResultDTO(saved)]);
		await caller.onComplete();

		// Assert
		expect(caller.getLastValue()).toBeNull();
		expect(store.confirmedDownloadSchedule.enabled).toBe(false);
		expect(store.confirmedDownloadSchedule.days).toEqual({});
		expect(store.settingsSaveState).toBe('idle');
	});

	test('Should complete both active and queued callers with null when the store resets', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const { promise } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);
		const first = subscribeSpyTo(store.saveDownloadSchedule({ enabled: true, days: { Monday: { '09:00': 100 } } }));
		const second = subscribeSpyTo(store.saveDownloadSchedule({ enabled: true, days: { Tuesday: { '10:00': 200 } } }));
		await vi.waitFor(() => expect(mock.history.put).toHaveLength(1));

		// Act
		store.$reset();
		await Promise.all([first.onComplete(), second.onComplete()]);

		// Assert
		expect(first.getLastValue()).toBeNull();
		expect(second.getLastValue()).toBeNull();
		expect(mock.history.put).toHaveLength(1);
		expect(store.settingsSaveState).toBe('idle');
	});

	test('Should complete a pending caller with null when its Pinia scope is disposed', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const { promise } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);
		const caller = subscribeSpyTo(store.saveDownloadSchedule({ enabled: true, days: { Sunday: { '23:30': 300 } } }));
		await vi.waitFor(() => expect(mock.history.put).toHaveLength(1));

		// Act
		disposePinia(pinia);
		await caller.onComplete();

		// Assert
		expect(caller.getLastValue()).toBeNull();
	});

	test('Should preflight before setup and preserve simultaneous timezone and general edits', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		store.dateTimeSettings.timeZone = 'Europe/Amsterdam';
		store.generalSettings.hideMediaFromOwnedServers = !settings.generalSettings.hideMediaFromOwnedServers;
		const policy: DownloadScheduleDTO = { enabled: true, days: { Wednesday: { '12:00': 400 } } };

		// Act
		const result = subscribeSpyTo(store.saveDownloadSchedule(policy));
		await result.onComplete();

		// Assert
		expect(mock.history.get).toHaveLength(1);
		const submitted = JSON.parse(mock.history.put[0]!.data) as SettingsModelDTO;
		expect(submitted.dateTimeSettings.timeZone).toBe('Europe/Amsterdam');
		expect(submitted.generalSettings.hideMediaFromOwnedServers).toBe(!settings.generalSettings.hideMediaFromOwnedServers);
		expect(submitted.downloadManagerSettings.downloadSchedule).toEqual(policy);
	});

	test('Should re-read authority after a lost response without retrying the failed schedule', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).networkErrorOnce();
		const failedPolicy: DownloadScheduleDTO = { enabled: true, days: { Thursday: { '13:00': 500 } } };
		await subscribeSpyTo(store.saveDownloadSchedule(failedPolicy)).onComplete();
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		const nextPolicy: DownloadScheduleDTO = { enabled: true, days: { Friday: { '14:00': 600 } } };

		// Act
		await subscribeSpyTo(store.saveDownloadSchedule(nextPolicy)).onComplete();

		// Assert
		expect(mock.history.put).toHaveLength(2);
		expect(mock.history.get).toHaveLength(2);
		expect(JSON.parse(mock.history.put[1]!.data).downloadManagerSettings.downloadSchedule).toEqual(nextPolicy);
		expect(store.confirmedDownloadSchedule).toEqual(nextPolicy);
	});

	test('Should complete an unobserved save with null when subscribed after reset', async () => {
		// Arrange
		const store = useSettingsStore();
		const settings = generateSettingsModel({ config });
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		await subscribeSpyTo(store.setup()).onComplete();
		const coldSave = store.saveDownloadSchedule({ enabled: true, days: { Saturday: { '15:00': 700 } } });

		// Act
		store.$reset();
		const caller = subscribeSpyTo(coldSave);
		await caller.onComplete();

		// Assert
		expect(caller.getLastValue()).toBeNull();
		expect(mock.history.put).toHaveLength(0);
	});
});
