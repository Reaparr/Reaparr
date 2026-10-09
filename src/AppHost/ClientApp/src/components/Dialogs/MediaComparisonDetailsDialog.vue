<template>
	<QCardDialog
		:name="DialogType.MediaComparisonDetailsDialog"
		close-button
		full-height
		full-width
		cy="media-comparison-details-dialog"
		@opened="onOpen"
		@closed="onClose">
		<template #title>
			<div class="media-comparison-details__header">
				<MediaComparisonStateButton
					v-if="selectedMediaItem"
					show-tooltip
					:comparison-state="getPlexMediaComparisonState(selectedMediaItem)"
					:media-type="selectedMediaItem.type"
					dense
					cy="media-comparison-details-dialog-state" />
				<QText
					class="media-comparison-details__title"
					size="h6"
					:value="selectedMediaItem?.title ?? t('general.error.unknown')" />
				<BaseButton
					v-if="$q.screen.gt.sm"
					class="media-comparison-details__download-button media-comparison-details__download-button--desktop"
					:label="t('components.media-overview.comparison.download-selected')"
					icon="mdi-download"
					:disabled="!canDownload || selectedDownloadRows.length === 0"
					cy="media-comparison-details-dialog-download-button"
					@click="downloadRows(selectedDownloadRows)" />
			</div>
		</template>

		<template #default>
			<div
				v-if="selectedMediaItem"
				class="media-comparison-details">
				<div
					v-if="comparisonFailed"
					role="alert"
					class="q-pa-md"
					data-cy="media-comparison-details-error">
					<QText :value="t('components.media-overview.comparison.details-load-error')" />
					<BaseButton
						:label="t('components.download-confirmation.retry')"
						cy="media-comparison-details-retry"
						@click="requestComparison" />
				</div>
				<QTreeTable
					v-if="!comparisonFailed"
					class="media-comparison-details__table"
					data-cy="media-comparison-details-table"
					:loading="loading"
					:nodes="detailTreeRows"
					:columns="responsiveComparisonColumns"
					:selection-keys="isMusic ? undefined : selectedRows"
					@selected="onSelectionChange">
					<template
						v-if="isMusic"
						#header-title>
						<QCheckbox
							:model-value="getMusicSelectionValue(comparisonRows)"
							:disable="!canDownload || getMissingMusicTracks(comparisonRows).length === 0"
							:aria-label="t('components.media-overview.comparison.download-selected')"
							data-cy="media-comparison-details-select-all"
							@update:model-value="toggleMusicSelection(comparisonRows, $event === true)" />
						<QText :value="t('components.media-overview.comparison.details-column-title')" />
					</template>
					<template #cell-title="{ node, data }: { node: IComparisonDetailTreeNode; data: IComparisonDetailsRow }">
						<div class="media-comparison-details__row-content">
							<div class="media-comparison-details__row-title">
								<QCheckbox
									v-if="isMusic"
									:model-value="getMusicSelectionValue([data])"
									:disable="!canDownload || getMissingMusicTracks([data]).length === 0"
									:aria-label="data.title"
									:data-cy="`media-comparison-details-select-${node.key}`"
									@update:model-value="toggleMusicSelection([data], $event === true)"
									@click.stop />
								<MediaComparisonStateButton
									:comparison-state="getComparisonState(data)"
									:media-type="data.type"
									show-tooltip
									dense />
								<QText
									:cy="`media-comparison-details-title-${node.key}`"
									:value="data.title" />
								<IconSquareButton
									v-if="isMusic && $q.screen.lt.md && getMissingMusicTracks([data]).length > 0"
									:cy="`media-comparison-details-download-${node.key}`"
									icon="mdi-download"
									:tooltip-text="t('components.media-overview.comparison.download-selected')"
									:disabled="!canDownload"
									dense
									@click.stop="downloadRows([data])" />
							</div>
							<div
								v-if="$q.screen.lt.md"
								class="media-comparison-details__metadata">
								<div
									v-if="!isMusic"
									class="media-comparison-details__metadata-item">
									<QText
										class="media-comparison-details__metadata-label"
										size="caption"
										:value="t('components.media-overview.comparison.details-column-owned-quality')" />
									<MediaVideoQuality :quality="data.ownedQuality ?? VideoQuality.None" />
								</div>
								<div
									v-if="!isMusic"
									class="media-comparison-details__metadata-item">
									<QText
										class="media-comparison-details__metadata-label"
										size="caption"
										:value="t('components.media-overview.comparison.details-column-remote-quality')" />
									<MediaVideoQuality :quality="data.remoteQuality ?? VideoQuality.None" />
								</div>
								<div class="media-comparison-details__metadata-item media-comparison-details__metadata-location">
									<QText
										class="media-comparison-details__metadata-label"
										size="caption"
										:value="t('components.media-overview.comparison.details-column-location')" />
									<div class="media-comparison-details__location">
										<QText :value="serverStore.getServerName(data.plexServerId)" />
										<QIcon name="mdi-arrow-right-thin" />
										<QText :value="libraryStore.getLibraryName(data.plexLibraryId)" />
									</div>
								</div>
							</div>
						</div>
					</template>
					<template #cell-ownedQuality="{ data }: { data: IComparisonDetailsRow }">
						<MediaVideoQuality :quality="data.ownedQuality ?? VideoQuality.None" />
					</template>
					<template #cell-remoteQuality="{ data }: { data: IComparisonDetailsRow }">
						<MediaVideoQuality :quality="data.remoteQuality ?? VideoQuality.None" />
					</template>
					<template #cell-location="{ data }: { data: IComparisonDetailsRow }">
						<QRow
							align="center"
							no-wrap>
							<QCol cols="auto">
								<QText :value="serverStore.getServerName(data.plexServerId)" />
							</QCol>
							<QCol cols="auto">
								<QIcon
									class="q-mx-xs"
									name="mdi-arrow-right-thin" />
							</QCol>
							<QCol>
								<QText :value="libraryStore.getLibraryName(data.plexLibraryId)" />
							</QCol>
						</QRow>
					</template>
					<template #cell-actions="{ node, data }: { node: IComparisonDetailTreeNode; data: IComparisonDetailsRow }">
						<QRow justify="end">
							<QCol cols="auto">
								<IconSquareButton
									v-if="!isMusic || getMissingMusicTracks([data]).length > 0"
									:cy="`media-comparison-details-download-${node.key}`"
									:disabled="!canDownload"
									icon="mdi-download"
									:tooltip-text="t('components.media-overview.comparison.download-selected')"
									dense
									@click.stop="downloadRows([data])" />
							</QCol>
						</QRow>
					</template>
					<template #empty>
						<div
							v-if="!loading"
							class="full-width text-center q-pa-md">
							{{ t('components.media-overview.comparison.details-no-actionable-rows') }}
						</div>
					</template>
				</QTreeTable>
			</div>
		</template>
		<template
			v-if="$q.screen.lt.md"
			#actions>
			<BaseButton
				class="media-comparison-details__download-button"
				:label="t('components.media-overview.comparison.download-selected')"
				icon="mdi-download"
				block
				:disabled="!canDownload || selectedDownloadRows.length === 0"
				cy="media-comparison-details-dialog-download-button"
				@click="downloadRows(selectedDownloadRows)" />
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import { PlexMediaComparisonState, PlexMediaType, VideoQuality } from '@dto';
import type {
	DownloadMediaDTO,
	PlexMediaComparisonDetailsRowDTO,
	PlexMediaSlimDTO,
} from '@dto';
import { DialogType } from '@enums';
import type { QTreeTableColumn } from '@props';
import { QTreeTableColumnType } from '@props';
import { useDialogStore, useLibraryStore, useMediaStore, useServerStore, useSettingsStore } from '@store';
import { getPlexMediaComparisonState, getPlexMediaComparisonStateFromId } from '@composables';
import type { TreeNode } from 'primevue/treenode';
import type { TreeTableSelectionKeys } from 'primevue/treetable';
import { omit, uniqueId, uniqBy } from 'lodash-es';
import { catchError, defer, EMPTY, finalize, map, Subject, switchMap, take, throwIfEmpty } from 'rxjs';

interface IComparisonDetailsRow extends Omit<PlexMediaComparisonDetailsRowDTO, 'children'> {
	key: string;
	children: IComparisonDetailsRow[];
}

interface IComparisonDetailTreeNode extends TreeNode {
	data: IComparisonDetailsRow;
	children?: IComparisonDetailTreeNode[];
}

const { t } = useI18n();
const $q = useQuasar();
const mediaStore = useMediaStore();
const dialogStore = useDialogStore();
const settingsStore = useSettingsStore();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();

const loading = ref(false);
const comparisonFailed = ref(false);
const comparisonRequests = new Subject<PlexMediaSlimDTO | null>();
const comparisonRows = ref<IComparisonDetailsRow[]>([]);
const selectedMediaItem = ref<PlexMediaSlimDTO | null>(null);
const selectedRows = ref<TreeTableSelectionKeys>({});

const detailTreeRows = computed(() => mapToTreeNodes(get(comparisonRows)));

const selectedDownloadRows = computed(() => getSelectedDownloadRows(get(comparisonRows)));
const isMusic = computed(() => get(selectedMediaItem)?.type === PlexMediaType.MusicArtist);
const canDownload = computed(() => get(selectedMediaItem) !== null && !get(loading) && !get(comparisonFailed));

const comparisonColumns: QTreeTableColumn[] = [
	{
		header: t('components.media-overview.comparison.details-column-title'),
		field: 'title',
	},
	{
		header: t('components.media-overview.comparison.details-column-owned-quality'),
		field: 'ownedQuality',
		type: QTreeTableColumnType.Custom,
		width: 160,
		align: 'left',
	},
	{
		header: t('components.media-overview.comparison.details-column-remote-quality'),
		field: 'remoteQuality',
		type: QTreeTableColumnType.Custom,
		width: 160,
		align: 'left',
	},
	{
		header: t('components.media-overview.comparison.details-column-location'),
		field: 'location',
		type: QTreeTableColumnType.Custom,
		width: 240,
		align: 'left',
	},
	{
		header: t('components.downloads-table.columns.actions'),
		field: 'actions',
		type: QTreeTableColumnType.Actions,
		width: 110,
		align: 'right',
		sortable: false,
	},
];

const responsiveComparisonColumns = computed<QTreeTableColumn[]>(() => {
	const columns = comparisonColumns
		.filter((column) => !get(isMusic) || !['ownedQuality', 'remoteQuality'].includes(column.field))
		.map((column) => column.field === 'title' ? { ...column, selection: !get(isMusic) } : column);
	return $q.screen.lt.md ? columns.filter((column) => column.field === 'title') : columns;
});

function mapToTreeNodes(rows: IComparisonDetailsRow[]): IComparisonDetailTreeNode[] {
	return rows.map((row) => ({
		key: row.key,
		label: row.title,
		data: row,
		children: mapToTreeNodes(row.children),
	}));
}

function toComparisonRow(row: PlexMediaComparisonDetailsRowDTO): IComparisonDetailsRow {
	return {
		...row,
		key: uniqueId(`${row.type}-`),
		children: row.children.map(toComparisonRow),
	};
}

function getComparisonState(row: IComparisonDetailsRow): PlexMediaComparisonState {
	return typeof row.state === 'number' ? getPlexMediaComparisonStateFromId(row.state) : row.state;
}

function onOpen(value: unknown) {
	set(selectedMediaItem, value as PlexMediaSlimDTO);
	requestComparison();
}

function requestComparison() {
	comparisonRequests.next(get(selectedMediaItem));
}

function onClose() {
	comparisonRequests.next(null);
	set(selectedMediaItem, null);
}

useSubscription(
	comparisonRequests.pipe(
		switchMap((mediaItem) => {
			set(comparisonRows, []);
			set(selectedRows, {});
			set(comparisonFailed, false);
			set(loading, mediaItem !== null);
			if (!mediaItem)
				return EMPTY;

			return defer(() => mediaStore.getMediaComparisonDetails(mediaItem.id, mediaItem.type)).pipe(
				take(1),
				throwIfEmpty(),
				map((details) => details.rows.map(toComparisonRow)),
				catchError(() => {
					set(comparisonFailed, true);
					return EMPTY;
				}),
				finalize(() => set(loading, false)),
			);
		}),
	).subscribe((rows) => set(comparisonRows, rows)),
);

function onSelectionChange(keys: TreeTableSelectionKeys) {
	if (!get(canDownload))
		return;
	set(selectedRows, Object.fromEntries(
		Object.entries(keys).filter(([, value]) => value.checked || value.partialChecked),
	));
	if (get(isMusic)) {
		const tracks = getMissingMusicTracks(getSelectedDownloadRows(get(comparisonRows)));
		set(selectedRows, Object.fromEntries(tracks.map((track) => [track.key, { checked: true, partialChecked: false }])));
	}
}

function getMissingMusicTracks(rows: IComparisonDetailsRow[]): IComparisonDetailsRow[] {
	return rows.flatMap((row) => {
		if (getComparisonState(row) === PlexMediaComparisonState.Owned)
			return [];
		if (row.type === PlexMediaType.MusicTrack) {
			return row.children.length === 0 && getComparisonState(row) === PlexMediaComparisonState.Missing ? [row] : [];
		}
		return getMissingMusicTracks(row.children);
	});
}

function getMusicSelectionValue(rows: IComparisonDetailsRow[]): boolean | null {
	const tracks = getMissingMusicTracks(rows);
	const selectedCount = tracks.filter((track) => get(selectedRows)[track.key]?.checked).length;
	return tracks.length > 0 && selectedCount === tracks.length ? true : selectedCount > 0 ? null : false;
}

function toggleMusicSelection(rows: IComparisonDetailsRow[], checked: boolean) {
	if (!get(canDownload))
		return;
	const tracks = getMissingMusicTracks(rows);
	const keys = checked ? { ...get(selectedRows) } : omit(get(selectedRows), tracks.map((track) => track.key));
	if (checked) {
		for (const track of tracks)
			keys[track.key] = { checked: true, partialChecked: false };
	}
	set(selectedRows, keys);
}

function getSelectedDownloadRows(rows: IComparisonDetailsRow[]): IComparisonDetailsRow[] {
	return rows.flatMap((row) => {
		const children = getSelectedDownloadRows(row.children ?? []);
		if (!get(selectedRows)[row.key]?.checked)
			return children;

		return [row, ...children];
	});
}

function downloadRows(rows: IComparisonDetailsRow[]) {
	if (!get(canDownload))
		return;
	const downloadRows = get(isMusic)
		? uniqBy(getMissingMusicTracks(rows), (row) => `${row.type}-${row.plexServerId}-${row.plexLibraryId}-${row.plexMediaId}`)
		: rows;
	const downloadCommands = downloadRows.map(toDownloadMediaCommand);

	if (downloadCommands.length === 0)
		return;

	dialogStore.openMediaConfirmationDownloadDialog(downloadCommands);
}

function toDownloadMediaCommand(row: IComparisonDetailsRow): DownloadMediaDTO {
	return {
		type: row.type,
		mediaIds: [row.plexMediaId],
		plexLibraryId: row.plexLibraryId,
		plexServerId: row.plexServerId,
		qualities: [],
		keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
	};
}
</script>

<style lang="scss">
.media-comparison-details {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.media-comparison-details__header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
  padding-right: 2.5rem;
}

.media-comparison-details__title {
  min-width: 0;
  overflow-wrap: anywhere;
}

.media-comparison-details__row-content {
  min-width: 0;
}

.media-comparison-details__row-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.media-comparison-details__metadata {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  margin-top: 0.75rem;
  padding-left: 2.5rem;
}

.media-comparison-details__metadata-item {
  min-width: 0;
}

.media-comparison-details__metadata-label {
  opacity: 0.7;
}

.media-comparison-details__metadata-location {
  grid-column: 1 / -1;
}

.media-comparison-details__location {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  min-width: 0;
}

.media-comparison-details__download-button {
  min-height: 44px;
}

.media-comparison-details__download-button--desktop {
  flex: 0 0 auto;
  margin-left: auto;
}

.media-comparison-details__table {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-height: 0;

  .p-treetable-table-container {
    flex: 1 1 auto;
    min-height: 0;
    overflow: auto;
  }

  .p-treetable-thead {
    position: sticky;
    top: 0;
    z-index: 2;
  }

  .p-paginator {
    position: sticky;
    bottom: 0;
    z-index: 2;
    flex: 0 0 auto;
  }
}

@media (max-width: 1023px) {
  .media-comparison-details__title .q-text {
    display: -webkit-box;
    overflow: hidden;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
  }

  .media-comparison-details__table {
    .p-treetable-table-container {
      overflow-x: hidden;
    }
    .p-treetable-thead {
      display: none;
    }

    .p-treetable-table {
      width: 100%;
      min-width: 0;
      table-layout: fixed;
    }

    .p-treetable-header-cell,
    .p-treetable-tbody > tr > td {
      white-space: normal;
    }

    .q-tree-table-title-cell > .media-comparison-details__row-content {
      overflow: visible;
      text-overflow: clip;
      white-space: normal;

      .q-text {
        overflow: visible;
        text-overflow: clip;
        overflow-wrap: anywhere;
        white-space: normal;
      }
    }
  }
}

@media (max-width: 599px) {
  .media-comparison-details__header {
    gap: 0.5rem;
  }

  .media-comparison-details__metadata {
    gap: 0.5rem;
    grid-template-columns: minmax(0, 1fr);
    padding-left: 0;
  }
}
</style>
