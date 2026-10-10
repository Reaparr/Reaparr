import { describe, expect, test } from 'vitest';
import Convert from '@class/Convert';
import { DownloadTaskType, FolderType, PlexMediaType } from '@dto';

const mediaTypes = [
	[PlexMediaType.Movie, DownloadTaskType.Movie, FolderType.MovieFolder],
	[PlexMediaType.TvShow, DownloadTaskType.TvShow, FolderType.TvShowFolder],
	[PlexMediaType.Season, DownloadTaskType.Season, FolderType.TvShowFolder],
	[PlexMediaType.Episode, DownloadTaskType.Episode, FolderType.TvShowFolder],
	[PlexMediaType.MusicArtist, DownloadTaskType.MusicArtist, FolderType.MusicFolder],
	[PlexMediaType.MusicAlbum, DownloadTaskType.MusicAlbum, FolderType.MusicFolder],
	[PlexMediaType.MusicTrack, DownloadTaskType.MusicTrack, FolderType.MusicFolder],
	[PlexMediaType.PhotoAlbum, DownloadTaskType.PhotoAlbum, FolderType.PhotosFolder],
	[PlexMediaType.PhotoImage, DownloadTaskType.PhotoImage, FolderType.PhotosFolder],
	[PlexMediaType.OtherVideos, DownloadTaskType.OtherVideo, FolderType.OtherVideosFolder],
] as const;

describe('Convert media identities', () => {
	test.each(mediaTypes)('Should retain %s identity and destination family', (mediaType, taskType, folderType) => {
		// Act
		const convertedTaskType = Convert.toDownloadTaskType(mediaType);
		const convertedMediaType = Convert.toPlexMediaType(taskType);
		const convertedFolderType = Convert.mediaTypeToFolderType(mediaType);

		// Assert
		expect(convertedTaskType).toBe(taskType);
		expect(convertedMediaType).toBe(mediaType);
		expect(convertedFolderType).toBe(folderType);
	});

	test.each([
		[DownloadTaskType.MusicTrackData, PlexMediaType.MusicTrack],
		[DownloadTaskType.MusicTrackPart, PlexMediaType.MusicTrack],
		[DownloadTaskType.PhotoData, PlexMediaType.PhotoImage],
		[DownloadTaskType.PhotoPart, PlexMediaType.PhotoImage],
		[DownloadTaskType.OtherVideoData, PlexMediaType.OtherVideos],
		[DownloadTaskType.OtherVideoPart, PlexMediaType.OtherVideos],
	])('Should retain the source media family for %s file tasks', (taskType, mediaType) => {
		// Act
		const convertedMediaType = Convert.toPlexMediaType(taskType);

		// Assert
		expect(convertedMediaType).toBe(mediaType);
	});

	test.each([PlexMediaType.None, PlexMediaType.Unknown, PlexMediaType.Games])(
		'Should reject %s as a downloadable source', (mediaType) => {
			// Act
			const taskType = Convert.toDownloadTaskType(mediaType);

			// Assert
			expect(taskType).toBe(DownloadTaskType.None);
		},
	);

	test('Should not invent a media identity for an unsupported task', () => {
		// Act
		const mediaType = Convert.toPlexMediaType(DownloadTaskType.None);

		// Assert
		expect(mediaType).toBe(PlexMediaType.Unknown);
	});
});
