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

	async function renderLayout() {
		const nuxtApp = useNuxtApp();
		const hook = vi.spyOn(nuxtApp, 'hook');
		const layout = await mountSuspended(DefaultLayout, {
			route: '/',
			global: { plugins: [pinia], stubs: { PageLoadOverlay: true, Background: true } },
		});
		wrapper = layout;
		hook.mock.calls.forEach(([name], index) => {
			const remove = hook.mock.results[index]?.value;
			if (name !== 'page:start' && name !== 'page:finish') return;
			if (typeof remove === 'function')
				removeHooks.push(() => remove());
		});
		hook.mockRestore();
		vi.useFakeTimers();
		return { nuxtApp, layout };
	}

	test('Should discard old-session callbacks after quick reauthentication and retain fresh setup and invite callbacks', async () => {
		// Arrange
		const settings = useSettingsStore();
		settings.generalSettings.firstTimeSetup = true;
		settings.generalSettings.hasBeenInvitedToDiscord = false;
		const auth = useAuthenticationStore();
		const dialogs = useDialogStore();
		await subscribeSpyTo(dialogs.setup()).onComplete();
		const events = subscribeSpyTo(dialogs.getDialogState());
		const { nuxtApp } = await renderLayout();
		const navigation = vi.spyOn(nuxtApp.$router, 'push').mockResolvedValue(undefined);
		auth.isLoggedIn = true;

		// Act: the session ends after page completion schedules its dialog.
		await nuxtApp.callHook('page:finish');
		auth.$reset();
		auth.isLoggedIn = true;
		await vi.advanceTimersByTimeAsync(1000);

		// Assert the old callback cannot show authenticated UI.
		expect(events.getValues()).toEqual([]);
		expect(navigation).not.toHaveBeenCalled();

		// Act: fresh page completions still make the normal setup and invite decisions.
		await nuxtApp.callHook('page:finish');
		await vi.advanceTimersByTimeAsync(1000);
		expect(navigation.mock.calls).toEqual([['/setup']]);
		settings.generalSettings.firstTimeSetup = false;
		await nuxtApp.callHook('page:finish');
		await vi.advanceTimersByTimeAsync(1000);
		dialogs.dialogUpdate.complete();
		await events.onComplete();

		// Assert
		expect(events.getValues()).toEqual([{
			name: DialogType.DiscordServerInviteDialog, state: true, data: {}, id: undefined,
		}]);
	});

	test('Should cancel pending callbacks and detach page hooks when the layout is disposed', async () => {
		// Arrange
		const settings = useSettingsStore();
		settings.generalSettings.firstTimeSetup = false;
		settings.generalSettings.hasBeenInvitedToDiscord = false;
		const auth = useAuthenticationStore();
		const dialogs = useDialogStore();
		await subscribeSpyTo(dialogs.setup()).onComplete();
		const events = subscribeSpyTo(dialogs.getDialogState());
		const { nuxtApp, layout } = await renderLayout();
		auth.isLoggedIn = true;

		// Act: disposal cancels scheduled work and prevents later hooks scheduling more.
		await nuxtApp.callHook('page:finish');
		layout.unmount();
		wrapper = undefined;
		await nuxtApp.callHook('page:finish');
		await vi.advanceTimersByTimeAsync(1000);
		dialogs.dialogUpdate.complete();
		await events.onComplete();

		// Assert
		expect(events.getValues()).toEqual([]);
	});
});
