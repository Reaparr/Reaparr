<template>
	<MediaOverviewTable
		:rows="rows"
		:columns="qTableProps.columns ?? []"
		:selected="getSelected"
		@selection="updateSelected($event as readonly PlexMediaSlimDTO[])"
		@row-click="emit('row-click', $event.row as PlexMediaSlimDTO)">
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
					<MediaQuality
						v-if="supportsFileQuality(row.type)"
						:qualities="row.qualities" />
					<MediaComparisonStateButton
						v-if="supportsComparison(row.type)"
						:comparison-state="getPlexMediaComparisonState(row)"
						:media-type="row.type"
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
				<MediaQuality
					v-if="supportsFileQuality(row.type)"
					:qualities="row.qualities" />
			</q-td>
		</template>
		<!-- Comparison State -->
		<template #body-cell-comparisonState="{ row }: { row: PlexMediaSlimDTO }">
			<q-td class="text-center">
				<MediaComparisonStateButton
					v-if="supportsComparison(row.type)"
					:comparison-state="getPlexMediaComparisonState(row)"
					:media-type="row.type"
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
	</MediaOverviewTable>
</template>

<script setup lang="ts">
import Log from 'consola';
import type { QTableProps } from 'quasar';
import { get } from '@vueuse/core';
import Convert from '@class/Convert';
import { ButtonType } from '@enums';
import type { DownloadMediaDTO, PlexMediaSlimDTO } from '@dto';
import { PlexMediaType } from '@dto';
import type { ISelection } from '@interfaces';
import { getMediaTableColumns } from '@composables/mediaTableColumns';
import {
	type IMediaOverviewCommands,
	sendMediaOverviewDownloadCommand,
} from '@composables/event-bus';
import { getPlexMediaComparisonState, toDownloadMedia } from '@composables/conversion';
import QDateTime from '@components/Common/QDateTime.vue';

const props = defineProps<{
	rows: PlexMediaSlimDTO[];
	selection: ISelection | null;
	downloadMediaFactory?: (row: PlexMediaSlimDTO) => DownloadMediaDTO[];
}>();

const mediaTableColumns = getMediaTableColumns();

const router = useRouter();

/**
 * The selected rows cannot be returned as just keys, they need to be the same object as the rows.
 */
const getSelected = computed((): PlexMediaSlimDTO[] => {
	return props.rows.filter((row) => (props.selection?.keys ?? []).includes(row.id));
});
const qTableProps = computed((): QTableProps => {
	return {
		rows: [],
		columns: mediaTableColumns.filter((column) => {
			if (column.field === 'quality')
				return props.rows.some((row) => supportsFileQuality(row.type));
			if (column.field === 'comparisonState')
				return props.rows.some((row) => supportsComparison(row.type));
			return true;
		}).map((x) => {
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

function supportsFileQuality(type: PlexMediaType): boolean {
	return [PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode, PlexMediaType.OtherVideos, PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack].includes(type);
}
function supportsComparison(type: PlexMediaType): boolean {
	return [PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode, PlexMediaType.MusicArtist, PlexMediaType.MusicTrack].includes(type);
}
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
			}).catch((error) => Log.error('Failed to navigate to TV show details', error));
			break;
		default:
			throw new Error(`Unknown action: ${action.command} in MediaQTable.vue`);
	}
}
function updateSelected(selected: readonly PlexMediaSlimDTO[]) {
	emit('selection', {
		keys: selected.map((x) => x.id),
		allSelected: selected.length === props.rows.length ? true : selected.length === 0 ? false : null,
		indexKey: 0,
	});
}

const emit = defineEmits<{
	(e: 'selection', payload: ISelection): void;
	(e: 'row-click', payload: PlexMediaSlimDTO): void;
}>();
</script>
