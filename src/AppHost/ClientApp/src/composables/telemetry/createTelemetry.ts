import type { PostHog } from 'posthog-js';
import type { CreateDownloadTasksRequest, DownloadPreviewRequest, FolderPathDTO, PlexServerConnectionDTO, DownloadTaskDTO, PlexMediaType, IntegrationType, NotificationLevel, SortDirection, PlexMediaComparisonState } from '@dto';
import { PhotoItemKind, DownloadActions } from '@dto';
import { MediaMetaDataTypes, SetupPanelType } from '@enums';
import { FrontendTelemetryAction as Action, FrontendTelemetryEvent as Event, FrontendTelemetrySettingsModule as Module, FrontendTelemetryLink as Link } from '@composables/telemetry/events';
import type { ITelemetry, MediaIdentity, PhotoIdentity, IntegrationIdentity, AccountContext } from './ITelemetry';
import type { ISelection } from '@interfaces';
import type { IMediaOverviewSort } from '@composables/event-bus';

function mediaIdentity(media: MediaIdentity) {
	return { media_id: media.id, media_type: media.type, library_id: media.plexLibraryId, server_id: media.plexServerId };
}

function photoIdentity(asset: PhotoIdentity) {
	return { asset_id: asset.id, library_id: asset.plexLibraryId, server_id: asset.plexServerId };
}

function integrationIdentity(type: IntegrationType, integration?: IntegrationIdentity | null) {
	return { ...(integration ? { integration_id: integration.id } : {}), integration_type: type };
}

/** Selects analytics fields explicitly; request bodies and credentials never pass through to PostHog. */
export function createTelemetry(client?: Pick<PostHog, 'capture'>): ITelemetry {
	function capture(event: Event, properties: Record<string, unknown>) {
		client?.capture(event, properties);
	}

	function mediaFilterChanged(filterType: MediaMetaDataTypes, value: number, libraryId: number, mediaType: PlexMediaType) {
		capture(Event.FrontendMediaInteraction, { action: value > 0 ? Action.Filter : Action.RemoveFilter, filter_type: filterType, filter_id: value, library_id: libraryId, media_type: mediaType });
	}

	return {
		downloadRequested(request: CreateDownloadTasksRequest) {
			const mediaIds = request.downloadMedias.flatMap((media) => media.mediaIds);
			capture(Event.FrontendDownloadInteraction, { action: Action.Download, selection_count: mediaIds.length, selection_group_count: request.downloadMedias.length, media_ids: mediaIds, media_types: request.downloadMedias.map((media) => media.type), library_ids: request.downloadMedias.map((media) => media.plexLibraryId), server_ids: request.downloadMedias.map((media) => media.plexServerId) });
		},
		downloadCommandRequested(action: DownloadActions, serverId: number | undefined, taskIds: string[]) {
			const actions = { [DownloadActions.Pause]: Action.Pause, [DownloadActions.Clear]: Action.ClearCompleted, [DownloadActions.Delete]: Action.Delete, [DownloadActions.Stop]: Action.Stop, [DownloadActions.Restart]: Action.Restart, [DownloadActions.Start]: Action.Start };
			const telemetryAction = actions[action as keyof typeof actions];
			if (telemetryAction) capture(Event.FrontendDownloadInteraction, { action: telemetryAction, server_id: serverId, download_task_ids: taskIds, selection_count: taskIds.length });
		},
		downloadPreviewRequested(request: DownloadPreviewRequest, mediaType: PlexMediaType, retry: boolean) {
			const destinationId = request.destinationFolderPathId ?? 0;
			capture(Event.FrontendDownloadInteraction, { action: Action.Preview, media_type: mediaType, selection_count: request.downloadMedias.reduce((count, item) => count + item.mediaIds.length, 0), destination_id: destinationId, is_custom_destination: destinationId === 0, is_retry: retry });
		},
		downloadDismissed(media: CreateDownloadTasksRequest['downloadMedias'], mediaType: PlexMediaType) {
			capture(Event.FrontendDownloadInteraction, { action: Action.Dismiss, media_type: mediaType, selection_count: media.reduce((count, item) => count + item.mediaIds.length, 0) });
		},
		downloadDetailsOpened(task: { id: string; mediaType: PlexMediaType }, serverId: number) {
			capture(Event.FrontendDownloadInteraction, { action: Action.OpenDetails, download_task_id: task.id, server_id: serverId, media_type: task.mediaType });
		},
		downloadDetailsClosed(taskId: string, task?: Pick<DownloadTaskDTO, 'mediaType' | 'plexLibraryId' | 'plexServerId'> | null) {
			capture(Event.FrontendDownloadInteraction, { action: Action.CloseDetails, download_task_id: taskId, media_type: task?.mediaType, library_id: task?.plexLibraryId, server_id: task?.plexServerId });
		},
		helpOpened() { capture(Event.FrontendHelpInteraction, { action: Action.Open }); },
		logsCleared() { capture(Event.FrontendLogInteraction, { action: Action.ClearLogs }); },
		logSearchCleared() { capture(Event.FrontendLogInteraction, { action: Action.ClearSearch }); },
		logSearchRequested() { capture(Event.FrontendLogInteraction, { action: Action.Search, has_query: true }); },
		logLevelsChanged(levelCount: number) { capture(Event.FrontendLogInteraction, { action: Action.SetLevel, level_count: levelCount }); },
		logSortChanged(direction: SortDirection) { capture(Event.FrontendLogInteraction, { action: Action.Sort, sort_direction: direction }); },
		logsCopied(count: number) { capture(Event.FrontendLogInteraction, { action: Action.Copy, selection_count: count }); },
		logScrollingPaused(paused: boolean) { capture(Event.FrontendLogInteraction, { action: paused ? Action.Pause : Action.Resume, paused }); },
		logsRefreshRequested() { capture(Event.FrontendLogInteraction, { action: Action.Refresh }); },
		downloadLogSortChanged(taskId: string, ascending: boolean) { capture(Event.FrontendLogInteraction, { action: Action.Sort, download_task_id: taskId, sort_ascending: ascending }); },
		downloadLogsCopied(taskId: string, count: number) { capture(Event.FrontendLogInteraction, { action: Action.Copy, download_task_id: taskId, log_count: count }); },
		downloadLogCopied(taskId: string, level: NotificationLevel) { capture(Event.FrontendLogInteraction, { action: Action.Copy, download_task_id: taskId, log_level: level, log_count: 1 }); },
		downloadLogsClearRequested(taskId: string, count: number) { capture(Event.FrontendLogInteraction, { action: Action.ClearLogs, download_task_id: taskId, log_count: count }); },
		downloadLogLevelChanged(taskId: string, level: NotificationLevel, enabled: boolean) { capture(Event.FrontendLogInteraction, { action: Action.SetLevel, download_task_id: taskId, log_level: level, enabled }); },
		notificationDismissed(id: number) { capture(Event.FrontendNotificationInteraction, { action: Action.Dismiss, notification_id: id }); },
		notificationsCleared() { capture(Event.FrontendNotificationInteraction, { action: Action.ClearAll }); },
		notificationsOpened() { capture(Event.FrontendNotificationInteraction, { action: Action.Open }); },
		notificationsClosed() { capture(Event.FrontendNotificationInteraction, { action: Action.Close }); },
		serverAccountsRefreshRequested(serverId: number) { capture(Event.FrontendAccountInteraction, { action: Action.Refresh, server_id: serverId }); },
		accountRefreshRequested(accountId: number) { capture(Event.FrontendAccountInteraction, { action: Action.Refresh, account_id: accountId }); },
		connectionTestRequested(connectionId: number, connection?: PlexServerConnectionDTO) { capture(Event.FrontendConnectionInteraction, { action: Action.TestConnection, connection_id: connectionId, ...(connection ? { server_id: connection.plexServerId } : {}) }); },
		serverConnectionsTestRequested(serverId: number, connections: PlexServerConnectionDTO[]) { capture(Event.FrontendConnectionInteraction, { action: Action.TestConnection, server_id: serverId, connection_count: connections.filter((connection) => connection.plexServerId === serverId).length }); },
		connectionUrlTestRequested(serverId: number) { capture(Event.FrontendConnectionInteraction, { action: Action.TestConnection, server_id: serverId }); },
		connectionCreateRequested(connection: { plexServerId: number }) { capture(Event.FrontendConnectionInteraction, { action: Action.Create, server_id: connection.plexServerId }); },
		connectionUpdateRequested(connection: { id: number; plexServerId: number }) { capture(Event.FrontendConnectionInteraction, { action: Action.Update, server_id: connection.plexServerId, connection_id: connection.id }); },
		connectionDeleteRequested(connectionId: number, connection?: PlexServerConnectionDTO) { capture(Event.FrontendConnectionInteraction, { action: Action.Delete, connection_id: connectionId, ...(connection ? { server_id: connection.plexServerId } : {}) }); },
		preferredConnectionChanged(serverId: number, connectionId: number) { capture(Event.FrontendConnectionInteraction, { action: Action.SetPreferred, server_id: serverId, connection_id: connectionId }); },
		serverAliasChanged(serverId: number) { capture(Event.FrontendServerInteraction, { action: Action.SetAlias, server_id: serverId }); },
		serverEnabledChanged(serverId: number, enabled: boolean) { capture(Event.FrontendServerInteraction, { action: enabled ? Action.Enable : Action.Disable, server_id: serverId, is_enabled: enabled }); },
		serverOwnershipChanged(serverId: number, owned: boolean) { capture(Event.FrontendServerInteraction, { action: Action.SetOwned, server_id: serverId, owned }); },
		serverDeleteRequested(serverId: number) { capture(Event.FrontendServerInteraction, { action: Action.Delete, server_id: serverId }); },
		serverPausedChanged(serverId: number, paused: boolean) { capture(Event.FrontendServerInteraction, { action: paused ? Action.Pause : Action.Resume, server_id: serverId, paused }); },
		serverOpened(serverId: number) { capture(Event.FrontendServerInteraction, { action: Action.Open, server_id: serverId }); },
		serverTabChanged(serverId: number, tabIndex: number) { capture(Event.FrontendServerInteraction, { action: Action.ChangeView, server_id: serverId, tab_index: tabIndex }); },
		serverRefreshRequested(serverId: number, forceMediaRefresh: boolean) { capture(Event.FrontendServerInteraction, { action: Action.Refresh, server_id: serverId, force_media_refresh: forceMediaRefresh }); },
		serverInspectionRequested(serverId: number) { capture(Event.FrontendServerInteraction, { action: Action.Inspect, server_id: serverId }); },
		libraryRefreshRequested(libraryId: number, forceMediaRefresh: boolean) { capture(Event.FrontendLibraryInteraction, { action: Action.Refresh, library_id: libraryId, force_media_refresh: forceMediaRefresh }); },
		librarySyncCancelRequested(libraryId: number) { capture(Event.FrontendLibraryInteraction, { action: Action.Cancel, library_id: libraryId }); },
		libraryDestinationChanged(libraryId: number, folderPathId: number) { capture(Event.FrontendLibraryInteraction, { action: Action.SetDestination, library_id: libraryId, folder_path_id: folderPathId }); },
		libraryEnabledChanged(libraryId: number, enabled: boolean) { capture(Event.FrontendLibraryInteraction, { action: enabled ? Action.Enable : Action.Disable, library_id: libraryId, is_enabled: enabled }); },
		timelineServerChanged(serverId: number | null) { capture(Event.FrontendLibraryInteraction, { action: Action.Filter, ...(serverId === null ? { filter_cleared: true } : { server_id: serverId }) }); },
		timelineLibraryChanged(libraryId: number | null) { capture(Event.FrontendLibraryInteraction, { action: Action.Filter, ...(libraryId === null ? { filter_cleared: true } : { library_id: libraryId }) }); },
		timelineZoomChanged(preset: string) { capture(Event.FrontendLibraryInteraction, { action: Action.Zoom, zoom_days: preset === 'All' ? 0 : Number.parseInt(preset.replace('d', ''), 10) }); },
		timelineRowSelected(rowId: string) { capture(Event.FrontendLibraryInteraction, { action: rowId ? Action.Select : Action.ClearSelection, selection_count: rowId ? 1 : 0 }); },
		folderCreateRequested(folder: FolderPathDTO) { capture(Event.FrontendFolderInteraction, { action: Action.Create, folder_type: folder.folderType, media_type: folder.mediaType }); },
		folderDirectoryChanged(id: number) { capture(Event.FrontendFolderInteraction, { action: Action.Confirm, folder_path_id: id }); },
		folderNameChanged(id: number) { capture(Event.FrontendFolderInteraction, { action: Action.Update, folder_path_id: id }); },
		folderDeleteRequested(id: number) { capture(Event.FrontendFolderInteraction, { action: Action.Delete, folder_path_id: id }); },
		accountTokenValidationRequested(account: AccountContext) { capture(Event.FrontendAccountInteraction, { action: Action.ValidateToken, account_id: account.id, is_new_account: account.isNewAccount }); },
		accountCredentialsValidationRequested(account: AccountContext) { capture(Event.FrontendAccountInteraction, { action: Action.ValidateCredentials, account_id: account.id, is_new_account: account.isNewAccount }); },
		accountVerificationRequested(id: number) { capture(Event.FrontendAccountInteraction, { action: Action.ValidateCredentials, account_id: id, verification_code_required: true }); },
		accountTokenGenerationRequested(id: number) { capture(Event.FrontendAccountInteraction, { action: Action.GenerateToken, account_id: id }); },
		accountSaveRequested(account: AccountContext) { capture(Event.FrontendAccountInteraction, { action: Action.Save, account_id: account.id, is_new_account: account.isNewAccount, is_auth_token_mode: account.isAuthTokenMode }); },
		accountDeleteRequested(id: number) { capture(Event.FrontendAccountInteraction, { action: Action.Delete, account_id: id }); },
		accountTokenCopied(id: number) { capture(Event.FrontendAccountInteraction, { action: Action.Copy, account_id: id }); },
		credentialsChangeRequested(usernameChanged: boolean, passwordChanged: boolean) { capture(Event.FrontendAuthenticationInteraction, { action: Action.Update, username_changed: usernameChanged, password_changed: passwordChanged }); },
		logoutRequested() { capture(Event.FrontendAuthenticationInteraction, { action: Action.Logout }); },
		integrationOpened(integration: IntegrationIdentity) { capture(Event.FrontendIntegrationInteraction, { action: Action.OpenDetails, ...integrationIdentity(integration.type, integration) }); },
		integrationDeleteRequested(integration: IntegrationIdentity) { capture(Event.FrontendIntegrationInteraction, { action: Action.Delete, ...integrationIdentity(integration.type, integration) }); },
		integrationTestRequested(type: IntegrationType, integration?: IntegrationIdentity | null) { capture(Event.FrontendIntegrationInteraction, { action: Action.TestConnection, ...integrationIdentity(type, integration) }); },
		integrationSaveRequested(type: IntegrationType, integration?: IntegrationIdentity | null) { capture(Event.FrontendIntegrationInteraction, { action: integration ? Action.Update : Action.Create, ...integrationIdentity(type, integration), is_new_integration: !integration }); },
		integrationSetupRequested(type: IntegrationType, integration: IntegrationIdentity | null, saveBeforeSetup: boolean) { capture(Event.FrontendIntegrationInteraction, { action: Action.Start, ...integrationIdentity(type, integration), save_before_setup: saveBeforeSetup }); },
		generalSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.General }); },
		confirmationSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Confirmation }); },
		dateTimeSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.DateTime }); },
		downloadManagerSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.DownloadManager }); },
		languageSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Language }); },
		networkSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Network }); },
		debugSettingsChanged() { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Debug }); },
		serverDownloadLimitChanged(machineIdentifier: string) { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Server, machine_identifier: machineIdentifier }); },
		serverStreamDownloaderChanged(serverId: number) { capture(Event.FrontendSettingsInteraction, { action: Action.Update, settings_module: Module.Server, server_id: serverId }); },
		downloadScheduleSaved() { capture(Event.FrontendSettingsInteraction, { action: Action.Save, settings_module: Module.DownloadManager }); },
		downloadScheduleResetRequested() { capture(Event.FrontendSettingsInteraction, { action: Action.Reset, settings_module: Module.DownloadManager }); },
		setupStepSelected(step: number) { capture(Event.FrontendSetupInteraction, { action: Action.ChooseStep, step }); },
		setupNextRequested(step: number) { capture(Event.FrontendSetupInteraction, { action: Action.Next, step }); },
		setupPreviousRequested(step: number) { capture(Event.FrontendSetupInteraction, { action: Action.Previous, step }); },
		setupLanguageChanged() { capture(Event.FrontendSetupInteraction, { action: Action.Update, settings_module: Module.Language }); },
		setupBackgroundChanged() { capture(Event.FrontendSetupInteraction, { action: Action.Update, settings_module: Module.General }); },
		setupDisclaimerAccepted() { capture(Event.FrontendSetupInteraction, { action: Action.Accept, step: SetupPanelType.DisclaimerPanel }); },
		setupFinishRequested() { capture(Event.FrontendSetupInteraction, { action: Action.Finish }); },
		updateOpened() { capture(Event.FrontendUpdateInteraction, { action: Action.Open }); },
		updateReleaseSelected(index: number) { capture(Event.FrontendUpdateInteraction, { action: Action.ChooseRelease, release_index: index }); },
		updateApplyRequested() { capture(Event.FrontendUpdateInteraction, { action: Action.ApplyUpdate }); },
		navigationDrawerToggled(open: boolean) { capture(Event.FrontendNavigationInteraction, { action: open ? Action.Close : Action.Open, target: 'navigation_drawer' }); },
		githubOpened() { capture(Event.FrontendNavigationInteraction, { action: Action.Open, link_category: Link.GitHub }); },
		externalLinkOpened(href?: string) { capture(Event.FrontendNavigationInteraction, { action: Action.Open, link_category: getLinkCategory(href) }); },
		diagnosticsRequested() { capture(Event.FrontendDiagnosticRequested, { action: Action.Inspect }); },
		mediaInspected(media: MediaIdentity) { capture(Event.FrontendMediaInteraction, { action: Action.Inspect, ...mediaIdentity(media) }); },
		mediaComparisonRetryRequested(media: MediaIdentity) { capture(Event.FrontendMediaInteraction, { action: Action.Retry, ...mediaIdentity(media) }); },
		mediaComparisonSelectionChanged(media: MediaIdentity, count: number, selected = count > 0) { capture(Event.FrontendMediaInteraction, { action: selected ? Action.Select : Action.ClearSelection, ...mediaIdentity(media), selection_count: count }); },
		mediaCountryFilterChanged(value: number, libraryId: number, mediaType: PlexMediaType) { mediaFilterChanged(MediaMetaDataTypes.Country, value, libraryId, mediaType); },
		mediaRoleFilterChanged(value: number, libraryId: number, mediaType: PlexMediaType) { mediaFilterChanged(MediaMetaDataTypes.Roles, value, libraryId, mediaType); },
		mediaGenreFilterChanged(value: number, libraryId: number, mediaType: PlexMediaType) { mediaFilterChanged(MediaMetaDataTypes.Genres, value, libraryId, mediaType); },
		mediaQualityFilterChanged(value: number, libraryId: number, mediaType: PlexMediaType) { mediaFilterChanged(MediaMetaDataTypes.Quality, value, libraryId, mediaType); },
		mediaComparisonFilterChanged(value: PlexMediaComparisonState | null, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: value ? Action.Filter : Action.RemoveFilter, filter_type: MediaMetaDataTypes.ComparisonState, comparison_state: value, library_id: libraryId, media_type: mediaType }); },
		mediaFiltersCleared(libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.RemoveFilter, filter_type: MediaMetaDataTypes.None, library_id: libraryId, media_type: mediaType }); },
		mediaSelectionChanged(selection: ISelection, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.Select, library_id: libraryId, media_type: mediaType, media_ids: selection.keys, selection_count: selection.keys.length, all_selected: selection.allSelected === true }); },
		mediaRangeSelected(range: { min: number; max: number; count: number }, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.SelectRange, library_id: libraryId, media_type: mediaType, min_index: range.min, max_index: range.max, selection_count: range.count }); },
		mediaRootSelectionChanged(selection: ISelection, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: selection.allSelected ? Action.SelectAll : Action.ClearSelection, library_id: libraryId, media_type: mediaType, selection_count: selection.keys.length, all_selected: selection.allSelected }); },
		mediaSortReset(libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.ResetSort, library_id: libraryId, media_type: mediaType }); },
		mediaSearchChanged(query: string, libraryId: number, mediaType: PlexMediaType) {
			const normalized = query.trim();
			capture(Event.FrontendMediaInteraction, { action: normalized ? Action.Search : Action.ClearSearch, library_id: libraryId, media_type: mediaType, search: query, has_query: normalized.length > 0, query_length: query.length });
		},
		mediaSortChanged(sort: IMediaOverviewSort, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.Sort, library_id: libraryId, media_type: mediaType, sort_field: sort.field, sort_direction: sort.sort }); },
		mediaViewChanged(view: { mode: string; photo?: boolean }, libraryId: number, mediaType: PlexMediaType) { capture(Event.FrontendMediaInteraction, { action: Action.ChangeView, library_id: libraryId, media_type: mediaType, view_mode: view.mode, photo_mode: view.photo }); },
		photoNavigated(asset: PhotoIdentity, index: number, forward: boolean) { capture(Event.FrontendPhotoInteraction, { action: forward ? Action.Next : Action.Previous, ...photoIdentity(asset), navigation_index: index }); },
		photoZoomChanged(asset: PhotoIdentity, zoomed: boolean) { capture(Event.FrontendPhotoInteraction, { action: Action.Zoom, ...photoIdentity(asset), zoomed }); },
		photoClosed(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.Close, ...photoIdentity(asset) }); },
		photoDetailsOpened(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.OpenDetails, ...photoIdentity(asset) }); },
		photoDetailsClosed(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.CloseDetails, ...photoIdentity(asset) }); },
		photoInspectionRetryRequested(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.Retry, ...photoIdentity(asset), inspection: true }); },
		photoImageRetryRequested(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.Retry, ...photoIdentity(asset), inspection: false }); },
		photoSearchChanged(query: string, libraryId: number) { capture(Event.FrontendPhotoInteraction, { action: query.trim() ? Action.Search : Action.ClearSearch, library_id: libraryId, query, has_query: query.trim().length > 0, query_length: query.length }); },
		photoSortChanged(sort: IMediaOverviewSort, libraryId: number, isDefault: boolean) { capture(Event.FrontendPhotoInteraction, { action: isDefault ? Action.ResetSort : Action.Sort, library_id: libraryId, sort_field: sort.field, sort_direction: sort.sort }); },
		photoFolderOpened(folderId: number, libraryId: number, serverId?: number) { capture(Event.FrontendPhotoInteraction, { action: Action.Open, item_kind: PhotoItemKind.Folder, item_id: folderId, library_id: libraryId, server_id: serverId }); },
		photoAlbumOpened(album: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.Open, item_kind: PhotoItemKind.Album, item_id: album.id, library_id: album.plexLibraryId, server_id: album.plexServerId }); },
		photoAssetOpened(asset: PhotoIdentity) { capture(Event.FrontendPhotoInteraction, { action: Action.Open, item_kind: PhotoItemKind.Asset, item_id: asset.id, library_id: asset.plexLibraryId, server_id: asset.plexServerId }); },
		photoSelectionChanged(item: PhotoIdentity, selection: { kind: PhotoItemKind; selected: boolean; count: number }) { capture(Event.FrontendPhotoInteraction, { action: selection.selected ? Action.Select : Action.ClearSelection, item_kind: selection.kind, item_id: item.id, library_id: item.plexLibraryId, server_id: item.plexServerId, selected: selection.selected, selection_count: selection.count }); },
		photosSelectAllRequested(selected: boolean, count: number, libraryId: number) { capture(Event.FrontendPhotoInteraction, { action: selected ? Action.SelectAll : Action.ClearSelection, library_id: libraryId, selection_count: selected ? count : 0, visible_count: count }); },
		photoSelectionCleared(count: number, libraryId: number) { capture(Event.FrontendPhotoInteraction, { action: Action.ClearSelection, library_id: libraryId, selection_count: count }); },
		photoOverviewRetryRequested(view: string, libraryId: number) { capture(Event.FrontendPhotoInteraction, { action: Action.Retry, library_id: libraryId, view }); },
	};
}

function getLinkCategory(href?: string): Link {
	if (!href) return Link.Other;
	const url = URL.parse(href, 'https://reaparr.invalid');
	if (!url) return Link.Other;
	const host = url.hostname.toLowerCase();
	const path = url.pathname.toLowerCase();
	if (host === 'github.com' || host.endsWith('.github.com')) return path.includes('/releases') ? Link.Releases : Link.GitHub;
	if (host === 'hub.docker.com' && path.includes('/tags')) return Link.Releases;
	if (host === 'discord.gg' || host === 'discord.com' || host.endsWith('.discord.com')) return Link.Discord;
	if (host === 'plex.tv' || host.endsWith('.plex.tv')) return Link.Plex;
	if (host === 'reaparr.rocks' || host.endsWith('.reaparr.rocks')) return Link.Documentation;
	return Link.Other;
}
