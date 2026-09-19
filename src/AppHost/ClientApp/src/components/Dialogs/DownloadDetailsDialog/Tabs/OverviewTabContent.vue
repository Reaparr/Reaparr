<template>
	<q-markup-table
		v-if="downloadTask"
		class="section-table">
		<tbody>
			<tr>
				<td style="width: 25%">
					{{ t('components.download-details-dialog.overview.status') }}
				</td>
				<td data-cy="download-details-dialog-status">
					{{ downloadTask.status }}
				</td>
			</tr>
			<tr v-if="downloadTask.fileName">
				<td>{{ t('components.download-details-dialog.overview.file-name') }}</td>
				<td data-cy="download-details-dialog-file-name">
					{{ downloadTask.fileName }}
				</td>
			</tr>
			<tr>
				<td>{{ t('components.download-details-dialog.overview.download-path') }}</td>
				<td data-cy="download-details-dialog-download-path">
					{{ downloadTask.downloadDirectory }}
				</td>
			</tr>
			<tr>
				<td>{{ t('components.download-details-dialog.overview.destination-path') }}</td>
				<td data-cy="download-details-dialog-destination-path">
					{{ downloadTask.destinationDirectory }}
				</td>
			</tr>
			<tr v-if="downloadTask.downloadUrl">
				<td>{{ t('components.download-details-dialog.overview.download-url') }}</td>
				<td data-cy="download-details-dialog-download-url">
					<div class="download-url-row">
						<span class="download-url-text">
							{{ downloadTask.downloadUrl }}
						</span>
						<div class="download-url-action">
							<ExternalLinkButton :href="downloadTask.downloadUrl" />
						</div>
					</div>
				</td>
			</tr>
		</tbody>
	</q-markup-table>
	<div v-else>
		<h4>{{ $t('components.download-details-dialog.invalid-download-task.body') }}</h4>
		<ul>
			<li
				v-for="(error, index) in errors"
				:key="index">
				{{ error.message }}
			</li>
		</ul>
	</div>
</template>

<script lang="ts" setup>
import type { DownloadTaskDTO, ErrorDTO } from '@dto';

const { t } = useI18n();

defineProps<{
	downloadTask: DownloadTaskDTO | undefined;
	errors: ErrorDTO[];
}>();
</script>

<style lang="scss">
.section-table {
  width: 100%;
  table-layout: fixed;
}

.download-url-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: flex-start;
  gap: 0.5rem;
  width: 100%;
}

.download-url-text {
  display: block;
  min-width: 0;
  max-width: 100%;
  white-space: normal;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.download-url-action {
  flex-shrink: 0;
}

@media (max-width: $breakpoint-sm-max) {
  .section-table {
    table-layout: auto;
    white-space: normal;

    tbody,
    tr,
    td {
      display: block;
      width: 100%;
    }

    tr {
      padding: 0.5rem 0;
      height: auto !important;
    }

    td {
      height: auto !important;
      max-width: none;
      padding: 0.25rem 0.75rem;
      white-space: normal;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    td:first-child {
      width: 100% !important;
      font-weight: 600;
      border-bottom: 0;
    }
  }
}
</style>
