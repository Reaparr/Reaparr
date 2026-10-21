import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import type { Subject, Observable } from 'rxjs';
import { catchError, ReplaySubject, forkJoin, of } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';
import type IAppConfig from '@class/IAppConfig';
import { suspendApiRequests } from '@/types/api/base/ApiPipes';
import { StoreNames, type ISetupResult } from '@interfaces';
import {
	useAccountStore,
	useAccountDialogStore,
	useAlertStore,
	useBackgroundJobsStore,
	useDialogStore,
	useDownloadStore,
	useFolderPathStore,
	useHelpStore,
	useIntegrationStore,
	useLibraryStore,
	useLocalizationStore,
	useLogsStore,
	useMediaOverviewStore,
	useMediaStore,
	useNotificationsStore,
	usePhotoBrowsingStore,
	useServerConnectionStore,
	useServerStore,
	useSettingsStore,
	useSignalrStore,
	useUpdateStore,
	useAuthenticationStore,
} from '@store';
import { cloneDeep } from 'lodash-es';

interface IAppConfigStoreState {
	version: string;
	platform: string;
	config: IAppConfig;
	pageReadyObservable: Subject<boolean>;
}

export const useGlobalStore = defineStore(StoreNames.GlobalStore, () => {
	const defaultState: IAppConfigStoreState = {
		version: '?',
		platform: '?',
		config: {} as IAppConfig,
		pageReadyObservable: new ReplaySubject<boolean>(),
	};

	const state = reactive<IAppConfigStoreState>(cloneDeep(defaultState));
	let resetOperation: Promise<void> | null = null;

	async function resetSessionState(): Promise<void> {
		const resumeApiRequests = suspendApiRequests();
		try {
			useAuthenticationStore().$reset();
			try {
				await useSignalrStore().resetSession();
			} catch (error) {
				Log.error('Failed to stop SignalR connections during session reset', error);
			}
			useAccountDialogStore().$reset();
			useAccountStore().$reset();
			useAlertStore().$reset();
			useBackgroundJobsStore().$reset();
			useDialogStore().$reset();
			useDownloadStore().$reset();
			useFolderPathStore().$reset();
			useHelpStore().$reset();
			useLibraryStore().$reset();
			useLocalizationStore().$reset();
			useLogsStore().$reset();
			useMediaOverviewStore().$reset();
			useMediaStore().$reset();
			usePhotoBrowsingStore().$reset();
			useNotificationsStore().$reset();
			useServerConnectionStore().$reset();
			useServerStore().$reset();
			useSettingsStore().$reset();
			useUpdateStore().$reset();
			useIntegrationStore().$reset();
		} finally {
			resumeApiRequests();
			state.pageReadyObservable.next(true);
		}
	}

	const actions = {
		setupServices({ config }: { config: IAppConfig }): Observable<ISetupResult[]> {
			Log.info('Runtime Config is ready:', config);

			state.config = config;
			state.platform = config.platform;
			state.version = config.version;

			return actions.setup();
		},
		setup(): Observable<ISetupResult[]> {
			return useAuthenticationStore().setup().pipe(
				tap(() => state.pageReadyObservable.next(false)),
				switchMap((authResult: ISetupResult): Observable<ISetupResult[]> => {
					if (!authResult.isSuccess) {
						state.pageReadyObservable.next(true);
						return of([authResult]);
					}

					return actions.setupDependencies().pipe(
						tap(() => state.pageReadyObservable.next(true)),
						switchMap((results) => of([authResult, ...results])),
					);
				}),
				catchError((error) => {
					if (error === 'Unauthorized') {
						return of([{ name: StoreNames.PageSetup, isSuccess: true }]);
					}
					Log.error('Page Setup has failed:', error);
					return of([{ name: StoreNames.PageSetup, isSuccess: false }]);
				}),
			);
		},
		setupDependencies(): Observable<ISetupResult[]> {
			return forkJoin([
				useAccountStore().setup(),
				useAlertStore().setup(),
				useBackgroundJobsStore().setup(),
				useDialogStore().setup(),
				useDownloadStore().setup(),
				useFolderPathStore().setup(),
				useHelpStore().setup(),
				useLibraryStore().setup(),
				useLocalizationStore().setup(),
				useLogsStore().setup(),
				useMediaStore().setup(),
				useNotificationsStore().setup(),
				usePhotoBrowsingStore().setup(),
				useServerConnectionStore().setup(),
				useServerStore().setup(),
				useSettingsStore().setup(),
				useSignalrStore().setup(),
				useUpdateStore().setup(),
			]).pipe(
				switchMap((results) => {
					return useIntegrationStore().setup().pipe(
						switchMap((integrationResult) => of([...results, integrationResult])),
					);
				}),
			);
		},
		setAppVersion(version: string): void {
			if (!version || state.version === version) {
				return;
			}
			Log.info('Reaparr App Version:', version);
			state.version = version;
		},
		setAppPlatform(platform: string): void {
			if (!platform || state.platform === platform) {
				return;
			}
			Log.info('Reaparr App Platform:', platform);
			state.platform = platform;
		},
		resetSession(): Promise<void> {
			resetOperation ??= resetSessionState().finally(() => {
				resetOperation = null;
			});
			return resetOperation;
		},
	};
	const getters = {
		getPageSetupReady: computed((): Observable<boolean> => state.pageReadyObservable.asObservable()),
		isDockerMode: computed(() => state.platform === 'docker'),
		isDesktopMode: computed(() => state.platform === 'desktop'),
	};
	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useGlobalStore, import.meta.hot));
}
