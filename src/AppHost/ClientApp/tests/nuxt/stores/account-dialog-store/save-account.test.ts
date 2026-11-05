import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths, PlexLibraryPaths, PlexServerPaths } from '@api-urls';
import { generateFailedResultDTO, generatePlexAccount, generateResultDTO } from '@mock';
import { DialogType } from '@enums';
import { useAccountDialogStore, useAccountStore, useDialogStore } from '@store';
import type { PlexAccountDTO } from '@dto';
import type { ResultDTO } from '@interfaces';

describe('AccountDialogStore.saveAccount()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
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

	test.each([
		{ scenario: 'a new account draft is reopened', firstId: 0, nextId: 0, failOld: false },
		{ scenario: 'the same saved account is reopened', firstId: 9, nextId: 9, failOld: false },
		{ scenario: 'another saved account is opened', firstId: 9, nextId: 10, failOld: false },
		{ scenario: 'an old update errors after switching accounts', firstId: 9, nextId: 10, failOld: true },
	])('Should preserve the newer draft and pending save when $scenario', async ({ firstId, nextId, failOld }) => {
		// Arrange
		const account = generatePlexAccount({
			id: 9,
			config: { seed: 109 },
			partialData: {
				displayName: 'Original account',
				username: 'original-user',
				password: 'OriginalPassword123!',
				customAuthenticationToken: '',
				is2Fa: false,
				isValidated: true,
			},
		});
		const accounts = useAccountStore();
		accounts.accounts = [account, { ...account, id: 10, displayName: 'Other account' }];
		const dialog = useAccountDialogStore();
		const oldSave = new Subject<ResultDTO<PlexAccountDTO>>();
		const currentSave = new Subject<ResultDTO<PlexAccountDTO>>();
		vi.spyOn(accounts, firstId === 0 ? 'createPlexAccount' : 'updatePlexAccount')
			.mockReturnValueOnce(oldSave).mockReturnValueOnce(currentSave);
		const closeSpy = vi.spyOn(useDialogStore(), 'closeDialog');
		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			...account,
			isUnAuthorized: false,
		}));
		const openAccount = async (accountId: number) => {
			dialog.openDialog({ accountId });
			if (accountId !== 0) return;
			dialog.displayName = account.displayName;
			dialog.username = account.username;
			dialog.password = account.password;
			await subscribeSpyTo(dialog.validatePlexAccount()).onComplete();
		};
		await openAccount(firstId);
		const oldRequest = subscribeSpyTo(dialog.saveAccount(), { expectErrors: failOld });
		dialog.closeDialog();
		await openAccount(nextId);
		const currentRequest = subscribeSpyTo(dialog.saveAccount());
		dialog.displayName = 'Newer unsaved edits';
		closeSpy.mockClear();
		expect(dialog.savingLoading).toBe(true);

		// Act
		if (failOld) {
			oldSave.error(new Error('Old update failed'));
		} else {
			oldSave.next(generateResultDTO(account));
			oldSave.complete();
			await oldRequest.onComplete();
		}

		// Assert
		expect(dialog.id).toBe(nextId);
		expect(dialog.displayName).toBe('Newer unsaved edits');
		expect(dialog.savingLoading).toBe(true);
		expect(closeSpy).not.toHaveBeenCalled();

		// Act: the current request still owns its loading state and failed draft.
		currentSave.next(generateFailedResultDTO({ statusCode: 409 }));
		currentSave.complete();
		await currentRequest.onComplete();

		// Assert
		expect(dialog.id).toBe(nextId);
		expect(dialog.displayName).toBe('Newer unsaved edits');
		expect(dialog.savingLoading).toBe(false);
		expect(closeSpy).not.toHaveBeenCalled();
	});
});
