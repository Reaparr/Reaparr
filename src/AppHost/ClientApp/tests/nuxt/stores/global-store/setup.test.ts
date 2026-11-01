import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { generatePlexServers, generateResultDTO, generateSettingsModel } from '@mock';
import {
	DownloadPaths,
	FolderPathPaths,
	NotificationPaths,
	PlexAccountPaths,
	PlexLibraryPaths,
	PlexServerConnectionPaths,
	PlexServerPaths,
	SettingsPaths,
} from '@api/api-paths';
import { useGlobalStore } from '@store';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '~~/tests/_base/base';

describe('GlobalService.setup()', () => {
	let { appConfig, mock, config } = baseVars();

	beforeAll(() => {
		const result = baseSetup();
		appConfig = result.appConfig;
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should return success and complete when all setup services are run', async () => {
		// Arrange
		config = {
			plexServerCount: 3,
		};
		const globalStore = useGlobalStore();

		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(FolderPathPaths.getAllFolderPathsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexLibraryPaths.getLibrarySyncStatusEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(NotificationPaths.getAllNotificationsEndpoint()).reply(200, generateResultDTO([]));
		mock.onGet(PlexServerPaths.getAllPlexServersEndpoint()).reply(200, generateResultDTO(generatePlexServers({ config })));
		mock.onGet(SettingsPaths.getUserSettingsEndpoint()).reply(200, generateResultDTO(generateSettingsModel({ config })));
		mock.onGet(PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint()).reply(200, generateResultDTO([]));

		// Act
		const setupResult = subscribeSpyTo(globalStore.setupServices({ config: appConfig }));
		await setupResult.onComplete();

		// Assert
		expect(setupResult.receivedComplete()).toEqual(true);
	});
});
