import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths, PlexLibraryPaths, PlexServerPaths } from '@api-urls';
import { generateFailedResultDTO, generatePlexAccount, generateResultDTO } from '@mock';
import { DialogType } from '@enums';
import { useAccountDialogStore, useAccountStore, useDialogStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AccountDialogStore.saveAccount()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should preserve a failed create draft and close only after a successful retry', async () => {
		// Arrange
		const account = generatePlexAccount({
			id: 99,
			partialData: {
				displayName: 'Retry account',
				is2Fa: false,
				isValidated: true,
				password: 'create-password',
				username: 'create-user',
			},
		});
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.openDialog({ accountId: 0 });
		accountDialogStore.displayName = account.displayName;
		accountDialogStore.username = account.username;
		accountDialogStore.password = account.password;
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			authenticationToken: account.authenticationToken,
			clientId: account.clientId,
			email: account.email,
			isUnAuthorized: false,
			isValidated: true,
			password: account.password,
			plexId: account.plexId,
			title: account.title,
			username: account.username,
			uuid: account.uuid,
			validatedAt: account.validatedAt,
		}));
		await subscribeSpyTo(accountDialogStore.validatePlexAccount()).onComplete();

		const failedResult = generateFailedResultDTO({ statusCode: 409 });
		const successfulResult = generateResultDTO(account);
		mock
			.onPost(PlexAccountPaths.createPlexAccountEndpoint())
			.replyOnce(200, failedResult)
			.onPost(PlexAccountPaths.createPlexAccountEndpoint())
			.reply(200, successfulResult);

		// Act
		const failedSave = subscribeSpyTo(accountDialogStore.saveAccount());
		await failedSave.onComplete();

		// Assert
		expect(failedSave.getFirstValue()).toEqual(failedResult);
		expect(accountDialogStore.displayName).toBe(account.displayName);
		expect(accountDialogStore.username).toBe(account.username);
		expect(accountDialogStore.savingLoading).toBe(false);
		expect(closeDialogSpy).not.toHaveBeenCalledWith(DialogType.AccountDialog);

		// Arrange successful retry refreshes
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(200, generateResultDTO([account]));
		mock.onGet(PlexServerPaths.getAllPlexServersEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO([]));

		// Act successful retry
		const successfulSave = subscribeSpyTo(accountDialogStore.saveAccount());
		await successfulSave.onComplete();

		// Assert successful retry
		expect(successfulSave.getFirstValue()).toEqual(successfulResult);
		expect(closeDialogSpy).toHaveBeenCalledWith(DialogType.AccountDialog);
		expect(accountDialogStore.savingLoading).toBe(false);
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendAccountInteraction, {
				action: FrontendTelemetryAction.ValidateCredentials,
				account_id: 0,
				is_new_account: true,
			}],
			[FrontendTelemetryEvent.FrontendAccountInteraction, {
				action: FrontendTelemetryAction.Save,
				account_id: 0,
				is_new_account: true,
				is_auth_token_mode: false,
			}],
			[FrontendTelemetryEvent.FrontendAccountInteraction, {
				action: FrontendTelemetryAction.Save,
				account_id: 0,
				is_new_account: true,
				is_auth_token_mode: false,
			}],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain('create-password');
	});

	test('Should preserve a failed update draft and close only after a successful retry', async () => {
		// Arrange
		const account = generatePlexAccount({
			id: 99,
			partialData: {
				displayName: 'Updated account',
				isValidated: true,
			},
		});
		const accountStore = useAccountStore();
		accountStore.accounts = [account];
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.openDialog({ accountId: account.id });
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');

		const failedResult = generateFailedResultDTO({ statusCode: 422 });
		const successfulResult = generateResultDTO(account);
		mock
			.onPut(PlexAccountPaths.updatePlexAccountByIdEndpoint())
			.replyOnce(200, failedResult)
			.onPut(PlexAccountPaths.updatePlexAccountByIdEndpoint())
			.reply(200, successfulResult);

		// Act
		const failedSave = subscribeSpyTo(accountDialogStore.saveAccount());
		await failedSave.onComplete();

		// Assert
		expect(failedSave.getFirstValue()).toEqual(failedResult);
		expect(accountDialogStore.displayName).toBe(account.displayName);
		expect(accountDialogStore.username).toBe(account.username);
		expect(accountDialogStore.savingLoading).toBe(false);
		expect(closeDialogSpy).not.toHaveBeenCalledWith(DialogType.AccountDialog);

		// Arrange successful retry refreshes
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(200, generateResultDTO([account]));
		mock.onGet(PlexServerPaths.getAllPlexServersEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO([]));

		// Act successful retry
		const successfulSave = subscribeSpyTo(accountDialogStore.saveAccount());
		await successfulSave.onComplete();

		// Assert successful retry
		expect(successfulSave.getFirstValue()).toEqual(successfulResult);
		expect(closeDialogSpy).toHaveBeenCalledWith(DialogType.AccountDialog);
		expect(accountDialogStore.savingLoading).toBe(false);
	});
});
