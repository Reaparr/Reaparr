<template>
	<QSection
		:header="t('components.library-access-timeline.chart.title')"
		class="library-access-timeline-page-scroll"
		data-cy="library-access-timeline-overview">
		<div class="library-access-timeline-layout">
			<div class="library-access-timeline-filters">
				<q-card class="q-pa-md">
					<QRow
						align="center"
						class="q-col-gutter-md">
						<QCol
							cols="12"
							md="4">
							<q-select
								v-model="selectedServerId"
								:data-cy="'library-access-timeline-server-filter'"
								emit-value
								map-options
								clearable
								dense
								filled
								:label="t('components.library-access-timeline.filters.server')"
								:options="serverOptions" />
						</QCol>
						<QCol
							cols="12"
							md="4">
							<q-select
								v-model="selectedLibraryId"
								:data-cy="'library-access-timeline-library-filter'"
								emit-value
								map-options
								clearable
								dense
								filled
								:disable="!hasSelectedServer"
								:label="t('components.library-access-timeline.filters.library')"
								:options="libraryOptions" />
						</QCol>
						<QCol
							cols="12"
							md="4">
							<QRow class="q-gutter-sm">
								<q-btn-toggle
									v-model="selectedZoomPreset"
									:data-cy="'library-access-timeline-zoom-filter'"
									dense
									spread
									unelevated
									:options="zoomOptions" />
							</QRow>
						</QCol>
					</QRow>
				</q-card>
			</div>

			<div class="library-access-timeline-scroll-region">
				<div class="q-pa-md library-access-timeline-scroll-card">
					<div
						class="library-access-timeline"
						data-cy="library-access-timeline-chart">
						<LibraryAccessGanttChart
							v-if="timelineStore.timelineIntervals.length > 0"
							cy="library-access-timeline-gantt"
							:intervals="timelineStore.timelineIntervals"
							:selected-row-id="timelineStore.selectedRowId"
							:zoom-preset="timelineStore.zoomPreset"
							@select-row="timelineStore.selectRow" />
						<QText
							v-else
							align="center"
							class="q-pa-xl"
							:value="t('components.library-access-timeline.chart.empty')" />
					</div>
				</div>
			</div>

			<!-- Current-state table removed per UX request (duplicate information). -->
		</div>
	</QSection>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { orderBy } from 'lodash-es';
import { useAccountStore, useLibraryAccessTimelineStore, useLibraryStore, useServerStore } from '@store';
import type { LibraryAccessTimelineZoomPreset } from '@store';

const { t } = useI18n();
const accountStore = useAccountStore();
const serverStore = useServerStore();
const libraryStore = useLibraryStore();
const timelineStore = useLibraryAccessTimelineStore();
const refreshTimeline$ = new Subject<void>();

const selectedServerId = computed({
	get: () => timelineStore.filters.plexServerId ?? null,
	set: (value: number | null) => {
		timelineStore.setServer(value);
		timelineStore.setLibrary(null);
		refreshTimeline$.next();
	},
});

const selectedLibraryId = computed({
	get: () => timelineStore.filters.plexLibraryId ?? null,
	set: (value: number | null) => {
		timelineStore.setLibrary(value);
		refreshTimeline$.next();
	},
});

const selectedZoomPreset = computed({
	get: () => timelineStore.zoomPreset,
	set: (value: LibraryAccessTimelineZoomPreset) => {
		timelineStore.setZoomPreset(value);
		applyZoomPreset(value);
		refreshTimeline$.next();
	},
});

const hasSelectedServer = computed(() => timelineStore.filters.plexServerId !== null);

const serverOptions = computed(() => orderBy(
	get(serverStore.getVisibleServers).map((server) => ({
		label: serverStore.getServerName(server.id),
		value: server.id,
	})),
	[(option) => option.label.toLocaleLowerCase(), (option) => option.value],
	['asc', 'asc'],
));

const libraryOptions = computed(() => {
	if (!timelineStore.filters.plexServerId) {
		return [];
	}

	const allLibrariesForServer = libraryStore.getLibrariesByServerId(timelineStore.filters.plexServerId);
	const filteredLibraryIds = new Set(timelineStore.filteredLibraryIdsForSelectedServer);
	const libraries = filteredLibraryIds.size === 0
		? allLibrariesForServer
		: allLibrariesForServer.filter((library) => filteredLibraryIds.has(library.id));

	return orderBy(
		libraries.map((library) => ({
			label: libraryStore.getLibraryName(library.id),
			value: library.id,
		})),
		[(option) => option.label.toLocaleLowerCase(), (option) => option.value],
		['asc', 'asc'],
	);
});

const zoomOptions: { label: string; value: LibraryAccessTimelineZoomPreset }[] = [
	{ label: '7d', value: '7d' },
	{ label: '30d', value: '30d' },
	{ label: '90d', value: '90d' },
	{ label: 'All', value: 'All' },
];

useSubscription(refreshTimeline$.pipe(
	debounceTime(100),
	switchMap(() => timelineStore.refreshTimeline()),
).subscribe());

onMounted(() => {
	if (accountStore.getAccounts.length > 0 && !timelineStore.filters.plexAccountId) {
		timelineStore.setAccount(accountStore.getAccounts[0]!.id);
	}

	refreshTimeline$.next();
	applyZoomPreset(timelineStore.zoomPreset);
});

watch(() => accountStore.getAccounts, (accounts) => {
	if (accounts.length > 0 && !timelineStore.filters.plexAccountId) {
		timelineStore.setAccount(accounts[0]!.id);
		refreshTimeline$.next();
	}
}, { deep: true });

function applyZoomPreset(zoomPreset: LibraryAccessTimelineZoomPreset) {
	if (zoomPreset === 'All') {
		timelineStore.setDateRange(null, null);
		return;
	}

	const days = Number.parseInt(zoomPreset.replace('d', ''), 10);
	const toUtc = new Date();
	toUtc.setHours(23, 59, 59, 999);
	const fromUtc = new Date(toUtc);
	fromUtc.setDate(toUtc.getDate() - (days - 1));
	fromUtc.setHours(0, 0, 0, 0);
	timelineStore.setDateRange(fromUtc.toISOString(), toUtc.toISOString());
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.library-access-timeline-page-scroll {
  height: calc(100vh - 6.5rem);
  overflow: hidden;
  align-items: stretch !important;

  > [class*='col-'] {
    height: 100%;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }

  > [class*='col-'] > .q-mx-md {
    flex: 0 0 auto;
  }

  > [class*='col-'] > .q-pa-md {
    min-height: 0;
    flex: 1 1 auto;
    display: flex;
  }
}

.library-access-timeline-layout {
  min-height: 0;
  flex: 1 1 auto;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 1rem;
  min-width: 0;
  width: 100%;
}

.library-access-timeline-filters {
  position: sticky;
  top: 0;
  z-index: 1;
  background: $dark-lg-background-color;
  backdrop-filter: blur(6px);
  min-width: 0;
  width: 100%;
}

.library-access-timeline-scroll-region {
  min-height: 0;
  overflow: hidden auto;
  padding-right: 0.25rem;
  min-width: 0;
  max-width: 100%;
}

.library-access-timeline-scroll-card {
  min-height: 18rem;
}
@media (max-width: $breakpoint-sm-max) {
  .library-access-timeline-page-scroll {
    height: auto;
    min-height: 100%;
    overflow: visible;
  }

  .library-access-timeline-layout {
    display: flex;
    flex-direction: column;
  }

  .library-access-timeline-scroll-region {
    min-height: 18rem;
    overflow: auto;
  }

  .library-access-timeline-filters .q-btn {
    min-width: 44px;
    min-height: 44px;
  }
}
</style>
