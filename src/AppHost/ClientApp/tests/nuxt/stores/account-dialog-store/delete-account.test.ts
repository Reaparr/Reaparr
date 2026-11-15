import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { of, Subject } from 'rxjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateFailedResultDTO, generatePlexAccount, generateResultDTO } from '@mock';
import type { BaseResultDTO } from '@dto';
import { useAccountDialogStore, useAccountStore, useDialogStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AccountDialogStore.deleteAccount()', () => {
	let { mock: _mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		_mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	test('Should stop delete loading after delete request completes', async () => {
		// Arrange
		const accountStore = useAccountStore();
		const dialogStore = useDialogStore();
		const deleteSpy = vi
			.spyOn(accountStore, 'deleteAccount')
			.mockReturnValue(of(generateResultDTO(true)));
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');

		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.id = 9;

		// Act
		const result = subscribeSpyTo(accountDialogStore.deleteAccount());
		await result.onComplete();

		// Assert
		expect(deleteSpy).toHaveBeenCalledWith(9);
		expect(closeDialogSpy).toHaveBeenCalledWith('account-dialog');
		expect(accountDialogStore.deleteLoading).toEqual(false);
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendAccountInteraction, {
			action: FrontendTelemetryAction.Delete,
			account_id: 9,
		});
	});

	test('Should not close dialog when delete request fails', async () => {
		// Arrange
		const accountStore = useAccountStore();
		const dialogStore = useDialogStore();
		const failedDeleteResult: BaseResultDTO = generateFailedResultDTO({ statusCode: 500 });
		vi.spyOn(accountStore, 'deleteAccount').mockReturnValue(of(failedDeleteResult));
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');

		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.id = 10;

		// Act
		const result = subscribeSpyTo(accountDialogStore.deleteAccount());
		await result.onComplete();

		// Assert
		expect(closeDialogSpy).not.toHaveBeenCalledWith('account-dialog');
		expect(accountDialogStore.deleteLoading).toEqual(false);
	});

	test.each([9, 10])('Should preserve the newer dialog and spinner when account %i is opened before an old delete finishes', async (nextId) => {
		// Arrange
		const accounts = useAccountStore();
		accounts.accounts = [generatePlexAccount({ id: 9 }), generatePlexAccount({ id: 10 })];
		const dialog = useAccountDialogStore();
		const dialogs = useDialogStore();
		const oldDelete = new Subject<BaseResultDTO>();
		const currentDelete = new Subject<BaseResultDTO>();
		const deleteSpy = vi.spyOn(accounts, 'deleteAccount')
			.mockReturnValueOnce(oldDelete).mockReturnValueOnce(currentDelete);
		const closeSpy = vi.spyOn(dialogs, 'closeDialog');
		dialog.openDialog({ accountId: 9 });
		const oldRequest = subscribeSpyTo(dialog.deleteAccount());
		dialog.closeDialog();
		dialog.openDialog({ accountId: nextId });
		const currentRequest = subscribeSpyTo(dialog.deleteAccount());
		dialog.username = 'newer-user-edits';
		closeSpy.mockClear();

		// Act
		oldDelete.next(generateResultDTO(true));
		oldDelete.complete();
		await oldRequest.onComplete();

		// Assert
		expect(dialog.id).toBe(nextId);
		expect(dialog.username).toBe('newer-user-edits');
		expect(dialog.deleteLoading).toBe(true);
		expect(closeSpy).not.toHaveBeenCalled();
		expect(deleteSpy).toHaveBeenNthCalledWith(1, 9);
		expect(deleteSpy).toHaveBeenNthCalledWith(2, nextId);

		// Act
		currentDelete.next(generateFailedResultDTO({ statusCode: 500 }));
		currentDelete.complete();
		await currentRequest.onComplete();

		// Assert
		expect(dialog.deleteLoading).toBe(false);
		expect(dialog.id).toBe(nextId);
		expect(dialog.username).toBe('newer-user-edits');
		expect(closeSpy).not.toHaveBeenCalled();
	});
});
