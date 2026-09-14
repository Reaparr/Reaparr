import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import type { Observable } from 'rxjs';
import { of } from 'rxjs';
import { concatMap, finalize, map, switchMap, tap } from 'rxjs/operators';
import { get } from '@vueuse/core';
import type {
	CreatePlexServerConnectionEndpointRequest,
	PlexServerConnectionDTO,
	PlexServerStatusDTO,
	UpdatePlexServerConnectionEndpointRequest,
} from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { plexServerApi, plexServerConnectionApi } from '@api';
import { RefreshDataType } from '@dto';
import { sortPlexServerConnections } from '@composables/common';
import { useServerStore, useSignalrStore } from '@store';
import { assign, cloneDeep } from 'lodash-es';

interface IServerConnectionStoreState {
	serverConnections: PlexServerConnectionDTO[];
	connectionLoadingById: Record<number, boolean>;
}

export const useServerConnectionStore = defineStore(StoreNames.ServerConnectionStore, () => {
	const defaultState: IServerConnectionStoreState = {
		serverConnections: [],
		connectionLoadingById: {},
	};

	const state = reactive<IServerConnectionStoreState>(cloneDeep(defaultState));

	const signalRStore = useSignalrStore();
	const serverStore = useServerStore();

	const actions = {
		setConnectionLoading(connectionId: number, isLoading: boolean) {
			state.connectionLoadingById[connectionId] = isLoading;
		},
		setConnectionsLoadingForServer(plexServerId: number, isLoading: boolean) {
			state.serverConnections
				.filter((connection) => connection.plexServerId === plexServerId)
				.forEach((connection) => {
					state.connectionLoadingById[connection.id] = isLoading;
				});
		},
		setup(): Observable<ISetupResult> {
			// Listen for refresh notifications
			signalRStore
				.getRefreshNotification(RefreshDataType.PlexServerConnection)
				.pipe(concatMap(() => actions.refreshPlexServerConnections()))
				.subscribe();

			return fetchAndSetPlexServerConnections().pipe(
				switchMap((result) => of({
					name: StoreNames.ServerConnectionStore,
					isSuccess: !!result?.isSuccess,
				})),
			);
		},
		refreshPlexServerConnections(): Observable<PlexServerConnectionDTO[]> {
			return fetchAndSetPlexServerConnections().pipe(
				map(() => get(getters.getServerConnections)),
			);
		},
		checkServerConnection(plexServerConnectionId: number): Observable<PlexServerStatusDTO | null> {
			signalRStore.clearServerConnectionCheckStatusProgress(plexServerConnectionId);
			actions.setConnectionLoading(plexServerConnectionId, true);
			return plexServerConnectionApi.checkConnectionStatusByIdEndpoint(plexServerConnectionId).pipe(
				map((res) => {
					if (res.isSuccess && res.value) {
						const index = state.serverConnections.findIndex((x) => x.id === plexServerConnectionId);
						if (index === -1) {
							return res.value;
						}
						assign(state.serverConnections[index]!.latestConnectionStatus, res.value);
					}
					return res?.value ?? null;
				}),
				finalize(() => actions.setConnectionLoading(plexServerConnectionId, false)),
			);
		},
		/**
     * Forces a recheck of all the server connections for the given server id
     * @param plexServerId
     */
		checkServerStatus(plexServerId: number) {
			actions.setConnectionsLoadingForServer(plexServerId, true);
			return plexServerConnectionApi.checkAllConnectionsStatusByPlexServerEndpoint(plexServerId).pipe(
				switchMap((response) => response.isSuccess ? actions.refreshPlexServerConnections() : of(response)),
				finalize(() => actions.setConnectionsLoadingForServer(plexServerId, false)),
			);
		},
		checkServerConnectionUrl: (connectionUrl: string) =>
			plexServerConnectionApi.validatePlexServerConnectionEndpoint({
				url: connectionUrl,
			}),
		createServerConnection: (data: CreatePlexServerConnectionEndpointRequest) =>
			plexServerConnectionApi.createPlexServerConnectionEndpoint(data).pipe(
				tap(({ isSuccess, value }) => {
					if (isSuccess && value) {
						state.serverConnections.push(value);
					}
				}),
			),
		updateServerConnection: (data: UpdatePlexServerConnectionEndpointRequest) =>
			plexServerConnectionApi.updatePlexServerConnectionEndpoint(data).pipe(
				tap(({ isSuccess, value }) => {
					if (isSuccess && value) {
						const index = state.serverConnections.findIndex((x) => x.id === value.id);
						if (index !== -1 && value) {
							state.serverConnections.splice(index, 1, value);
						}
					}
				}),
			),
		deleteServerConnection: (connectionId: number) =>
			plexServerConnectionApi.deletePlexServerConnectionById(connectionId).pipe(
				tap(({ isSuccess }) => {
					if (isSuccess) {
						const index = state.serverConnections.findIndex((x) => x.id === connectionId);
						if (index !== -1) {
							state.serverConnections.splice(index, 1);
						}
					}
				}),
			),
		setPreferredPlexServerConnection: (plexServerId: number, connectionId: number) =>
			plexServerApi
				.setPreferredPlexServerConnectionEndpoint(plexServerId, connectionId)
				.pipe(switchMap((response) => response.isSuccess ? serverStore.refreshPlexServer(plexServerId) : of(response))),
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	function fetchAndSetPlexServerConnections() {
		return plexServerConnectionApi.getAllPlexServerConnectionsEndpoint().pipe(
			tap((serverConnections) => {
				if (serverConnections.isSuccess) {
					state.serverConnections = serverConnections.value ?? [];
				}
			}),
		);
	}
	const getters = {
		getServerConnectionsByServerId: (plexServerId = 0): PlexServerConnectionDTO[] =>
			sortPlexServerConnections(
				state.serverConnections.filter((connection) => (plexServerId > 0 ? connection.plexServerId === plexServerId : false)),
			),
		getServerConnection: (connectionId: number) => state.serverConnections.find((x) => x.id === connectionId),
		getConnectionLoading: (connectionId: number) => state.connectionLoadingById[connectionId] ?? false,
		isAnyConnectionLoadingForServer: (plexServerId: number) =>
			state.serverConnections
				.filter((connection) => connection.plexServerId === plexServerId)
				.some((connection) => state.connectionLoadingById[connection.id] ?? false),
		getServerConnections: computed((): PlexServerConnectionDTO[] => state.serverConnections),
		isServerConnected: (plexServerId = 0) =>
			state.serverConnections
				.filter((x) => x.plexServerId === plexServerId)
				.some((x) => x.latestConnectionStatus?.isSuccessful ?? false),
		IsUrlExisting: (
			url: string,
		): {
			plexServerId: number;
			connectionId: number;
		} => {
			const connection = state.serverConnections.find((x) => x.url === url);
			return connection
				? { plexServerId: connection.plexServerId, connectionId: connection.id }
				: {
						plexServerId: 0,
						connectionId: 0,
					};
		},
	};
	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useServerConnectionStore, import.meta.hot));
}
