import { checkConfig, generateResultDTO, type MockConfig } from '@mock';
import { type IBasePageSetupResult, BasePageSetupResult, headers } from '@fixtures';
import { IntegrationPaths } from '@api/generated/Integration';

export function basePageSetup(config: Partial<MockConfig> = {}): Cypress.Chainable<IBasePageSetupResult> {
	const validConfig = checkConfig(config);
	const result = new BasePageSetupResult();

	if (
		config.override === undefined
		|| !config.override.plexServer
		|| !config.override.plexServerConnections
		|| !config.override.plexLibraries
		|| !config.override.plexAccounts
		|| !config.override.downloadTasks
		|| !config.override.settings
	) {
		throw new Error('All override properties must be defined.');
	}

	// Authentication call
	result.setupAuthenticationEndpoints(validConfig);

	// PlexServers call
	result.setupPlexServersEndpoints(validConfig);

	// PlexServerConnections call
	result.setupPlexServerConnectionsEndpoints(validConfig);

	// PlexLibraries call
	result.setupPlexLibrariesEndpoints(validConfig);

	// PlexLibraryMetaData call
	result.setupMockPlexLibraryMetaDataEndpoints(validConfig);

	// PlexLibrarySyncJobStatus call
	result.setupMockPlexLibrarySyncJobStatusEndpoints(validConfig);

	// PlexAccount call
	result.setupPlexAccountsEndpoints(validConfig);

	// DownloadTasks call
	result.setupDownloadTasksEndpoints(validConfig);

	// Settings call
	result.setupSettingsEndpoints(validConfig);

	// PlexMedia call
	result.setupPlexMediaEndpoints(validConfig);

	// FolderPaths call
	result.setupFolderPathsEndpoints(validConfig);

	// Background Jobs call
	result.setupBackgroundJobsEndpoints(validConfig);

	// Debug logs call
	result.setupDebugEndpoints();

	// Update check call
	result.setupUpdateEndpoints();

	// SignalR call
	result.setupSignalREndpoints();

	// Notifications call
	result.setupNotificationsEndpoints();

	// Integration setup runs after the other startup requests complete.
	cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), {
		statusCode: 200,
		body: generateResultDTO([]),
		...headers,
	});

	// Calculate library media size and count
	for (const library of result.plexLibraries) {
		const mediaList = result.mediaData.find((x) => x.libraryId === library.id)?.media ?? [];
		if (mediaList.length) {
			library.mediaSize = mediaList.reduce((acc, x) => acc + x.mediaSize, 0);
			library.count = mediaList.length;
		}
	}

	return cy.wrap(result as IBasePageSetupResult);
}

export function route(path: string) {
	return `http://localhost:${Cypress.env('WEB_PORT')}` + path;
}
