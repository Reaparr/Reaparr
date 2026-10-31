import { useEventBus, type UseEventBusReturn } from '@vueuse/core';
import type { DownloadMediaDTO, SortDirection, SortType } from '@dto';

// region MediaOverview

export interface IMediaOverviewSort {
	field: SortType;
	sort: SortDirection;
}

// region General
export interface IMediaOverviewCommands {
	command: 'download' | 'open-details';
	scrollIndex?: number;
	downloadMediaCommands?: DownloadMediaDTO[];
	mediaId?: number;
}

export function resetMediaOverviewCommandsBus(): void {
	return useMediaOverviewCommandsBus().reset();
}

export function useMediaOverviewCommandsBus(): UseEventBusReturn<IMediaOverviewCommands, unknown> {
	// Do not set this to a constant, it will cause issues with the event bus.
	return useEventBus<IMediaOverviewCommands>('mediaOverViewCommands');
}

// endregion

/**
 * This is used to send a command to from the MediaOverviewBar to trigger a download command.
 */
export function useMediaOverviewBarDownloadCommandBus(): UseEventBusReturn<string, unknown> {
	return useEventBus<string>('downloadCommand');
}

// region Download command

export function sendMediaOverviewDownloadCommand(downloadMediaCommands: DownloadMediaDTO[]): void {
	useMediaOverviewCommandsBus().emit({
		command: 'download',
		downloadMediaCommands,
	});
}

export function listenMediaOverviewDownloadCommand(action: (downloadMediaCommands: DownloadMediaDTO[]) => void): void {
	useMediaOverviewCommandsBus().on(({ command, downloadMediaCommands }) => {
		if (command === 'download') {
			action(downloadMediaCommands ?? []);
		}
	});
}

// endregion

// endregion
