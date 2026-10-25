import type { BasePageSetupResult } from '@fixtures';
import {
	type MockConfig,
	Seed,
	generatePlexLibrariesFromPlexServers,
	generateResultDTO,
	generateSupportedSorts,
} from '@mock';
import { headers } from '@fixtures';

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
		cy.intercept('GET', PlexLibraryPaths.getLibraryMediaMetadata(library.id, {
			mediaType: library.type,
		}), {
			statusCode: 200,
			body: generateResultDTO({ countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0, qualities: [], qualityCount: 0, mediaCount: 0, supportedSorts: generateSupportedSorts(library.type) }),
			...headers,
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

	return this;
}
