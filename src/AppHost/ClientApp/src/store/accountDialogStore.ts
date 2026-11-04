import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs, shallowRef, watch } from 'vue';
import { get, set } from '@vueuse/core';
import { tap, catchError, finalize, filter } from 'rxjs/operators';
import { type Observable, of, EMPTY, throwError } from 'rxjs';
import { DialogType } from '@enums';
import { plexAccountApi } from '@api';
import type { ErrorDTO, PlexAccountDTO } from '@dto';
import { StoreNames, type IAccountDialog, type ResultDTO } from '@interfaces';
import { useAccountStore, useDialogStore } from '@store';
import { cloneDeep, isEqual } from 'lodash-es';

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

type AuthenticationInput = Pick<PlexAccountDTO, 'id' | 'username' | 'password' | 'customAuthenticationToken'> & {
	isAuthTokenMode: boolean;
};

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
	const originalAuthentication = shallowRef<{ input: AuthenticationInput; isValidated: boolean } | null>(null);
	const validatedAuthentication = shallowRef<AuthenticationInput | null>(null);
	let validationSequence = 0;
	let dialogSession = 0;
	let applyingAccountState = false;
	const dialogStore = useDialogStore();
	const accountStore = useAccountStore();

	const actions = {
		openDialog({ accountId }: IAccountDialog): void {
			actions.$reset();
			state.isNewAccount = accountId === 0;
			if (!state.isNewAccount) {
				const account = accountStore.getAccount(accountId);
				if (account) {
					applyAccountState({
						...account,
						isAuthTokenMode: !!account.customAuthenticationToken && account.customAuthenticationToken.length > 0,
					});
					set(originalAuthentication, { input: getAuthenticationInput(), isValidated: account.isValidated });
				}
			}
			rememberCurrentValidation();
		},
		closeDialog(): void {
			dialogStore.closeDialog(DialogType.AccountVerificationCodeDialog);
			dialogStore.closeDialog(DialogType.AccountConfirmationDialog);
			dialogStore.closeDialog(DialogType.AccountDialog);
			actions.$reset();
		},
		validatePlexToken() {
			return ownValidation(plexAccountApi.validatePlexTokenEndpoint({
				plexAccountId: state.id,
				displayName: state.displayName,
				manualAuthenticationToken: state.customAuthenticationToken,
			})).pipe(
				tap(({ value, isSuccess, errors }) => {
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
					state.isValidated = false;
					state.hasValidationErrors = true;
					Log.error('Token validation failed', error);
					dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
					return of({ value: null, isSuccess: false });
				}),
			);
		},
		validatePlexAccount() {
			return ownValidation(plexAccountApi.validatePlexCredentialsEndpoint(get(getters.getAccountData))).pipe(
				tap(({ value, isSuccess }) => {
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
					state.isValidated = false;
					state.hasValidationErrors = true;
					Log.error('Credentials validation failed', error);
					dialogStore.openDialog(DialogType.AccountTokenValidateDialog);
					return of({ value: null, isSuccess: false });
				}),
			);
		},
		validateVerificationCode() {
			return ownValidation(plexAccountApi.validatePlexCredentialsEndpoint(get(getters.getAccountData))).pipe(
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
						state.hasValidationErrors = true;
						Log.error('Validate Error', value);
					}
					state.verificationCode = '';
				}),
			);
		},
		generateToken(verificationCode: string = '') {
			return plexAccountApi.generatePlexTokenEndpoint(state.id, { verificationCode });
		},
		saveAccount(): Observable<ResultDTO<PlexAccountDTO>> {
			if (!get(getters.isAllowedToSave)) {
				return of({
					isSuccess: false,
					statusCode: 400,
					errors: [{
						message: 'Validate the current authentication inputs before saving.',
						reasons: [],
						metadata: {},
					}],
					successes: [],
				});
			}
			const session = dialogSession;
			const accountId = state.id;
			state.savingLoading = true;
			const accountData = get(getters.getAccountData);
			const saveRequest = state.isNewAccount
				? accountStore.createPlexAccount({
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
					})
				: accountStore.updatePlexAccount(accountData);
			return saveRequest.pipe(
				tap((result) => {
					if (result.isSuccess && ownsDialog(session, accountId)) {
						dialogStore.closeDialog(DialogType.AccountDialog);
					}
				}),
				finalize(() => {
					if (ownsDialog(session, accountId)) state.savingLoading = false;
				}),
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
			const session = dialogSession;
			const accountId = state.id;
			state.deleteLoading = true;
			return accountStore
				.deleteAccount(accountId)
				.pipe(
					tap((res) => {
						if (res.isSuccess && ownsDialog(session, accountId)) {
							dialogStore.closeDialog(DialogType.AccountDialog);
						}
					}),
					finalize(() => {
						if (ownsDialog(session, accountId)) state.deleteLoading = false;
					}),
				);
		},
		$reset() {
			validationSequence++;
			dialogSession++;
			applyAccountState(cloneDeep(defaultState));
			set(originalAuthentication, null);
			set(validatedAuthentication, null);
		},
	};
	function ownsDialog(session: number, accountId: number): boolean {
		return session === dialogSession && accountId === state.id;
	}

	// Helper function to update state with validated account data
	function updateStateWithAccountData(accountData: {
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
	}) {
		applyAccountState({
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
		rememberCurrentValidation();
	}

	function getAuthenticationInput(): AuthenticationInput {
		return {
			id: state.id,
			isAuthTokenMode: state.isAuthTokenMode,
			username: state.isAuthTokenMode ? '' : state.username,
			password: state.isAuthTokenMode ? '' : state.password,
			customAuthenticationToken: state.isAuthTokenMode ? state.customAuthenticationToken ?? '' : '',
		};
	}

	function applyAccountState(patch: Partial<IAccountDialogStore>) {
		applyingAccountState = true;
		try {
			Object.assign(state, patch);
		} finally {
			applyingAccountState = false;
		}
	}

	function rememberCurrentValidation() {
		set(validatedAuthentication, state.isValidated ? getAuthenticationInput() : null);
	}

	function hasCurrentValidation() {
		const validated = get(validatedAuthentication);
		return state.isValidated && validated !== null && isEqual(validated, getAuthenticationInput());
	}

	function ownValidation<T>(request: Observable<T>): Observable<T> {
		const sequence = ++validationSequence;
		set(validatedAuthentication, null);
		state.validateLoading = true;
		return request.pipe(
			filter(() => sequence === validationSequence),
			catchError((error) => sequence === validationSequence ? throwError(() => error) : EMPTY),
			finalize(() => {
				if (sequence === validationSequence) {
					state.validateLoading = false;
				}
			}),
		);
	}

	watch(getAuthenticationInput, () => {
		if (applyingAccountState) {
			return;
		}
		validationSequence++;
		set(validatedAuthentication, null);
		state.validateLoading = false;
		state.isValidated = false;
		state.validatedAt = null;
		state.authenticationToken = '';
		state.verificationCode = '';
	}, { flush: 'sync' });

	const getters = {
		hasCredentialsChanged: computed(() => {
			const original = get(originalAuthentication);
			return !state.isNewAccount && original !== null && !isEqual(original.input, getAuthenticationInput());
		}),
		isAllowedToSave: computed(() => {
			if (state.validateLoading) {
				return false;
			}
			if (state.isNewAccount) {
				return state.displayName !== '' && hasCurrentValidation();
			}
			const original = get(originalAuthentication);
			return original !== null && (hasCurrentValidation()
				|| (!original.isValidated && !get(getters.hasCredentialsChanged)));
		}),
		getAccountData: computed((): PlexAccountDTO => {
			return {
				id: state.id,
				isValidated: hasCurrentValidation(),
				password: state.isAuthTokenMode ? '' : state.password,
				username: state.username,
				uuid: state.uuid,
				validatedAt: state.validatedAt,
				verificationCode: state.verificationCode,
				customAuthenticationToken: state.isAuthTokenMode ? state.customAuthenticationToken : '',
				authenticationToken: state.isAuthTokenMode ? '' : state.authenticationToken,
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
