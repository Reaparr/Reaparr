<template>
	<QRow
		align="center"
		class="media-table-row"
		full-height
		justify="between">
		<template v-if="row">
			<!-- Checkbox -->
			<QCol
				v-if="selectable"
				class="q-ml-md q-pl-sm"
				cols="auto">
				<q-checkbox
					:model-value="selected"
					dense
					@update:model-value="$emit('selected', $event)" />
			</QCol>
			<template
				v-for="(column, i) in columns"
				:key="i">
				<!-- Index -->
				<template v-if="column['type'] === 'index'">
					<QCol
						class="media-table-row--column"
						cols="auto"
						style="min-width: 50px">
						<QText>{{ `#${n(row[column.field])}` }}</QText>
					</QCol>
				</template>
				<!-- Title -->
				<template v-else-if="column['type'] === 'title'">
					<QCol
						:class="[
							'media-table-row--column',
							'media-table-row--title',
							!disableHoverClick ? 'media-table-row--title--hover' : '',
						]"
						:data-label="column.label"
						@click.stop="!disableHoverClick ? onRowAction({ command: 'open-details' }) : () => {}">
						<QText :value="row[column.field]" />
					</QCol>
				</template>
				<!-- Duration format -->
				<template v-else-if="column['type'] === 'duration'">
					<QCol
						class="media-table-row--column media-table-row--secondary"
						:data-label="column.label"
						cols="1">
						<QDuration
							align="center"
							:value="row[column.field]"
							short />
					</QCol>
				</template>
				<!-- Date format -->
				<template v-else-if="column['type'] === 'date'">
					<QCol
						class="media-table-row--column media-table-row--secondary"
						:data-label="column.label"
						cols="1">
						<QDateTime
							align="center"
							:text="row[column.field]"
							short-date />
					</QCol>
				</template>
				<!-- Media size -->
				<template v-else-if="column['type'] === 'file-size'">
					<QCol
						class="media-table-row--column media-table-row--secondary"
						:data-label="column.label"
						cols="1">
						<QFileSize
							align="center"
							:size="row[column.field]" />
					</QCol>
				</template>
				<!-- Actions -->
				<template v-else-if="column['type'] === 'actions'">
					<QCol
						class="media-table-row--column"
						cols="auto">
						<q-btn
							:icon="Convert.buttonTypeToIcon(ButtonType.Download)"
							flat
							@click.stop="onRowAction({ command: 'download' })" />
					</QCol>
				</template>
			</template>
		</template>
		<!-- No row -->
		<QCol v-else>
			<QText :value="t('components.q-tree-view-table-row.invalid-node')" />
		</QCol>
		<!--	Highlight animation effect	-->
		<svg
			v-if="!disableHighlight"
			class="glow-container">
			<!-- suppress HtmlUnknownAttribute -->
			<rect
				class="glow-blur"
				height="5"
				pathLength="100"
				stroke-linecap="round"
				width="5" />
			<!-- suppress HtmlUnknownAttribute -->
			<rect
				class="glow-line"
				height="5"
				pathLength="100"
				stroke-linecap="round"
				width="5" />
		</svg>
	</QRow>
</template>

<script lang="ts" setup>
import type { QTreeViewTableHeader } from '@props';
import type { PlexMediaSlimDTO } from '@dto';
import Convert from '@class/Convert';
import { ButtonType } from '@enums';
import {
	type IMediaOverviewCommands,
	sendMediaOverviewDownloadCommand,
} from '@composables/event-bus';
import { toDownloadMedia } from '@composables/conversion';

const props = withDefaults(
	defineProps<{
		selected?: boolean | null;
		isHeader?: boolean;
		selectable?: boolean;
		row: PlexMediaSlimDTO;
		columns: QTreeViewTableHeader[];
		index: number;
		disableHoverClick?: boolean;
		disableHighlight?: boolean;
	}>(),
	{
		selected: false,
		disableHoverClick: false,
		disableHighlight: false,
	},
);

const router = useRouter();
const mediaOverviewStore = useMediaOverviewStore();
const { t, n } = useI18n();

defineEmits<{
	(e: 'selected', state: boolean): void;
}>();

function onRowAction(action: IMediaOverviewCommands) {
	switch (action.command) {
		case 'download':
			sendMediaOverviewDownloadCommand(toDownloadMedia(props.row));
			break;
		case 'open-details':
			mediaOverviewStore.setPendingMediaHighlight(props.row.id, mediaOverviewStore.libraryId);
			router.push({
				name: 'tvshows-libraryId-details-tvShowId',
				params: {
					libraryId: props.row.plexLibraryId.toString(),
					tvShowId: props.row.id.toString(),
				},
			});
			break;
		default:
			throw new Error(`Unknown action: ${action.command} in MediaTableRow.vue`);
	}
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.media-table-row-container,
.media-table-row-container > div {
  height: 42px;
}

.media-table-row {
  border-bottom: 1px solid;

  &--column {
    text-align: center;
    white-space: nowrap;
    margin: auto 8px;
  }

  &--title {
    font-weight: bold;
    text-align: left;
    display: inline-block;
    text-overflow: ellipsis;
    white-space: nowrap;
    overflow: hidden;

    &--hover {
      cursor: pointer;

      &:hover {
        color: $primary;
      }
    }
  }
}

@media (max-width: $breakpoint-xs-max) {
  .media-table-row {
    min-height: 96px;
    padding: 0.5rem;
    align-content: center;
    flex-wrap: wrap;

    > .col-auto:first-child {
      margin-left: 0;
      padding-left: 0;
    }

    &--title {
      min-width: 0;
      flex: 1 1 calc(100% - 96px);
      margin-inline: 0.25rem;
    }

    &--secondary {
      display: none;
    }

    &--column:last-child {
      margin-inline: 0;

      .q-btn {
        min-width: 44px;
        min-height: 44px;
      }
    }
  }
}

.body--dark {
  .media-table-row {
    border-bottom-color: $separator-dark-color;
  }
}

.body--light {
  .media-table-row {
    border-bottom-color: $separator-color;
  }
}
</style>
