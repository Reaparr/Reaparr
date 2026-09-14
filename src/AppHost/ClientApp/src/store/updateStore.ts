import { acceptHMRUpdate, defineStore } from 'pinia';
import { concatMap, finalize, of, type Observable, switchMap, tap } from 'rxjs';
import { updateApi } from '@api';
import type { ReleaseNoteDTO } from '@api/generated/data-contracts';
import { RefreshDataType } from '@api/generated/data-contracts';
import { StoreNames, type ISetupResult } from '@interfaces';
import { cloneDeep } from 'lodash-es';
import { useSignalrStore } from '@store';

interface IUpdateStoreState {
	updateCheckError: string | null;
	hasUpdateAvailable: boolean;
	releaseNotes: ReleaseNoteDTO[];
	downloadProgress: number;
	isDownloading: boolean;
	isApplyingUpdate: boolean;
}

export const useUpdateStore = defineStore(StoreNames.UpdateStore, () => {
	const defaultState: IUpdateStoreState = {
		updateCheckError: null,
		hasUpdateAvailable: false,
		releaseNotes: [],
		downloadProgress: 0,
		isDownloading: false,
		isApplyingUpdate: false,
	};

	const state = reactive<IUpdateStoreState>(cloneDeep(defaultState));
	const signalrStore = useSignalrStore();

	const actions = {
		setup(): Observable<ISetupResult> {
			signalrStore
				.getRefreshNotification(RefreshDataType.UpdateAvailable)
				.pipe(concatMap(() => actions.checkForUpdate()))
				.subscribe();

			signalrStore.getAppUpdateDownloadProgress().subscribe((data) => {
				state.downloadProgress = data.percentage;
			});

			return actions.checkForUpdate().pipe(switchMap(() => of({ name: StoreNames.UpdateStore, isSuccess: true })));
		},
		checkForUpdate() {
			return updateApi.checkForUpdateEndpoint().pipe(tap(({ isSuccess, value }) => {
				if (isSuccess && value) {
					state.hasUpdateAvailable = value.isUpdateAvailable;
					state.releaseNotes = value.releaseNotes;
				}
			}));
		},
		downloadUpdate() {
			state.isDownloading = true;
			state.downloadProgress = 0;
			return updateApi.downloadUpdateEndpoint().pipe(
				finalize(() => {
					state.isDownloading = false;
				}),
			);
		},
		applyUpdate() {
			state.isApplyingUpdate = true;
			return updateApi.applyUpdateEndpoint().pipe(
				finalize(() => {
					state.isApplyingUpdate = false;
				}),
			);
		},
		downloadAndApplyUpdate() {
			state.isDownloading = true;
			state.isApplyingUpdate = false;
			state.downloadProgress = 0;
			return updateApi.downloadUpdateEndpoint().pipe(
				tap(() => {
					state.isApplyingUpdate = true;
				}),
				switchMap(() => updateApi.applyUpdateEndpoint()),
				finalize(() => {
					state.isDownloading = false;
					state.isApplyingUpdate = false;
				}),
			);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	const getters = {
		isUpdateAvailable: computed(() => state.hasUpdateAvailable),
		releaseNotes: computed(() => state.releaseNotes),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useUpdateStore, import.meta.hot));
}
