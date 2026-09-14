import { reactive, computed, toRefs } from 'vue';
import { acceptHMRUpdate, defineStore } from 'pinia';
import type { Observable } from 'rxjs';
import { forkJoin, of } from 'rxjs';
import { concatMap, finalize, map, switchMap, tap } from 'rxjs/operators';
import type { CreatePlexAccountEndpointRequest, PlexAccountDTO } from '@dto';
import { RefreshDataType } from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { plexAccountApi, plexServerApi } from '@api';
import { useLibraryStore, useServerStore, useSettingsStore, useSignalrStore } from '@store';
import { cloneDeep } from 'lodash-es';

interface IAccountStoreState {
	accounts: PlexAccountDTO[];
	accessSyncLoading: boolean;
	accessSyncLoadingAccountId: number | null;
	refreshingServerIds: Set<number>;
}

export const useAccountStore = defineStore(StoreNames.AccountStore, () => {
	const defaultState = {
		accessSyncLoading: false,
		accessSyncLoadingAccountId: null,
		refreshingServerIds: new Set<number>(),
		accounts: [],
	};

	const state = reactive<IAccountStoreState>(cloneDeep(defaultState));

	const settingsStore = useSettingsStore();
	const serverStore = useServerStore();
	const libraryStore = useLibraryStore();
	const signalRStore = useSignalrStore();

	const actions = {
		setup(): Observable<ISetupResult> {
			// Listen for refresh notifications
			signalRStore
				.getRefreshNotification(RefreshDataType.PlexAccount)
				.pipe(concatMap(() => actions.refreshAccounts()))
				.subscribe();

			return actions.refreshAccounts().pipe(
				map((result) => ({ name: StoreNames.AccountStore, isSuccess: result.isSuccess })),
			);
		},
		refreshAccounts() {
			return plexAccountApi.getAllPlexAccountsEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess && result.value) {
						state.accounts = result.value;
					}
				}),
			);
		},
		reSyncServer(serverId: number) {
			state.refreshingServerIds.add(serverId);
			return plexServerApi.refreshPlexServerAccountsAccessEndpoint(serverId).pipe(
				switchMap((result) => {
					if (!result.isSuccess) {
						return of(result);
					}

					return forkJoin([
						actions.refreshAccounts(),
						serverStore.refreshPlexServers(),
						libraryStore.refreshLibraries(),
					]).pipe(switchMap(() => of(result)));
				}),
				finalize(() => state.refreshingServerIds.delete(serverId)),
			);
		},
		reSyncAccount(accountId: number) {
			state.accessSyncLoading = true;
			state.accessSyncLoadingAccountId = accountId;
			return plexAccountApi.refreshPlexAccountAccessEndpoint(accountId).pipe(
				switchMap((result) => {
					if (!result.isSuccess) {
						return of(result);
					}

					return forkJoin([
						actions.refreshAccounts(),
						serverStore.refreshPlexServers(),
						libraryStore.refreshLibraries(),
					]).pipe(switchMap(() => of(result)));
				}),
				finalize(() => {
					if (state.accessSyncLoadingAccountId === accountId) {
						state.accessSyncLoading = false;
						state.accessSyncLoadingAccountId = null;
					}
				}),
			);
		},
		/**
     * Creates a PlexAccount in the database, returns the new accountId and then also refreshes all the Plex Servers that are accessible
     * @param {PlexAccountDTO} account
     */
		createPlexAccount(account: CreatePlexAccountEndpointRequest): Observable<PlexAccountDTO> {
			return plexAccountApi.createPlexAccountEndpoint(account).pipe(
				switchMap((result) =>
					forkJoin([actions.refreshAccounts(), serverStore.refreshPlexServers(), libraryStore.refreshLibraries()]).pipe(
						switchMap(() => of(result.value as PlexAccountDTO)),
					),
				),
			);
		},
		updatePlexAccount(account: PlexAccountDTO) {
			return plexAccountApi
				.updatePlexAccountByIdEndpoint(account)
				.pipe(
					switchMap((result) => {
						if (!result.isSuccess) {
							return of(result);
						}

						return forkJoin([actions.refreshAccounts(), serverStore.refreshPlexServers(), libraryStore.refreshLibraries()]).pipe(
							switchMap(() => of(actions.getAccount(account.id))),
						);
					}),
				);
		},
		deleteAccount(accountId: number) {
			return plexAccountApi
				.deletePlexAccountByIdEndpoint(accountId)
				.pipe(switchMap((result) => result.isSuccess ? actions.refreshAccounts() : of(result)));
		},
		getAccount(id: number): PlexAccountDTO | undefined {
			return state.accounts.find((x) => x.id === id);
		},
		getAccountDisplayName(id: number): string {
			if (settingsStore.shouldMaskAccountNames) {
				return '**MASKED**';
			}

			const account = actions.getAccount(id);
			if (!account) {
				return '**UNKNOWN**';
			}

			const customName = account.displayName ?? '';
			if (customName !== '') {
				return customName;
			}

			return account.email;
		},
		getAccountUserName(id: number): string {
			if (settingsStore.shouldMaskAccountNames) {
				return '**MASKED**';
			}

			const account = actions.getAccount(id);
			if (!account) {
				return '**UNKNOWN**';
			}

			return account.username;
		},
		/**
     * Checks if there is any enabled account that has access to at least one library on the server
     * NOTE: This will only check enabled accounts
     * @param plexServerId
     */
		getHasAccountServerAccess(plexServerId: number): boolean {
			const serverLibraries = libraryStore.getAllLibrariesByServerId(plexServerId);

			for (const account of state.accounts.filter((x) => x.isEnabled)) {
				if (
					account.plexServerAccess.includes(plexServerId)
					&& serverLibraries.some((library) => account.plexLibraryAccess.includes(library.id))
				) {
					return true;
				}
			}

			return false;
		},
		/**
     * Checks if there is any enabled account that has access to the library.
     * @param plexLibraryId
     */
		getHasAccountLibraryAccess(plexLibraryId: number): boolean {
			for (const account of state.accounts.filter((x) => x.isEnabled)) {
				if (account.plexLibraryAccess.includes(plexLibraryId)) {
					return true;
				}
			}

			return false;
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	// Getters
	const getters = {
		getAccounts: computed(() => state.accounts),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useAccountStore, import.meta.hot));
}
