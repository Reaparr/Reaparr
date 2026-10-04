import Log from 'consola';
import { defineStore, acceptHMRUpdate } from 'pinia';
import { of, Subject, type Observable, type Subscription } from 'rxjs';
import { debounceTime, switchMap, tap, map, catchError } from 'rxjs/operators';
import { reactive, computed, toRefs } from 'vue';
import { PlexMediaType, type SettingsModelDTO, ViewMode } from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { settingsApi } from '@api';
import { cloneDeep } from 'lodash-es';

export const useSettingsStore = defineStore(StoreNames.SettingsStore, () => {
	// State
	const defaultState: SettingsModelDTO = {
		generalSettings: {
			activeAccountId: 0, firstTimeSetup: true, disableAnimatedBackground: false,
			hideMediaFromOfflineServers: false, hideMediaFromOwnedServers: false,
			useLowQualityPosterImages: false, hasBeenInvitedToDiscord: false, hasAgreedToDisclaimer: false,
		},
		debugSettings: {
			debugModeEnabled: false,
			maskLibraryNames: false,
			maskServerNames: false,
			maskAccountNames: false,
		},
		confirmationSettings: {
			askDownloadEpisodeConfirmation: true,
			askDownloadMovieConfirmation: true,
			askDownloadSeasonConfirmation: true,
			askDownloadTvShowConfirmation: true,
		},
		dateTimeSettings: {
			longDateFormat: 'EEEE, dd MMMM yyyy',
			shortDateFormat: 'dd/MM/yyyy',
			showRelativeDates: false,
			timeFormat: 'HH:mm:ss',
			timeZone: 'UTC',
		},
		displaySettings: {
			movieViewMode: ViewMode.Poster, tvShowViewMode: ViewMode.Poster, allOverviewViewMode: PlexMediaType.TvShow,
		},
		downloadManagerSettings: {
			downloadSegments: 4, keepCompletedInDownloadFolder: false, downloadSchedule: { enabled: false, days: {} },
		},
		languageSettings: { language: 'en-US' },
		serverSettings: {
			data: [],
		},
		networkSettings: {
			reverseProxyUrl: '',
			basePath: '',
			trustProxyHeaders: false,
			allowedProxyIps: [],
			forwardedHostHeader: '',
			forwardedPathHeader: '',
		},
	};
	const state = reactive<SettingsModelDTO>(cloneDeep(defaultState));
	let settingsUpdated = new Subject<SettingsModelDTO>();
	let settingsUpdatedSubscription: Subscription | null = null;
	let unsubscribeStoreChanges: (() => void) | null = null;

	// Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			return actions.refreshSettings().pipe(
				tap((settings) => {
					if (!settings) return;
					initializeAutoSave();
				}),
				map((settings) => ({
					name: StoreNames.SettingsStore,
					isSuccess: settings !== null,
				})),
				catchError((error) => {
					Log.error('Failed to setup settings store', error);
					return of({ name: StoreNames.SettingsStore, isSuccess: false });
				}),
			);
		},
		refreshSettings(): Observable<SettingsModelDTO | null> {
			return settingsApi.getUserSettingsEndpoint().pipe(switchMap((settingsResult) => of(settingsResult?.value ?? null)), tap((settings) => {
				if (settings) {
					actions.setSettingsState(settings);
				}
			}));
		},
		saveSettings: (): Observable<SettingsModelDTO | null> => settingsApi.updateUserSettingsEndpoint(state).pipe(switchMap((settingsResult) => of(settingsResult?.value ?? null)), tap((settings) => {
			if (settings) {
				actions.setSettingsState(settings);
			}
		})),
		setSettingsState(settings: SettingsModelDTO) {
			// Special handling to preserve reactivity of nested objects and arrays
			Object.assign(state.generalSettings, settings.generalSettings);
			Object.assign(state.debugSettings, settings.debugSettings);
			Object.assign(state.confirmationSettings, settings.confirmationSettings);
			Object.assign(state.dateTimeSettings, settings.dateTimeSettings);
			Object.assign(state.displaySettings, settings.displaySettings);
			Object.assign(state.downloadManagerSettings, settings.downloadManagerSettings);
			Object.assign(state.languageSettings, settings.languageSettings);

			// Preserve references held by consumers of these arrays.
			state.serverSettings.data.splice(0, state.serverSettings.data.length, ...settings.serverSettings.data);
			state.networkSettings.allowedProxyIps.splice(0, state.networkSettings.allowedProxyIps.length, ...settings.networkSettings.allowedProxyIps);
			Object.assign(state.networkSettings, {
				reverseProxyUrl: settings.networkSettings.reverseProxyUrl, basePath: settings.networkSettings.basePath,
				trustProxyHeaders: settings.networkSettings.trustProxyHeaders, forwardedHostHeader: settings.networkSettings.forwardedHostHeader,
				forwardedPathHeader: settings.networkSettings.forwardedPathHeader,
			});
		},
		updateDownloadLimit(machineIdentifier: string, downloadLimit: number) {
			const index = state.serverSettings.data.findIndex((server) => server.machineIdentifier === machineIdentifier);
			if (index > -1) state.serverSettings.data.splice(index, 1, { ...state.serverSettings.data[index]!, downloadSpeedLimit: downloadLimit });
		},
		updateAllowStreamDownloader(machineIdentifier: string, allowStreamDownloader: boolean) {
			const index = state.serverSettings.data.findIndex((server) => server.machineIdentifier === machineIdentifier);
			if (index > -1) state.serverSettings.data.splice(index, 1, { ...state.serverSettings.data[index]!, allowStreamDownloader });
		},
		updateDisplayMode(type: PlexMediaType, viewMode: ViewMode) {
			switch (type) {
				case PlexMediaType.Movie:
					state.displaySettings.movieViewMode = viewMode;
					break;
				case PlexMediaType.TvShow:
					state.displaySettings.tvShowViewMode = viewMode;
					break;
				default: Log.error('Could not set view mode for type' + type);
			}
		},
		getServerSettings: (machineIdentifier?: string) => machineIdentifier ? state.serverSettings.data.find((user) => user.machineIdentifier === machineIdentifier) : null,
		isConfirmationEnabled: (type: PlexMediaType) => {
			switch (type) {
				case PlexMediaType.Movie: return state.confirmationSettings.askDownloadMovieConfirmation;
				case PlexMediaType.TvShow: return state.confirmationSettings.askDownloadTvShowConfirmation;
				case PlexMediaType.Season: return state.confirmationSettings.askDownloadSeasonConfirmation;
				case PlexMediaType.Episode: return state.confirmationSettings.askDownloadEpisodeConfirmation;
				default: return true;
			}
		},
		$reset() {
			tearDownAutoSave();
			actions.setSettingsState(cloneDeep(defaultState));
			initializeAutoSave();
		},
	};

	function initializeAutoSave() {
		if (settingsUpdatedSubscription || unsubscribeStoreChanges) {
			return;
		}

		settingsUpdatedSubscription = settingsUpdated.pipe(
			debounceTime(500),
			tap((settings) => Log.debug('Settings updated', settings)),
			switchMap((settings) => settingsApi.updateUserSettingsEndpoint(settings)),
		)
			.subscribe();

		unsubscribeStoreChanges = useSettingsStore().$subscribe((mutation, currentState) => {
			if (mutation.type) {
				settingsUpdated.next(currentState);
			}
		});
	}

	function tearDownAutoSave() {
		settingsUpdatedSubscription?.unsubscribe();
		settingsUpdatedSubscription = null;

		unsubscribeStoreChanges?.();
		unsubscribeStoreChanges = null;

		settingsUpdated.complete();
		settingsUpdated = new Subject<SettingsModelDTO>();
	}

	// Getters
	const getters = {
		confirmedDownloadSchedule: computed(() => state.downloadManagerSettings.downloadSchedule),
		debugMode: computed(() => state.debugSettings.debugModeEnabled),
		shouldMaskServerNames: computed(() => state.debugSettings.maskServerNames),
		shouldMaskLibraryNames: computed(() => state.debugSettings.maskLibraryNames),
		shouldMaskAccountNames: computed(() => state.debugSettings.maskAccountNames),
	};
	return { ...toRefs(state), ...actions, ...getters };
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useSettingsStore, import.meta.hot));
}
