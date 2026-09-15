import MockAdapter from 'axios-mock-adapter';
import axios from 'axios';

// Install a global axios mock before any tests run
const mock = new MockAdapter(axios, { onNoMatch: 'throwException' });

// Helper response builders
const ok = <T>(value: T) => ({
	isSuccess: true, errors: [], successes: [], statusCode: 200, value,
});

// Match either relative or fully-qualified URLs
const authStatusMatcher = /\/api\/Authentication\/status$/;
const backgroundJobsMatcher = /\/api\/BackgroundJobs$/;
const plexAccountMatcher = /\/api\/PlexAccount$/;
const downloadMatcher = /\/api\/Download$/;
const folderPathMatcher = /\/api\/FolderPath$/;
const integrationMatcher = /\/api\/Integration$/;
const plexLibraryMatcher = /\/api\/PlexLibrary$/;
const plexLibrarySyncStatusMatcher = /\/api\/PlexLibrary\/sync-status$/;
const notificationMatcher = /\/api\/Notification$/;
const plexServerConnectionMatcher = /\/api\/PlexServerConnection$/;
const plexServerMatcher = /\/api\/PlexServer$/;
const settingsMatcher = /\/api\/Settings$/;
const debugLogsMatcher = /\/api\/Debug\/logs$/;
const updateCheckMatcher = /\/api\/Update\/Check$/;

// Default: user is authenticated
mock.onGet(authStatusMatcher).reply(200, ok({
	claims: [], isLoggedIn: true, userName: 'test-user',
}));

// Quiet background jobs polling in setup flows
mock.onGet(backgroundJobsMatcher).reply(200, ok([]));

// Default empty collections for common setup fetches
mock.onGet(plexAccountMatcher).reply(200, ok([]));
mock.onGet(downloadMatcher).reply(200, ok([]));
mock.onGet(folderPathMatcher).reply(200, ok([]));
mock.onGet(integrationMatcher).reply(200, ok([]));
mock.onGet(plexLibraryMatcher).reply(200, ok([]));
mock.onGet(plexLibrarySyncStatusMatcher).reply(200, ok([]));
mock.onGet(notificationMatcher).reply(200, ok([]));
mock.onGet(plexServerConnectionMatcher).reply(200, ok([]));
mock.onGet(plexServerMatcher).reply(200, ok([]));

// Default settings model for initial store setup
mock.onGet(debugLogsMatcher).reply(200, ok([]));
mock.onGet(updateCheckMatcher).reply(200, ok({ updateAvailable: false }));

mock.onGet(settingsMatcher).reply(200, ok({
	generalSettings: {
		activeAccountId: 0,
		firstTimeSetup: true,
		disableAnimatedBackground: false,
		hideMediaFromOfflineServers: false,
		hideMediaFromOwnedServers: false,
		useLowQualityPosterImages: false,
		hasBeenInvitedToDiscord: false,
		hasAgreedToDisclaimer: false,
	},
	debugSettings: {
		debugModeEnabled: false,
		maskAccountNames: false,
		maskLibraryNames: false,
		maskServerNames: false,
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
		movieViewMode: 0,
		tvShowViewMode: 0,
		allOverviewViewMode: 1,
	},
	downloadManagerSettings: { downloadSegments: 4, keepCompletedInDownloadFolder: false },
	languageSettings: { language: 'en-US' },
	serverSettings: { data: [] },
	networkSettings: {
		reverseProxyUrl: '',
		basePath: '',
		trustProxyHeaders: false,
		allowedProxyIps: [],
		forwardedHostHeader: '',
		forwardedPathHeader: '',
	},
}));

export {}; // ensure this file is treated as a module
