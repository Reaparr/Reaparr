import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { concatMap, finalize, map, switchMap, tap } from 'rxjs/operators';
import type { Observable } from 'rxjs';
import { defer, of } from 'rxjs';
import { clone, cloneDeep, flatMapDeep, keyBy, merge, sum, values } from 'lodash-es';
import {
	type BaseResultDTO,
	type CreateDownloadTasksRequest,
	DownloadActions,
	type DownloadPreviewContainerDTO,
	type DownloadPreviewRequest,
	type DownloadPatchMessagePackDTO,
	type DownloadProgressDTO,
	DownloadStatus,
	type PlexServerDTO,
	type ServerDownloadProgressDTO,
	RefreshDataType,
} from '@dto';
import { StoreNames, type IDownloadsSelection, type IPTreeTableSelectionKeys, type ISetupResult } from '@interfaces';
import { downloadApi } from '@api';
import { translateDownloadNotification } from '@composables';
import { useServerStore, useSignalrStore } from '@store';

interface IDownloadsStoreState {
	serverDownloads: ServerDownloadProgressDTO[];
	selected: IDownloadsSelection[];
	latestPatchSequenceByServer: Record<number, number>;
}

interface PendingDownloadSnapshot {
	patches: DownloadPatchMessagePackDTO[];
}

export const useDownloadStore = defineStore(StoreNames.DownloadStore, () => {
	const defaultState: IDownloadsStoreState = {
		serverDownloads: [], selected: [], latestPatchSequenceByServer: {},
	};

	const state = reactive<IDownloadsStoreState>(cloneDeep(defaultState));
	let pendingSnapshot: PendingDownloadSnapshot | null = null;

	const signalRStore = useSignalrStore();
	const serverStore = useServerStore();

	// Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			// Listen for refresh notifications
			signalRStore
				.getRefreshNotification(RefreshDataType.DownloadTasks)
				.pipe(concatMap(() => actions.fetchDownloadList()))
				.subscribe();

			return actions.fetchDownloadList().pipe(switchMap(() => of({
				name: StoreNames.DownloadStore,
				isSuccess: true,
			})));
		},
		/**
         * Fetch the download list from the API.
         */
		fetchDownloadList() {
			return defer(() => {
				const snapshot: PendingDownloadSnapshot = { patches: pendingSnapshot?.patches ?? [] };
				pendingSnapshot = snapshot;
				return downloadApi.getAllDownloadTasksEndpoint().pipe(
					tap((downloads) => finishSnapshot(snapshot, downloads.isSuccess ? downloads.value ?? [] : null, downloads.isSuccess)),
					finalize(() => finishSnapshot(snapshot, null, false)),
				);
			});
		},
		reconcileAfterReconnect() {
			return defer(() => {
				// Sequence numbers belong to the server process that produced this connection.
				pendingSnapshot = null;
				state.latestPatchSequenceByServer = {};
				return actions.fetchDownloadList();
			});
		},
		executeDownloadCommand(action: DownloadActions, downloadTaskIds: string[], plexServerId?: number): Observable<BaseResultDTO> {
			if (downloadTaskIds.length === 0 && action !== DownloadActions.Clear) {
				Log.error(`No downloadTaskIds provided for action: ${action}`);
				return of({
					errors: [], isSuccess: false, statusCode: 400, successes: [],
				} as BaseResultDTO);
			}

			const downloadTaskId = downloadTaskIds[0];
			// TODO verify if we need to re-fetch the download list after each action
			if (!downloadTaskId && action !== DownloadActions.Clear && action !== DownloadActions.Delete) {
				Log.error(`No downloadTaskId provided for action: ${action}`);
				return of({
					errors: [], isSuccess: false, statusCode: 400, successes: [],
				} as BaseResultDTO);
			}
			const id = downloadTaskId as string;
			switch (action) {
				case DownloadActions.Pause:
					return downloadApi.pauseDownloadTaskEndpoint(id);
				case DownloadActions.Clear: {
					if (downloadTaskIds.length > 0) {
						return downloadApi
							.clearCompletedDownloadTasksByDownloadTaskIdEndpoint(downloadTaskIds)
							.pipe(switchMap(refreshAfterSuccessfulCommand));
					}

					return downloadApi
						.clearCompletedDownloadTasksByServerIdEndpoint(plexServerId!)
						.pipe(switchMap(refreshAfterSuccessfulCommand));
				}
				case DownloadActions.Delete:
					return downloadApi
						.deleteDownloadTaskEndpoint(downloadTaskIds)
						.pipe(switchMap(refreshAfterSuccessfulCommand));
				case DownloadActions.Stop:
					return downloadApi.stopDownloadTaskEndpoint(id);
				case DownloadActions.Restart:
					return downloadApi.restartDownloadTaskEndpoint(id);
				case DownloadActions.Start:
					return downloadApi.startDownloadTaskEndpoint(id);
				default:
					Log.error(`Action: ${action} does not have a assigned command with payload: ${downloadTaskIds}`);
					return of();
			}
		},
		updateServerDownloadProgress(serverDownloadProgress: ServerDownloadProgressDTO | null): void {
			if (!serverDownloadProgress) {
				Log.warn('Received null server download progress update');
				return;
			}

			const i = state.serverDownloads.findIndex((x) => x.id === serverDownloadProgress.id);
			if (i === -1) {
				state.serverDownloads.push(serverDownloadProgress);
				actions.setupSelection(serverDownloadProgress.id);
				return;
			}

			const existing = state.serverDownloads[i]!;
			const downloads = existing.downloads ?? [];
			const existingById = keyBy(downloads, 'id');
			merge(existingById, keyBy(serverDownloadProgress.downloads ?? [], 'id'));
			downloads.splice(0, downloads.length, ...values(existingById));
			existing.downloads = downloads;
			downloadNodeIndexCache.delete(downloads);
		},
		updateDownloadPatch(patch: DownloadPatchMessagePackDTO): void {
			if (!patch) return;
			const latest = state.latestPatchSequenceByServer[patch.serverId] ?? 0;
			if (patch.sequence <= latest) return;
			state.latestPatchSequenceByServer[patch.serverId] = patch.sequence;
			pendingSnapshot?.patches.push(patch);
			if (!applyDownloadPatch(patch) && !pendingSnapshot) actions.fetchDownloadList().subscribe();
		},
		previewDownload(request: DownloadPreviewRequest): Observable<DownloadPreviewContainerDTO | null> {
			return downloadApi.getDownloadPreviewEndpoint(request).pipe(map((response) => {
				if (response && response.isSuccess) {
					return response.value ?? null;
				}
				return null;
			}));
		},
		downloadMedia(request: CreateDownloadTasksRequest): void {
			downloadApi
				.createDownloadTasksEndpoint(request)
				.pipe(tap((result) => {
					const message = translateDownloadNotification(result, request);
					if (message === null) {
						return;
					}

					Notify.create({
						type: 'positive',
						message: message,
						progress: true,
						timeout: 3500,
						icon: 'mdi-download',
						position: 'top',
						actions: [{
							icon: 'mdi-close', color: 'white', round: true, handler: () => {
							},
						}],
					});
				}), switchMap(() => actions.fetchDownloadList()))
				.subscribe();
		},
		setupSelection(serverId: number): void {
			const downloads = getters.getDownloadsByServerId(serverId);
			const getLeafs = (x) => {
				if (!x.children || !x.children.length) {
					return x;
				}
				return [x, flatMapDeep(x.children, getLeafs)];
			};

			const allSelection: IPTreeTableSelectionKeys = flatMapDeep(downloads, getLeafs).reduce((a, v) => ({
				...a, [v.id]: {
					checked: true, partialChecked: false,
				},
			}), {});
			state.selected.push({
				plexServerId: serverId,
				allSelection,
				maxSelectionCount: Object.keys(allSelection).length,
				selection: {},
			});
		},
		setAllSelectedDownloadTasks(serverId: number, value: boolean): void {
			let i = state.selected.findIndex((x) => x.plexServerId === serverId);
			if (i === -1) {
				actions.setupSelection(serverId);
				i = state.selected.length - 1;
			}
			const current = state.selected[i]!;
			state.selected.splice(i, 1, {
				...current, selection: value ? clone(current.allSelection) : {},
			});
		},
		updateSelectedDownloadTasks(serverId: number, selection: IPTreeTableSelectionKeys): void {
			let i = state.selected.findIndex((x) => x.plexServerId === serverId);
			if (i === -1) {
				actions.setupSelection(serverId);
				i = state.selected.length - 1;
			}
			const current = state.selected[i]!;
			state.selected.splice(i, 1, {
				plexServerId: serverId,
				allSelection: current.allSelection,
				maxSelectionCount: current.maxSelectionCount,
				selection,
			});
		},
		$reset() {
			pendingSnapshot = null;
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	const downloadNodeIndexCache = new WeakMap<DownloadProgressDTO[], Map<string, DownloadProgressDTO>>();

	function refreshAfterSuccessfulCommand(result: BaseResultDTO): Observable<BaseResultDTO> {
		return result.isSuccess ? actions.fetchDownloadList() : of(result);
	}

	function finishSnapshot(snapshot: PendingDownloadSnapshot, downloads: ServerDownloadProgressDTO[] | null, refreshUnknown: boolean): void {
		if (pendingSnapshot !== snapshot) return;
		pendingSnapshot = null;
		if (downloads !== null) state.serverDownloads = downloads;
		let missingNodes = false;
		for (const patch of snapshot.patches) {
			if (!applyDownloadPatch(patch)) missingNodes = true;
		}
		// Preserve the existing unknown-node refresh, but never retry a failed/cancelled snapshot.
		if (missingNodes && refreshUnknown) actions.fetchDownloadList().subscribe();
	}

	function applyDownloadPatch(patch: DownloadPatchMessagePackDTO): boolean {
		const server = state.serverDownloads.find((item) => item.id === patch.serverId);
		if (!server) return false;
		const downloads = server.downloads ?? [];
		if (patch.upserts.length === 0) {
			if (patch.deletedIds.length > 0) removeDeletedInPlace(downloads, new Set(patch.deletedIds));
			downloadNodeIndexCache.delete(downloads);
			return true;
		}
		const nodeIndex = getDownloadNodeIndex(downloads);
		const deletedNodeIds = getDeletedNodeIds(nodeIndex, patch.deletedIds);
		if (patch.upserts.some((upsert) => !nodeIndex.has(upsert.id) || deletedNodeIds.has(upsert.id))) return false;
		if (deletedNodeIds.size > 0) {
			removeDeletedInPlace(downloads, deletedNodeIds);
			for (const id of deletedNodeIds) nodeIndex.delete(id);
		}
		for (const upsert of patch.upserts) {
			const existing = nodeIndex.get(upsert.id)!;
			existing.status = upsert.status;
			existing.percentage = upsert.percentage;
			existing.dataReceived = upsert.dataReceived;
			existing.dataTotal = upsert.dataTotal;
			existing.downloadSpeed = upsert.downloadSpeed;
			existing.timeRemaining = upsert.timeRemaining;
		}
		return true;
	}

	function findNodeById(nodes: DownloadProgressDTO[], id: string): DownloadProgressDTO | null {
		for (const node of nodes) {
			if (node.id === id) return node;

			const childNode = findNodeById(node.children ?? [], id);
			if (childNode) return childNode;
		}

		return null;
	}

	function getDownloadNodeIndex(nodes: DownloadProgressDTO[]): Map<string, DownloadProgressDTO> {
		const cached = downloadNodeIndexCache.get(nodes);
		if (cached) return cached;

		const nodeIndex = new Map<string, DownloadProgressDTO>();
		function visit(items: DownloadProgressDTO[]): void {
			for (const item of items) {
				nodeIndex.set(item.id, item);
				visit(item.children ?? []);
			}
		}

		visit(nodes);
		downloadNodeIndexCache.set(nodes, nodeIndex);
		return nodeIndex;
	}

	function getDeletedNodeIds(nodeIndex: Map<string, DownloadProgressDTO>, deletedIds: string[]): Set<string> {
		const deletedNodeIds = new Set(deletedIds);

		function collectDescendantIds(node: DownloadProgressDTO): void {
			for (const child of node.children ?? []) {
				if (deletedNodeIds.has(child.id)) continue;
				deletedNodeIds.add(child.id);
				collectDescendantIds(child);
			}
		}

		for (const deletedId of deletedIds) {
			const node = nodeIndex.get(deletedId);
			if (node) collectDescendantIds(node);
		}

		return deletedNodeIds;
	}

	function removeDeletedInPlace(nodes: DownloadProgressDTO[], deletedNodeIds: ReadonlySet<string>): void {
		for (let index = nodes.length - 1; index >= 0; index--) {
			const node = nodes[index]!;
			if (deletedNodeIds.has(node.id)) {
				nodes.splice(index, 1);
				continue;
			}

			removeDeletedInPlace(node.children ?? [], deletedNodeIds);
		}
	}

	// Getters
	const getters = {
		getDownloadsByServerId: (serverId = 0): DownloadProgressDTO[] => {
			if (serverId === 0) {
				return state.serverDownloads.flatMap((x) => x.downloads);
			}
			return state.serverDownloads.find((server) => server.id === serverId)?.downloads ?? [];
		},
		getDownloadTaskById(downloadTaskId: string): DownloadProgressDTO | null {
			return findNodeById(state.serverDownloads.flatMap((x) => x.downloads), downloadTaskId);
		},
		getServersWithDownloads: computed((): { plexServer: PlexServerDTO; downloads: DownloadProgressDTO[] }[] => {
			const serverIds = state.serverDownloads.map((x) => x.id);
			const plexServersWithDownloads = serverStore.servers.filter((x) => serverIds.includes(x.id));

			return plexServersWithDownloads.map((x) => {
				return {
					plexServer: x, downloads: getters.getDownloadsByServerId(x.id),
				};
			}).filter((x) => x.downloads.length > 0);
		}),
		getActiveDownloadList(serverId = 0): DownloadProgressDTO[] {
			const active: DownloadProgressDTO[] = [];
			function collectActiveLeaves(node: DownloadProgressDTO): void {
				if (node.children?.length) {
					for (const child of node.children)
						collectActiveLeaves(child);
				} else if (node.status !== DownloadStatus.Completed && node.status !== DownloadStatus.Error) {
					active.push(node);
				}
			}
			for (const root of getters.getDownloadsByServerId(serverId))
				collectActiveLeaves(root);
			return active;
		},
		/**
         * Get the total number of download tasks that are downloadable in the download list.
         */
		getTotalDownloadsCount: computed((): number => {
			return sum(state.serverDownloads.flatMap((x) => x.downloadableTasksCount));
		}),
		hasSelected: computed(() => state.selected.some((x) => Object.keys(x?.selection ?? {}).length > 0)),
		getSelectedDownloadTasks(serverId: number): IPTreeTableSelectionKeys {
			return getters.getDownloadSelection(serverId)?.selection ?? {};
		},
		getDownloadSelection(serverId: number): IDownloadsSelection | null {
			return state.selected.find((x) => x.plexServerId === serverId) ?? null;
		},
		getHeaderSelection(serverId: number): boolean | null {
			const downloadSelection = getters.getDownloadSelection(serverId);
			const selectionLength = Object.keys(downloadSelection?.selection ?? {}).length;
			if (selectionLength === 0) {
				return false;
			}

			return downloadSelection?.maxSelectionCount === selectionLength ? true : null;
		},
	};

	return {
		...toRefs(state), ...actions, ...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useDownloadStore, import.meta.hot));
}
