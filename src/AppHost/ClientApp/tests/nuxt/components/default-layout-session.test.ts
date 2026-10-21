import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useAuthenticationStore, useDialogStore, useSettingsStore } from '@store';
import { DialogType } from '@enums';
import DefaultLayout from '@/layouts/default.vue';

describe('Default layout session callbacks', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let wrapper: { unmount: () => void } | undefined;
	const removeHooks: Array<() => void> = [];

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		wrapper?.unmount();
		wrapper = undefined;
		removeHooks.splice(0).forEach((remove) => remove());
		vi.clearAllTimers();
		vi.useRealTimers();
		vi.restoreAllMocks();
		mock.restore();
	});

	test('Should suppress an old authenticated dialog callback while allowing a current session callback', async () => {
		// Arrange
		const settings = useSettingsStore();
		settings.generalSettings.firstTimeSetup = false;
		settings.generalSettings.hasBeenInvitedToDiscord = false;
		const auth = useAuthenticationStore();
		const dialogs = useDialogStore();
		await subscribeSpyTo(dialogs.setup()).onComplete();
		const events = subscribeSpyTo(dialogs.getDialogState());
		const nuxtApp = useNuxtApp();
		const hook = vi.spyOn(nuxtApp, 'hook');
		wrapper = await mountSuspended(DefaultLayout, {
			route: '/',
			global: { plugins: [pinia], stubs: { PageLoadOverlay: true, Background: true } },
		});
		hook.mock.calls.forEach(([name], index) => {
			const remove = hook.mock.results[index]?.value;
			if (name !== 'page:start' && name !== 'page:finish') return;
			if (typeof remove === 'function')
				removeHooks.push(() => remove());
		});
		hook.mockRestore();
		vi.useFakeTimers();
		auth.isLoggedIn = true;

		// Act: the session ends after page completion schedules its dialog.
		await nuxtApp.callHook('page:finish');
		auth.$reset();
		await vi.advanceTimersByTimeAsync(1000);

		// Assert the old callback cannot show authenticated UI.
		expect(events.getValues()).toEqual([]);

		// Act: a new authenticated completion still offers the normal dialog.
		auth.isLoggedIn = true;
		await nuxtApp.callHook('page:finish');
		await vi.advanceTimersByTimeAsync(1000);
		dialogs.dialogUpdate.complete();
		await events.onComplete();

		// Assert
		expect(events.getValues()).toEqual([{
			name: DialogType.DiscordServerInviteDialog, state: true, data: {}, id: undefined,
		}]);
	});
});
