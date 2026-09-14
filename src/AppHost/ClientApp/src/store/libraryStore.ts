import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, toRefs } from 'vue';
import { forkJoin, type Observable } from 'rxjs';
import { of } from 'rxjs';
import { concatMap, map, switchMap, tap } from 'rxjs/operators';
import { get } from '@vueuse/core';
import {
	type LibrarySyncProgressDTO,
	type LibrarySyncJobQueueDTO,
	LibrarySyncJobStatus,
	type PlexLibraryDTO,
	type PlexServerDTO,
} from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { plexLibraryApi } from '@api';
import { RefreshDataType } from '@dto';
import {
	useBackgroundJobsStore,
	useMediaOverviewStore,
	useServerStore,
	useSettingsStore,
	useSignalrStore,
} from '@store';
import { cloneDeep } from 'lodash-es';
import Log from 'consola';

interface ILibraryStoreState {
	libraries: PlexLibraryDTO[];
	syncQueues: LibrarySyncJobQueueDTO[];
	progress: LibrarySyncProgressDTO[];
}

export const useLibraryStore = defineStore(StoreNames.LibraryStore, () => {
	const defaultState: ILibraryStoreState = {
		libraries: [], syncQueues: [], progress: [],
	};

	const state = reactive<ILibraryStoreState>(cloneDeep(defaultState));

	const serverStore = useServerStore();
	const settingsStore = useSettingsStore();
	const signalRStore = useSignalrStore();
	const backgroundJobsStore = useBackgroundJobsStore();

	const actions = {
		setup(): Observable<ISetupResult> {
			// Listen for refresh notifications
			signalRStore
				.getRefreshNotification(RefreshDataType.PlexLibrary)
				.pipe(concatMap(() => actions.refreshLibraries()))
				.subscribe();
			signalRStore
				.getRefreshNotification(RefreshDataType.PlexLibrarySyncStatus)
				.pipe(concatMap(() => actions.refreshLibrarySyncStatus()))
				.subscribe();

			// Listen for library sync job status updates
			backgroundJobsStore.getLibrarySyncJobUpdate().subscribe((update) => {
				actions.updateSyncQueue(update.data);
			});

			return forkJoin([actions.refreshLibraries(), actions.refreshLibrarySyncStatus()]).pipe(switchMap(() => of({
				name: StoreNames.LibraryStore, isSuccess: true,
			})));
		},
		updateSyncQueue(queue: LibrarySyncJobQueueDTO): void {
			const index = state.syncQueues.findIndex((x) => x.plexLibraryId === queue.plexLibraryId);
			if (index > -1) {
				state.syncQueues.splice(index, 1, queue);
			} else {
				state.syncQueues.push(queue);
			}
		},
		updateLibraryProgress(progress: LibrarySyncProgressDTO): void {
			const index = state.progress.findIndex((x) => x.plexLibraryId === progress.plexLibraryId);
			if (index > -1) {
				state.progress.splice(index, 1, progress);
			} else {
				state.progress.push(progress);
			}
		},
		refreshLibraries(): Observable<PlexLibraryDTO[]> {
			return plexLibraryApi.getAllPlexLibrariesEndpoint().pipe(tap(({ isSuccess, value }) => {
				if (isSuccess) {
					state.libraries = value ?? [];
				}
			}), map(() => get(getters.getLibraries())));
		},
		refreshLibrary(libraryId: number) {
			return plexLibraryApi.getPlexLibraryByIdEndpoint(libraryId).pipe(map(({ isSuccess, value }) => {
				if (isSuccess && value) {
					return value;
				}
				return null;
			}), tap((library) => actions.updateLibrary(library)));
		},
		refreshLibrarySyncStatus(): Observable<LibrarySyncJobQueueDTO[]> {
			return plexLibraryApi.getLibrarySyncStatusEndpoint().pipe(map(({ isSuccess, value }) => {
				if (isSuccess && value) {
					return value;
				}
				return [];
			}), tap((libraryQueues) => {
				state.syncQueues.splice(0, state.syncQueues.length, ...libraryQueues);
			}));
		},
		/**
     * Re-syncs a library by re-requesting all media from the Plex server.
     * @param libraryId
     * @param forceMediaRefresh Whether to replace all stored media entries before rebuilding the library.
     */
		reSyncLibrary(libraryId: number, forceMediaRefresh = false): Observable<PlexLibraryDTO | null> {
			return plexLibraryApi.refreshLibraryMediaEndpoint(libraryId, {
				forceLibrarySync: true,
				forceMediaRefresh,
			}).pipe(tap((library) => actions.updateLibrary(library.value)), switchMap((library): Observable<PlexLibraryDTO | null> => of(getters.getLibrary(library.value?.id ?? 0))));
		},
		cancelLibrarySync(libraryId: number) {
			return plexLibraryApi.cancelLibrarySyncEndpoint(libraryId);
		},
		updateDefaultDestination(libraryId: number, folderPathId: number): void {
			plexLibraryApi.setPlexLibraryDefaultDestinationByIdEndpoint(libraryId, folderPathId).subscribe((result) => {
				if (result.isSuccess) {
					const index = state.libraries.findIndex((x) => x.id === libraryId);
					if (index > -1) {
						const updated = {
							...(state.libraries[index] as PlexLibraryDTO), defaultDestinationId: folderPathId,
						} as PlexLibraryDTO;
						state.libraries.splice(index, 1, updated);
					}
				}
			});
		},
		setLibraryEnabled(libraryId: number, isEnabled: boolean): Observable<PlexLibraryDTO | null> {
			return plexLibraryApi.setLibraryEnabledEndpoint(libraryId, { isEnabled }).pipe(
				switchMap((response) => {
					if (response.isSuccess && response.value) {
						const mediaOverviewStore = useMediaOverviewStore();
						actions.updateLibrary(response.value);

						// When disabling a library, clear the media overview so the
						// disabled-library alert shows immediately without requiring
						// a page refresh.
						if (!isEnabled) {
							mediaOverviewStore.clearLibraryMediaData(response.value.id);
						}

						return actions.refreshLibrary(response.value.id).pipe(
							switchMap((library) => actions.refreshLibrarySyncStatus().pipe(map(() => library))),
							switchMap((library) => {
								if (isEnabled && library && mediaOverviewStore.libraryId === library.id && !getters.getIsLibrarySyncing(library.id)) {
									return mediaOverviewStore.initializeLibrary(library.id).pipe(map(() => library));
								}

								return of(library);
							}),
						);
					}
					return of(null);
				}),
			);
		},
		updateLibrary(library?: PlexLibraryDTO | null): void {
			if (!library) {
				Log.error('Library was invalid, cannot update store.', library);
				return;
			}
			const i = state.libraries.findIndex((x) => x.id === library.id);
			if (i > -1) {
				// We freeze library here as it doesn't have to be Vue reactive.
				state.libraries.splice(i, 1, Object.freeze(library));
				return;
			}
			state.libraries.push(Object.freeze(library));
		}, $reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};
	const getters = {
		/**
     * Get the enabled Plex libraries
     * @param plexServerId
     */
		getLibrariesByServerId: (plexServerId: number) =>
			state.libraries.filter((y) => y.plexServerId === plexServerId && y.isEnabled),
		/**
     * Gets all the Plex libraries regardless of enabled/disabled state
     * @param plexServerId
     */
		getAllLibrariesByServerId: (plexServerId: number) =>
			state.libraries.filter((y) => y.plexServerId === plexServerId),
		getLibrary: (libraryId: number): PlexLibraryDTO | null => state.libraries.find((x) => x.id === libraryId) ?? null,
		getLibraries: (libraryIds: number[] = []): PlexLibraryDTO[] => {
			if (libraryIds.length === 0) {
				return state.libraries.map((x) => getters.getLibrary(x.id)).filter((x) => !!x);
			}
			return libraryIds.map((x) => getters.getLibrary(x)).filter((x) => !!x);
		},
		getServerByLibraryId: (libraryId: number): PlexServerDTO | null => {
			const library = state.libraries.find((x) => x.id === libraryId) ?? null;
			if (library) {
				return serverStore.getServer(library.plexServerId);
			}
			return null;
		},
		getLibraryName: (libraryId: number): string => {
			if (settingsStore.shouldMaskLibraryNames) {
				return '**MASKED**';
			}
			return getters.getLibrary(libraryId)?.title ?? '';
		},
		getLibraryProgress: (libraryId: number): LibrarySyncProgressDTO | null => {
			return state.progress.find((x) => x.plexLibraryId === libraryId) ?? null;
		},
		getIsLibrarySyncing: (libraryId: number): boolean => {
			return state.syncQueues.some((x) => x.plexLibraryId === libraryId && x.status === LibrarySyncJobStatus.Processing);
		},
		getLibrarySyncQueueGrouped: (plexServerId?: number, terminalCutoff?: Date): ILibrarySyncProgress[] => {
			return state.syncQueues
				.filter((queue) => plexServerId === undefined || queue.plexServerId === plexServerId)
				.filter((queue) => {
					const isTerminal = [LibrarySyncJobStatus.Completed, LibrarySyncJobStatus.Failed, LibrarySyncJobStatus.Cancelled].includes(queue.status);
					return !isTerminal || terminalCutoff === undefined || (!!queue.completedAt && Date.parse(queue.completedAt) >= terminalCutoff.getTime());
				})
				.reduce<ILibrarySyncProgress[]>((acc, queue) => {
					const progress = state.progress.find((p) => p.plexLibraryId === queue.plexLibraryId);
					const isTerminal = [LibrarySyncJobStatus.Completed, LibrarySyncJobStatus.Failed, LibrarySyncJobStatus.Cancelled].includes(queue.status);
					const queueWithProgress = {
						...queue,
						...progress,
						percentage: isTerminal ? 100 : progress?.percentage,
						isComplete: isTerminal || progress?.isComplete,
					} as LibrarySyncJobQueueDTO & LibrarySyncProgressDTO;
					const existing = acc.find((x) => x.serverId === queue.plexServerId);
					if (existing) {
						existing.progress.push(queueWithProgress);
					} else {
						acc.push({
							serverId: queue.plexServerId, progress: [queueWithProgress],
						});
					}
					return acc;
				}, []);
		},
	};
	return {
		...toRefs(state), ...actions, ...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useLibraryStore, import.meta.hot));
}

interface ILibrarySyncProgress {
	serverId: number;
	progress: (LibrarySyncJobQueueDTO & LibrarySyncProgressDTO)[];
}
