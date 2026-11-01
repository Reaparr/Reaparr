import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { PhotoItemKind, PlexMediaType } from '@dto';
import { baseSetup } from '@services-test-base';
import { generatePlexMedia } from '@mock';
import { toDownloadMedia } from '@composables/conversion/download-actions.conversion';

function media(type: PlexMediaType) {
	return generatePlexMedia({
		config: { seed: 318 },
		partialData: { id: 42, plexServerId: 1, plexLibraryId: 2, type, qualities: [] },
	});
}

describe('download-actions.conversion', () => {
	beforeAll(() => baseSetup());
	beforeEach(() => setActivePinia(createPinia()));

	test('Should mark photo albums with the album selector kind', () => {
		const [selection] = toDownloadMedia(media(PlexMediaType.PhotoAlbum));

		expect(selection).toMatchObject({ type: PlexMediaType.PhotoAlbum, selectionKind: PhotoItemKind.Album });
	});

	test('Should mark direct photo assets with the asset selector kind', () => {
		const [selection] = toDownloadMedia(media(PlexMediaType.PhotoImage));

		expect(selection).toMatchObject({ type: PlexMediaType.PhotoImage, selectionKind: PhotoItemKind.Asset });
	});

	test('Should set photo selector metadata to null for non-photo media', () => {
		const [selection] = toDownloadMedia(media(PlexMediaType.Movie));

		expect(selection).toHaveProperty('selectionKind', null);
	});
});
