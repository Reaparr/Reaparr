<template>
	<div
		:class="{ 'media-table-header-column': true, 'sorted': sorted }"
		@click="onClick">
		<span> {{ column.label }} <q-icon
			v-if="column.sortable"
			:name="icon"
			class="header-sort-icon" /></span>
	</div>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { SortDirection } from '@dto';
import type { QTreeViewTableHeader } from '@props';
import { useMediaOverviewStore } from '@store';

const mediaOverviewStore = useMediaOverviewStore();

const props = defineProps<{
	column: QTreeViewTableHeader;
}>();

const sortField = computed(() => props.column.sortField);

const sorted = computed(() => {
	if (!props.column.sortable) {
		return false;
	}

	return get(mediaOverviewStore.getActiveSort).field === get(sortField);
});

const icon = computed(() => {
	if (!get(sorted)) {
		return '';
	}

	switch (get(mediaOverviewStore.getActiveSort).sort) {
		case SortDirection.Asc:
			return 'mdi-arrow-up';
		case SortDirection.Desc:
			return 'mdi-arrow-down';
		default:
			return '';
	}
});

function onClick() {
	if (!props.column.sortable) {
		return;
	}

	const field = get(sortField);
	if (!field) {
		return;
	}

	mediaOverviewStore.toggleSortMedia(field);
}
</script>

<style lang="scss">
.media-table-header-column {
  white-space: nowrap;
  font-weight: bold;

  .header-sort-icon {
    opacity: 0;
    transition: transform 0.3s cubic-bezier(0.25, 0.8, 0.5, 1);
  }

  &:hover,
  &.sorted {
    cursor: pointer;

    .header-sort-icon {
      opacity: 1;
    }
  }
}
</style>
