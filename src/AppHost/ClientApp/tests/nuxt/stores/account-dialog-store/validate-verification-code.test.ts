import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { useAccountDialogStore, useDialogStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

describe('AccountDialogStore.validateVerificationCode()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	function openNewCredentialAccount() {
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.openDialog({ accountId: 0 });
		return accountDialogStore;
	}

	test('Should close verification dialog, update state, and reset validation errors on success', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		const accountDialogStore = openNewCredentialAccount();
		accountDialogStore.username = 'user';
		accountDialogStore.password = 'pass';
		accountDialogStore.clientId = 'client-id';
		accountDialogStore.verificationCode = '123456';
		accountDialogStore.hasValidationErrors = true;

		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: true,
			authenticationToken: 'valid-token',
			clientId: 'client-id',
			email: 'user@test.dev',
			isUnAuthorized: false,
			isValidated: true,
			password: 'pass',
			plexId: 99,
			title: 'User Title',
			username: 'user',
			uuid: 'account-uuid',
			validatedAt: '2026-04-03T00:00:00Z',
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validateVerificationCode());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(true);
		expect(accountDialogStore.hasValidationErrors).toEqual(false);
		expect(accountDialogStore.validateLoading).toEqual(false);
		expect(accountDialogStore.email).toEqual('user@test.dev');
		expect(accountDialogStore.authenticationToken).toEqual('valid-token');
		expect(closeDialogSpy).toHaveBeenCalledWith('account-verification-code-dialog');
		expect(capture).toHaveBeenCalledTimes(1);
		expect(capture).toHaveBeenCalledWith(FrontendTelemetryEvent.FrontendAccountInteraction, {
			action: FrontendTelemetryAction.ValidateCredentials,
			account_id: 0,
			verification_code_required: true,
		});
		expect(JSON.stringify(capture.mock.calls)).not.toContain('123456');
	});

	test('Should keep verification dialog open and not update state when verification code is rejected', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		const accountDialogStore = openNewCredentialAccount();
		accountDialogStore.username = 'user';
		accountDialogStore.password = 'pass';
		accountDialogStore.clientId = 'client-id';
		accountDialogStore.verificationCode = 'wrong-code';
		accountDialogStore.hasValidationErrors = true;
		accountDialogStore.isValidated = false;

		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: true,
			authenticationToken: '',
			clientId: 'client-id',
			email: '',
			isUnAuthorized: true,
			isValidated: false,
			password: 'pass',
			plexId: 0,
			title: '',
			username: 'user',
			uuid: '',
			validatedAt: null,
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validateVerificationCode());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(false);
		expect(accountDialogStore.hasValidationErrors).toEqual(true);
		expect(closeDialogSpy).not.toHaveBeenCalled();
	});

	test('Should expose a failed verification after the initial challenge and accept a corrected retry', async () => {
		// Arrange
		const close = vi.spyOn(useDialogStore(), 'closeDialog');
		const store = openNewCredentialAccount();
		store.displayName = 'Retry account';
		store.username = 'user';
		store.password = 'pass';
		const challenge = {
			is2Fa: true, authenticationToken: '', clientId: 'client-id', email: 'user@test.dev',
			isUnAuthorized: true, isValidated: false, password: 'pass', plexId: 99,
			title: 'User Title', username: 'user', uuid: 'account-uuid', validatedAt: null,
		};
		const path = PlexAccountPaths.validatePlexCredentialsEndpoint();
		mock.onPost(path).replyOnce(200, generateResultDTO(challenge))
			.onPost(path).replyOnce(500, generateFailedResultDTO({ statusCode: 500 }))
			.onPost(path).replyOnce(200, generateResultDTO({
				...challenge, isUnAuthorized: false, isValidated: true,
				authenticationToken: 'valid-token', validatedAt: '2026-04-03T00:00:00Z',
			}));

		// Act
		await subscribeSpyTo(store.validatePlexAccount()).onComplete();

		// Assert
		expect(store.hasValidationErrors).toBe(false);
		expect(store.clientId).toBe('client-id');
		expect(store.isAllowedToSave).toBe(false);

		// Act
		store.verificationCode = '111111';
		const failure = subscribeSpyTo(store.validateVerificationCode());
		await failure.onComplete();

		// Assert
		expect(failure.getFirstValue()?.isSuccess).toBe(false);
		expect(store.hasValidationErrors).toBe(true);
		expect(store.validateLoading).toBe(false);
		expect(store.verificationCode).toBe('');
		expect([store.username, store.password, store.clientId]).toEqual(['user', 'pass', 'client-id']);
		expect(store.isAllowedToSave).toBe(false);
		expect(close).not.toHaveBeenCalled();

		// Act
		store.verificationCode = '123456';
		await subscribeSpyTo(store.validateVerificationCode()).onComplete();

		// Assert
		expect(store.hasValidationErrors).toBe(false);
		expect(store.validateLoading).toBe(false);
		expect(store.verificationCode).toBe('');
		expect(store.isValidated).toBe(true);
		expect(store.authenticationToken).toBe('valid-token');
		expect(store.isAllowedToSave).toBe(true);
		expect([store.username, store.password, store.clientId]).toEqual(['user', 'pass', 'client-id']);
		expect(close).toHaveBeenCalledTimes(1);
		expect(close).toHaveBeenCalledWith('account-verification-code-dialog');
		expect(mock.history.post.map((request) => JSON.parse(request.data).verificationCode)).toEqual(['', '111111', '123456']);
	});

	test('Should preserve account credentials in state after successful verification code submission', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		const accountDialogStore = openNewCredentialAccount();
		accountDialogStore.username = 'preserved-user';
		accountDialogStore.password = 'preserved-pass';
		accountDialogStore.clientId = 'preserved-client-id';
		accountDialogStore.verificationCode = '654321';
		accountDialogStore.hasValidationErrors = true;

		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			authenticationToken: 'new-token',
			clientId: 'preserved-client-id',
			email: 'preserved@test.dev',
			isUnAuthorized: false,
			isValidated: true,
			password: 'preserved-pass',
			plexId: 77,
			title: 'Preserved',
			username: 'preserved-user',
			uuid: 'preserved-uuid',
			validatedAt: '2026-04-03T00:00:00Z',
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validateVerificationCode());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(true);
		expect(accountDialogStore.hasValidationErrors).toEqual(false);
		expect(accountDialogStore.username).toEqual('preserved-user');
		expect(accountDialogStore.password).toEqual('preserved-pass');
		expect(accountDialogStore.plexId).toEqual(77);
		expect(accountDialogStore.uuid).toEqual('preserved-uuid');
		expect(accountDialogStore.title).toEqual('Preserved');
		expect(closeDialogSpy).toHaveBeenCalledWith('account-verification-code-dialog');
	});

	test('Should clear verification code after successful submission', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		const accountDialogStore = openNewCredentialAccount();
		accountDialogStore.username = 'user';
		accountDialogStore.password = 'pass';
		accountDialogStore.clientId = 'client-id';
		accountDialogStore.verificationCode = '123456';
		accountDialogStore.hasValidationErrors = true;

		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			authenticationToken: 'token',
			clientId: 'client-id',
			email: 'user@test.dev',
			isUnAuthorized: false,
			isValidated: true,
			password: 'pass',
			plexId: 1,
			title: 'Title',
			username: 'user',
			uuid: 'uuid',
			validatedAt: '2026-04-03T00:00:00Z',
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validateVerificationCode());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.verificationCode).toEqual('');
		expect(closeDialogSpy).toHaveBeenCalledWith('account-verification-code-dialog');
	});

	test('Should clear verification code after rejected submission', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const closeDialogSpy = vi.spyOn(dialogStore, 'closeDialog');
		const accountDialogStore = openNewCredentialAccount();
		accountDialogStore.username = 'user';
		accountDialogStore.password = 'pass';
		accountDialogStore.clientId = 'client-id';
		accountDialogStore.verificationCode = 'wrong-code';
		accountDialogStore.hasValidationErrors = true;

		mock.onPost(PlexAccountPaths.validatePlexCredentialsEndpoint()).reply(200, generateResultDTO({
			is2Fa: true,
			authenticationToken: '',
			clientId: 'client-id',
			email: '',
			isUnAuthorized: true,
			isValidated: false,
			password: 'pass',
			plexId: 0,
			title: '',
			username: 'user',
			uuid: '',
			validatedAt: null,
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validateVerificationCode());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.verificationCode).toEqual('');
		expect(accountDialogStore.isValidated).toEqual(false);
		expect(closeDialogSpy).not.toHaveBeenCalled();
	});
});
