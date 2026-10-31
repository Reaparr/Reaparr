import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { type Observable, of } from 'rxjs';
import { catchError, finalize, map, tap } from 'rxjs/operators';
import { cloneDeep, groupBy, orderBy, compact } from 'lodash-es';
import { plexLibraryApi } from '@api';
import {
	PlexAccessState,
	type PlexLibraryAccessCurrentStateLibraryDTO,
	type PlexLibraryAccessTimelineDTO,
	type PlexLibraryAccessTimelineEventDTO,
	SortDirection,
} from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { useSettingsStore } from '@store';
import { getSortOrder } from '@composables';

export type LibraryAccessTimelineZoomPreset = '1d' | '7d' | '30d' | '90d' | 'All';

export interface LibraryAccessTimelineInterval {
	id: string;
	rowId: string;
	accountName: string;
	serverGroupId: string;
	serverName: string;
	libraryName: string;
	grantedAt: string;
	revokedAt: string | null;
	start: Date;
	end: Date;
	durationLabel: string;
}

interface ILibraryAccessTimelineFilters {
	plexServerId: number | null;
	plexLibraryId: number | null;
	fromUtc: string | null;
	toUtc: string | null;
}

interface ILibraryAccessTimelineStoreState {
	filters: ILibraryAccessTimelineFilters;
	timeline: PlexLibraryAccessTimelineDTO;
	isLoading: boolean;
	selectedRowId: string;
	zoomPreset: LibraryAccessTimelineZoomPreset;
}

export const useLibraryAccessTimelineStore = defineStore(StoreNames.LibraryAccessTimelineStore, () => {
	const defaultState: ILibraryAccessTimelineStoreState = {
		filters: {
			plexServerId: null,
			plexLibraryId: null,
			fromUtc: null,
			toUtc: null,
		},
		timeline: {
			events: [],
			currentState: [],
		},
		isLoading: false,
		selectedRowId: '',
		zoomPreset: '7d',
	};

	const state = reactive<ILibraryAccessTimelineStoreState>(cloneDeep(defaultState));

	const settingsStore = useSettingsStore();

	const actions = {
		setup(): Observable<ISetupResult> {
			return of({ name: StoreNames.LibraryAccessTimelineStore, isSuccess: true });
		},
		setServer(plexServerId: number | null) {
			state.filters.plexServerId = plexServerId;
		},
		setLibrary(plexLibraryId: number | null) {
			state.filters.plexLibraryId = plexLibraryId;
		},
		setDateRange(fromUtc: string | null, toUtc: string | null) {
			state.filters.fromUtc = fromUtc;
			state.filters.toUtc = toUtc;
		},
		setZoomPreset(zoomPreset: LibraryAccessTimelineZoomPreset) {
			state.zoomPreset = zoomPreset;
		},
		selectRow(rowId: string) {
			state.selectedRowId = rowId;
		},
		refreshTimeline(): Observable<PlexLibraryAccessTimelineDTO> {
			state.isLoading = true;

			return plexLibraryApi.getPlexLibraryAccessTimelineEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess && result.value) {
						state.timeline = result.value;
					}
				}),
				map(() => state.timeline),
				catchError((error) => {
					Log.error('Failed to refresh library access timeline', error);
					return of(state.timeline);
				}),
				finalize(() => state.isLoading = false),
			);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	function buildRowId(plexAccountId: number, plexServerId?: number | null, plexLibraryId?: number | null): string {
		return `account-${plexAccountId}-server-${plexServerId ?? 'unknown'}-library-${plexLibraryId ?? 'unknown'}`;
	}

	function getAccountName(event: PlexLibraryAccessTimelineEventDTO): string {
		return settingsStore.shouldMaskAccountNames ? '**MASKED**' : event.plexAccountName;
	}

	function getServerGroupId(event: PlexLibraryAccessTimelineEventDTO): string {
		return `server-${event.plexServerId ?? 'unknown'}`;
	}

	function getServerName(event: PlexLibraryAccessTimelineEventDTO): string {
		return settingsStore.shouldMaskServerNames ? '**MASKED**' : event.plexServerName ?? '';
	}

	function getLibraryName(event: PlexLibraryAccessTimelineEventDTO): string {
		return settingsStore.shouldMaskLibraryNames ? '**MASKED**' : event.plexLibraryName ?? '';
	}

	function formatDuration(start: Date, end: Date): string {
		const durationMs = Math.max(0, end.getTime() - start.getTime());
		const days = Math.floor(durationMs / 86400000);
		if (days > 0) {
			return `${days}d`;
		}

		const hours = Math.floor(durationMs / 3600000);
		return `${hours}h`;
	}

	function getFilteredEvents(options?: {
		ignoreLibraryFilter?: boolean;
		ignoreDateFilter?: boolean;
	}): PlexLibraryAccessTimelineEventDTO[] {
		const ignoreDateFilter = options?.ignoreDateFilter ?? false;
		const fromMs = !ignoreDateFilter && state.filters.fromUtc ? new Date(state.filters.fromUtc).getTime() : null;
		const toMs = !ignoreDateFilter && state.filters.toUtc ? new Date(state.filters.toUtc).getTime() : null;
		const ignoreLibraryFilter = options?.ignoreLibraryFilter ?? false;

		return state.timeline.events.filter((event) => {
			if (state.filters.plexServerId && event.plexServerId !== state.filters.plexServerId) {
				return false;
			}

			if (!ignoreLibraryFilter && state.filters.plexLibraryId && event.plexLibraryId !== state.filters.plexLibraryId) {
				return false;
			}

			const eventMs = new Date(event.createdAt).getTime();
			if (!Number.isFinite(eventMs)) {
				return false;
			}

			if ((fromMs !== null) && eventMs < fromMs) {
				return false;
			}

			if ((toMs !== null) && eventMs > toMs) {
				return false;
			}

			return true;
		});
	}

	function intervalOverlapsDateWindow(interval: LibraryAccessTimelineInterval, fromMs: number | null, toMs: number | null): boolean {
		if (fromMs === null && toMs === null) {
			return true;
		}

		const intervalStartMs = interval.start.getTime();
		const intervalEndMs = interval.end.getTime();
		if (!Number.isFinite(intervalStartMs) || !Number.isFinite(intervalEndMs)) {
			return false;
		}

		if (fromMs !== null && intervalEndMs < fromMs) {
			return false;
		}

		if (toMs !== null && intervalStartMs > toMs) {
			return false;
		}

		return true;
	}

	const getters = {
		timelineIntervals: computed((): LibraryAccessTimelineInterval[] => {
			const now = new Date();
			const fromMs = state.filters.fromUtc ? new Date(state.filters.fromUtc).getTime() : null;
			const toMs = state.filters.toUtc ? new Date(state.filters.toUtc).getTime() : null;
			const filteredEvents = getFilteredEvents({ ignoreDateFilter: true });
			const groupedEvents = groupBy(
				orderBy(filteredEvents, (event) => event.createdAt, getSortOrder(SortDirection.Asc)),
				(event) => buildRowId(event.plexAccountId, event.plexServerId, event.plexLibraryId),
			);

			return Object.entries(groupedEvents).flatMap(([rowId, events]) => {
				const intervals: LibraryAccessTimelineInterval[] = [];
				let openGrant: PlexLibraryAccessTimelineEventDTO | null = null;

				for (const event of events) {
					if (event.state === PlexAccessState.Granted) {
						openGrant = event;
						continue;
					}

					if ((event.state === PlexAccessState.Revoked) && openGrant) {
						const start = new Date(openGrant.createdAt);
						const end = new Date(event.createdAt);
						if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
							openGrant = null;
							continue;
						}
						intervals.push({
							id: `${rowId}-${openGrant.refreshRunId}-${event.refreshRunId}`,
							rowId,
							serverGroupId: getServerGroupId(openGrant),
							accountName: getAccountName(openGrant),
							serverName: getServerName(openGrant),
							libraryName: getLibraryName(openGrant),
							grantedAt: openGrant.createdAt,
							revokedAt: event.createdAt,
							start,
							end,
							durationLabel: formatDuration(start, end),
						});
						openGrant = null;
					}
				}

				if (openGrant) {
					const start = new Date(openGrant.createdAt);
					if (Number.isNaN(start.getTime())) {
						return intervals;
					}
					intervals.push({
						id: `${rowId}-${openGrant.refreshRunId}-active`,
						rowId,
						serverGroupId: getServerGroupId(openGrant),
						accountName: getAccountName(openGrant),
						serverName: getServerName(openGrant),
						libraryName: getLibraryName(openGrant),
						grantedAt: openGrant.createdAt,
						revokedAt: null,
						start,
						end: now,
						durationLabel: formatDuration(start, now),
					});
				}

				return intervals.filter((interval) => intervalOverlapsDateWindow(interval, fromMs, toMs));
			});
		}),
		currentStateRows: computed((): PlexLibraryAccessCurrentStateLibraryDTO[] => compact(state.timeline.currentState.flatMap((server) => server.libraries))),
		filteredLibraryIdsForSelectedServer: computed((): number[] => {
			if (!state.filters.plexServerId) {
				return [];
			}

			const eventLibraryIds = getFilteredEvents({ ignoreLibraryFilter: true })
				.map((event) => event.plexLibraryId)
				.filter((id): id is number => typeof id === 'number');

			return Array.from(new Set(eventLibraryIds)).sort((left, right) => left - right);
		}),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useLibraryAccessTimelineStore, import.meta.hot));
}
