import { beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { PhotoItemKind, PlexMediaType } from '@dto';
import { generatePlexMedia } from '@mock';
import { toDownloadMedia } from '@composables/conversion/download-actions.conversion';

function media(type: PlexMediaType) {
	return generatePlexMedia({
		config: { seed: 677 },
		partialData: {
			id: 17,
			type,
			plexServerId: 3,
			plexLibraryId: 4,
			qualities: [],
		},
	});
}

const nonPhotoTypes = [
	PlexMediaType.None,
	PlexMediaType.Movie,
	PlexMediaType.TvShow,
	PlexMediaType.Season,
	PlexMediaType.Episode,
	PlexMediaType.MusicArtist,
	PlexMediaType.MusicAlbum,
	PlexMediaType.MusicTrack,
	PlexMediaType.OtherVideos,
	PlexMediaType.Games,
	PlexMediaType.Unknown,
] as const;

describe('toDownloadMedia()', () => {
	beforeEach(() => {
		setActivePinia(createPinia());
	});

	test.each([
		[PlexMediaType.PhotoAlbum, PhotoItemKind.Album],
		[PlexMediaType.PhotoImage, PhotoItemKind.Asset],
	] as const)('Should encode %s selections as %s', (type, selectionKind) => {
		// Arrange
		const item = media(type);

		// Act
		const [command] = toDownloadMedia(item);

		// Assert
		expect(command).toEqual({
			mediaIds: [17],
			type,
			plexServerId: 3,
			plexLibraryId: 4,
			qualities: [],
			selectionKind,
			keepCompletedInDownloadFolder: false,
		});
	});

	test.each(nonPhotoTypes)('Should encode non-photo %s commands with a null photo selection kind', (type) => {
		// Arrange
		const item = media(type);

		// Act
		const [command] = toDownloadMedia(item);

		// Assert
		expect(command).toEqual({
			mediaIds: [17],
			type,
			plexServerId: 3,
			plexLibraryId: 4,
			qualities: [],
			selectionKind: null,
			keepCompletedInDownloadFolder: false,
		});
	});
});
