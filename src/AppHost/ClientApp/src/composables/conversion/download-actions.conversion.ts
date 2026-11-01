import {
	DownloadActions,
	PhotoItemKind,
	PlexMediaType,
	type DownloadMediaDTO,
	DownloadStatus,
	type PlexMediaDTO,
	type PlexMediaSlimDTO,
} from '@dto';
import { useSettingsStore } from '@store';

export function toDownloadSelectionKind(type: PlexMediaType): PhotoItemKind | null {
	switch (type) {
		case PlexMediaType.PhotoAlbum:
			return PhotoItemKind.Album;
		case PlexMediaType.PhotoImage:
			return PhotoItemKind.Asset;
		default:
			return null;
	}
}

export function toDownloadMedia(mediaItem: PlexMediaDTO | PlexMediaSlimDTO): DownloadMediaDTO[] {
	const settingsStore = useSettingsStore();
	return [
		{
			mediaIds: [mediaItem.id],
			type: mediaItem.type,
			plexServerId: mediaItem.plexServerId,
			plexLibraryId: mediaItem.plexLibraryId,
			qualities: mediaItem.qualities,
			selectionKind: toDownloadSelectionKind(mediaItem.type),
			keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
		},
	];
}

export function toDownloadActions(downloadStatus: DownloadStatus): DownloadActions[] {
	const actions: DownloadActions[] = [DownloadActions.Details];

	// NOTE: When updating this, also update back-end: src/Domain/Common/Converters/DownloadTaskActions.cs
	switch (downloadStatus) {
		case DownloadStatus.Unknown:
			actions.push(DownloadActions.Delete);
			break;
		case DownloadStatus.Queued:
			actions.push(DownloadActions.Start, DownloadActions.Delete);
			break;
		case DownloadStatus.Downloading:
		case DownloadStatus.Restarting:
			actions.push(DownloadActions.Pause, DownloadActions.Stop);
			break;
		case DownloadStatus.DownloadFinished:
			actions.push(DownloadActions.Start, DownloadActions.Delete);
			break;
		case DownloadStatus.MoveFinished:
			actions.push(DownloadActions.Delete);
			break;
		case DownloadStatus.Paused:
		case DownloadStatus.AutoPaused:
		case DownloadStatus.MovePaused:
		case DownloadStatus.AutoMovePaused:
			actions.push(DownloadActions.Start, DownloadActions.Stop, DownloadActions.Delete);
			break;
		case DownloadStatus.Completed:
			actions.push(DownloadActions.Clear, DownloadActions.Restart);
			break;
		case DownloadStatus.Stopped:
			actions.push(DownloadActions.Restart, DownloadActions.Delete);
			break;
		case DownloadStatus.Moving:
			actions.push(DownloadActions.Pause, DownloadActions.Stop);
			break;
		case DownloadStatus.Error:
		case DownloadStatus.StorageError:
		case DownloadStatus.SourceUnavailable:
		case DownloadStatus.DownloadClientError:
		case DownloadStatus.IntegrityError:
			actions.push(DownloadActions.Restart, DownloadActions.Delete);
			break;
		case DownloadStatus.AuthError:
			actions.push(DownloadActions.Start, DownloadActions.Delete);
			break;
		case DownloadStatus.MoveError:
			actions.push(DownloadActions.Start, DownloadActions.Restart, DownloadActions.Delete);
			break;
		case DownloadStatus.ServerUnreachable:
			actions.push(DownloadActions.Start, DownloadActions.Stop, DownloadActions.Delete);
			break;
		default:
			console.error(`Unknown download status: ${downloadStatus}`);
			break;
	}

	return actions;
}
