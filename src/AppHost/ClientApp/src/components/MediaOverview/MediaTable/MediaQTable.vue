<template>
	<div class="media-q-table-scroll">
		<QTable
			:selected="getSelected"
			selection="multiple"
			row-key="id"
			:columns="qTableProps.columns"
			:rows="rows"
			:rows-per-page-options="[0]"
			:grid="$q.screen.lt.md"
			:hide-header="$q.screen.lt.md"
			hide-pagination
			flat
			@update:selected="updateSelected($event as PlexMediaSlimDTO[])">
			<template #item="{ row }: { row: PlexMediaSlimDTO }">
				<div
					class="media-q-table-card"
					data-cy="media-q-table-mobile-card">
					<div class="media-q-table-card__header">
						<q-checkbox
							:aria-label="`${$t('general.commands.selection')} ${row.title}`"
							:model-value="isRowSelected(row.id)"
							@update:model-value="updateRowSelected(row, Boolean($event))" />
						<QText
							class="media-q-table-card__title"
							:value="row.title" />
						<q-btn
							:aria-label="$t('general.commands.download')"
							data-cy="media-q-table-mobile-download"
							flat
							:icon="Convert.buttonTypeToIcon(ButtonType.Download)"
							@click.stop="onRowAction(row, { command: 'download' })" />
					</div>
					<div class="media-q-table-card__metadata">
						<MediaQuality :qualities="row.qualities" />
						<MediaComparisonStateButton
							:comparison-state="getPlexMediaComparisonState(row)"
							show-tooltip
							dense
							rounded
							outline
							flat
							:cy="`episode-comparison-chip-${getPlexMediaComparisonState(row)}`" />
						<QText :value="row.year" />
						<QDuration
							short
							:value="row.duration" />
						<QFileSize :size="row.mediaSize" />
					</div>
				</div>
			</template>
			<!-- Title -->
			<template #body-cell-title="{ row }">
				<q-td class="row-title text-eclipse">
					<QText :value="row.title" />
				</q-td>
			</template>
			<!-- Media Quality	-->
			<template #body-cell-quality="{ row }: { row: PlexMediaSlimDTO }">
				<q-td class="text-eclipse">
					<MediaQuality :qualities="row.qualities" />
				</q-td>
			</template>
			<!-- Comparison State -->
			<template #body-cell-comparisonState="{ row }: { row: PlexMediaSlimDTO }">
				<q-td class="text-center">
					<MediaComparisonStateButton
						:comparison-state="getPlexMediaComparisonState(row)"
						show-tooltip
						dense
						rounded
						outline
						flat
						:cy="`episode-comparison-chip-${getPlexMediaComparisonState(row)}`" />
				</q-td>
			</template>
			<!-- Media Year -->
			<template #body-cell-year="{ row }">
				<q-td class="text-center">
					<QText
						:value="row.year"
						align="center" />
				</q-td>
			</template>
			<!-- Duration -->
			<template #body-cell-duration="{ row }">
				<q-td class="text-center">
					<QDuration
						short
						align="center"
						:value="row.duration" />
				</q-td>
			</template>
			<!-- Media size -->
			<template #body-cell-mediaSize="{ row }">
				<q-td class="text-center">
					<QFileSize
						align="center"
						:size="row.mediaSize" />
				</q-td>
			</template>
			<!-- Added At Date format -->
			<template #body-cell-addedAt="{ row }">
				<q-td class="text-center">
					<QDateTime
						align="center"
						:text="row.addedAt"
						short-date />
				</q-td>
			</template>
			<!-- Updated At Date format -->
			<template #body-cell-updatedAt="{ row }">
				<q-td class="text-center">
					<QDateTime
						align="center"
						:text="row.updatedAt"
						short-date />
				</q-td>
			</template>
			<!-- Actions -->
			<template #body-cell-actions="{ row }">
				<q-td class="text-center">
					<q-btn
						flat
						:icon="Convert.buttonTypeToIcon(ButtonType.Download)"
						@click.stop="onRowAction(row, { command: 'download' })" />
				</q-td>
			</template>
		</QTable>
	</div>
</template>

<script setup lang="ts">
import type { QTableProps } from 'quasar';
import { get } from '@vueuse/core';
import Convert from '@class/Convert';
import { ButtonType } from '@enums';
import type { DownloadMediaDTO, PlexMediaSlimDTO } from '@dto';
import type { ISelection } from '@interfaces';
import { getMediaTableColumns } from '@composables/mediaTableColumns';
import {
	type IMediaOverviewCommands,
	sendMediaOverviewDownloadCommand,
} from '@composables/event-bus';
import { getPlexMediaComparisonState, toDownloadMedia } from '@composables/conversion';
import QDateTime from '@components/Common/QDateTime.vue';

const mediaTableColumns = getMediaTableColumns();
const router = useRouter();
const $q = useQuasar();

const props = defineProps<{
	rows: PlexMediaSlimDTO[];
	selection: ISelection | null;
	downloadMediaFactory?: (row: PlexMediaSlimDTO) => DownloadMediaDTO[];
}>();

const emit = defineEmits<{
	(e: 'selection', payload: ISelection): void;
	(e: 'row-click', payload: PlexMediaSlimDTO): void;
}>();

/**
 * The selected rows cannot be returned as just keys, they need to be the same object as the rows.
 */
const getSelected = computed((): PlexMediaSlimDTO[] => {
	return props.rows.filter((row) => (props.selection?.keys ?? []).includes(row.id));
});

const qTableProps = computed((): QTableProps => {
	return {
		rows: [],
		columns: mediaTableColumns.map((x) => {
			return {
				label: x.label,
				field: x.field,
				name: x.field,
				align: x.align,
				type: x.type,
				sortable: x.sortable,
			};
		}),
	};
});

function isRowSelected(rowId: number): boolean {
	return (props.selection?.keys ?? []).includes(rowId);
}

function updateRowSelected(row: PlexMediaSlimDTO, selected: boolean): void {
	const nextSelected = selected
		? [...get(getSelected), row]
		: get(getSelected).filter((selectedRow) => selectedRow.id !== row.id);
	updateSelected(nextSelected);
}

function onRowAction(row: PlexMediaSlimDTO, action: IMediaOverviewCommands) {
	switch (action.command) {
		case 'download':
			sendMediaOverviewDownloadCommand(props.downloadMediaFactory?.(row) ?? toDownloadMedia(row));
			break;
		case 'open-details':
			router.push({
				name: 'tvshows-libraryId-details-tvShowId',
				params: {
					libraryId: row.plexLibraryId.toString(),
					tvShowId: row.id.toString(),
				},
			});
			break;
		default:
			throw new Error(`Unknown action: ${action.command} in MediaQTable.vue`);
	}
}

function updateSelected(selected: PlexMediaSlimDTO[]) {
	emit('selection', {
		keys: selected.map((x) => x.id) as number[],
		allSelected: selected.length === props.rows.length ? true : selected.length === 0 ? false : null,
		indexKey: 0,
	});
}
</script>

<style lang="scss">
@media (max-width: $breakpoint-sm-max) {
  .media-q-table-scroll {
    min-width: 0;
    max-width: 100%;
    overflow-x: hidden;
  }

  .media-q-table-card {
    width: 100%;
    min-width: 0;
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid currentcolor;

    &__header {
      display: grid;
      grid-template-columns: 44px minmax(0, 1fr) 44px;
      align-items: center;
      gap: 0.5rem;
    }

    &__title {
      min-width: 0;
      font-weight: 600;
      overflow-wrap: anywhere;
    }

    &__metadata {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      min-width: 0;
      padding: 0.25rem 0 0.25rem 3.25rem;
      flex-wrap: wrap;
    }

    .q-btn,
    .q-checkbox {
      min-width: 44px;
      min-height: 44px;
    }
  }
}
</style>
