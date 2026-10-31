import type { BasePageSetupResult } from '@fixtures';
import {
	type MockConfig,
	Seed,
	generatePlexLibrariesFromPlexServers,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { headers } from '@fixtures';
import { PlexLibraryPaths } from '@api/api-paths';
import { PhotoItemKind, PlexMediaType, SortType } from '@dto';

export function setupMockPlexLibraryMetaDataEndpoints(
	this: BasePageSetupResult,
	config: MockConfig,
): BasePageSetupResult {
	this.plexLibraries = generatePlexLibrariesFromPlexServers({ seed: new Seed(config.seed),
		plexServers: this.plexServers, config });

	if (config.override.plexLibraries) {
		this.plexLibraries = config.override.plexLibraries(this.plexLibraries);
	}

	// Detail call for every library
	for (const library of this.plexLibraries) {
		cy.intercept('GET', `**/api/PlexLibrary/${library.id}/metadata?*`, (request) => {
			if (library.type === PlexMediaType.PhotoAlbum) request.alias = 'photoLibraryMetadata';
			const isPhotoFolderView = library.type === PlexMediaType.PhotoAlbum
				&& String(request.query.photoView ?? '') === PhotoItemKind.Folder;
			request.reply({
				statusCode: 200,
				body: generateResultDTO({
					countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0,
					qualities: [], qualityCount: 0, mediaCount: 0,
					supportedSorts: isPhotoFolderView ? [SortType.Title] : generateSupportedSorts(library.type),
				}),
				...headers,
			});
		}).then(() => {
			if (config.debugDisplayData) {
				cy.log('BasePageSetup -> plexLibraries', this.plexLibraries);
			}
		});

		cy.intercept('GET', PlexLibraryPaths.getMetadataFilter(library.id, {
			mediaType: library.type,
		}), {
			statusCode: 200,
			body: generateResultDTO({ roles: [], countries: [], genres: [], qualities: [] }),
			...headers,
		});
	}

	// Aggregate Photos browsing uses the root library metadata profile.
	cy.intercept({
		method: 'GET',
		pathname: '/api/PlexLibrary/0/metadata',
		query: { mediaType: PlexMediaType.PhotoAlbum },
	}, (request) => {
		request.alias = 'photoLibraryMetadata';
		request.reply({
			statusCode: 200,
			body: generateResultDTO({
				countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0,
				qualities: [], qualityCount: 0, mediaCount: 0,
				supportedSorts: generateSupportedSorts(PlexMediaType.PhotoAlbum),
			}),
			...headers,
		});
	});

	return this;
}
