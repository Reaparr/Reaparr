<template>
	<QRow
		align="center"
		justify="between"
		class="media-table-header">
		<QCol
			v-if="selectable"
			cols="auto"
			class="media-table-header--column">
			<q-checkbox
				dense
				:model-value="selected"
				class="q-ml-md"
				@update:model-value="$emit('selected', $event)" />
		</QCol>
		<template
			v-for="(column, i) in columns"
			:key="i">
			<!-- Index -->
			<template v-if="column['type'] === 'index'">
				<QCol
					cols="auto"
					style="min-width: 45px"
					class="media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
			<!-- Title -->
			<template v-else-if="column['type'] === 'title'">
				<QCol class="media-table-header--title media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
			<!-- Duration format -->
			<template v-else-if="column['type'] === 'duration'">
				<QCol
					cols="1"
					class="media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
			<!-- Date format -->
			<template v-else-if="column['type'] === 'date'">
				<QCol
					cols="1"
					class="media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
			<!-- Filesize format -->
			<template v-else-if="column['type'] === 'file-size'">
				<QCol
					cols="1"
					class="media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
			<!-- Actions -->
			<template v-else-if="column['type'] === 'actions'">
				<QCol
					cols="auto"
					class="media-table-header--column">
					<MediaTableHeaderColumn :column="column" />
				</QCol>
			</template>
		</template>
	</QRow>
</template>

<script setup lang="ts">
import type { QTreeViewTableHeader, QTreeViewTableItem } from '@props';

defineProps<{
	selected?: boolean | null;
	selectable?: boolean;
	columns: QTreeViewTableHeader[];
}>();

defineEmits<{
	(e: 'selected', payload: boolean): void;
	(e: 'action', payload: { action: string; data: QTreeViewTableItem }): void;
}>();
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.media-table-header {
  border-bottom: 1px solid;

  &--column {
    text-align: center;
    margin: auto 8px;
  }

  &--title {
    font-weight: bold;
    text-align: left;
    display: inline-block;
    text-overflow: ellipsis;
    white-space: nowrap;
    overflow: hidden;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .media-table-header {
    min-height: 48px;

    &--column:not(:first-child):not(:last-child):not(.media-table-header--title) {
      display: none;
    }

    &--title {
      min-width: 0;
      flex: 1 1 auto;
    }
  }
}

.body--dark {
  .media-table-header {
    border-bottom-color: $separator-dark-color;
  }
}

.body--light {
  .media-table-header {
    border-bottom-color: $separator-color;
  }
}
</style>
