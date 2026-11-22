import { seed as falsoSeed } from '@ngneat/falso';
import type { MockConfig } from '@mock';
import { PlexMediaType } from '@dto';

let currentSeed = 0;

export function checkConfig(config: Partial<MockConfig> = {}): MockConfig {
	if (config === null || config === undefined) {
		return checkConfig({});
	}
	const defaultConfig: MockConfig = {
		isLoggedIn: true,
		seed: 1234,
		pageLoadDelay: 0,
		debugDisplayData: false,
		plexAccountCount: 1,
		firstTimeSetup: false,
		plexServerAccessCount: 3,
		plexServerStatusCount: 3,
		plexLibraryTypes: [PlexMediaType.Movie, PlexMediaType.TvShow],
		plexServerCount: 5,
		movieDownloadTask: 5,
		tvShowDownloadTask: 5,
		seasonDownloadTask: 5,
		episodeDownloadTask: 5,
		connectionHasProgress: false,
		setDownloadDetails: false,
		maxServerConnections: 5,
		plexMovieLibraryCount: 2,
		plexTvShowLibraryCount: 2,
		movieCount: 0,
		tvShowCount: 0,
		seasonCount: 0,
		episodeCount: 0,
		folderPathCount: 0,
		invalidDefaultFolderPaths: false,
		override: {
			plexServer: (plexServers) => plexServers,
			plexServerConnections: (plexServerConnections) => plexServerConnections,
			plexLibraries: (plexLibraries) => plexLibraries,
			plexAccounts: (plexAccounts) => plexAccounts,
			downloadTasks: (downloadTasks) => downloadTasks,
			folderPaths: (folderPaths) => folderPaths,
			settings: (settings) => settings,
		},
	};

	for (const key of Object.keys(defaultConfig) as (keyof MockConfig)[]) {
		if (!Object.hasOwn(config, key)) {
			Object.assign(config, { [key]: defaultConfig[key] });
		} else if (key === 'override') {
			config.override = { ...defaultConfig.override, ...config.override };
		}
	}

	setSeed(config?.seed ?? defaultConfig.seed);

	return config as MockConfig;
}

export function setSeed(seed: number) {
	currentSeed = seed;
	falsoSeed('' + seed);
}

export function incrementSeed(value = 1): void {
	falsoSeed('' + currentSeed + value);
}
