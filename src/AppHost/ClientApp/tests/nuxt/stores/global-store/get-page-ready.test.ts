import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import {
	AuthenticationPaths,
	DownloadPaths,
	FolderPathPaths,
	NotificationPaths,
	PlexAccountPaths,
	PlexLibraryPaths,
	PlexServerConnectionPaths,
	PlexServerPaths,
	SettingsPaths,
} from '@api/api-paths';
import { generatePlexServers, generateResultDTO, generateSettingsModel } from '@mock';
import { useGlobalStore } from '@store';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '~~/tests/_base/base';

describe('GlobalStore.getConfigReady()', () => {
	let { appConfig, mock, config } = baseVars();
	beforeAll(() => {
		const result = baseSetup();
		appConfig = result.appConfig;
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should return success and complete when setup is run', async () => {
		// Arrange
		config = {
			plexServerCount: 3,
		};
		const globalStore = useGlobalStore();

		mock.onGet(AuthenticationPaths.authenticationStatusEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(FolderPathPaths.getAllFolderPathsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getLibrarySyncStatusEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(NotificationPaths.getAllNotificationsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexServerPaths.getAllPlexServersEndpoint()).reply(200, generateResultDTO(generatePlexServers({ config })));
		mock.onGet(PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(generateSettingsModel({ config })));

		// Act
		const setupResult = subscribeSpyTo(globalStore.setupServices({ config: appConfig }));
		const pageSetupResult = subscribeSpyTo(globalStore.getPageSetupReady);
		await setupResult.onComplete();
		// Assert
		expect(setupResult.receivedComplete()).toEqual(true);
		expect(pageSetupResult.getValues()).toHaveLength(2);
		expect(pageSetupResult.getFirstValue()).toEqual(false);
		expect(pageSetupResult.getValues()[1]).toEqual(true);
	});
});
