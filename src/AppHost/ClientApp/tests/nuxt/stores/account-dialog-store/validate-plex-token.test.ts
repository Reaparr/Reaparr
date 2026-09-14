import { describe, beforeAll, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { PlexAccountPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { useAccountDialogStore, useDialogStore } from '@store';

describe('AccountDialogStore.validatePlexToken()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should open token validate dialog and update state on successful token validation', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const openDialogSpy = vi.spyOn(dialogStore, 'openDialog');
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.displayName = 'Test User';
		accountDialogStore.authenticationToken = 'existing-authentication-token';
		accountDialogStore.customAuthenticationToken = 'valid-token';

		mock.onPost(PlexAccountPaths.validatePlexTokenEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			clientId: 'client-id',
			customAuthenticationToken: 'valid-token',
			email: 'user@test.dev',
			isUnAuthorized: false,
			isValidated: true,
			plexId: 42,
			title: 'User Title',
			username: 'plex-user',
			uuid: 'account-uuid',
			validatedAt: '2026-04-03T00:00:00Z',
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validatePlexToken());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(true);
		expect(accountDialogStore.hasValidationErrors).toEqual(false);
		expect(accountDialogStore.validateLoading).toEqual(false);
		expect(accountDialogStore.plexId).toEqual(42);
		expect(accountDialogStore.title).toEqual('User Title');
		expect(accountDialogStore.email).toEqual('user@test.dev');
		expect(accountDialogStore.authenticationToken).toEqual('existing-authentication-token');
		expect(accountDialogStore.getAccountData.authenticationToken).toEqual('existing-authentication-token');
		expect(mock.history.post[0]?.data).toContain('"plexAccountId":0');
		expect(openDialogSpy).toHaveBeenCalledWith('account-token-validate-dialog');
	});

	test('Should set displayName from email when token validates but displayName is empty', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const openDialogSpy = vi.spyOn(dialogStore, 'openDialog');
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.displayName = '';
		accountDialogStore.customAuthenticationToken = 'valid-token';

		mock.onPost(PlexAccountPaths.validatePlexTokenEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			clientId: 'client-id',
			customAuthenticationToken: 'valid-token',
			email: 'fallback@test.dev',
			isUnAuthorized: false,
			isValidated: true,
			plexId: 7,
			title: '',
			username: 'plex-user',
			uuid: 'uuid',
			validatedAt: '2026-04-03T00:00:00Z',
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validatePlexToken());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(true);
		expect(accountDialogStore.hasValidationErrors).toEqual(false);
		expect(accountDialogStore.displayName).toEqual('fallback@test.dev');
		expect(openDialogSpy).toHaveBeenCalledWith('account-token-validate-dialog');
	});

	test('Should open token validate dialog and set error state when token is unauthorized', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const openDialogSpy = vi.spyOn(dialogStore, 'openDialog');
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.displayName = 'Test User';
		accountDialogStore.customAuthenticationToken = 'bad-token';

		mock.onPost(PlexAccountPaths.validatePlexTokenEndpoint()).reply(200, generateResultDTO({
			is2Fa: false,
			clientId: '',
			customAuthenticationToken: '',
			email: '',
			isUnAuthorized: true,
			isValidated: false,
			plexId: 0,
			title: '',
			username: '',
			uuid: '',
			validatedAt: null,
		}));

		// Act
		const result = subscribeSpyTo(accountDialogStore.validatePlexToken());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(false);
		expect(accountDialogStore.hasValidationErrors).toEqual(true);
		expect(accountDialogStore.validateLoading).toEqual(false);
		expect(openDialogSpy).toHaveBeenCalledWith('account-token-validate-dialog');
	});

	test('Should open token validate dialog when API returns unsuccessful result', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const openDialogSpy = vi.spyOn(dialogStore, 'openDialog');
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.displayName = 'Test User';
		accountDialogStore.customAuthenticationToken = 'token';

		mock.onPost(PlexAccountPaths.validatePlexTokenEndpoint()).reply(200, generateFailedResultDTO());

		// Act
		const result = subscribeSpyTo(accountDialogStore.validatePlexToken());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(false);
		expect(accountDialogStore.hasValidationErrors).toEqual(true);
		expect(accountDialogStore.validateLoading).toEqual(false);
		expect(openDialogSpy).toHaveBeenCalledWith('account-token-validate-dialog');
	});

	test('Should open token validate dialog and show validation errors on network error', async () => {
		// Arrange
		const dialogStore = useDialogStore();
		const openDialogSpy = vi.spyOn(dialogStore, 'openDialog');
		const accountDialogStore = useAccountDialogStore();
		accountDialogStore.displayName = 'Test User';
		accountDialogStore.customAuthenticationToken = 'token';

		mock.onPost(PlexAccountPaths.validatePlexTokenEndpoint()).reply(500);

		// Act
		const result = subscribeSpyTo(accountDialogStore.validatePlexToken());
		await result.onComplete();

		// Assert
		expect(accountDialogStore.isValidated).toEqual(false);
		expect(accountDialogStore.hasValidationErrors).toEqual(true);
		expect(accountDialogStore.validateLoading).toEqual(false);
		expect(openDialogSpy).toHaveBeenCalledWith('account-token-validate-dialog');
	});
});
