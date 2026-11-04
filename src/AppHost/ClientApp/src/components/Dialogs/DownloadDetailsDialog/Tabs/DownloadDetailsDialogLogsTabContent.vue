<template>
	<div class="logs-panel">
		<!-- Logs Toolbar -->
		<q-toolbar
			class="logs-toolbar q-px-none q-pb-xs">
			<!-- Log Copy -->
			<q-btn
				:aria-label="t('components.download-details-dialog.logs.copy-all')"
				dense
				flat
				icon="mdi-content-copy"
				round
				@click="copyLogs">
				<q-tooltip>{{ t('components.download-details-dialog.logs.copy-all') }}</q-tooltip>
			</q-btn>
			<!-- Log Sort -->
			<q-btn
				:aria-label="sortAsc ? t('components.download-details-dialog.logs.sort-oldest-first') : t('components.download-details-dialog.logs.sort-newest-first')"
				:icon="sortAsc ? 'mdi-sort-ascending' : 'mdi-sort-descending'"
				dense
				flat
				round
				@click="toggleSort">
				<q-tooltip>{{ sortAsc ? t('components.download-details-dialog.logs.sort-oldest-first') : t('components.download-details-dialog.logs.sort-newest-first') }}</q-tooltip>
			</q-btn>
			<!-- Log Filter -->
			<q-btn
				:aria-label="t('components.download-details-dialog.logs.filter-by-level')"
				dense
				flat
				icon="mdi-filter-outline"
				round>
				<q-tooltip>{{ t('components.download-details-dialog.logs.filter-by-level') }}</q-tooltip>
				<q-menu auto-close>
					<q-list dense>
						<q-item
							v-for="level in allLevels"
							:key="level"
							clickable
							@click.stop="toggleLevel(level)">
							<q-item-section avatar>
								<q-checkbox
									:model-value="activeFilters.includes(level)"
									:val="level"
									dense
									@update:model-value="toggleLevel(level)" />
							</q-item-section>
							<q-item-section avatar>
								<q-icon
									:color="Convert.logLevelToColor(level)"
									:name="Convert.logLevelToIcon(level)"
									size="xs" />
							</q-item-section>
							<q-item-section>{{ logLevelLabels[level] }}</q-item-section>
						</q-item>
					</q-list>
				</q-menu>
			</q-btn>
			<q-toolbar-title />
			<!-- Delete Logs -->
			<q-btn
				:aria-label="t('components.download-details-dialog.logs.delete-all')"
				dense
				flat
				icon="mdi-delete-sweep"
				round
				@click="dialogStore.openDialog(DialogType.DownloadDetailsDeleteLogsConfirmationDialog)">
				<q-tooltip>{{ t('components.download-details-dialog.logs.delete-all') }}</q-tooltip>
			</q-btn>
		</q-toolbar>

		<!-- Logs Virtual List -->
		<div
			ref="scrollEl"
			class="logs-scroll-container">
			<!-- Empty State Banner -->
			<template v-if="!logsLoading && filteredLogs.length === 0">
				<QAlert type="info">
					{{ t('components.download-details-dialog.logs.no-logs') }}
				</QAlert>
			</template>
			<template v-else>
				<!-- Total height spacer — required by TanStack Virtual -->
				<div
					class="logs-virtual-spacer"
					:style="{ height: `${virtualizer.getTotalSize()}px` }">
					<div
						v-for="row in virtualizer.getVirtualItems()"
						:key="String(row.key)"
						:ref="el => measureRow(el as Element | null, row)"
						class="log-row"
						:data-last="row.index === filteredLogs.length - 1"
						:data-index="row.index"
						:style="{
							position: 'absolute',
							top: 0,
							left: 0,
							width: '100%',
							transform: `translateY(${row.start}px)`,
						}">
						<QTimeline
							class="log-timeline"
							layout="dense"
							side="right">
							<QTimelineEntry
								:id="`download-log-entry-${getLogAtIndex(row.index).id}`"
								class="log-timeline-entry"
								:class="{ 'log-timeline-entry--fresh': isFreshLog(getLogAtIndex(row.index).id) }"
								:color="Convert.logLevelToColor(getLogAtIndex(row.index).logLevel)"
								:icon="Convert.logLevelToIcon(getLogAtIndex(row.index).logLevel)"
								:title="translateDownloadStatus(getLogAtIndex(row.index).status)"
								@click="copyLogEntry(getLogAtIndex(row.index))">
								<q-tooltip
									class="log-level-tooltip"
									persistent
									:target="logIconTooltipTarget(getLogAtIndex(row.index).id)">
									{{ logLevelLabels[getLogAtIndex(row.index).logLevel] }}
								</q-tooltip>
								<template #subtitle>
									<div class="log-timeline-entry__subtitle-row">
										<QDateTime
											:text="getLogAtIndex(row.index).createdAt"
											short-date
											time />
										<q-icon
											class="log-timeline-entry__copy-icon"
											name="mdi-content-copy"
											size="sm" />
									</div>
								</template>
								<div class="log-timeline-entry__body">
									<QText :value="getLogAtIndex(row.index).message" />
								</div>
							</QTimelineEntry>
						</QTimeline>
					</div>
				</div>
			</template>
			<!-- Loading Indicator -->
			<QRow
				v-if="logsLoading"
				align="center"
				column>
				<QCol cols="auto">
					<QSpinnerDots
						color="primary"
						size="40px" />
				</QCol>
			</QRow>
		</div>

		<!-- Delete Logs Confirmation Dialog -->
		<ConfirmationDialog
			:name="DialogType.DownloadDetailsDeleteLogsConfirmationDialog"
			:title="t('components.download-details-dialog.logs.delete-confirmation.title')"
			:text="t('components.download-details-dialog.logs.delete-confirmation.text')"
			@confirm="deleteLogs" />
	</div>
</template>

<script lang="ts" setup>
import { get, set, useClipboard } from '@vueuse/core';
import { useVirtualizer, type VirtualItem } from '@tanstack/vue-virtual';
import { format } from 'date-fns';
import { downloadApi } from '@api';
import type { BaseResultDTO, DownloadTaskDTO, DownloadTaskLogDTO } from '@dto';
import { NotificationLevel } from '@dto';
import Convert from '@class/Convert';
import { translateDownloadStatus, showSuccessNotification } from '@composables';
import { DialogType } from '@enums';
import { useDialogStore } from '@store';

const INITIAL_TAKE = 50;
const NEW_LOG_ANIMATION_MS = 2200;

const { t } = useI18n();
const { copy } = useClipboard({ legacy: true });
const dialogStore = useDialogStore();

const props = defineProps<{
	downloadTaskId: string;
	downloadTask?: DownloadTaskDTO;
	initialLogs?: DownloadTaskLogDTO[];
}>();

const emit = defineEmits<{
	(e: 'logs-deleted'): void;
	(e: 'logs-refreshed', logs: DownloadTaskLogDTO[]): void;
}>();

const scrollEl = ref<HTMLElement | null>(null);
const sortAsc = ref(true);
const activeFilters = ref<NotificationLevel[]>([...Object.values(NotificationLevel).filter((v) => v !== NotificationLevel.None)]);
const logs = ref<DownloadTaskLogDTO[]>([]);
const logsLoading = ref(false);
const highestSeenId = ref<number | undefined>(undefined);
const freshLogIds = ref<number[]>([]);
const logRefreshTimer = useIntervalFn(() => refreshLogs(), 1000, { immediate: false });

// Sync initial logs from parent pre-fetch (first 50)
watch(
	() => props.initialLogs,
	(newLogs) => {
		if (newLogs && newLogs.length > 0 && get(logs).length === 0) {
			set(logs, [...newLogs]);
			const maxId = Math.max(...newLogs.map((l) => l.id));
			set(highestSeenId, maxId);
		}
	},
	{ immediate: true },
);

const allLevels = Object.values(NotificationLevel).filter((v) => v !== NotificationLevel.None && v !== NotificationLevel.Verbose);

const logLevelLabels = computed<Record<NotificationLevel, string>>(() => ({
	[NotificationLevel.None]: t('general.commands.unknown'),
	[NotificationLevel.Verbose]: t('general.logs.level.verbose'),
	[NotificationLevel.Debug]: t('general.logs.level.debug'),
	[NotificationLevel.Information]: t('general.logs.level.information'),
	[NotificationLevel.Success]: t('general.logs.level.success'),
	[NotificationLevel.Warning]: t('general.logs.level.warning'),
	[NotificationLevel.Error]: t('general.logs.level.error'),
	[NotificationLevel.Fatal]: t('general.logs.level.fatal'),
}));

const filteredLogs = computed(() => {
	const filtered = get(logs).filter((item) => get(activeFilters).includes(item.logLevel));
	return [...filtered].sort((a, b) => {
		const diff = a.id - b.id;
		return get(sortAsc) ? diff : -diff;
	});
});

// TanStack Virtual — variable height rows via measureElement
const virtualizer = useVirtualizer(computed(() => ({
	count: filteredLogs.value.length,
	getScrollElement: () => get(scrollEl),
	estimateSize: () => 120,
	overscan: 5,
	getItemKey: (i: number) => filteredLogs.value[i]!.id,
})));

function getLogAtIndex(index: number) {
	return filteredLogs.value[index]!;
}

function isFreshLog(id: number) {
	return freshLogIds.value.includes(id);
}

function trackFreshLogs(ids: number[]) {
	if (ids.length === 0) {
		return;
	}

	set(freshLogIds, [...new Set([...freshLogIds.value, ...ids])]);
	window.setTimeout(() => {
		set(freshLogIds, freshLogIds.value.filter((id) => !ids.includes(id)));
	}, NEW_LOG_ANIMATION_MS);
}

function measureRow(el: Element | null, _row: VirtualItem) {
	if (el) {
		get(virtualizer).measureElement(el);
	}
}

function toggleSort() {
	set(sortAsc, !get(sortAsc));
	// After sort flip, scroll to top so the user sees the reordered list
	get(scrollEl)?.scrollTo({ top: 0 });
}

function copyLogs() {
	const text = get(logs)
		.map((item) => formatLogCopyText(item))
		.join('\n');
	copy(text);
	showSuccessNotification(t('components.download-details-dialog.logs.copied-to-clipboard'));
}

function copyLogEntry(item: DownloadTaskLogDTO) {
	copy(formatLogCopyText(item));
	showSuccessNotification(t('components.download-details-dialog.logs.entry-copied-to-clipboard'));
}

function formatLogCopyText(item: DownloadTaskLogDTO) {
	return `[${format(new Date(item.createdAt), 'yyyy-MM-dd HH:mm:ss')}] [${item.logLevel}] ${item.message}`;
}

function logIconTooltipTarget(logId: number) {
	return `#download-log-entry-${logId} .q-timeline__dot .q-icon`;
}

function deleteLogs() {
	if (!props.downloadTaskId || !props.downloadTask) {
		return;
	}

	set(logsLoading, true);
	logRefreshTimer.pause();
	dialogStore.closeDialog(DialogType.DownloadDetailsDeleteLogsConfirmationDialog);

	useSubscription(
		downloadApi.deleteAllDownloadTaskLogsByDownloadTaskIdEndpoint(props.downloadTaskId, {
			type: props.downloadTask.downloadTaskType,
			plexLibraryId: props.downloadTask.plexLibraryId,
			plexServerId: props.downloadTask.plexServerId,
		}).subscribe({
			next: (result) => {
				const deleteResult = result as BaseResultDTO;
				if (!deleteResult.isSuccess) {
					set(logsLoading, false);
					logRefreshTimer.resume();
					return;
				}

				set(logs, []);
				set(highestSeenId, undefined);
				emit('logs-deleted');
				showSuccessNotification(t('components.download-details-dialog.logs.deleted'), 5000);
				set(logsLoading, false);
				logRefreshTimer.resume();
			},
			error: () => {
				set(logsLoading, false);
				logRefreshTimer.resume();
			},
		}),
	);
}

function toggleLevel(level: NotificationLevel) {
	const current = get(activeFilters);
	if (current.includes(level)) {
		set(activeFilters, current.filter((l) => l !== level));
	} else {
		set(activeFilters, [...current, level]);
	}
}

function reset() {
	set(sortAsc, false);
	set(activeFilters, [...Object.values(NotificationLevel).filter((v) => v !== NotificationLevel.None)]);
}

function refreshLogs() {
	if (!props.downloadTaskId || !props.downloadTask) {
		return;
	}

	logRefreshTimer.pause();

	const sinceId = get(highestSeenId);
	// On the very first poll (no initialLogs from parent), use take to limit initial load
	const take = sinceId === undefined ? INITIAL_TAKE : undefined;

	useSubscription(
		downloadApi.getDownloadTaskLogsByDownloadTaskIdEndpoint(props.downloadTaskId, {
			type: props.downloadTask.downloadTaskType,
			plexLibraryId: props.downloadTask.plexLibraryId,
			plexServerId: props.downloadTask.plexServerId,
			sinceId,
			take,
		}).subscribe((data) => {
			if (data.isSuccess && data.value && data.value.length > 0) {
				if (sinceId !== undefined) {
					trackFreshLogs(data.value.map((l) => l.id));
				}
				set(logs, [...get(logs), ...data.value]);
				const maxId = Math.max(...data.value.map((l) => l.id));
				set(highestSeenId, Math.max(get(highestSeenId) ?? 0, maxId));
				emit('logs-refreshed', get(logs));
			}
			set(logsLoading, false);
			logRefreshTimer.resume();
		}),
	);
}

onMounted(() => {
	// If initialLogs were pre-fetched by parent, skip the initial full fetch — just poll for new ones
	if (get(logs).length === 0) {
		set(logsLoading, true);
	}
	logRefreshTimer.resume();
});

onUnmounted(() => {
	logRefreshTimer.pause();
	set(logs, []);
	set(freshLogIds, []);
	set(highestSeenId, undefined);
});

defineExpose({ reset });
</script>

<style lang="scss">
:root {
  --logs-horizontal-padding: 8px;
  --logs-content-gutter: 1rem;
  --logs-dot-size: 47px;
  --logs-dot-tail-offset-top: 56px;
  --logs-dot-tail-offset-left: 22px;
  --logs-icon-size: 2rem;
  --logs-copy-fade-ms: 180ms;
  --logs-fresh-pulse-ms: 2200ms;
  --logs-fresh-pulse-ease: cubic-bezier(0.16, 1, 0.3, 1);
}

.q-tab-panel:has(.logs-panel) {
  min-height: 0;
  height: 100%;
  padding: 0;
}

.logs-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  padding-inline: var(--logs-horizontal-padding);
}

.logs-toolbar {
  flex: 0 0 auto;
}

.logs-scroll-container {
  flex: 1;
  min-height: 0;
  max-width: 100%;
  overflow-y: auto;

  .log-container {
    margin: 0px 1rem;
  }
}

.logs-virtual-spacer {
  position: relative;
  margin-inline: var(--logs-content-gutter);
}

.log-row {
  inset-inline: 0;
}

.log-timeline {
  width: 100%;
  margin: 0;

  .q-timeline__entry--icon .q-timeline__dot {
    left: -16px;
  }
}

.log-timeline .q-timeline__entry:last-child .q-timeline__dot::after {
  content: '';
}

.log-row[data-last='true'] .q-timeline__entry:last-child .q-timeline__dot::after {
  content: none;
}

.log-timeline-entry {
  cursor: copy;
  margin: 0;

  .q-timeline__dot {
    width: var(--logs-dot-size);

    &::before {
      display: none;
    }

    &::after {
      top: var(--logs-dot-tail-offset-top);
      left: var(--logs-dot-tail-offset-left);
    }

    .q-icon {
      color: currentColor;
      font-size: var(--logs-icon-size);
      height: 56px;
      line-height: 56px;
    }
  }

  .q-timeline__subtitle {
    padding-top: 12px;
  }

  .q-timeline__content {
    position: relative;
    padding-bottom: 24px;
  }

  &:hover .log-timeline-entry__copy-icon {
    opacity: 1;
  }
}

.log-timeline-entry--fresh {
  animation: log-entry-pulse var(--logs-fresh-pulse-ms) var(--logs-fresh-pulse-ease);

  .q-timeline__content,
  .q-timeline__dot,
  .q-timeline__subtitle {
    animation: log-entry-pulse var(--logs-fresh-pulse-ms) var(--logs-fresh-pulse-ease);
  }
}

.log-timeline-entry__subtitle-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.log-timeline-entry__copy-icon {
  opacity: 0;
  transition: opacity var(--logs-copy-fade-ms) ease;
  flex-shrink: 0;
}

@media (prefers-reduced-motion: reduce) {
  .log-timeline-entry--fresh,
  .log-timeline-entry--fresh .q-timeline__content,
  .log-timeline-entry--fresh .q-timeline__dot,
  .log-timeline-entry--fresh .q-timeline__subtitle,
  .log-timeline-entry__copy-icon {
    animation: none;
    transition: none;
  }
}

@keyframes log-entry-pulse {
  0% {
    opacity: 0.72;
    transform: translateY(4px);
  }

  45% {
    opacity: 1;
    transform: translateY(0);
  }

  100% {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
