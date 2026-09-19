<template>
	<q-input
		v-if="$q.screen.gt.sm"
		:model-value="mediaOverviewStore.filterQuery"
		:debounce="100"
		outlined
		aria-label="Search media"
		input-style="font-size: 1.25rem"
		rounded
		@update:model-value="(value) => useSubscription(mediaOverviewStore.setFilterQuery(String(value ?? '')).subscribe())">
		<template #prepend>
			<IconButton
				icon="mdi-magnify"
				aria-label="Filter media"
				cy="media-overview-filter-btn">
				<MediaFilterMenu :library-id="libraryId" />
			</IconButton>
		</template>
		<template #append>
			<QGlowChip
				v-for="chip in mediaOverviewStore.getFilterChips"
				:key="chip.id"
				:value="chip.text"
				:color="chip.color"
				removable
				@remove="useSubscription(chip.unset.subscribe())" />
			<q-btn
				v-if="mediaOverviewStore.filterQuery !== ''"
				flat
				round
				dense
				icon="mdi-close"
				aria-label="Clear media search"
				@click="useSubscription(mediaOverviewStore.clearFilter().subscribe())" />
		</template>
	</q-input>

	<div
		v-else
		class="media-overview-search">
		<q-input
			class="media-overview-search__input"
			:model-value="mediaOverviewStore.filterQuery"
			:debounce="100"
			outlined
			aria-label="Search media"
			input-style="font-size: 1.1rem"
			rounded
			@update:model-value="(value) => useSubscription(mediaOverviewStore.setFilterQuery(String(value ?? '')).subscribe())">
			<template #prepend>
				<IconButton
					class="media-overview-search__filter"
					icon="mdi-magnify"
					aria-label="Filter media"
					tooltip-text="Filter media"
					cy="media-overview-filter-btn">
					<MediaFilterMenu :library-id="libraryId" />
				</IconButton>
			</template>
			<template #append>
				<q-btn
					v-if="mediaOverviewStore.filterQuery !== ''"
					flat
					round
					dense
					icon="mdi-close"
					aria-label="Clear media search"
					@click="useSubscription(mediaOverviewStore.clearFilter().subscribe())" />
			</template>
		</q-input>
		<div
			v-if="mediaOverviewStore.getFilterChips.length > 0"
			class="media-overview-search__chips">
			<QGlowChip
				v-for="chip in mediaOverviewStore.getFilterChips"
				:key="chip.id"
				:value="chip.text"
				:color="chip.color"
				removable
				@remove="useSubscription(chip.unset.subscribe())" />
		</div>
	</div>
</template>

<script setup lang="ts">
import { useMediaOverviewStore } from '@store';

const $q = useQuasar();

const mediaOverviewStore = useMediaOverviewStore();

withDefaults(defineProps<{
	libraryId?: number;
}>(), {
	libraryId: 0,
});
</script>

<style lang="scss">
.media-overview-search {
  display: flex;
  align-items: center;
  min-width: 0;
  width: 100%;
}

.media-overview-search__input {
  min-width: 0;
  flex: 1 1 auto;
}

.media-overview-search__filter {
  min-width: 44px;
  min-height: 44px;
}

.media-overview-search__chips {
  display: flex;
  flex: 0 1 45%;
  flex-wrap: nowrap;
  gap: 0.25rem;
  min-width: 0;
  max-width: 45%;
  overflow-x: auto;
  overscroll-behavior-inline: contain;

  .q-chip {
    flex: 0 0 auto;
    margin: 0;
  }
}
</style>
