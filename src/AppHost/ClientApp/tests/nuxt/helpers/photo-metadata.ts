import { of } from 'rxjs';
import { vi } from 'vitest';
import { plexLibraryApi } from '@api';
import { generateResultDTO, generateSupportedSorts } from '@mock';
import { PhotoItemKind, PlexMediaType, SortType } from '@dto';

export function mockPhotoMetadata() {
	return vi.spyOn(plexLibraryApi, 'getLibraryMediaMetadata').mockImplementation((_libraryId, query) => of(generateResultDTO({
		countries: [],
		countryCount: 0,
		genres: [],
		genreCount: 0,
		roles: [],
		roleCount: 0,
		qualities: [],
		qualityCount: 0,
		mediaCount: 0,
		supportedSorts: query.photoView === PhotoItemKind.Folder
			? [SortType.Title]
			: generateSupportedSorts(PlexMediaType.PhotoAlbum),
	})));
}
