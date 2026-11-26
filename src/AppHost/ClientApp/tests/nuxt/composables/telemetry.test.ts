import { describe, expect, test, vi } from 'vitest';
import { createTelemetry } from '@composables/telemetry/createTelemetry';
import { FrontendTelemetryAction as Action, FrontendTelemetryEvent as Event } from '@composables/telemetry/events';
import { DownloadActions, IntegrationType, PlexMediaType } from '@dto';
import type { CreateDownloadTasksRequest } from '@dto';

describe('Business telemetry facade', () => {
	test('Should select download identities without copying destination paths or request bodies', () => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });
		const request: CreateDownloadTasksRequest = {
			customDestinationFolderPath: '/credential-bearing/path',
			destinationFolderPathId: 12,
			downloadMedias: [{ mediaIds: [1, 2], plexLibraryId: 3, plexServerId: 4, type: PlexMediaType.Movie, qualities: [], keepCompletedInDownloadFolder: false, selectionKind: null }],
		};

		// Act
		telemetry.downloadRequested(request);
		telemetry.downloadPreviewRequested(request, PlexMediaType.Movie, true);

		// Assert
		expect(capture.mock.calls).toEqual([
			[Event.FrontendDownloadInteraction, { action: Action.Download, selection_count: 2, selection_group_count: 1, media_ids: [1, 2], media_types: [PlexMediaType.Movie], library_ids: [3], server_ids: [4] }],
			[Event.FrontendDownloadInteraction, { action: Action.Preview, media_type: PlexMediaType.Movie, selection_count: 2, destination_id: 12, is_custom_destination: false, is_retry: true }],
		]);
	});

	test('Should copy only authentication and integration intent fields from existing models', () => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });
		const account = { id: 7, isNewAccount: false, isAuthTokenMode: true, password: 'private-password', authenticationToken: 'private-token', verificationCode: '123456' };
		const integration = { id: '8', type: IntegrationType.Radarr, apiKey: 'private-api-key', url: 'https://user:password@example.test' };

		// Act
		telemetry.accountSaveRequested(account);
		telemetry.integrationSaveRequested(integration.type, integration);

		// Assert
		expect(capture.mock.calls).toEqual([
			[Event.FrontendAccountInteraction, { action: Action.Save, account_id: 7, is_new_account: false, is_auth_token_mode: true }],
			[Event.FrontendIntegrationInteraction, { action: Action.Update, integration_id: '8', integration_type: IntegrationType.Radarr, is_new_integration: false }],
		]);
	});

	test.each([
		PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode,
		PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack,
		PlexMediaType.PhotoAlbum, PlexMediaType.PhotoImage, PlexMediaType.OtherVideos,
	])('Should preserve exact submitted search and media identity for %s', (mediaType) => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });
		const search = '  "星際  旅行" café  ';
		// Act
		telemetry.mediaSearchChanged(search, 17, mediaType);
		// Assert
		expect(capture.mock.calls).toEqual([[Event.FrontendMediaInteraction, {
			action: Action.Search, library_id: 17, media_type: mediaType, search, has_query: true, query_length: search.length,
		}]]);
	});

	test('Should retain exact submitted search text and preserve selection intent', () => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });
		const media = { id: 1, type: PlexMediaType.MusicAlbum, plexLibraryId: 2, plexServerId: 3 };

		// Act
		telemetry.mediaSearchChanged('  "星際  旅行" café  ', 2, PlexMediaType.MusicAlbum);
		telemetry.mediaSearchChanged('   ', 2, PlexMediaType.MusicAlbum);
		telemetry.photoSearchChanged('  "夏の  写真" café  ', 2);
		telemetry.photoSearchChanged('', 2);
		telemetry.mediaComparisonSelectionChanged(media, 2, false);

		// Assert
		expect(capture.mock.calls).toEqual([
			[Event.FrontendMediaInteraction, { action: Action.Search, library_id: 2, media_type: PlexMediaType.MusicAlbum, search: '  "星際  旅行" café  ', has_query: true, query_length: '  "星際  旅行" café  '.length }],
			[Event.FrontendMediaInteraction, { action: Action.ClearSearch, library_id: 2, media_type: PlexMediaType.MusicAlbum, search: '   ', has_query: false, query_length: 3 }],
			[Event.FrontendPhotoInteraction, { action: Action.Search, library_id: 2, query: '  "夏の  写真" café  ', has_query: true, query_length: '  "夏の  写真" café  '.length }],
			[Event.FrontendPhotoInteraction, { action: Action.ClearSearch, library_id: 2, query: '', has_query: false, query_length: 0 }],
			[Event.FrontendMediaInteraction, { action: Action.ClearSelection, media_id: 1, media_type: PlexMediaType.MusicAlbum, library_id: 2, server_id: 3, selection_count: 2 }],
		]);
	});

	test('Should map business download commands without generating events for unrelated commands', () => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });

		// Act
		telemetry.downloadCommandRequested(DownloadActions.Clear, undefined, []);
		telemetry.downloadCommandRequested(DownloadActions.Details, 4, ['task']);

		// Assert
		expect(capture).toHaveBeenCalledExactlyOnceWith(Event.FrontendDownloadInteraction, { action: Action.ClearCompleted, server_id: undefined, download_task_ids: [], selection_count: 0 });
	});

	test.each([
		['https://user:password@github.com/Reaparr/Reaparr/releases?token=private#secret', 'Releases'],
		['https://example.test/private?token=private', 'Other'],
		['not a valid URL', 'Other'],
	])('Should classify external navigation without transmitting its URL: %s', (url, category) => {
		// Arrange
		const capture = vi.fn();
		const telemetry = createTelemetry({ capture });

		// Act
		telemetry.externalLinkOpened(url);

		// Assert
		expect(capture).toHaveBeenCalledExactlyOnceWith(Event.FrontendNavigationInteraction, { action: Action.Open, link_category: category });
	});
});
