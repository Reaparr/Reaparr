import { checkConfig, type MockConfig } from '@mock';
import type { PlexServerDTO, SettingsModelDTO } from '@dto';
import { PlexMediaType, ViewMode } from '@dto';

export function generateSettingsModel({
	plexServers = [],
	config = {},
	partialData = {},
}: {
	partialData?: Partial<SettingsModelDTO>;
	plexServers?: PlexServerDTO[];
	config?: Partial<MockConfig>;
}): SettingsModelDTO {
	const validConfig = checkConfig(config);

	return <SettingsModelDTO>{
		dateTimeSettings: {
			longDateFormat: 'EEEE, dd MMMM yyyy',
			shortDateFormat: 'dd/MM/yyyy',
			showRelativeDates: false,
			timeFormat: 'HH:mm:ss',
			timeZone: 'UTC',
		},
		debugSettings: {
			debugModeEnabled: false,
			maskAccountNames: false,
			maskLibraryNames: false,
			maskServerNames: false,
		},
		displaySettings: {
			movieViewMode: ViewMode.Poster,
			tvShowViewMode: ViewMode.Poster,
			allOverviewViewMode: PlexMediaType.Movie,
		},
		downloadManagerSettings: {
			downloadSegments: 4,
			keepCompletedInDownloadFolder: false,
		},
		languageSettings: {
			language: 'en-US',
		},
		generalSettings: {
			firstTimeSetup: validConfig.firstTimeSetup,
			activeAccountId: 0,
			disableAnimatedBackground: true,
			hideMediaFromOfflineServers: false,
			hideMediaFromOwnedServers: false,
			useLowQualityPosterImages: false,
			hasAgreedToDisclaimer: false,
			hasBeenInvitedToDiscord: false,
		},
		confirmationSettings: {
			askDownloadEpisodeConfirmation: true,
			askDownloadMovieConfirmation: true,
			askDownloadSeasonConfirmation: true,
			askDownloadTvShowConfirmation: true,
		},
		serverSettings: {
			data: plexServers.map((x) => {
				return {
					machineIdentifier: x.machineIdentifier,
					plexServerName: '',
					downloadSpeedLimit: 0,
					hidden: false,
				};
			}),
		},
		networkSettings: {
			reverseProxyUrl: '',
			basePath: '',
			trustProxyHeaders: false,
			allowedProxyIps: [],
			forwardedHostHeader: '',
			forwardedPathHeader: '',
		},
		...partialData,
	};
}
