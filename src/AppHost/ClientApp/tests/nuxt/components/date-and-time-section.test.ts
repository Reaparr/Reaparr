import { useNuxtApp } from '#app';
import { fireEvent, getByRole, queryByRole, waitFor } from '@testing-library/dom';
import { h, nextTick, render } from 'vue';
import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO, generateSettingsModel } from '@mock';
import { SettingsPaths } from '@api-urls';
import { useSettingsStore } from '@store';
import type { SettingsModelDTO } from '@dto';
import DateAndTimeSection from '@components/Views/Settings/DateAndTimeSection.vue';

describe('DateAndTimeSection searchable timezone selection', () => {
	let container: HTMLElement;
	let store: ReturnType<typeof useSettingsStore>;
	let mock: ReturnType<typeof getAxiosMock>;
	let settings: SettingsModelDTO;

	beforeAll(() => baseSetup());
	beforeEach(async () => {
		mock = getAxiosMock();
		store = useNuxtApp().vueApp.runWithContext(() => useSettingsStore());
		store.$reset();
		settings = generateSettingsModel({ config: { seed: 20261006 } });
		settings.dateTimeSettings.timeZone = 'UTC';
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(settings));
		mock.onPut(SettingsPaths.updateUserSettingsEndpoint()).reply((request) => [200, generateResultDTO(JSON.parse(request.data))]);
		await subscribeSpyTo(store.setup()).onComplete();
		container = document.createElement('div');
		document.body.append(container);
		const vnode = h(DateAndTimeSection);
		vnode.appContext = useNuxtApp().vueApp._context;
		render(vnode, container);
		await nextTick();
	});
	afterEach(() => {
		render(null, container);
		container.remove();
		store.$reset();
	});

	test('Should filter names and offsets without saving search text, then persist only the chosen timezone identifier', async () => {
		// Arrange
		const input = getByRole(container, 'combobox', { name: /time.?zone/i }) as HTMLInputElement;
		input.focus();

		// Act
		fireEvent.input(input, { target: { value: 'tOkYo' } });
		await waitFor(() => getByRole(document.body, 'option', { name: '(UTC+09:00) Asia/Tokyo' }));

		// Assert
		expect(queryByRole(document.body, 'option', { name: '(UTC+00:00) UTC' })).toBeNull();
		expect(store.dateTimeSettings.timeZone).toBe('UTC');
		expect(mock.history.put).toEqual([]);

		// Act: clearing restores the dropdown; offset search still selects an identifier, not its label.
		fireEvent.input(input, { target: { value: '' } });
		await waitFor(() => getByRole(document.body, 'option', { name: '(UTC+00:00) UTC' }));
		fireEvent.input(input, { target: { value: '+09:00' } });
		const option = await waitFor(() => getByRole(document.body, 'option', { name: '(UTC+09:00) Asia/Tokyo' }));
		fireEvent.click(option);
		await waitFor(() => expect(store.settingsSaveState).toBe('saved'));
		await waitFor(() => expect(mock.history.put).toHaveLength(1));

		// Assert
		const saved: SettingsModelDTO = JSON.parse(mock.history.put[0]!.data);
		expect(saved.dateTimeSettings).toEqual({ ...settings.dateTimeSettings, timeZone: 'Asia/Tokyo' });
		expect(saved.downloadManagerSettings).toEqual(settings.downloadManagerSettings);
		expect(store.dateTimeSettings.timeZone).toBe('Asia/Tokyo');
	});
});
