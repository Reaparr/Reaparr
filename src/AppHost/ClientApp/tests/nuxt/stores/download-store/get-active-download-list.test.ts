import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { DownloadStatus, PlexMediaType, type DownloadProgressDTO } from '@dto';
import { useDownloadStore } from '@store';

function node(id: string, mediaType: PlexMediaType, children: DownloadProgressDTO[] = [], status = DownloadStatus.Queued): DownloadProgressDTO {
	return {
		id, mediaType, children, status, title: id,
		dataReceived: 0, dataTotal: 100, downloadSpeed: 0, percentage: 0, timeRemaining: 0,
	};
}

function hierarchy(types: PlexMediaType[], file: DownloadProgressDTO): DownloadProgressDTO {
	return types.reduceRight((child, type) => node(`${file.id}-${type}`, type, [child]), file);
}

describe('DownloadStore.getActiveDownloadList()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should count active files at every family depth without counting ancestors or terminal files', async () => {
		// Arrange
		const movie = node('movie-file', PlexMediaType.Movie);
		const episode = node('episode-file', PlexMediaType.Episode, [], DownloadStatus.Downloading);
		const track = node('track-file', PlexMediaType.MusicTrack, [], DownloadStatus.Paused);
		const photo = node('photo-file', PlexMediaType.PhotoImage);
		const nestedPhoto = node('nested-photo-file', PlexMediaType.PhotoImage);
		const video = node('video-file', PlexMediaType.OtherVideos);
		const downloads = [
			hierarchy([PlexMediaType.Movie], movie),
			hierarchy([PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode], episode),
			hierarchy([PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack], track),
			hierarchy([PlexMediaType.PhotoImage], photo),
			hierarchy([PlexMediaType.PhotoAlbum, PlexMediaType.PhotoAlbum, PlexMediaType.PhotoImage], nestedPhoto),
			hierarchy([PlexMediaType.OtherVideos], video),
			hierarchy([PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack], node('completed', PlexMediaType.MusicTrack, [], DownloadStatus.Completed)),
			hierarchy([PlexMediaType.PhotoAlbum, PlexMediaType.PhotoImage], node('error', PlexMediaType.PhotoImage, [], DownloadStatus.Error)),
		];
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO([
			{ id: 1, downloadableTasksCount: 8, downloads },
			{ id: 2, downloadableTasksCount: 1, downloads: [node('second-server-file', PlexMediaType.OtherVideos)] },
		]));
		const store = useDownloadStore();

		// Act
		await subscribeSpyTo(store.setup()).onComplete();
		const firstServer = store.getActiveDownloadList(1);
		const allServers = store.getActiveDownloadList();

		// Assert
		expect(firstServer).toEqual([movie, episode, track, photo, nestedPhoto, video]);
		expect(allServers.map((entry) => entry.id)).toEqual([...firstServer.map((entry) => entry.id), 'second-server-file']);
		expect(store.getActiveDownloadList(99)).toEqual([]);
	});
});
