import { defineStore, acceptHMRUpdate } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import type { Observable } from 'rxjs';
import { of } from 'rxjs';
import { catchError, concatMap, map, switchMap, tap } from 'rxjs/operators';
import type { PlexServerDTO } from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { plexServerApi } from '@api';
import { RefreshDataType } from '@dto';
import { cloneDeep, orderBy } from 'lodash-es';
import { useServerConnectionStore, useSettingsStore, useSignalrStore } from '@store';

interface IServerStoreState {
	servers: PlexServerDTO[];
}

export const useServerStore = defineStore(StoreNames.ServerStore, () => {
	const defaultState: IServerStoreState = {
		servers: [],
	};

	const state = reactive<IServerStoreState>(cloneDeep(defaultState));

	const serverConnectionStore = useServerConnectionStore();
	const settingsStore = useSettingsStore();
	const signalRStore = useSignalrStore();

	// Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			// Listen for refresh notifications
			signalRStore
				.getRefreshNotification(RefreshDataType.PlexServer)
				.pipe(concatMap(() => actions.refreshPlexServers()))
				.subscribe();

			return fetchAndSetPlexServers().pipe(
				map((result) => ({
					name: StoreNames.ServerStore,
					isSuccess: !!result?.isSuccess,
				})),
				catchError(() => of({ name: StoreNames.ServerStore, isSuccess: false })),
			);
		},
		refreshPlexServer(serverId: number) {
			return plexServerApi.getPlexServerByIdEndpoint(serverId).pipe(
				tap((result) => {
					if (result.isSuccess && result.value) {
						const i = state.servers.findIndex((x) => x.id === serverId);
						if (i > -1) {
							state.servers.splice(i, 1, result.value);
						}
					}
				}),
			);
		},
		/**
     * Forces a refresh of all the PlexServers currently in store by fetching it from the API.
     */
		refreshPlexServers() {
			return fetchAndSetPlexServers().pipe(map(() => [...state.servers]));
		},
		setServerAlias(serverId: number, serverAlias: string) {
			return plexServerApi
				.setServerAlias(serverId, {
					serverAlias,
				})
				.pipe(switchMap((response) => response.isSuccess ? settingsStore.refreshSettings() : of(response)));
		},
		setServerEnabled(serverId: number, isEnabled: boolean) {
			return plexServerApi
				.setServerEnabledEndpoint(serverId, {
					isEnabled,
				})
				.pipe(
					tap((response) => {
						if (response.isSuccess && response.value) {
							const i = state.servers.findIndex((x) => x.id === serverId);
							if (i > -1) {
								state.servers.splice(i, 1, response.value);
							}
						}
					}),
				);
		},
		setServerOwned(serverId: number, owned: boolean) {
			return plexServerApi
				.setServerOwnedEndpoint(serverId, {
					isOwned: owned,
				})
				.pipe(
					tap((response) => {
						if (response.isSuccess && response.value) {
							const i = state.servers.findIndex((x) => x.id === serverId);
							if (i > -1) {
								state.servers.splice(i, 1, response.value);
							}
						}
					}),
				);
		},
		deleteServer(serverId: number) {
			return plexServerApi.deletePlexServerEndpoint(serverId).pipe(
				tap((response) => {
					if (response.isSuccess) {
						state.servers = state.servers.filter((x) => x.id !== serverId);
					}
				}),
			);
		},
		setServerPaused(serverId: number, paused: boolean) {
			const request$ = paused
				? plexServerApi.pausePlexServerDownloadsEndpoint(serverId)
				: plexServerApi.resumePlexServerDownloadsEndpoint(serverId);

			return request$.pipe(
				switchMap((response) => response.isSuccess ? actions.refreshPlexServer(serverId) : of(response)),
			);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	function fetchAndSetPlexServers() {
		return plexServerApi.getAllPlexServersEndpoint().pipe(
			tap((plexServers) => {
				if (plexServers.isSuccess) {
					state.servers = plexServers?.value ?? [];
				}
			}),
		);
	}

	// Getters
	const getters = {
		getServer: (serverId: number): PlexServerDTO | null => {
			return state.servers.find((x) => x.id === serverId) ?? null;
		},
		getServers: (serverIds: number[] = []): PlexServerDTO[] => {
			if (serverIds.length === 0) {
				return state.servers.map((x) => getters.getServer(x.id)).filter((x) => !!x) ?? [];
			}
			return serverIds.map((x) => getters.getServer(x)).filter((x) => !!x) ?? [];
		},
		getVisibleServers: computed((): PlexServerDTO[] => {
			const servers = getters.getServers().filter((x) => x.isEnabled);
			return orderBy(servers, [(x) => x.owned, (x) => x.name.toLocaleLowerCase()], ['desc', 'asc']);
		}),
		getDisabledServers: computed((): PlexServerDTO[] => getters.getServers().filter((x) => !x.isEnabled)),
		getServerName: (serverId: number): string => {
			if (settingsStore.shouldMaskServerNames) {
				return '**MASKED**';
			}

			const server = getters.getServer(serverId);
			if (!server) {
				return '**UNKNOWN**';
			}

			const customName = settingsStore.getServerSettings(server.machineIdentifier)?.plexServerName ?? '';
			if (customName !== '') {
				return customName;
			}

			return server.name;
		},
		getServerStatus: (plexServerId: number) =>
			serverConnectionStore
				.getServerConnectionsByServerId(plexServerId)
				.some((x) => x.latestConnectionStatus?.isSuccessful ?? false),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useServerStore, import.meta.hot));
}
