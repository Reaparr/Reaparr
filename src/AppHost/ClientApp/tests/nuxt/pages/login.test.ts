import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { flushPromises } from '@vue/test-utils';
import { Subject, of } from 'rxjs';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { AuthenticationPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import {
	useAccountStore, useAlertStore, useAuthenticationStore, useBackgroundJobsStore, useDialogStore,
	useDownloadStore, useFolderPathStore, useGlobalStore, useHelpStore, useIntegrationStore,
	useLibraryStore, useLocalizationStore, useLogsStore, useMediaStore, useNotificationsStore,
	useServerConnectionStore, useServerStore, useSettingsStore, useSignalrStore, useUpdateStore,
} from '@store';
import { StoreNames, type ISetupResult } from '@interfaces';
import LoginPage from '@/pages/login/index.vue';
import messages from '@/lang/en-US.json';

const loginPath = AuthenticationPaths.appUserLoginEndpoint();

function holdApplicationInitialization() {
	const result: ISetupResult = { name: StoreNames.PageSetup, isSuccess: true };
	for (const store of [
		useAccountStore(), useAlertStore(), useBackgroundJobsStore(), useDialogStore(),
		useFolderPathStore(), useHelpStore(), useIntegrationStore(), useLibraryStore(),
		useLocalizationStore(), useLogsStore(), useMediaStore(), useNotificationsStore(),
		useServerConnectionStore(), useServerStore(), useSettingsStore(), useSignalrStore(), useUpdateStore(),
	]) {
		vi.spyOn(store, 'setup').mockReturnValue(of(result));
	}
	const dependency = new Subject<ISetupResult>();
	const setup = vi.spyOn(useDownloadStore(), 'setup').mockReturnValue(dependency);
	return { dependency, setup, result };
}

describe('Login page request lifecycle', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	let mounted: { unmount: () => void } | undefined;

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		mounted?.unmount();
		mounted = undefined;
		vi.restoreAllMocks();
		mock.restore();
	});

	async function renderLogin() {
		const wrapper = await mountSuspended(LoginPage, {
			route: '/login',
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: { QPage: { template: '<main><slot /></main>' } },
			},
		});
		mounted = wrapper;
		await wrapper.get('input[type="text"]').setValue('admin');
		await wrapper.get('input[name="password"]').setValue('password');
		return wrapper;
	}

	test('Should serialize login through initialization and allow failed or cancelled attempts to retry', async () => {
		// Arrange
		let release!: (reply: [number, unknown]) => void;
		const response = new Promise<[number, unknown]>((resolve) => {
			release = resolve;
		});
		mock.onPost(loginPath).replyOnce(() => response)
			.onPost(loginPath).reply(200, generateResultDTO(null));
		const initialization = new Subject<ISetupResult[]>();
		const setup = vi.spyOn(useGlobalStore(), 'setup').mockReturnValueOnce(initialization).mockReturnValue(of([]));
		const wrapper = await renderLogin();
		const submit = wrapper.get('[data-cy="login-submit-button"]');
		try {
			// Both clicks arrive before Vue renders the disabled button.
			await Promise.all([submit.trigger('click'), submit.trigger('click')]);
			await vi.waitFor(() => expect(mock.history.post).toHaveLength(1));
			expect(submit.attributes('disabled')).toBeDefined();
			expect(wrapper.get('input[type="text"]').attributes('disabled')).toBeDefined();
			expect(wrapper.get('input[name="password"]').attributes('disabled')).toBeDefined();
			expect(wrapper.get('[data-cy="login-remember-me-input"]').attributes('aria-disabled')).toBe('true');
			expect(setup).not.toHaveBeenCalled();

			// A rejected login unlocks the actual form for a corrected attempt.
			release([401, generateFailedResultDTO({ statusCode: 401 })]);
			await flushPromises();
			expect(submit.attributes('disabled')).toBeUndefined();
			expect(wrapper.get('[data-cy="login-invalid-credentials-alert"]').isVisible()).toBe(true);
			await wrapper.get('input[name="password"]').setValue('corrected-password');
			await submit.trigger('click');
			await vi.waitFor(() => expect(setup).toHaveBeenCalledTimes(1));
			expect(submit.attributes('disabled')).toBeDefined();
			await submit.trigger('click');
			expect(mock.history.post).toHaveLength(2);

			// Session expiry can cancel initialization without emitting a status.
			initialization.complete();
			await flushPromises();
			expect(submit.attributes('disabled')).toBeUndefined();
			await submit.trigger('click');
			await flushPromises();
			expect(mock.history.post).toHaveLength(3);
			expect(setup).toHaveBeenCalledTimes(2);
			expect(useAuthenticationStore().isLoggedIn).toBe(true);
			expect(wrapper.find('[data-cy="login-invalid-credentials-alert"]').exists()).toBe(false);
			expect(submit.attributes('disabled')).toBeUndefined();
		} finally {
			release([401, generateFailedResultDTO({ statusCode: 401 })]);
			initialization.complete();
			await flushPromises();
		}
	});

	test('Should unlock the form after an initialization error and accept a later login', async () => {
		// Arrange
		mock.onPost(loginPath).reply(200, generateResultDTO(null));
		const initialization = new Subject<ISetupResult[]>();
		vi.spyOn(useGlobalStore(), 'setup').mockReturnValueOnce(initialization).mockReturnValue(of([]));
		const wrapper = await renderLogin();
		const submit = wrapper.get('[data-cy="login-submit-button"]');

		// Act and assert
		await submit.trigger('click');
		await flushPromises();
		expect(submit.attributes('disabled')).toBeDefined();
		initialization.error(new Error('Initialization failed'));
		await flushPromises();
		expect(submit.attributes('disabled')).toBeUndefined();
		await submit.trigger('click');
		await flushPromises();
		expect(mock.history.post).toHaveLength(2);
		expect(useAuthenticationStore().isLoggedIn).toBe(true);
		expect(submit.attributes('disabled')).toBeUndefined();
	});

	test('Should finish application initialization after navigation unmounts Login', async () => {
		// Arrange: use the real global coordinator with one held dependency.
		mock.onPost(loginPath).reply(200, generateResultDTO(null));
		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO({
			claims: [], isLoggedIn: true, userName: 'CurrentUser',
		}));
		const initialization = holdApplicationInitialization();
		const values: boolean[] = [];
		const readiness = useGlobalStore().getPageSetupReady.subscribe((value) => values.push(value));
		const wrapper = await renderLogin();

		try {
			// Act: navigation leaves Login after authentication but before setup finishes.
			await wrapper.get('[data-cy="login-submit-button"]').trigger('click');
			await vi.waitFor(() => expect(initialization.setup).toHaveBeenCalledTimes(1));
			expect(values.at(-1)).toBe(false);
			wrapper.unmount();
			mounted = undefined;
			initialization.dependency.next(initialization.result);
			initialization.dependency.complete();
			await flushPromises();

			// Assert the persistent layout can leave its loading state.
			expect(values.at(-1)).toBe(true);
			expect(useAuthenticationStore().isLoggedIn).toBe(true);
		} finally {
			initialization.dependency.complete();
			readiness.unsubscribe();
		}
	});
});
