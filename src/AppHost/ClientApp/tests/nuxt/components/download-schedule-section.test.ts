import { useNuxtApp } from '#app';
import { fireEvent, getByRole, waitFor } from '@testing-library/dom';
import { h, nextTick, render } from 'vue';
import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO, generateSettingsModel, generateFailedResultDTO } from '@mock';
import { SettingsPaths } from '@api-urls';
import { useSettingsStore } from '@store';
import type { SettingsModelDTO } from '@dto';
import DownloadScheduleSection from '@components/Views/Settings/DownloadScheduleSection.vue';
import { decodeDownloadScheduleDays } from '@composables/download-schedule';

describe('DownloadScheduleSection reset confirmation', () => {
	let container: HTMLElement;
	let store: ReturnType<typeof useSettingsStore>;
	let mock: ReturnType<typeof getAxiosMock>;
	let settings: SettingsModelDTO;

	beforeAll(() => baseSetup());
	beforeEach(async () => {
		vi.useFakeTimers();
		mock = getAxiosMock();
		store = useNuxtApp().vueApp.runWithContext(() => useSettingsStore());
		store.$reset();
		settings = generateSettingsModel({ config: { seed: 20261005 } });
		settings.downloadManagerSettings.downloadSchedule = {
			enabled: true,
			days: { Monday: { '09:00': 5000, '10:00': null }, Sunday: { '23:30': 1000 } },
		};
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).replyOnce((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		await subscribeSpyTo(store.setup()).onComplete();
		// Drain initialization writes from the native store before exercising the component.
		await vi.advanceTimersByTimeAsync(600);
		mock.resetHistory();
		vi.useRealTimers();
		container = document.createElement('div');
		document.body.append(container);
		const vnode = h(DownloadScheduleSection);
		vnode.appContext = useNuxtApp().vueApp._context;
		render(vnode, container);
		await nextTick();
	});
	afterEach(() => {
		render(null, container);
		container.remove();
		store.$reset();
		vi.useRealTimers();
	});

	async function openReset() {
		fireEvent.click(getByRole(container, 'button', { name: 'Reset schedule' }));
		return waitFor(() => getByRole(document.body, 'dialog'));
	}

	test('Should keep all saved limits when the reset confirmation is cancelled', async () => {
		// Arrange
		const dialog = await openReset();

		// Act
		fireEvent.click(getByRole(dialog, 'button', { name: 'Cancel' }));
		await nextTick();

		// Assert
		expect(mock.history.put).toEqual([]);
		expect(store.confirmedDownloadSchedule).toEqual(settings.downloadManagerSettings.downloadSchedule);
	});

	test('Should retain the selected block and reuse its chosen rate when selecting new hours after saving', async () => {
		// Arrange
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		const firstCell = container.querySelector('[data-cy=schedule-cell-0-18]')!;
		fireEvent.click(firstCell);
		fireEvent.keyDown(firstCell, { key: 'ArrowRight', shiftKey: true });
		await nextTick();
		fireEvent.keyDown(container.querySelector('[data-cy=schedule-cell-0-19]')!, { key: 'ArrowDown', shiftKey: true });
		await nextTick();
		fireEvent.click(getByRole(container, 'radio', { name: 'Limit' }));
		await nextTick();
		const input = getByRole(container, 'spinbutton') as HTMLInputElement;
		input.focus();
		input.select();
		for (const key of '3000') fireEvent.keyPress(input, { key, code: `Digit${key}` });
		await nextTick();

		vi.useFakeTimers();
		await vi.advanceTimersByTimeAsync(600);
		expect(mock.history.put).toEqual([]);
		vi.useRealTimers();
		// Act
		fireEvent.click(getByRole(container, 'button', { name: 'Apply to selection & save' }));
		await waitFor(() => expect(store.confirmedDownloadSchedule.days.Monday?.['09:00']).toBe(3000));
		await waitFor(() => expect(container.querySelector<HTMLButtonElement>('[data-cy=schedule-apply]')!.disabled).toBe(false));

		// Assert
		expect(container.querySelectorAll('[role=gridcell][aria-selected=true]')).toHaveLength(4);
		expect(store.confirmedDownloadSchedule.days.Monday?.['09:00']).toBe(3000);
		expect(store.confirmedDownloadSchedule.days.Tuesday?.['09:00']).toBe(3000);

		// Act
		fireEvent.click(container.querySelector('[data-cy=schedule-cell-2-24]')!);
		await nextTick();
		fireEvent.click(getByRole(container, 'button', { name: 'Apply to selection & save' }));
		await waitFor(() => expect(mock.history.put).toHaveLength(2));
		await waitFor(() => expect(store.confirmedDownloadSchedule.days.Wednesday?.['12:00']).toBe(3000));
		await waitFor(() => expect(container.querySelector<HTMLButtonElement>('[data-cy=schedule-apply]')!.disabled).toBe(false));

		// Assert
		expect(store.confirmedDownloadSchedule.days.Wednesday?.['12:00']).toBe(3000);
		expect(store.confirmedDownloadSchedule.days.Monday?.['09:00']).toBe(3000);
		expect(getByRole(container, 'radio', { name: 'Limit' }).getAttribute('aria-checked')).toBe('true');
	});

	test('Should keep the initial Limit mode when starting and extending a box over Unlimited hours', async () => {
		// Arrange
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		const firstCell = container.querySelector('[data-cy=schedule-cell-1-24]')!;

		// Act
		fireEvent.click(firstCell);
		await nextTick();

		// Assert: the first selected hour must not import Unlimited into the painting tool.
		expect(getByRole(container, 'radio', { name: 'Limit' }).getAttribute('aria-checked')).toBe('true');
		expect(Number((getByRole(container, 'spinbutton') as HTMLInputElement).value.replace(/[^\d.-]/g, ''))).toBe(5000);
		expect(mock.history.put).toEqual([]);

		// Act: expand the box and apply without changing either the mode or the rate.
		fireEvent.keyDown(firstCell, { key: 'ArrowRight', shiftKey: true });
		await nextTick();
		fireEvent.keyDown(container.querySelector('[data-cy=schedule-cell-1-25]')!, { key: 'ArrowDown', shiftKey: true });
		await nextTick();
		fireEvent.click(getByRole(container, 'button', { name: 'Apply to selection & save' }));
		await waitFor(() => expect(store.confirmedDownloadSchedule.days.Wednesday?.['12:00']).toBe(5000));

		// Assert
		expect(mock.history.put).toHaveLength(1);
		const saved: SettingsModelDTO = JSON.parse(mock.history.put[0]!.data);
		expect(saved.downloadManagerSettings.downloadSchedule).toEqual({
			...settings.downloadManagerSettings.downloadSchedule,
			days: {
				...settings.downloadManagerSettings.downloadSchedule.days,
				Tuesday: { '12:00': 5000, '13:00': null },
				Wednesday: { '12:00': 5000, '13:00': null },
			},
		});
		expect(container.querySelectorAll('[role=gridcell][aria-selected=true]')).toHaveLength(4);
	});

	test('Should display the configured clock format while saving canonical half-hour keys', async () => {
		// Arrange
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		store.dateTimeSettings.timeFormat = 'pp';
		await nextTick();
		const cell = container.querySelector('[data-cy=schedule-cell-1-43]')!;

		// Act
		fireEvent.click(cell);
		await nextTick();

		// Assert
		expect(cell.getAttribute('aria-label')).toBe('Tuesday 9:30 PM–10:00 PM: 5,000 kB/s');
		expect(container.querySelector('[data-cy=schedule-from]')!.textContent).toContain('9:30 PM');
		expect(container.querySelector('[data-cy=schedule-until]')!.textContent).toContain('10:00 PM');

		// Act
		fireEvent.click(getByRole(container, 'button', { name: 'Apply to selection & save' }));
		await waitFor(() => expect(store.confirmedDownloadSchedule.days.Tuesday?.['21:30']).toBe(5000));

		// Assert
		expect(store.confirmedDownloadSchedule.days.Tuesday).toEqual({ '21:30': 5000, '22:00': null });
		expect(store.confirmedDownloadSchedule.days.Monday).toEqual(settings.downloadManagerSettings.downloadSchedule.days.Monday);
		expect(getByRole(container, 'radio', { name: 'Limit' }).getAttribute('aria-checked')).toBe('true');
	});

	test('Should start in Limit mode and prevent Apply from overwriting an enabled change while it saves', async () => {
		// Arrange
		expect(getByRole(container, 'radio', { name: 'Limit' }).getAttribute('aria-checked')).toBe('true');
		expect(Number((getByRole(container, 'spinbutton') as HTMLInputElement).value.replace(/[^\d.-]/g, ''))).toBe(5000);
		fireEvent.click(container.querySelector('[data-cy=schedule-cell-0-18]')!);
		await nextTick();
		const apply = getByRole(container, 'button', { name: 'Apply to selection & save' }) as HTMLButtonElement;
		const toggle = container.querySelector<HTMLElement>('[data-cy=schedule-enable]')!;
		const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);

		// Act
		fireEvent.click(toggle);
		await waitFor(() => expect(mock.history.put).toHaveLength(1));
		apply.click();
		await nextTick();

		// Assert
		expect(toggle.getAttribute('aria-busy')).toBe('true');
		expect(apply.disabled).toBe(true);
		expect(mock.history.put).toHaveLength(1);
		expect(toggle.getAttribute('aria-checked')).toBe('true');
		const saved: SettingsModelDTO = JSON.parse(mock.history.put[0]!.data);
		expect(saved.downloadManagerSettings.downloadSchedule).toEqual({
			...settings.downloadManagerSettings.downloadSchedule,
			enabled: false,
		});
		resolve([200, generateResultDTO(saved)]);
		await waitFor(() => expect(toggle.getAttribute('aria-busy')).toBe('false'));
		expect(toggle.getAttribute('aria-busy')).toBe('false');
		expect(apply.disabled).toBe(false);
	});

	test('Should make the whole week Unlimited only after a confirmed reset succeeds', async () => {
		// Arrange
		const { promise, resolve } = Promise.withResolvers<[number, unknown]>();
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply(() => promise);
		const dialog = await openReset();

		// Act
		fireEvent.click(getByRole(dialog, 'button', { name: 'Reset schedule' }));
		await waitFor(() => expect(mock.history.put).toHaveLength(1));
		const saved: SettingsModelDTO = JSON.parse(mock.history.put[0]!.data);

		// Assert: no optimistic wipe, and unrelated download settings are retained.
		expect(container.querySelector('[data-cy=schedule-cell-0-18]')!.getAttribute('aria-label')).toContain('5,000 kB/s');
		expect(saved.downloadManagerSettings).toEqual({
			...settings.downloadManagerSettings,
			downloadSchedule: { enabled: true, days: {} },
		});
		resolve([200, generateResultDTO(saved)]);
		await waitFor(() => expect(container.querySelector('[data-cy=schedule-cell-0-18]')!.getAttribute('aria-label')).toContain('Unlimited'));
		expect(decodeDownloadScheduleDays(store.confirmedDownloadSchedule.days)).toEqual(Array(336).fill(null));
	});

	test('Should retain the saved schedule and editable draft when resetting fails', async () => {
		// Arrange
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).replyOnce(500, generateFailedResultDTO());
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		fireEvent.click(getByRole(container, 'button', { name: 'Select weekday work hours' }));
		await nextTick();
		fireEvent.click(getByRole(container, 'radio', { name: 'Limit' }));
		await nextTick();
		const input = getByRole(container, 'spinbutton') as HTMLInputElement;
		input.focus();
		input.select();
		for (const key of '3000') fireEvent.keyPress(input, { key, code: `Digit${key}` });
		await nextTick();
		const dialog = await openReset();

		// Act
		fireEvent.click(getByRole(dialog, 'button', { name: 'Reset schedule' }));
		await waitFor(() => expect(getByRole(container, 'alert').textContent).toContain('Save failed.'));

		// Assert
		expect(store.confirmedDownloadSchedule).toEqual(settings.downloadManagerSettings.downloadSchedule);
		expect(store.downloadManagerSettings.downloadSchedule).toEqual(settings.downloadManagerSettings.downloadSchedule);
		expect(container.querySelector('[data-cy=schedule-cell-0-18]')!.getAttribute('aria-label')).toContain('3,000 kB/s');
		expect(container.querySelectorAll('[role=gridcell][aria-selected=true]')).toHaveLength(90);
		expect(Number((getByRole(container, 'spinbutton') as HTMLInputElement).value.replace(/[^\d.-]/g, ''))).toBe(3000);
		fireEvent.click(getByRole(container, 'button', { name: 'Apply to selection & save' }));
		await waitFor(() => expect(store.confirmedDownloadSchedule.days.Monday?.['09:00']).toBe(3000));
		expect(store.confirmedDownloadSchedule.days.Monday!['09:00']).toBe(3000);
		expect(store.confirmedDownloadSchedule.days.Sunday!['23:30']).toBe(1000);
		expect(container.querySelector('[data-cy=schedule-save-error]')).toBeNull();
		expect(mock.history.put).toHaveLength(2);
		const saved: SettingsModelDTO = JSON.parse(mock.history.put[1]!.data);
		expect(saved.downloadManagerSettings.downloadSchedule).toEqual(store.confirmedDownloadSchedule);
		expect(saved.serverSettings).toEqual(settings.serverSettings);
	});
});
