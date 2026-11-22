<template>
	<div class="media-overview-search">
		<q-input
			class="media-overview-search__input"
			:model-value="searchQuery"
			:debounce="100"
			outlined
			:aria-label="searchLabel"
			:data-cy="photoMode ? 'photo-search-input' : undefined"
			:input-style="$q.screen.gt.sm ? 'font-size: 1.25rem' : 'font-size: 1.1rem'"
			rounded
			@update:model-value="updateSearch">
			<template #prepend>
				<q-icon
					v-if="photoMode"
					name="mdi-magnify"
					:aria-label="searchLabel" />
				<IconButton
					v-else
					:class="{ 'media-overview-search__filter': $q.screen.lt.md }"
					icon="mdi-magnify"
					:aria-label="$t('components.media-overview.filter-label')"
					:tooltip-text="$q.screen.lt.md ? $t('components.media-overview.filter-label') : undefined"
					cy="media-overview-filter-btn">
					<MediaFilterMenu :library-id="libraryId" />
				</IconButton>
			</template>
			<template #append>
				<template v-if="!photoMode && $q.screen.gt.sm">
					<QGlowChip
						v-for="chip in filterChips"
						:key="chip.id"
						:value="chip.text"
						:color="chip.color"
						removable
						@remove="useSubscription(chip.unset.subscribe())" />
				</template>
				<q-btn
					v-if="searchQuery !== ''"
					flat
					round
					dense
					icon="mdi-close"
					:aria-label="$t('components.media-overview.clear-search-label')"
					@click="clearSearch" />
			</template>
		</q-input>
		<div
			v-if="!photoMode && $q.screen.lt.md && filterChips.length > 0"
			class="media-overview-search__chips">
			<QGlowChip
				v-for="chip in filterChips"
				:key="chip.id"
				:value="chip.text"
				:color="chip.color"
				removable
				@remove="useSubscription(chip.unset.subscribe())" />
		</div>
	</div>
</template>

<script setup lang="ts">
import { useMediaOverviewStore, usePhotoBrowsingStore } from '@store';
import { useSubscription } from '@vueuse/rxjs';

const props = withDefaults(defineProps<{
	libraryId?: number;
	photoMode?: boolean;
}>(), {
	libraryId: 0,
	photoMode: false,
});

const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const photoStore = usePhotoBrowsingStore();

const searchQuery = computed(() => props.photoMode ? photoStore.searchQuery : mediaOverviewStore.filterQuery);
const filterChips = computed(() => props.photoMode ? [] : mediaOverviewStore.getFilterChips);
const searchLabel = computed(() => props.photoMode
	? t('components.photo-browser.search-label')
	: t('components.media-overview.search-label'));

function updateSearch(value: unknown): void {
	const query = String(value ?? '');
	if (props.photoMode) {
		emit('photo-search', query);
		return;
	}
	useSubscription(mediaOverviewStore.setFilterQuery(query).subscribe());
}
function clearSearch(): void {
	if (props.photoMode) {
		emit('photo-search', '');
		return;
	}
	useSubscription(mediaOverviewStore.clearFilter().subscribe());
}

const emit = defineEmits<{
	(e: 'photo-search', value: string): void;
}>();
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
