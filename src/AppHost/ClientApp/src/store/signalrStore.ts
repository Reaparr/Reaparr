import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, toRefs } from 'vue';
import type { Observable, Subscription } from 'rxjs';
import { from, of, tap, Subject } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, filter, map, switchMap, take } from 'rxjs/operators';
import Log from 'consola';
import type { HubConnection, IHttpConnectionOptions } from '@microsoft/signalr';
import { HttpTransportType, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useCypressSignalRMock } from 'cypress-signalr-mock';
import { isEqual, cloneDeep, isArray } from 'lodash-es';
import {
	StoreNames,
	type DownloadPatchEntryMessagePackTuple,
	type DownloadPatchMessagePackTuple,
	type ISetupResult,
	type ServerDownloadEntryMessagePackTuple,
	type ServerDownloadProgressMessagePackTuple,
} from '@interfaces';
import type {
	AppUpdateDownloadProgressDTO,
	DownloadPatchMessagePackDTO,
	DownloadPatchDTO,
	LibrarySyncProgressDTO,
	NotificationDTO,
	ServerConnectionCheckStatusProgressDTO,
	ServerDownloadProgressDTO,
	ServerDownloadProgressMessagePackDTO,
	LiveLogEventDTO,
	IntegrationSetupProgressDTO,
	RefreshDataType,
} from '@dto';
import { MessageTypes } from '@dto';
import type { IRetryPolicy } from '@microsoft/signalr/src/IRetryPolicy';
import {
	useDownloadStore,
	useBackgroundJobsStore,
	useNotificationsStore,
	useLibraryStore,
} from '@store';
import Axios from 'axios';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';

export enum HubName {
	Progress = 'progress',
	Download = 'download',
	Notifications = 'notifications',
	Logs = 'logs',
}

export const useSignalrStore = defineStore(StoreNames.SignalrStore, () => {
	interface ISignalRStoreState {
		// Data
		serverConnectionCheckStatusProgress: ServerConnectionCheckStatusProgressDTO[];
		// Subjects
		serverConnectionCheckStatusProgressSubject: Subject<ServerConnectionCheckStatusProgressDTO[]>;
		refreshDataNotificationSubject: Subject<RefreshDataType>;
		appUpdateDownloadProgressSubject: Subject<AppUpdateDownloadProgressDTO>;
		logEventSubject: Subject<LiveLogEventDTO>;
		integrationSetupProgressSubject: Subject<IntegrationSetupProgressDTO>;
	}

	const defaultState: ISignalRStoreState = {
		// Data
		serverConnectionCheckStatusProgress: [],

		// Subjects
		serverConnectionCheckStatusProgressSubject: new Subject<ServerConnectionCheckStatusProgressDTO[]>(),
		refreshDataNotificationSubject: new Subject<RefreshDataType>(),
		appUpdateDownloadProgressSubject: new Subject<AppUpdateDownloadProgressDTO>(),
		logEventSubject: new Subject<LiveLogEventDTO>(),
		integrationSetupProgressSubject: new Subject<IntegrationSetupProgressDTO>(),
	};

	const state = reactive<ISignalRStoreState>(cloneDeep(defaultState));

	// Connections
	let progressHubConnection: HubConnection | null;
	let downloadHubConnection: HubConnection | null;
	let notificationHubConnection: HubConnection | null;
	let logHubConnection: HubConnection | null;
	let connectionOperation: Promise<void> = Promise.resolve();
	let setupOperation: Promise<void> | null = null;
	let downloadRecovery: Subscription | null = null;
	let activeDownloadConnection: HubConnection | null = null;

	const actions = {
		setup(): Observable<ISetupResult> {
			if (!setupOperation) {
				const operation = connectionOperation.then(async () => {
					await stopHubConnections();
					Log.debug('Setting up SignalR Service');
					const options: IHttpConnectionOptions = {
						logMessageContent: false,
						skipNegotiation: true,
						logger: LogLevel.None,
						transport: HttpTransportType.WebSockets,
						withCredentials: true,
					};

					const retryPolicy: IRetryPolicy = {
						nextRetryDelayInMilliseconds: () => 2000,
					};

					const baseApiUrl = Axios.defaults.baseURL;
					// Setup Connections
					progressHubConnection = useCypressSignalRMock(HubName.Progress, { enableForVitest: true }) ?? new HubConnectionBuilder()
						.configureLogging(LogLevel.None)
						.withUrl(`${baseApiUrl}/progress`, options)
						.withAutomaticReconnect(retryPolicy)
						.build();

					const mock = useCypressSignalRMock(HubName.Download, { enableForVitest: true });
					if (mock) {
						downloadHubConnection = mock; // mock uses JSON internally
					} else {
						downloadHubConnection = new HubConnectionBuilder()
							.withHubProtocol(new MessagePackHubProtocol())
							.configureLogging(LogLevel.None)
							.withUrl(`${baseApiUrl}/download`, options)
							.withAutomaticReconnect(retryPolicy)
							.build();
					}

					notificationHubConnection = useCypressSignalRMock(HubName.Notifications, { enableForVitest: true }) ?? new HubConnectionBuilder()
						.configureLogging(LogLevel.None)
						.withUrl(`${baseApiUrl}/notifications`, options)
						.withAutomaticReconnect(retryPolicy)
						.build();

					logHubConnection = useCypressSignalRMock(HubName.Logs, { enableForVitest: true }) ?? new HubConnectionBuilder()
						.configureLogging(LogLevel.None)
						.withUrl(`${baseApiUrl}/logs`, options)
						.withAutomaticReconnect(retryPolicy)
						.build();

					setupSubscriptions();

					try {
						await Promise.all([startDownloadHubConnection(), startProgressHubConnection(), startNotificationHubConnection(), startLogHubConnection()]);
					} catch (error) {
						await stopHubConnections();
						throw error;
					}
				});
				setupOperation = operation;
				connectionOperation = operation.catch(() => {
					if (setupOperation === operation) {
						setupOperation = null;
					}
				});
			}
			return from(setupOperation).pipe(
				switchMap(() => of({ name: StoreNames.SignalrStore, isSuccess: true })),
				catchError((error) => {
					Log.error('Failed to setup SignalR Service', error);
					return of({ name: StoreNames.SignalrStore, isSuccess: false });
				}),
				take(1),
			);
		},
		clearServerConnectionCheckStatusProgress(plexServerConnectionId: number): void {
			removeStateItem<ServerConnectionCheckStatusProgressDTO>(
				'serverConnectionCheckStatusProgress',
				{ plexServerConnectionId } as ServerConnectionCheckStatusProgressDTO,
				'plexServerConnectionId',
			);
		},
		resetSession(): Promise<void> {
			setupOperation = null;
			const operation = connectionOperation.then(async () => {
				await stopHubConnections();
				state.serverConnectionCheckStatusProgressSubject.complete();
				state.refreshDataNotificationSubject.complete();
				state.appUpdateDownloadProgressSubject.complete();
				state.logEventSubject.complete();
				state.integrationSetupProgressSubject.complete();
				Object.assign(state, cloneDeep(defaultState));
			});
			connectionOperation = operation.catch(() => {});
			return operation;
		},
	};

	function setupSubscriptions(): void {
		const backgroundStore = useBackgroundJobsStore();
		const notificationsStore = useNotificationsStore();
		const libraryStore = useLibraryStore();

		logHubConnection?.on('LogEvent', (data: LiveLogEventDTO) => state.logEventSubject.next(data));

		setupDownloadSubscriptions();

		progressHubConnection?.on(MessageTypes.LibraryProgress, (data: LibrarySyncProgressDTO) => libraryStore.updateLibraryProgress(data));

		progressHubConnection?.on(MessageTypes.ServerConnectionCheckStatusProgress, (data: ServerConnectionCheckStatusProgressDTO) => updateState<ServerConnectionCheckStatusProgressDTO>('serverConnectionCheckStatusProgress', data, 'plexServerConnectionId'));

		progressHubConnection?.on(MessageTypes.JobStatusUpdate, (data) => backgroundStore.setStatusJobUpdate(data));

		progressHubConnection?.on(MessageTypes.AppUpdateDownloadProgress, (data: AppUpdateDownloadProgressDTO) => state.appUpdateDownloadProgressSubject.next(data));

		progressHubConnection?.on(MessageTypes.IntegrationSetupProgress, (data: IntegrationSetupProgressDTO) => state.integrationSetupProgressSubject.next(data));

		notificationHubConnection?.on(MessageTypes.Notification, (data: NotificationDTO) => notificationsStore.setNotification(data));

		notificationHubConnection?.on(MessageTypes.RefreshNotification, (data: RefreshDataType) => {
			state.refreshDataNotificationSubject.next(data);
		});
		const notificationConnection = notificationHubConnection;
		notificationConnection?.onreconnected?.(() => {
			if (notificationHubConnection !== notificationConnection || notificationConnection?.state !== HubConnectionState.Connected) return;
			libraryStore.refreshLibrarySyncStatus().subscribe();
		});
	}

	function setupDownloadSubscriptions(): void {
		const downloadStore = useDownloadStore();
		const connection = downloadHubConnection;
		if (!connection) return;
		activeDownloadConnection = connection;
		connection.on(MessageTypes.ServerDownloadProgress, handleDownloadProgress);
		connection.on(MessageTypes.DownloadPatch, handleDownloadPatch);
		connection.onreconnected?.(() => recoverDownloadQueue(connection));

		function handleDownloadProgress(rawData: ServerDownloadProgressMessagePackDTO | ServerDownloadProgressMessagePackTuple): void {
			if (activeDownloadConnection !== connection) return;
			Log.debug(rawData);
			if (Array.isArray(rawData)) {
				downloadStore.updateServerDownloadProgress(toServerDownloadProgressDTO(rawData));
			} else {
				downloadStore.updateServerDownloadProgress(rawData);
			}
		}

		function handleDownloadPatch(rawData: DownloadPatchMessagePackTuple): void {
			if (activeDownloadConnection !== connection) return;
			downloadStore.updateDownloadPatch(toDownloadPatchDTO(rawData));
		}
	}

	function recoverDownloadQueue(connection: HubConnection): void {
		if (activeDownloadConnection !== connection || connection.state !== HubConnectionState.Connected) return;
		downloadRecovery?.unsubscribe();
		downloadRecovery = useDownloadStore().reconcileAfterReconnect().subscribe();
	}

	function updateState<T>(propertyName: keyof ISignalRStoreState, newObject: T, idName: keyof T): void {
		if (!state[propertyName]) {
			Log.error(`Failed to get ISignalRStoreState property name: ${propertyName}`);
			return;
		}

		if (isArray(newObject)) {
			for (const item of newObject) {
				update(item);
			}
		} else {
			update(newObject);
		}

		function update(item: T): void {
			if (!item[idName]) {
				Log.error(`Failed to find the correct id property in ${propertyName} with idName: ${String(idName)}`, item);
				return;
			}

			const i = (state[propertyName] as Array<T>).findIndex((x) => x[idName] === item[idName]);
			if (i > -1) {
				(state[propertyName] as Array<T>).splice(i, 1, item);
			} else {
				(state[propertyName] as Array<T>).push(item);
			}
		}

		// Trigger Subject to send current state
		(state[propertyName + 'Subject'] as Subject<Array<T>>).next(state[propertyName] as Array<T>);
	}

	function removeStateItem<T>(propertyName: keyof ISignalRStoreState, itemToRemove: T, idName: keyof T): void {
		if (!state[propertyName]) {
			Log.error(`Failed to get ISignalRStoreState property name: ${propertyName}`);
			return;
		}

		if (isArray(itemToRemove)) {
			for (const item of itemToRemove) {
				remove(item);
			}
		} else {
			remove(itemToRemove);
		}

		function remove(item: T): void {
			if (!item[idName]) {
				Log.error(`Failed to find the correct id property in ${propertyName} with idName: ${String(idName)}`, item);
				return;
			}

			const i = (state[propertyName] as Array<T>).findIndex((x) => x[idName] === item[idName]);
			if (i > -1) {
				(state[propertyName] as Array<T>).splice(i, 1);
			}
		}

		// Trigger Subject to send current state
		(state[propertyName + 'Subject'] as Subject<Array<T>>).next(state[propertyName] as Array<T>);
	}

	// region Start / Stop Hub Connections
	async function stopHubConnections(): Promise<void> {
		const connections = [progressHubConnection, downloadHubConnection, notificationHubConnection, logHubConnection];
		activeDownloadConnection = null;
		downloadRecovery?.unsubscribe();
		downloadRecovery = null;
		for (const connection of connections) {
			if (!connection) continue;
			connection.off('LogEvent');
			for (const messageType of Object.values(MessageTypes)) {
				connection.off(messageType);
			}
		}
		await Promise.all(connections.map((connection) => connection?.stop()));
		progressHubConnection = null;
		downloadHubConnection = null;
		notificationHubConnection = null;
		logHubConnection = null;
	}

	async function startDownloadHubConnection(): Promise<void> {
		if (!downloadHubConnection || downloadHubConnection.state !== HubConnectionState.Disconnected) return;

		await downloadHubConnection.start();
		Log.info('DownloadHub connected');
	}

	async function startProgressHubConnection() {
		if (!progressHubConnection || progressHubConnection.state !== HubConnectionState.Disconnected) return;

		await progressHubConnection.start();
		Log.info('ProgressHub connected');
	}

	async function startNotificationHubConnection() {
		if (!notificationHubConnection || notificationHubConnection.state !== HubConnectionState.Disconnected) return;

		await notificationHubConnection.start();
		Log.info('NotificationHub connected');
	}

	async function startLogHubConnection() {
		if (!logHubConnection || logHubConnection.state !== HubConnectionState.Disconnected) return;

		await logHubConnection.start();
		Log.info('LogHub connected');
	}

	// endregion

	const getters = {
		// region Array Progress
		getAllServerConnectionProgress: (): Observable<ServerConnectionCheckStatusProgressDTO[]> => state.serverConnectionCheckStatusProgressSubject.asObservable(), // endregion

		// region Single Progress

		getServerConnectionProgressByPlexServerId(plexServerId: number): Observable<ServerConnectionCheckStatusProgressDTO[]> {
			return getters.getAllServerConnectionProgress().pipe(map((x) => x?.filter((y) => y.plexServerId === plexServerId)), distinctUntilChanged(isEqual));
		},
		getRefreshNotification(filterOn: RefreshDataType): Observable<RefreshDataType> {
			return state.refreshDataNotificationSubject.asObservable().pipe(
				filter((x) => x === filterOn),
				debounceTime(250),
				tap(() => Log.debug('Refreshing ' + filterOn)),
			);
		},
		getAppUpdateDownloadProgress(): Observable<AppUpdateDownloadProgressDTO> {
			return state.appUpdateDownloadProgressSubject.asObservable();
		},
		getLogEvents(): Observable<LiveLogEventDTO> {
			return state.logEventSubject.asObservable();
		},
		// endregion
	};
	return {
		...toRefs(state), ...actions, ...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useSignalrStore, import.meta.hot));
}

function toServerDownloadProgressDTO(arr: ServerDownloadProgressMessagePackTuple): ServerDownloadProgressDTO | null {
	if (!Array.isArray(arr))
		return null;

	function mapDownload(item: ServerDownloadEntryMessagePackTuple | unknown): ServerDownloadProgressDTO['downloads'][number] | null {
		if (!Array.isArray(item))
			return null;

		const childrenRaw = Array.isArray(item[9]) ? item[9] : [];
		const children = childrenRaw.map((x) => mapDownload(x)).filter((x): x is NonNullable<typeof x> => x !== null);

		return {
			id: item[0],
			title: item[1],
			mediaType: item[2],
			status: item[3],
			percentage: Number(item[4]),
			dataReceived: item[5],
			dataTotal: item[6],
			downloadSpeed: item[7],
			timeRemaining: item[8],
			children,
		};
	}

	const downloadsRaw = Array.isArray(arr[2]) ? arr[2] : [];
	const downloads = downloadsRaw.map((x) => mapDownload(x)).filter((x): x is NonNullable<typeof x> => x !== null);

	return {
		id: arr[0], downloadableTasksCount: arr[1], downloads,
	};
}

function toDownloadPatchDTO(arr: DownloadPatchMessagePackTuple): DownloadPatchMessagePackDTO {
	if (!Array.isArray(arr))
		throw new Error('Invalid DownloadPatch MessagePack tuple payload');

	const upsertsRaw = Array.isArray(arr[2]) ? arr[2] : [];
	const upserts: DownloadPatchDTO[] = upsertsRaw.map((item) => {
		if (!isDownloadPatchEntryTuple(item))
			throw new Error('Invalid DownloadPatch MessagePack tuple entry');

		return {
			id: item[0],
			parentId: item[1],
			status: item[2],
			percentage: Number(item[3]),
			dataReceived: item[4],
			dataTotal: item[5],
			downloadSpeed: item[6],
			timeRemaining: item[7],
		};
	});

	return {
		serverId: arr[0],
		sequence: arr[1],
		upserts,
		deletedIds: Array.isArray(arr[3]) ? arr[3] : [],
	};
}

function isDownloadPatchEntryTuple(value: unknown): value is DownloadPatchEntryMessagePackTuple {
	if (!Array.isArray(value) || value.length !== 8)
		return false;

	return typeof value[0] === 'string'
		&& typeof value[1] === 'string'
		&& typeof value[2] === 'string'
		&& (typeof value[3] === 'number' || typeof value[3] === 'string')
		&& typeof value[4] === 'number'
		&& typeof value[5] === 'number'
		&& typeof value[6] === 'number'
		&& typeof value[7] === 'number';
}
