<template>
	<div
		class="media-table"
		data-cy="media-table">
		<MediaTableHeader
			:columns="mediaTableColumns"
			selectable
			:selected="mediaOverviewStore.isRootSelected"
			class="media-table--header"
			@selected="mediaOverviewStore.setRootSelected($event)" />
		<QScroll
			ref="scrollAreaRef"
			scroll-id="media-table-scroll"
			:fit="false"
			height="calc(100vh - 137px)"
			width="100%"
			:class="['media-table--content', isScrollable ? 'scroll' : '']"
			data-cy="media-table-scroll">
			<!-- Total height spacer — required by TanStack Virtual to define the scrollable area -->
			<div :style="{ height: `${safeTotalSize}px`, position: 'relative' }">
				<!-- Only virtual rows are rendered, positioned absolutely via translateY -->
				<div
					v-for="virtualRow in rowVirtualizer.getVirtualItems()"
					:key="String(virtualRow.key)"
					:style="{
						position: 'absolute',
						top: 0,
						left: 0,
						width: '100%',
						transform: `translateY(${virtualRow.start}px)`,
					}"
					class="media-table--intersection highlight-border-box"
					:data-media-id="getRowItem(virtualRow.index)?.id"
					:data-scroll-index="virtualRow.index">
					<MediaTableRow
						v-if="getRowItem(virtualRow.index)"
						:index="virtualRow.index"
						:data-cy="`media-table-row-${virtualRow.index}`"
						:columns="mediaTableColumns"
						:row="getRowItem(virtualRow.index)!"
						selectable
						:selected="isSelected(getRowItem(virtualRow.index)!.id)"
						:disable-highlight="disableHighlight"
						:disable-hover-click="disableHoverClick"
						@selected="updateSelectedRow(getRowItem(virtualRow.index)!.id, $event)" />
					<div
						v-else
						class="media-table--placeholder"
						data-cy="media-table-row-placeholder">
						<q-skeleton type="text" />
					</div>
				</div>
			</div>
		</QScroll>
	</div>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useVirtualizer } from '@tanstack/vue-virtual';
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import type { PlexMediaSlimDTO } from '@dto';
import type { ISelection } from '@interfaces';
import {
	triggerBoxHighlight,
	waitForElement,
	useMediaOverviewStore,
} from '#imports';
import { getMediaTableColumns } from '~/composables/mediaTableColumns';

const mediaOverviewStore = useMediaOverviewStore();
const mediaTableColumns = getMediaTableColumns();
type QScrollInstance = {
	getScrollTarget: () => HTMLElement | null;
};

const scrollAreaRef = ref<QScrollInstance | null>(null);
const qTableRef = ref<HTMLElement | null>(null);
const scrollTargetElement = ref<HTMLElement | null>(null);
const autoScrollEnabled = ref(false);
const pendingHighlightIndex = ref<number | null>(null);

const props = withDefaults(
	defineProps<{
		rows?: Readonly<PlexMediaSlimDTO[]>;
		selection?: ISelection | null;
		rowKey?: string;
		disableHoverClick?: boolean;
		disableHighlight?: boolean;
		disableIntersection?: boolean;
		isScrollable?: boolean;
	}>(),
	{
		selection: null,
		rowKey: 'id',
		disableHoverClick: false,
		disableHighlight: false,
		disableIntersection: false,
		isScrollable: true,
	},
);

defineEmits<{
	(e: 'row-click', payload: PlexMediaSlimDTO): void;
}>();

// Row height matches $media-table-row-height in _variables.scss
const ROW_HEIGHT = 42;

// Firefox and Chrome silently clamp CSS element heights at ~33.5M px, causing a blank render.
// This guard ensures the spacer div never exceeds that limit regardless of item count.
const BROWSER_MAX_CSS_HEIGHT = 33_000_000;

const rowVirtualizer = useVirtualizer(
	computed(() => ({
		count: props.rows?.length ?? mediaOverviewStore.totalCount,
		getScrollElement,
		estimateSize: () => ROW_HEIGHT,
		overscan: 10,
		getItemKey: (index: number) => getRowItem(index)?.id ?? index,
		onChange: (_instance: unknown, sync: boolean) => {
			// sync=false means TanStack has finished its scroll-triggered re-render
			if (sync)
				return;

			requestVisibleRange();

			const index = get(pendingHighlightIndex);
			if (index === null)
				return;
			const container = get(qTableRef);
			if (!container)
				return;
			const element: HTMLElement | null = container.querySelector(`[data-scroll-index="${index}"]`) ?? null;
			if (!element)
				return;
			set(pendingHighlightIndex, null);
			set(scrollTargetElement, element);
			triggerBoxHighlight(element);
		},
	})),
);

const safeTotalSize = computed(() => Math.min(rowVirtualizer.value.getTotalSize(), BROWSER_MAX_CSS_HEIGHT));

function getScrollElement(): HTMLElement | null {
	const target = get(scrollAreaRef)?.getScrollTarget() ?? null;
	if (target !== get(qTableRef)) {
		set(qTableRef, target);
	}
	return target;
}

function getRowItem(index: number): PlexMediaSlimDTO | undefined {
	return props.rows?.[index] ?? mediaOverviewStore.getMediaItemsForRange(index, index + 1).at(0);
}

function requestVisibleRange() {
	if (props.rows) {
		return;
	}

	const virtualItems = get(rowVirtualizer).getVirtualItems();
	const first = virtualItems.at(0)?.index;
	const last = virtualItems.at(-1)?.index;
	if (first !== undefined && last !== undefined) {
		useSubscription(mediaOverviewStore.requestRange(first, last).subscribe());
	}
}

function isSelected(mediaId: number) {
	return (mediaOverviewStore.selection?.keys ?? []).includes(mediaId);
}

function updateSelectedRow(mediaId: number, state: boolean) {
	mediaOverviewStore.setSelection({
		...mediaOverviewStore.selection,
		keys: state
			? [...(mediaOverviewStore.selection?.keys ?? []), mediaId]
			: (mediaOverviewStore.selection?.keys ?? []).filter((x) => x !== mediaId),
		allSelected: false,
	} as ISelection);
}

function scrollToIndex(index: number, highlight = true) {
	const container = getScrollElement();
	if (!container) {
		Log.error(`Could not find scroll container reference`);
		return;
	}

	set(autoScrollEnabled, true);
	if (highlight)
		set(pendingHighlightIndex, index);
	get(rowVirtualizer).scrollToIndex(index, { align: 'start' });
}

function highlightPendingMedia() {
	const pendingMediaHighlightId = get(mediaOverviewStore.pendingMediaHighlightId);
	if (pendingMediaHighlightId === null)
		return;

	const container = getScrollElement();
	waitForElement(container, `[data-media-id="${pendingMediaHighlightId}"]`).then((element) => {
		if (get(mediaOverviewStore.pendingMediaHighlightId) !== pendingMediaHighlightId)
			return;

		if (element)
			triggerBoxHighlight(element);
		mediaOverviewStore.consumePendingMediaHighlight(pendingMediaHighlightId);
	});
}

onMounted(() => {
	// Listen for scroll to navigation index command
	useSubscription(mediaOverviewStore.getScrollCommand().subscribe(({ index, highlight }) => {
		scrollToIndex(index, highlight);
	}));

	const requestedScrollIndex = get(mediaOverviewStore.currentScrollIndex);
	if (requestedScrollIndex > 0) {
		scrollToIndex(requestedScrollIndex - 1, get(mediaOverviewStore.pendingMediaHighlightId) === null);
	}

	highlightPendingMedia();
	if (get(mediaOverviewStore.pendingMediaHighlightId) !== null)
		return;

	const lastMediaItemViewed = get(mediaOverviewStore.lastMediaItemViewed);
	if (requestedScrollIndex <= 0 && lastMediaItemViewed && lastMediaItemViewed.sortIndex > 0) {
		// If we have a last viewed media item, scroll to it
		scrollToIndex(lastMediaItemViewed.sortIndex - 1);
	}
});
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.media-table {
  flex: 1 1 auto;
  width: 100%;
  height: 100%;

  &--header,
  &--intersection,
  &--intersection > div {
    height: $media-table-row-height;
  }

  &--content {
    max-height: calc($page-height-minus-app-bar - $media-overview-bar-height - $media-table-row-height);
  }
}

.row-title {
  font-weight: bold;
  min-width: 300px;
  max-width: 300px;

  &--hover {
    cursor: pointer;

    :hover {
      color: $primary;
    }
  }
}
</style>
