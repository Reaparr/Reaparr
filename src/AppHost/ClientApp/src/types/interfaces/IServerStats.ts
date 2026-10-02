import type { PlexLibraryDTO } from '@dto';

export type ServerStatsStatus
	= | 'not-indexed'
		| 'partial'
		| 'complete'
		| 'no-enabled-libraries';

export interface IServerStats {
	libraries: PlexLibraryDTO[];
	mediaSize: number;
	movieCount: number;
	tvShowCount: number;
	seasonCount: number;
	episodeCount: number;
	hasIndexedData: boolean;
	status: ServerStatsStatus;
}
