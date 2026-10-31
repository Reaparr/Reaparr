import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { computed, reactive, toRefs } from 'vue';
import type { Observable } from 'rxjs';
import { catchError, map, of, tap } from 'rxjs';
import type { ISetupResult } from '@interfaces';
import { StoreNames } from '@interfaces';
import { cloneDeep } from 'lodash-es';
import { useSignalrStore } from '@store';
import type { LiveLogEventDTO } from '@dto';
import { LogSeverity, SortDirection } from '@dto';
import { debugApi } from '@api';

interface ILogsStoreState {
	searchText: string;
	logs: LiveLogEventDTO[];
	sortDirection: SortDirection;
	selectedLevels: LogSeverity[];
}

export const useLogsStore = defineStore(StoreNames.LogsStore, () => {
	const defaultState: ILogsStoreState = {
		searchText: '',
		sortDirection: SortDirection.Asc,
		logs: [],
		selectedLevels: [LogSeverity.Verbose, LogSeverity.Debug, LogSeverity.Information, LogSeverity.Warning, LogSeverity.Error, LogSeverity.Fatal],
	};

	const state = reactive<ILogsStoreState>(cloneDeep(defaultState));
	const signalrStore = useSignalrStore();

	function levelClass(level: LogSeverity): string {
		return `live-log-viewer__row--${level.toString().toLowerCase()}`;
	}

	const actions = {
		setup(): Observable<ISetupResult> {
			signalrStore.getLogEvents().pipe(
				tap((entry) => state.logs.push(entry)),
			).subscribe();

			return actions.refreshLogs().pipe(
				map((result) => ({ name: StoreNames.LogsStore, isSuccess: result.isSuccess })),
				catchError((error) => {
					Log.error('Failed to load cached logs', error);
					return of({ name: StoreNames.LogsStore, isSuccess: false });
				}),
			);
		},
		refreshLogs() {
			return debugApi.getAllLogsEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess) {
						mergeSnapshot(result.value ?? []);
					}
				}),
			);
		},
		clearLogs(): void {
			state.logs = [];
		},
		clearSearch(): void {
			state.searchText = '';
		},
		setSearch(value: string): void {
			state.searchText = value;
		},
		toggleSortDirection() {
			state.sortDirection = state.sortDirection === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
		},
		$reset(): void {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	const getters = {
		getLogs: computed((): LiveLogEventDTO[] => {
			const query = state.searchText.trim().toLowerCase();
			const levels = state.selectedLevels;

			const filteredLogs = state.logs.filter(
				(x) => (x.message.toLowerCase().includes(query) || (x.exception ?? '').toLowerCase().includes(query)) && levels.includes(x.level),
			);

			if (state.sortDirection === SortDirection.Desc) {
				return [...filteredLogs].reverse();
			}
			return filteredLogs;
		}),
		levelCount: (value: LogSeverity): number => state.logs.filter((entry) => entry.level === value).length,
	};

	function mergeSnapshot(entries: LiveLogEventDTO[]): void {
		if (state.logs.length === 0) {
			state.logs = entries;
			return;
		}

		const snapshotSequences = new Set(entries.map((entry) => entry.sequence));
		const mergedEntries = [...entries];

		for (const entry of state.logs) {
			if (!snapshotSequences.has(entry.sequence)) {
				mergedEntries.push(entry);
			}
		}

		state.logs = mergedEntries.sort((left, right) => left.sequence - right.sequence);
	}

	return {
		...toRefs(state),
		...actions,
		...getters,
		levelClass,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useLogsStore, import.meta.hot));
}
