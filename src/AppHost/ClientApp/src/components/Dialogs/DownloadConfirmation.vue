<template>
	<QCardDialog
		:loading="loading"
		:name="DialogType.MediaDownloadConfirmationDialog"
		:type="[] as DownloadMediaDTO[]"
		cy="download-confirmation-dialog"
		full-height
		@opened="openDialog"
		@closed="cancelPreview">
		<template #top-row>
			<QRow class="download-confirmation-summary q-pa-md">
				<QCol>
					<QText size="h5">
						{{ t('components.download-confirmation.description') }}
					</QText>
				</QCol>
				<QCol cols="auto">
					<QText>
						{{ t('components.download-confirmation.total-size') }}
						<QFileSize
							:size="totalSize"
							class="q-ml-sm" />
					</QText>
				</QCol>
			</QRow>
			<TreeTable
				:lazy="true"
				:loading="loading"
				:size="'small'"
				class="download-confirmation-table-header">
				<Column
					v-for="(col, i) in getDownloadPreviewTableColumns"
					:key="i"
					:expander="i === 0"
					:field="col.field"
					:header="col.label"
					:style="{ 'width': col.type === 'file-size' ? 'var(--download-confirmation-size-width)' : col.type === 'media-quality' ? 'var(--download-confirmation-quality-width)' : col.width ? `${col.width}px` : 'auto', 'text-align': 'center' }" />
			</TreeTable>
		</template>
		<template #default>
			<q-banner
				v-if="previewFailed"
				data-cy="download-confirmation-error"
				role="alert">
				{{ t('components.download-confirmation.preview-error') }}
				<template #action>
					<q-btn
						data-cy="download-confirmation-retry"
						:label="t('components.download-confirmation.retry')"
						outline
						@click="requestPreview" />
				</template>
			</q-banner>
			<TreeTable
				v-else
				v-model:expanded-keys="expandedKeys"
				:lazy="true"
				:loading="loading"
				:size="'small'"
				:value="downloadPreview"
				class="download-confirmation-table-body">
				<Column
					v-for="(col, i) in getDownloadPreviewTableColumns"
					:key="i"
					:expander="i === 0 && hasAnyChildren"
					:field="col.field"
					:header="col.label"
					:style="{ width: col.type === 'file-size' ? 'var(--download-confirmation-size-width)' : col.type === 'media-quality' ? 'var(--download-confirmation-quality-width)' : col.width ? `${col.width}px` : 'auto' }">
					<template #body="{ node }: { node: DownloadPreviewDTO }">
						<template v-if="col.type === 'title'">
							<QMediaTypeIcon
								:media-type="node.type"
								:size="26" />
							<QText
								:cy="`column-title-${node.key}`"
								:value="node.title" />
						</template>
						<!-- Media Quality -->
						<MediaQuality
							v-else-if="col.type === 'media-quality'"
							:align="'center'"
							:data-cy="`column-${col.field}-${node.key}`"
							:qualities="node.qualities"
							class="q-mx-auto" />
						<!-- File Size -->
						<QFileSize
							v-else-if="col.type === 'file-size'"
							:cy="`column-dataTotal-${node.key}`"
							:size="node.size"
							align="center"
							class="q-mx-auto" />
					</template>
				</Column>
			</TreeTable>
		</template>
		<!-- Download Actions -->
		<template #actions="{ close }">
			<CancelButton
				cy="download-confirmation-cancel"
				@click="close()" />
			<div class="download-confirmation-actions">
				<div
					class="download-confirmation-destination"
					:title="selectedFolderPath.directory">
					<QText
						:value="t('components.download-confirmation.destination.selected')"
						bold="" />
					<QText
						:value="selectedDestination"
						class="download-confirmation-destination-path" />
				</div>
				<q-btn-dropdown
					data-cy="download-confirmation-submit"
					:disable="!canDownload"
					color="green"
					:label="t('general.commands.download')"
					outline
					split
					@click="onDownload(close)">
					<template #default>
						<q-list class="download-destination-menu">
							<q-item-label header>
								{{ $t('components.download-confirmation.destination.header') }}
							</q-item-label>
							<q-separator />
							<!-- Download Destination -->
							<q-item
								v-for="folderPath in folderPathDestinations"
								:key="folderPath.id"
								:data-cy="`download-confirmation-destination-${folderPath.id}`"
								clickable
								tag="label">
								<q-item-section avatar>
									<q-radio
										v-model="selectedFolderPath"
										:val="folderPath" />
								</q-item-section>
								<q-item-section>
									<q-item-label>{{ folderPath.displayName }}</q-item-label>
									<q-item-label caption>
										{{ folderPath.directory }}
									</q-item-label>
								</q-item-section>
							</q-item>
							<!-- Custom Directory -->
							<q-item
								data-cy="download-confirmation-destination-custom"
								clickable
								@click="dialogStore.openDirectoryBrowserDialog(customDirectory)">
								<q-item-section avatar>
									<q-radio
										v-model="selectedFolderPath"
										:val="customDirectory" />
								</q-item-section>
								<q-item-section>
									<q-item-label>
										{{ $t('components.download-confirmation.destination.custom-destination-option') }}
									</q-item-label>
									<q-item-label caption>
										{{ customDirectory.directory }}
									</q-item-label>
								</q-item-section>
							</q-item>
						</q-list>
					</template>
				</q-btn-dropdown>
			</div>
			<!--	Directory Browser	-->
			<DirectoryBrowser @confirm="onCustomDirectorySelected" />
		</template>
	</QCardDialog>
</template>

<script lang="ts" setup>
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { EMPTY, Subject, catchError, of, switchMap } from 'rxjs';
import {
	type CreateDownloadTasksRequest,
	type DownloadMediaDTO,
	type DownloadPreviewDTO,
	type FolderPathDTO,
	FolderType,
	PlexMediaType,
} from '@dto';
import { DialogType } from '@enums';
import { useI18n } from 'vue-i18n';
import { useDialogStore, useDownloadStore, useFolderPathStore } from '@store';
import Convert from '@class/Convert';
import Log from 'consola';

const { t } = useI18n();
const downloadStore = useDownloadStore();
const folderPathStore = useFolderPathStore();
const dialogStore = useDialogStore();

const emits = defineEmits<{
	(e: 'download', downloadCommand: CreateDownloadTasksRequest): void;
}>();
const expandedKeys = ref<Record<string, boolean>>({});
const loading = ref(true);
const previewFailed = ref(false);
const previewRequests = new Subject<DownloadMediaDTO[] | null>();
const downloadPreview = ref<DownloadPreviewDTO[]>([]);
const downloadMediaCommand = ref<DownloadMediaDTO[]>([]);
const mediaType = ref<PlexMediaType>(PlexMediaType.Unknown);
const totalSize = ref(0);
const customDirectory = ref<FolderPathDTO>({
	id: 0,
	displayName: 'Custom',
	directory: '',
	mediaType: get(mediaType),
	folderType: FolderType.Unknown,
	isValid: true,
	isDefault: false,
});

const getDownloadPreviewTableColumns = computed((): {
	label: string;
	field: keyof DownloadPreviewDTO;
	type?: 'title' | 'duration' | 'file-size' | 'file-speed' | 'date' | 'actions' | 'datetime' | 'percentage' | 'index' | 'media-quality';
	width?: number;
}[] => {
	return [
		{
			label: t('components.download-confirmation.columns.title'),
			field: 'title',
			type: 'title',
		},
		...(Convert.mediaTypeToFolderType(get(mediaType)) === FolderType.MusicFolder
			|| Convert.mediaTypeToFolderType(get(mediaType)) === FolderType.PhotosFolder
			? []
			: [{
					label: t('components.media-list.columns.quality'),
					field: 'qualities' as const,
					type: 'media-quality' as const,
					width: 200,
				}]),
		{
			label: t('components.download-confirmation.columns.file-size'),
			field: 'size',
			type: 'file-size',
			width: 150,
		},
	];
});

const hasAnyChildren = computed(() => get(downloadPreview).some((x) => x.children && x.children.length > 0));

const selectedFolderPath = ref<FolderPathDTO>(get(customDirectory));

const folderPathDestinations = computed(() => folderPathStore.getFolderPaths().filter((path) =>
	Convert.mediaTypeToFolderType(path.mediaType) === Convert.mediaTypeToFolderType(get(mediaType)),
));
const canDownload = computed(() => !get(loading) && !get(previewFailed) && get(downloadPreview).length > 0);

const selectedDestination = computed(() => get(selectedFolderPath).directory || t('components.download-confirmation.destination.not-set'));

function openDialog(data: DownloadMediaDTO[]): void {
	set(mediaType, data.find((item) => item.type === PlexMediaType.Movie)?.type ?? data[0]?.type ?? PlexMediaType.Unknown);
	set(customDirectory, {
		...get(customDirectory),
		mediaType: get(mediaType),
		folderType: Convert.mediaTypeToFolderType(get(mediaType)),
	});
	const destinations = get(folderPathDestinations);
	set(selectedFolderPath, destinations.find((path) => path.isDefault) ?? destinations[0] ?? get(customDirectory));
	set(downloadMediaCommand, data);
	requestPreview();
}

function requestPreview(): void {
	cancelPreview();
	reset();
	set(previewFailed, false);
	set(loading, true);
	previewRequests.next(get(downloadMediaCommand));
}

function onPreviewError(): void {
	Log.error('Download preview failed');
	reset();
	set(previewFailed, true);
	set(loading, false);
}

function cancelPreview(): void {
	previewRequests.next(null);
}

useSubscription(
	previewRequests.pipe(
		switchMap((data) => data
			? downloadStore.previewDownload(data).pipe(catchError(() => of(null)))
			: EMPTY),
	).subscribe((result) => {
		if (!result || result.previews.length === 0) {
			onPreviewError();
			return;
		}
		set(downloadPreview, Object.freeze(result.previews));
		set(totalSize, result.totalSize);
		set(expandedKeys, result.expanded);
		set(loading, false);
	}),
);

function onCustomDirectorySelected(path: FolderPathDTO): void {
	set(customDirectory, path);
	set(selectedFolderPath, get(customDirectory));
}

function onDownload(close: () => void) {
	if (!get(canDownload))
		return;
	emits('download', {
		downloadMedias: get(downloadMediaCommand),
		destinationFolderPathId: get(selectedFolderPath).id > 0 ? get(selectedFolderPath).id : 0,
		customDestinationFolderPath: get(selectedFolderPath).id === 0 ? get(selectedFolderPath).directory : '',
	});
	close();
}

function reset() {
	set(downloadPreview, []);
	set(totalSize, 0);
	set(expandedKeys, {});
}
</script>

<style lang="scss">
.download-confirmation-table-header,
.download-confirmation-table-body {
  --download-confirmation-size-width: 150px;
  --download-confirmation-quality-width: 200px;
}

.download-confirmation-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
  min-width: 0;
}

@media (max-width: $breakpoint-xs-max) {
  @media (min-height: 480px) {
    .download-confirmation-summary {
      flex-direction: column;
      gap: 0.5rem;
    }

    .download-confirmation-actions {
      width: 100%;
      flex-direction: column;
      align-items: stretch;
    }
  }

  .download-confirmation-destination {
    max-width: 100%;
  }

  .download-confirmation-actions .q-btn-dropdown {
    min-height: 44px;
  }

  .download-confirmation-actions .q-btn-dropdown .q-btn {
    min-width: 44px;
    min-height: 44px;
  }

  .download-confirmation-table-header {
    display: none;
  }

  .download-confirmation-table-body {
    --download-confirmation-size-width: 5rem;
    --download-confirmation-quality-width: 6rem;
    overflow-x: auto;

    .p-treetable-thead {
      display: table-header-group;
    }
  }
  .download-confirmation-destination-path {
    min-width: 0;
    max-width: 100%;

    .col,
    .q-text {
      display: block;
      min-width: 0;
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }
}

.download-confirmation-destination {
  min-width: 0;
  max-width: 320px;
}

.download-confirmation-destination-path {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.download-confirmation-table-header {
  padding-right: 10px;

  .p-treetable-thead tr > th:not(:first-child) .p-treetable-column-title {
    margin: 0 auto;
    padding-left: 1rem;
  }

  .p-treetable-empty-message {
    display: none;
  }
}

.download-confirmation-table-body {
  .p-treetable-tbody > tr > td:first-child .q-text-container {
    flex: 1;
    min-width: 0;

    .col {
      min-width: 0;
    }

    .q-text {
      display: block;
      white-space: normal;
      overflow-wrap: anywhere;
    }
  }

  .p-treetable-thead {
    display: none;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .download-confirmation-destination {
    max-width: 100%;
  }
}
</style>
