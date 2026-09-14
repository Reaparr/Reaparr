import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { get } from '@vueuse/core';
import { tap, catchError, switchMap, finalize } from 'rxjs/operators';
import { type Observable, of } from 'rxjs';
import { DialogType } from '@enums';
import { plexAccountApi } from '@api';
import type { ErrorDTO, PlexAccountDTO } from '@dto';
import { StoreNames, type IAccountDialog } from '@interfaces';
import { useAccountStore, useDialogStore } from '@store';
import { cloneDeep } from 'lodash-es';

interface IAccountDialogStore extends PlexAccountDTO {
	isAuthTokenMode: boolean;
	isNewAccount: boolean;
	isInputValid: boolean;
	showPassword: boolean;
	showAuthToken: boolean;
	deleteLoading: boolean;
	validateLoading: boolean;
	savingLoading: boolean;
	hasValidationErrors: boolean;
	validationErrors: ErrorDTO[];
}

export const useAccountDialogStore = defineStore(StoreNames.AccountDialogStore, () => {
	const defaultState: IAccountDialogStore = {
		id: 0,
		isEnabled: true,
		isMain: true,
		username: '',
		password: '',
		displayName: '',
		clientId: '',
		verificationCode: '',
		uuid: '',
		hasPassword: false,
		validatedAt: '0001-01-01T00:00:00Z',
		is2Fa: false,
		title: '',
		plexId: 0,
		authenticationToken: '',
		customAuthenticationToken: '',
		email: '',
		plexServerAccess: [],
		plexLibraryAccess: [],
		// Dialog properties
		isAuthTokenMode: false,
		isNewAccount: false,
		isValidated: false,
		showAuthToken: false,
		showPassword: false,
		hasValidationErrors: false,
		deleteLoading: false,
		validateLoading: false,
		savingLoading: false,
		isInputValid: true,
		validationErrors: [],
	};

	const state = reactive<IAccountDialogStore>(cloneDeep(defaultState));
	const dialogStore = useDialogStore();
	const accountStore = useAccountStore();

	// Helper function to update state with validated account data
	const updateStateWithAccountData = (accountData: {
		clientId: string;
		username: string;
		email: string;
		title: string;
		plexId: number;
		uuid: string;
		authenticationToken?: string;
		customAuthenticationToken?: string;
		isValidated: boolean;
		validatedAt?: string | null;
		is2Fa: boolean;
	}) => {
		Object.assign(state, {
			clientId: accountData.clientId,
			username: accountData.username,
			email: accountData.email,
			title: accountData.title,
			plexId: accountData.plexId,
			uuid: accountData.uuid,
			authenticationToken: accountData.authenticationToken ?? state.authenticationToken,
			customAuthenticationToken: accountData.customAuthenticationToken,
			isValidated: accountData.isValidated,
			validatedAt: accountData.validatedAt,
			is2Fa: accountData.is2Fa,
		});
	};

	const actions = {
		openDialog({ accountId }: IAccountDialog): void {
			actions.$reset();
			state.isNewAccount = accountId === 0;
			if (!state.isNewAccount) {
				const account = accountStore.getAccount(accountId);
				if (account) {
					Object.assign(state, account);
					// Set auth token mode if the account was registered with a custom token
					state.isAuthTokenMode = !!account.customAuthenticationToken && account.customAuthenticationToken.length > 0;
				}
			}
		},
		closeDialog(): void {
			dialogStore.closeDialog(DialogType.AccountVerificationCodeDialog);
			dialogStore.closeDialog(DialogType.AccountConfirmationDialog);
			dialogStore.closeDialog(DialogType.AccountDialog);
			actions.$reset();
		},
		validatePlexToken() {
			state.validateLoading = true;

			return plexAccountApi.validatePlexTokenEndpoint({
				plexAccountId: state.id,
				displayName: state.displayName,
				manualAuthenticationToken: state.customAuthenticationToken,
			}).pipe(
				tap(({ value, isSuccess, errors }) => {
					// Always reset loading state
					state.validateLoading = false;

					if (!isSuccess || !value || value?.isUnAuthorized) {
						state.isValidated = false;
						state.hasValidationErrors = true;
						Log.error('Token validation failed', errors);
						dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
						return;
					}

					// Update state with validated token data
					updateStateWithAccountData(value);

					if (value.isValidated && !state.displayName && value.email) {
						state.displayName = value.email;
					}
					state.hasValidationErrors = false;

					// Account was validated successfully
					if (value.isValidated) {
						Log.info('Token validation successful');
						dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
						return;
					}
				}),
				catchError((error) => {
					// Reset loading state on error
					state.validateLoading = false;
					state.isValidated = false;
					state.hasValidationErrors = true;
					Log.error('Token validation failed', error);
					dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
					return of({ value: null, isSuccess: false });
				}),
			);
		},
		validatePlexAccount() {
			state.validateLoading = true;

			return plexAccountApi.validatePlexCredentialsEndpoint(get(getters.getAccountData)).pipe(
				tap(({ value, isSuccess }) => {
					// Always reset loading state
					state.validateLoading = false;

					if (!isSuccess || !value || value.isUnAuthorized) {
						state.isValidated = false;
						state.hasValidationErrors = true;

						// If 2FA is required, open the verification code dialog instead
						if (value?.is2Fa) {
							state.clientId = value.clientId;
							state.hasValidationErrors = false;
							Log.info('Account has 2FA enabled');
							dialogStore.openDialog(DialogType.AccountVerificationCodeDialog);
							return;
						}

						dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
						return;
					}

					// Update state with validated credentials data
					updateStateWithAccountData(value);
					state.hasValidationErrors = false;

					// Account has no 2FA and was valid
					if (value.isValidated && !value.is2Fa) {
						Log.info('Account has no 2FA and was valid');
						return;
					}

					// Account has no 2FA and was invalid
					if (!value.isValidated && !value.is2Fa) {
						Log.info('Account has no 2FA and was invalid');
						state.hasValidationErrors = true;
						dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
						return;
					}

					// Account has 2FA
					if (!value.isValidated && value.is2Fa) {
						Log.info('Account has 2FA enabled');
						dialogStore.openDialog(DialogType.AccountVerificationCodeDialog);
						return;
					}

					if (value.isValidated && value.is2Fa) {
						Log.info('Account was valid and has 2FA enabled, this makes no sense and sounds like a bug');
					}
				}),
				catchError((error) => {
					// Reset loading state on error
					state.validateLoading = false;
					state.isValidated = false;
					state.hasValidationErrors = true;
					Log.error('Credentials validation failed', error);
					dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
					return of({ value: null, isSuccess: false });
				}),
			);
		},
		validateVerificationCode() {
			return plexAccountApi.validatePlexCredentialsEndpoint(get(getters.getAccountData)).pipe(
				tap(({ value, isSuccess }) => {
					if (isSuccess && value) {
						if (value.isUnAuthorized) {
							Log.error('Invalid verification code, 2FA still required');
							state.hasValidationErrors = true;
							state.verificationCode = '';
							return;
						}
						dialogStore.closeDialog(DialogType.AccountVerificationCodeDialog);
						// Update state with validated credentials data
						updateStateWithAccountData(value);
						state.hasValidationErrors = false;
					} else {
						Log.error('Validate Error', value);
					}
					state.verificationCode = '';
				}),
			);
		},
		generateToken(verificationCode: string = '') {
			return plexAccountApi.generatePlexTokenEndpoint(state.id, { verificationCode });
		},
		saveAccount(): Observable<void> {
			state.savingLoading = true;
			if (state.isNewAccount) {
				const accountData = get(getters.getAccountData);
				return accountStore.createPlexAccount({
					customAuthenticationToken: accountData.customAuthenticationToken ?? '',
					authenticationToken: accountData.authenticationToken ?? '',
					clientId: accountData.clientId,
					displayName: accountData.displayName,
					email: accountData.email,
					is2Fa: accountData.is2Fa,
					isEnabled: accountData.isEnabled,
					isMain: accountData.isMain,
					isValidated: accountData.isValidated,
					password: accountData.password,
					plexId: accountData.plexId,
					title: accountData.title,
					username: accountData.username,
					uuid: accountData.uuid,
					validatedAt: accountData.validatedAt!,
				}).pipe(
					tap(() => dialogStore.closeDialog(DialogType.AccountDialog)),
					finalize(() => state.savingLoading = false),
					switchMap(() => of(void 0)),
				);
			}
			return accountStore.updatePlexAccount(get(getters.getAccountData)).pipe(
				tap(() => dialogStore.closeDialog(DialogType.AccountDialog)),
				finalize(() => state.savingLoading = false),
				switchMap(() => of(void 0)),
			);
		},
		switchInputMode(isAuthTokenMode: boolean) {
			state.isAuthTokenMode = isAuthTokenMode;
			// Clear input fields
			state.username = '';
			state.password = '';
			state.authenticationToken = '';
			state.isValidated = false;
		},
		deleteAccount() {
			state.deleteLoading = true;
			return accountStore
				.deleteAccount(state.id)
				.pipe(
					tap((res) => {
						if (res.isSuccess) {
							dialogStore.closeDialog(DialogType.AccountDialog);
						}
					}),
					finalize(() => state.deleteLoading = false),
				);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};
	const getters = {
		hasCredentialsChanged: computed(() => {
			if (!state.isNewAccount) {
				const originalPlexAccount = accountStore.getAccount(state.id);
				if (!originalPlexAccount) {
					return false;
				}

				return originalPlexAccount.username !== state.username || originalPlexAccount.password !== state.password;
			}
			return false;
		}),
		isAllowedToSave: computed(() => {
			if (state.isNewAccount) {
				return state.displayName !== '' && state.isValidated;
			}
			return true;
		}),
		getAccountData: computed((): PlexAccountDTO => {
			return {
				id: state.id,
				isValidated: state.isValidated,
				password: state.password,
				username: state.username,
				uuid: state.uuid,
				validatedAt: state.validatedAt,
				verificationCode: state.verificationCode,
				customAuthenticationToken: state.customAuthenticationToken,
				authenticationToken: state.authenticationToken,
				clientId: state.clientId,
				displayName: state.displayName,
				email: state.email,
				hasPassword: state.hasPassword,
				is2Fa: state.is2Fa,
				isEnabled: state.isEnabled,
				isMain: state.isMain,
				plexId: state.plexId,
				plexLibraryAccess: state.plexLibraryAccess,
				plexServerAccess: state.plexServerAccess,
				title: state.title,
			};
		}),
	};
	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useAccountDialogStore, import.meta.hot));
}
