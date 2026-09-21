<template>
	<QCardDialog
		:loading="loading"
		:name="DialogType.DownloadDetailsDialog"
		:type="'' as string"
		:aria-label="downloadTask?.fullTitle"
		close-button
		full-height
		@closed="onClose"
		@opened="onOpen">
		<!-- Title -->
		<template #title>
			<template v-if="downloadTask">
				<QText
					:value="downloadTask.fullTitle"
					cy="download-details-dialog-title"
					size="h6">
					<template #prepend>
						<QMediaTypeIcon
							:media-type="downloadTask.mediaType"
							:size="36"
							class="q-mr-sm" />
					</template>
				</QText>
			</template>
			<template v-else>
				{{ $t('components.download-details-dialog.invalid-download-task.title') }}
			</template>
		</template>
		<template #default>
			<div class="layout-container">
				<div class="tabs">
					<q-tabs
						v-model="tabIndex"
						active-color="red"
						vertical>
						<!-- Overview Tab -->
						<q-tab
							class="q-mr-md"
							data-cy="download-details-dialog-tab-1"
							icon="mdi-chart-box-outline"
							label="Overview"
							name="overview" />

						<!-- Logs Tab -->
						<q-tab
							class="q-mr-md"
							data-cy="download-details-dialog-tab-2"
							icon="mdi-text-box-outline"
							label="Logs"
							name="logs" />
					</q-tabs>
				</div>
				<div class="panels">
					<q-tab-panels
						v-model="tabIndex"
						animated
						keep-alive
						transition-next="slide-up"
						transition-prev="slide-down"
						vertical>
						<!-- Overview Panel -->
						<q-tab-panel
							data-cy="download-details-dialog-tab-content-1"
							name="overview">
							<OverviewTabContent
								:download-task="downloadTask"
								:errors="errors" />
						</q-tab-panel>

						<!-- Logs Panel -->
						<q-tab-panel
							data-cy="download-details-dialog-tab-content-2"
							name="logs">
							<DownloadDetailsDialogLogsTabContent
								:download-task="downloadTask"
								:download-task-id="downloadTaskId"
								:initial-logs="logs"
								@logs-deleted="handleLogsDeleted"
								@logs-refreshed="handleLogsRefreshed" />
						</q-tab-panel>
					</q-tab-panels>
				</div>
			</div>
		</template>
	</QCardDialog>
</template>

<script lang="ts" setup>
import { set, get } from '@vueuse/core';
import type { DownloadTaskDTO, DownloadTaskLogDTO, ErrorDTO } from '@dto';
import { downloadApi } from '@api';
import { DialogType } from '@enums';
import { useDownloadStore } from '@store';

const downloadStore = useDownloadStore();

const tabIndex = ref<string>('overview');

const loading = ref(true);

const downloadTaskId = ref<string>('');
const downloadTask = ref<DownloadTaskDTO>();

const errors = ref<ErrorDTO[]>([]);

const logs = ref<DownloadTaskLogDTO[]>([]);

function fetchLogs() {
	const task = get(downloadTask);
	const taskId = get(downloadTaskId);
	if (!taskId || !task) {
		return;
	}

	useSubscription(
		downloadApi.getDownloadTaskLogsByDownloadTaskIdEndpoint(taskId, {
			type: task.downloadTaskType,
			plexLibraryId: task.plexLibraryId,
			plexServerId: task.plexServerId,
			take: 50,
		}).subscribe((data) => {
			if (data.isSuccess && data.value) {
				set(logs, [...data.value].sort((a, b) => b.id - a.id));
			}
		}),
	);
}

function onOpen(event: string) {
	set(loading, true);
	set(downloadTaskId, event);
	set(downloadTask, downloadStore.getDownloadTaskById(event) as DownloadTaskDTO | null);
	set(errors, []);
	set(logs, []);

	useSubscription(downloadApi.getDownloadTaskByGuidEndpoint(get(downloadTaskId)).subscribe((data) => {
		if (data.isSuccess && data.value) {
			set(errors, []);
			set(downloadTask, data.value);
			fetchLogs();
		} else {
			set(errors, data?.errors ?? []);
		}
		set(loading, false);
	}));
}

function onClose() {
	set(downloadTask, null);
	set(downloadTaskId, '');
	set(errors, []);
	set(loading, true);
	set(tabIndex, 'overview');
	set(logs, []);
}

function handleLogsDeleted() {
	set(logs, []);
}

function handleLogsRefreshed(updated: DownloadTaskLogDTO[]) {
	set(logs, updated);
}
</script>

<style lang="scss">
.layout-container {
  height: 100%;
  min-height: 0;
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  grid-template-rows: repeat(1, 1fr);
  gap: 0;
  flex-grow: 1;

  .tabs {
    grid-column: span 1 / span 1;
    min-height: 0;
  }

  .panels {
    grid-column: span 7 / span 7;
    display: flex;
    min-height: 0;

    .q-tab-panels {
      display: flex;
      flex-grow: 1;
      min-height: 0;
    }

    .q-panel,
    .q-tab-panel {
      height: 100%;
      min-height: 0;
    }
  }
}

@media (max-width: $breakpoint-xs-max) {
  .layout-container {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: min-content minmax(0, 1fr);

    .tabs {
      grid-column: 1;
      min-width: 0;

      .q-tabs {
        flex-direction: row;
      }
    }

    .panels {
      grid-column: 1;
      min-width: 0;

      .q-tab-panel {
        padding: 0.75rem;
      }
    }
  }
}
</style>
