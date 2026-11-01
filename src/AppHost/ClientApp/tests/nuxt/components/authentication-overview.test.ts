import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { flushPromises, type VueWrapper } from '@vue/test-utils';
import type { Component } from 'vue';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { AuthenticationPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import type { AppCredentialsDTO } from '@dto';
import { SetupPanelType } from '@enums';
import { useAuthenticationStore } from '@store';
import { suspendApiRequests } from '@/types/api/base/ApiPipes';
import AuthenticationOverview from '@/components/Overviews/AuthenticationOverview.vue';
import AuthorizationSetupPanel from '@/components/Views/Setup/Panels/AuthorizationSetupPanel.vue';
import ValidationButton from '@/components/Buttons/ValidationButton.vue';
import messages from '@/lang/en-US.json';

const original: AppCredentialsDTO = {
	userName: 'ExistingUser22', password: 'masked-password', isDefaultCredentials: false,
};
const updated: AppCredentialsDTO = { ...original, userName: 'RecoveredUser22' };
const credentialPath = AuthenticationPaths.getAppCredentials();
const savePath = AuthenticationPaths.updateCredentialsEndpoint();

function deferredResponse() {
	let reply!: (value: [number, unknown]) => void;
	const response = new Promise<[number, unknown]>((resolve) => {
		reply = resolve;
	});
	return { response, reply };
}

function expectFieldsDisabled(wrapper: VueWrapper, disabled: boolean): void {
	for (const selector of ['app-username-input', 'app-password-input', 'app-confirm-password-input']) {
		expect(wrapper.get(`[data-cy="${selector}"]`).attributes('disabled') !== undefined, selector).toBe(disabled);
	}
}

describe('AuthenticationOverview loading lifecycle', () => {
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

	async function renderCredentials(inSetup = false) {
		const component: Component = inSetup ? AuthorizationSetupPanel : AuthenticationOverview;
		const wrapper = await mountSuspended(component, {
			props: inSetup ? { name: SetupPanelType.AuthorizationPanel } : {},
			global: {
				plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					HelpRow: { template: '<section><slot /></section>' },
					QTabPanel: { template: '<section><slot /></section>' },
					QSection: { template: '<section><slot /></section>' },
				},
			},
		});
		mounted = wrapper;
		return wrapper;
	}

	test('Should lock the Settings form through initial load, save and follow-up refresh', async () => {
		// Arrange
		const initial = deferredResponse();
		const saveResponse = deferredResponse();
		const refresh = deferredResponse();
		mock.onGet(credentialPath).replyOnce(() => initial.response)
			.onGet(credentialPath).reply(() => refresh.response);
		mock.onPut(savePath).reply(() => saveResponse.response);
		useAuthenticationStore().username = 'PendingDraftUser22';
		const wrapper = await renderCredentials();
		const save = wrapper.get('[data-cy="save-credentials-button"]');

		try {
			// Initial loading also blocks an already-changeable shared draft.
			expectFieldsDisabled(wrapper, true);
			expect(save.attributes('disabled')).toBeDefined();
			wrapper.getComponent(ValidationButton).vm.$emit('click');
			expect(mock.history.put).toEqual([]);
			initial.reply([200, generateResultDTO(original)]);
			await flushPromises();
			expectFieldsDisabled(wrapper, false);
			expect(wrapper.get<HTMLInputElement>('[data-cy="app-username-input"]').element.value).toBe(original.userName);

			// Two clicks before the disabled state renders still create only one write.
			await wrapper.get('[data-cy="app-username-input"]').setValue(updated.userName);
			await Promise.all([save.trigger('click'), save.trigger('click')]);
			await vi.waitFor(() => expect(mock.history.put).toHaveLength(1));
			expectFieldsDisabled(wrapper, true);
			expect(save.attributes('disabled')).toBeDefined();
			saveResponse.reply([200, generateResultDTO(null)]);
			await vi.waitFor(() => expect(mock.history.get).toHaveLength(2));
			expectFieldsDisabled(wrapper, true);
			expect(save.classes()).not.toContain('base-btn-positive');
			wrapper.getComponent(ValidationButton).vm.$emit('click');
			expect(mock.history.put).toHaveLength(1);

			// Successful refresh restores editable fields and normal unchanged-draft validation.
			refresh.reply([200, generateResultDTO(updated)]);
			await flushPromises();
			expectFieldsDisabled(wrapper, false);
			expect(useAuthenticationStore().currentUsername).toBe(updated.userName);
			expect(save.classes()).toContain('base-btn-positive');
			expect(save.attributes('disabled')).toBeDefined();
		} finally {
			initial.reply([200, generateResultDTO(original)]);
			saveResponse.reply([200, generateResultDTO(null)]);
			refresh.reply([200, generateResultDTO(updated)]);
			await flushPromises();
		}
	});

	test('Should leave the Setup form correctable after a failed load and rejected save', async () => {
		// Arrange
		mock.onGet(credentialPath).replyOnce(500, generateFailedResultDTO({ statusCode: 500 }))
			.onGet(credentialPath).reply(200, generateResultDTO(updated));
		mock.onPut(savePath).replyOnce(400, generateFailedResultDTO({ statusCode: 400 }))
			.onPut(savePath).reply(200, generateResultDTO(null));
		const wrapper = await renderCredentials(true);
		await flushPromises();
		const save = wrapper.get('[data-cy="save-credentials-button"]');

		// Act and assert: neither failure strands the loading state or erases the draft.
		expectFieldsDisabled(wrapper, false);
		await wrapper.get('[data-cy="app-username-input"]').setValue('Invalid Username');
		await save.trigger('click');
		await flushPromises();
		expectFieldsDisabled(wrapper, false);
		expect(useAuthenticationStore().username).toBe('Invalid Username');
		expect(save.classes()).toContain('base-btn-negative');
		await wrapper.get('[data-cy="app-username-input"]').setValue(updated.userName);
		await save.trigger('click');
		await flushPromises();
		expect(mock.history.put).toHaveLength(2);
		expect(useAuthenticationStore().currentUsername).toBe(updated.userName);
		expectFieldsDisabled(wrapper, false);
		expect(save.classes()).toContain('base-btn-positive');
	});

	test('Should release an empty cancelled load and allow saving after API requests resume', async () => {
		// Arrange: a session reset can suspend new SDK requests without emitting a result.
		mock.onGet(credentialPath).reply(200, generateResultDTO(updated));
		mock.onPut(savePath).reply(200, generateResultDTO(null));
		const resume = suspendApiRequests();
		try {
			const wrapper = await renderCredentials();
			await flushPromises();
			expect(mock.history.get).toEqual([]);
			expectFieldsDisabled(wrapper, false);
			resume();

			// Act and assert
			await wrapper.get('[data-cy="app-username-input"]').setValue(updated.userName);
			await wrapper.get('[data-cy="save-credentials-button"]').trigger('click');
			await flushPromises();
			expect(useAuthenticationStore().currentUsername).toBe(updated.userName);
			expectFieldsDisabled(wrapper, false);
		} finally {
			resume();
		}
	});
});
