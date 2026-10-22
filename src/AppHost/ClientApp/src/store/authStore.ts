import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { StoreNames, type ISetupResult } from '@interfaces';
import type { Observable } from 'rxjs';
import { authenticationApi } from '@api';
import { catchError, from, of } from 'rxjs';
import { map, tap, switchMap } from 'rxjs/operators';
import { useGlobalStore } from '@store';
import { get } from '@vueuse/core';
import { cloneDeep } from 'lodash-es';

interface IAuthenticationStoreState {
	isLoggedIn: boolean;
	isDefaultCredentials: boolean;
	currentUsername: string;
	username: string;
	currentPassword: string;
	password: string;
	isPasswordValid: boolean;
	confirmPassword: string;
}

export const useAuthenticationStore = defineStore(StoreNames.AuthenticationStore, () => {
	const defaultState = {
		isLoggedIn: false,
		isDefaultCredentials: true,
		currentUsername: '',
		username: '',
		currentPassword: '',
		password: '',
		confirmPassword: '',
		isPasswordValid: false,
	};

	const state = reactive<IAuthenticationStoreState>(cloneDeep(defaultState));

	const globalStore = useGlobalStore();

	const actions = {
		setup(): Observable<ISetupResult> {
			return actions.status().pipe(map((statusResult) => ({
				name: StoreNames.AuthenticationStore,
				isSuccess: !!statusResult?.isSuccess,
			})));
		},
		refreshCredentials() {
			return authenticationApi.getAppCredentials().pipe(
				tap((res) => {
					if (res.isSuccess && res.value) {
						state.currentUsername = res.value.userName;
						state.username = res.value.userName;
						state.currentPassword = res.value.password;
						state.password = res.value.password;
						state.confirmPassword = '';
						state.isDefaultCredentials = res.value.isDefaultCredentials;
					}
				}));
		},
		updateCredentials() {
			const usernameChanged = get(getters.hasUsernameChanged);
			const passwordChanged = get(getters.hasPasswordChanged);

			if (!usernameChanged && !passwordChanged) {
				return of(null);
			}
			return authenticationApi.updateCredentialsEndpoint({
				username: usernameChanged ? state.username : null,
				password: passwordChanged ? state.password : null,
			}).pipe(
				switchMap((result) => {
					if (!result.isSuccess) return of(result);
					Log.info('User credentials updated', result);
					return actions.refreshCredentials();
				}),
				catchError((err) => {
					Log.error('User credentials update failed', err);
					return of(err);
				}));
		},
		login(username: string, password: string, rememberMe: boolean): Observable<number> {
			const data = new FormData();
			data.append('username', username);
			data.append('password', password);
			data.append('rememberMe', rememberMe + '');

			// @ts-expect-error - FormData is not assignable to type 'AppUserLoginEndpointRequest'
			return authenticationApi.appUserLoginEndpoint(data).pipe(switchMap((res) => {
				if (res.isSuccess) {
					Log.info('User logged in');
					state.isLoggedIn = true;
					return globalStore.setup().pipe(switchMap(() => of(res.statusCode)));
				}
				Log.error('User login failed');
				state.isLoggedIn = false;
				return of(res.statusCode);
			}));
		},
		logout() {
			const router = useRouter();

			return authenticationApi.appUserLogOutEndpoint().pipe(
				switchMap((result) => {
					if (!result.isSuccess) return of(result);
					Log.info('User logged out', result);
					return from(globalStore.resetSession()).pipe(
						map(() => result),
						tap(() => state.isLoggedIn = false),
						tap(() => router.push('/login')),
					);
				}),
			);
		},
		status: () => authenticationApi.authenticationStatusEndpoint({ timeout: 5000 }).pipe(
			tap((res) => state.isLoggedIn = res.isSuccess && !!res.value?.isLoggedIn),
			catchError((err) => {
				state.isLoggedIn = false;
				return of(err);
			})),
		$reset: () => {
			Object.assign(state, cloneDeep(defaultState));
		},
	};
	const getters = {
		hasUsernameChanged: computed(() => state.currentUsername !== state.username),
		hasPasswordChanged: computed(() => state.currentPassword !== state.password),
		equalPassword: computed(() => get(getters.hasPasswordChanged) && state.password === state.confirmPassword),
		canUpdateCredentials: computed(() => {
			const usernameValid = !!state.username && state.username.length >= 8;
			const hasChanges = get(getters.hasUsernameChanged) || get(getters.hasPasswordChanged);

			if (!usernameValid || !hasChanges) {
				return false;
			}

			if (get(getters.hasPasswordChanged)) {
				return state.isPasswordValid && state.password === state.confirmPassword;
			}

			return true;
		}),
	};
	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useAuthenticationStore, import.meta.hot));
}
