import { randDirectoryPath, randProductName } from '@ngneat/falso';
import { times } from 'lodash-es';
import type { FileSystemModelDTO, FolderPathDTO } from '@dto';
import { FileSystemEntityType, FolderType, PlexMediaType } from '@dto';
import Convert from '@class/Convert';
import { checkConfig, type MockConfig } from '~/mock-data';

let folderPathIdIndex = 1;

export function generateFolderPath({
	id,
	type,
	config = {},
	partialData = {},
}: {
	id: number;
	type: PlexMediaType;
	partialData?: Partial<FolderPathDTO>;
	config?: Partial<MockConfig>;
}): FolderPathDTO {
	checkConfig(config);

	return {
		id,
		directory: randDirectoryPath(),
		displayName: randProductName(),
		folderType: Convert.mediaTypeToFolderType(type),
		mediaType: type,
		isValid: true,
		isDefault: false,
		...partialData,
	};
}

export function generateFolderPaths({
	type,
	config = {},
	partialData = {},
}: {
	type: PlexMediaType;
	partialData?: Partial<FolderPathDTO>;
	config?: Partial<MockConfig>;
}): FolderPathDTO[] {
	const validConfig = checkConfig(config);
	return times(validConfig.folderPathCount, () => generateFolderPath({
		id: folderPathIdIndex++,
		type,
		partialData,
		config,
	}));
}

export function generateDefaultFolderPaths({ config = {} }: { config?: Partial<MockConfig> } = {}): FolderPathDTO[] {
	checkConfig(config);

	const defaultFolderPaths: FolderPathDTO[] = [];

	const mediaTypes: PlexMediaType[] = [
		PlexMediaType.None,
		PlexMediaType.Movie,
		PlexMediaType.TvShow,
		PlexMediaType.MusicArtist,
		PlexMediaType.PhotoAlbum,
		PlexMediaType.OtherVideos,
		PlexMediaType.Games,
		PlexMediaType.None,
		PlexMediaType.None,
		PlexMediaType.None,
	];

	const folderTypes: FolderType[] = [
		FolderType.DownloadFolder,
		FolderType.MovieFolder,
		FolderType.TvShowFolder,
		FolderType.MusicFolder,
		FolderType.PhotosFolder,
		FolderType.OtherVideosFolder,
		FolderType.GamesVideosFolder,
		FolderType.None,
		FolderType.None,
		FolderType.None,
	];

	const defaultFolderDirectories: string[] = [
		'/Downloads',
		'/Movies',
		'/TvShows',
		'/Music',
		'/Photos',
		'/Other',
		'/Games',
		'/',
		'/',
		'/',
	];

	for (let i = 0; i < 10; i++) {
		defaultFolderPaths.push(
			generateFolderPath({
				id: i + 1,
				type: mediaTypes[i] || PlexMediaType.Unknown,
				partialData: {
					folderType: folderTypes[i],
					isDefault: true,
					isValid: !config.invalidDefaultFolderPaths,
					directory: !config.invalidDefaultFolderPaths ? defaultFolderDirectories[i] : '/WRONG-PATH',
				},
				config,
			}),
		);
	}
	return defaultFolderPaths;
}

export function generateFileSystemModelDTO({
	path, partialData = {},
}: {
	path?: string;
	partialData?: Partial<FileSystemModelDTO>;
	config?: Partial<MockConfig>;
} = {}): FileSystemModelDTO {
	return {
		extension: '',
		hasReadPermission: true,
		hasWritePermission: true,
		lastModified: undefined,
		name: path || '',
		path: path || '',
		size: 0,
		type: FileSystemEntityType.Folder,
		...partialData,
	};
}
