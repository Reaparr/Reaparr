import { describe, beforeAll, test, expect } from 'vitest';
import { baseSetup } from '@services-test-base';
import { PlexMediaType, type CreateDownloadTasksRequest, type DownloadTaskCreationReportDTO } from '@dto';
import { generateResultDTO } from '@mock';
import { translateDownloadNotification } from '@composables';

const request = (type: PlexMediaType): CreateDownloadTasksRequest => ({
	customDestinationFolderPath: '',
	downloadMedias: [{
		keepCompletedInDownloadFolder: false,
		mediaIds: [1],
		plexLibraryId: 1,
		plexServerId: 1,
		qualities: [],
		type,
	}],
});

const familyReport = (counts: Partial<DownloadTaskCreationReportDTO>): DownloadTaskCreationReportDTO => ({
	movies: 0, tvShows: 0, seasons: 0, episodes: 0,
	musicArtists: 0, musicAlbums: 0, musicTracks: 0,
	photoAlbums: 0, photoImages: 0, otherVideos: 0, total: 0,
	...counts,
});

describe('translateDownloadNotification()', () => {
	beforeAll(() => {
		baseSetup();
	});

	test.each([
		[PlexMediaType.TvShow, { movies: 0, tvShows: 1, seasons: 1, episodes: 1 }, 'Downloading 1 TV show'],
		[PlexMediaType.Season, { movies: 0, tvShows: 0, seasons: 1, episodes: 1 }, 'Downloading 1 season'],
		[PlexMediaType.Episode, { movies: 0, tvShows: 0, seasons: 0, episodes: 1 }, 'Downloading 1 episode'],
	])('Should translate a single %s download', (type, counts, message) => {
		const report = generateResultDTO(counts as DownloadTaskCreationReportDTO);

		expect(translateDownloadNotification(report, request(type))).toBe(message);
	});

	test.each([
		[1, 1, 1, 1, 'Downloading 1 movie and 1 TV show with 1 season and 1 episode'],
		[1, 1, 1, 2, 'Downloading 1 movie and 1 TV show with 1 season and 2 episodes'],
		[1, 1, 2, 1, 'Downloading 1 movie and 1 TV show with 2 seasons and 1 episode'],
		[1, 1, 2, 2, 'Downloading 1 movie and 1 TV show with 2 seasons and 2 episodes'],
		[1, 2, 1, 1, 'Downloading 1 movie and 2 TV shows with 1 season and 1 episode'],
		[1, 2, 1, 2, 'Downloading 1 movie and 2 TV shows with 1 season and 2 episodes'],
		[1, 2, 2, 1, 'Downloading 1 movie and 2 TV shows with 2 seasons and 1 episode'],
		[1, 2, 2, 2, 'Downloading 1 movie and 2 TV shows with 2 seasons and 2 episodes'],
		[2, 1, 1, 1, 'Downloading 2 movies and 1 TV show with 1 season and 1 episode'],
		[2, 1, 1, 2, 'Downloading 2 movies and 1 TV show with 1 season and 2 episodes'],
		[2, 1, 2, 1, 'Downloading 2 movies and 1 TV show with 2 seasons and 1 episode'],
		[2, 1, 2, 2, 'Downloading 2 movies and 1 TV show with 2 seasons and 2 episodes'],
		[2, 2, 1, 1, 'Downloading 2 movies and 2 TV shows with 1 season and 1 episode'],
		[2, 2, 1, 2, 'Downloading 2 movies and 2 TV shows with 1 season and 2 episodes'],
		[2, 2, 2, 1, 'Downloading 2 movies and 2 TV shows with 2 seasons and 1 episode'],
		[2, 2, 2, 2, 'Downloading 2 movies and 2 TV shows with 2 seasons and 2 episodes'],
	])('Should preserve mixed download variant %s', (movies, tvShows, seasons, episodes, message) => {
		const report = generateResultDTO({ movies, tvShows, seasons, episodes } as DownloadTaskCreationReportDTO);

		expect(translateDownloadNotification(report, request(PlexMediaType.Movie))).toBe(message);
	});

	test.each([
		[1, 1, 1, 'Downloading 1 TV show with 1 season and 1 episode'],
		[1, 1, 2, 'Downloading 1 TV show with 1 season and 2 episodes'],
		[1, 2, 1, 'Downloading 1 TV show with 2 seasons and 1 episode'],
		[1, 2, 2, 'Downloading 1 TV show with 2 seasons and 2 episodes'],
		[2, 1, 1, 'Downloading 2 TV shows with 1 season and 1 episode'],
		[2, 1, 2, 'Downloading 2 TV shows with 1 season and 2 episodes'],
		[2, 2, 1, 'Downloading 2 TV shows with 2 seasons and 1 episode'],
		[2, 2, 2, 'Downloading 2 TV shows with 2 seasons and 2 episodes'],
	])('Should preserve TV detail variant %s', (tvShows, seasons, episodes, message) => {
		const report = generateResultDTO({ movies: 0, tvShows, seasons, episodes } as DownloadTaskCreationReportDTO);

		expect(translateDownloadNotification(report, request(PlexMediaType.None))).toBe(message);
	});

	test.each([
		[PlexMediaType.MusicArtist, 'musicArtists', 'music artist', 'music artists'],
		[PlexMediaType.MusicAlbum, 'musicAlbums', 'music album', 'music albums'],
		[PlexMediaType.MusicTrack, 'musicTracks', 'music track', 'music tracks'],
		[PlexMediaType.PhotoAlbum, 'photoAlbums', 'photo album', 'photo albums'],
		[PlexMediaType.PhotoImage, 'photoImages', 'photo', 'photos'],
		[PlexMediaType.OtherVideos, 'otherVideos', 'other video', 'other videos'],
	] as const)('Should translate singular and plural %s creation counts', (type, field, singular, plural) => {
		// Arrange
		const single = generateResultDTO(familyReport({ [field]: 1 }));
		const multiple = generateResultDTO(familyReport({ [field]: 2 }));
		const multipleRequest = request(type);
		multipleRequest.downloadMedias[0]!.mediaIds = [1, 2];

		// Act
		const singleMessage = translateDownloadNotification(single, request(type));
		const multipleMessage = translateDownloadNotification(multiple, multipleRequest);

		// Assert
		expect(singleMessage).toBe(`Downloading 1 ${singular}`);
		expect(multipleMessage).toBe(`Downloading 2 ${plural}`);
	});

	test.each([
		[PlexMediaType.MusicArtist, { musicArtists: 2, musicAlbums: 4, musicTracks: 20 }, 'Downloading 2 music artists'],
		[PlexMediaType.MusicAlbum, { musicAlbums: 2, musicTracks: 10 }, 'Downloading 2 music albums'],
		[PlexMediaType.PhotoAlbum, { photoAlbums: 2, photoImages: 10 }, 'Downloading 2 photo albums'],
	] as const)('Should count the selected %s hierarchy without inflating it with descendants', (type, counts, expected) => {
		// Arrange
		const report = generateResultDTO(familyReport(counts));
		const downloadRequest = request(type);
		downloadRequest.downloadMedias[0]!.mediaIds = [1, 2];

		// Act
		const message = translateDownloadNotification(report, downloadRequest);

		// Assert
		expect(message).toBe(expected);
	});

	test.each([
		[PlexMediaType.MusicAlbum, { musicArtists: 1, musicAlbums: 1, musicTracks: 10 }, 'Downloading 1 music album'],
		[PlexMediaType.MusicTrack, { musicArtists: 1, musicAlbums: 1, musicTracks: 1 }, 'Downloading 1 music track'],
		[PlexMediaType.PhotoImage, { photoAlbums: 1, photoImages: 1 }, 'Downloading 1 photo'],
	] as const)('Should preserve a single %s request intent when ancestors are reported', (type, counts, expected) => {
		// Arrange
		const report = generateResultDTO(familyReport(counts));

		// Act
		const message = translateDownloadNotification(report, request(type));

		// Assert
		expect(message).toBe(expected);
	});

	test.each([
		[PlexMediaType.MusicAlbum, { musicArtists: 1, musicAlbums: 2, musicTracks: 10 }, 'Downloading 2 music albums'],
		[PlexMediaType.MusicTrack, { musicArtists: 1, musicAlbums: 1, musicTracks: 2 }, 'Downloading 2 music tracks'],
		[PlexMediaType.PhotoImage, { photoAlbums: 1, photoImages: 2 }, 'Downloading 2 photos'],
	] as const)('Should preserve plural %s request intent when ancestors are reported', (type, counts, expected) => {
		// Arrange
		const report = generateResultDTO(familyReport(counts));
		const downloadRequest = request(type);
		downloadRequest.downloadMedias[0]!.mediaIds = [1, 2];

		// Act & Assert
		expect(translateDownloadNotification(report, downloadRequest)).toBe(expected);

		downloadRequest.downloadMedias = [
			{ ...downloadRequest.downloadMedias[0]!, mediaIds: [1] },
			{ ...downloadRequest.downloadMedias[0]!, mediaIds: [2] },
		];
		expect(translateDownloadNotification(report, downloadRequest)).toBe(expected);
	});
});
