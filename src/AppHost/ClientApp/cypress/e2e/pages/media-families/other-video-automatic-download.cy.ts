import { headers, route } from '@fixtures';
import { generatePlexMedia, generateResultDTO, generateSupportedSorts } from '@mock';
import { PlexLibraryPaths, PlexMediaPaths } from '@api/api-paths';
import { AudioQuality, PlexMediaType, VideoQuality } from '@dto';

it('keeps multipart Other Video root downloads automatic instead of forcing the first version', () => {
	// Arrange
	cy.viewport(1280, 800);
	cy.basePageSetup({
		seed: 625,
		plexServerCount: 1,
		plexMovieLibraryCount: 1,
		plexTvShowLibraryCount: 0,
		movieCount: 0,
		tvShowCount: 0,
		movieDownloadTask: 0,
		tvShowDownloadTask: 0,
		firstTimeSetup: false,
		override: {
			plexLibraries: (libraries) => libraries.map((library) => ({
				...library,
				id: 45,
				count: 1,
			})),
		},
	}).then(({ config, plexLibraries }) => {
		const library = { ...plexLibraries[0]!, type: PlexMediaType.OtherVideos };
		const media = generatePlexMedia({
			config,
			partialData: {
				id: 100,
				type: PlexMediaType.OtherVideos,
				title: 'Multipart recording',
				plexLibraryId: library.id,
				plexServerId: library.plexServerId,
				hasThumb: false,
				hasArt: false,
				children: [],
				mediaSize: 3072,
				mediaData: [
					{ id: 10, plexApiMediaId: 1000, plexApiPartId: 1010, fileName: 'recording-part-1.mp4', duration: 6000, size: 1024, videoQuality: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac', audioQuality: AudioQuality.None },
					{ id: 11, plexApiMediaId: 1000, plexApiPartId: 1011, fileName: 'recording-part-2.mp4', duration: 6000, size: 1024, videoQuality: VideoQuality.FullHD, videoCodec: 'h264', audioCodec: 'aac', audioQuality: AudioQuality.None },
					{ id: 30, plexApiMediaId: 2000, plexApiPartId: 2030, fileName: 'recording-alternative.mkv', duration: 12000, size: 1024, videoQuality: VideoQuality.UHD_4K, videoCodec: 'hevc', audioCodec: 'aac', audioQuality: AudioQuality.None },
				],
				qualities: [
					{ mediaId: 100, dataId: 10, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.FullHD, audioQuality: AudioQuality.None },
					{ mediaId: 100, dataId: 30, mediaDataType: PlexMediaType.OtherVideos, videoQuality: VideoQuality.UHD_4K, audioQuality: AudioQuality.None },
				],
			},
		});
		const expectedCommands = media.mediaData.map((part) => ({
			mediaIds: [media.id],
			type: PlexMediaType.OtherVideos,
			plexLibraryId: library.id,
			plexServerId: library.plexServerId,
			qualities: [{ mediaId: media.id, dataId: part.id, mediaDataType: PlexMediaType.OtherVideos, videoQuality: part.videoQuality, audioQuality: AudioQuality.None }],
			selectionKind: null,
			keepCompletedInDownloadFolder: false,
		}));
		const metadata = { countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0, qualities: [], qualityCount: 0, mediaCount: 1, supportedSorts: generateSupportedSorts(library.type) };
		cy.intercept('GET', PlexLibraryPaths.getAllPlexLibrariesEndpoint(), {
			statusCode: 200, body: generateResultDTO([library]), ...headers,
		});
		cy.intercept('GET', PlexLibraryPaths.getPlexLibraryByIdEndpoint(library.id), {
			statusCode: 200, body: generateResultDTO(library), ...headers,
		});
		cy.intercept('GET', PlexLibraryPaths.getLibraryMediaMetadata(library.id, { mediaType: library.type }), {
			statusCode: 200, body: generateResultDTO(metadata), ...headers,
		});
		cy.intercept('GET', PlexLibraryPaths.getMetadataFilter(library.id, { mediaType: library.type }), {
			statusCode: 200, body: generateResultDTO({ countries: [], genres: [], roles: [], qualities: [] }), ...headers,
		});
		cy.intercept('GET', PlexMediaPaths.getMediaDetailByIdEndpoint(media.id, { type: media.type }), {
			statusCode: 200, body: generateResultDTO(media), ...headers,
		}).as('videoDetail');
		cy.intercept('POST', '**/api/Download/preview', {
			statusCode: 200,
			body: generateResultDTO({
				previews: [{ key: 'preview-100', title: media.title, type: media.type, size: media.mediaSize, qualities: [], children: [] }],
				totalSize: media.mediaSize,
				expanded: { 'preview-100': true },
			}),
			...headers,
		}).as('automaticPreview');

		// Act
		cy.visit(route(`/other-videos/${library.id}/details/${media.id}`));
		cy.wait('@videoDetail');
		cy.getCy('other-video-root-checkbox').should('be.visible').click();
		cy.getCy('media-overview-bar-download-button').filter(':visible').click();

		// Assert
		cy.wait('@automaticPreview').its('request.body').should('deep.equal', { downloadMedias: expectedCommands, customDestinationFolderPath: '' });
		cy.getCy('download-confirmation-dialog').should('be.visible').and('contain.text', media.title);
	});
});
