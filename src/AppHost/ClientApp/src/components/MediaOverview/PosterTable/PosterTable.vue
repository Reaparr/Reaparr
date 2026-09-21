<template>
	<!-- Poster display -->
	<QScroll
		ref="scrollAreaRef"
		scroll-id="poster-table"
		data-cy="poster-table">
		<div
			class="poster-table-content"
			:style="{ paddingLeft: `${gridPaddingLeft}px` }">
			<!-- Total height spacer — required by TanStack Virtual to define the scrollable area -->
			<div
				:data-pages-version="mediaOverviewStore.mediaPagesVersion"
				:style="{ height: `${safeTotalSize}px`, position: 'relative' }">
				<!-- Only virtual rows are rendered, positioned absolutely via translateY -->
				<div
					v-for="virtualRow in rowVirtualizer.getVirtualItems()"
					:key="virtualRow.index"
					:ref="measureVirtualRow"
					:data-index="virtualRow.index"
					class="poster-table-item"
					:style="{
						position: 'absolute',
						top: 0,
						left: 0,
						width: '100%',
						transform: `translateY(${virtualRow.start}px)`,
						display: 'flex',
					}">
					<template
						v-for="rowItem in getRowItems(virtualRow.index)"
						:key="rowItem.item?.id ?? `skeleton-${rowItem.index}`">
						<MediaPoster
							v-if="rowItem.item"
							:media-item="rowItem.item"
							:active="true"
							:data-media-id="rowItem.item.id"
							:data-scroll-index="rowItem.index"
							@download="sendMediaOverviewDownloadCommand($event)"
							@open-media-details="onOpenMediaDetails" />
						<div
							v-else
							class="media-poster-placeholder">
							<div class="media-poster-placeholder__image" />
							<div class="media-poster-placeholder__quality" />
						</div>
					</template>
				</div>
			</div>
		</div>
	</QScroll>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useVirtualizer } from '@tanstack/vue-virtual';

import { get, set, useCssVar, useElementBounding } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { PlexMediaType } from '@dto';
import type { PlexMediaSlimDTO } from '@dto';
import { sendMediaOverviewDownloadCommand } from '@composables/event-bus';
import { triggerBoxHighlight } from '@composables/animations';
import { waitForElement } from '@composables';
import { useRouter, useMediaOverviewStore } from '#imports';

const mediaOverviewStore = useMediaOverviewStore();

type QScrollInstance = {
	getScrollTarget: () => HTMLElement | null;
};

const scrollAreaRef = ref<QScrollInstance | null>(null);
const scrollContainerRef = ref<HTMLElement | null>(null);
const posterCardWidthCss = useCssVar('--poster-card-width', scrollContainerRef);
const posterImageWidthCss = useCssVar('--poster-image-width', scrollContainerRef);
const posterCardPaddingCss = useCssVar('--poster-card-padding', scrollContainerRef);
const posterCardWidth = ref(232);
const posterCardHeight = ref(372);
const gridItems = ref(10);
const gridRowGap = ref(20);
const gridPaddingLeft = ref(0);
const hasRunInitialPageReady = ref(false);
const router = useRouter();

defineProps<{
	mediaType: PlexMediaType;
	libraryId: number;
}>();

// Number of rows = ceil(total items / columns)
const rowCount = computed(() => Math.ceil(mediaOverviewStore.totalCount / get(gridItems)));

// Row virtualizer — re-configures reactively when rowCount or posterCardHeight changes
const rowVirtualizer = useVirtualizer(
	computed(() => ({
		count: get(rowCount),
		getScrollElement,
		estimateSize: () => get(posterCardHeight) - get(gridRowGap),
		gap: get(gridRowGap),
		// Render extra rows above and below viewport for smoother jumps
		overscan: 10,
		// Stable row keys: use the first item id in each row
		getItemKey: (rowIndex: number): number => rowIndex,
		onChange: (_instance: unknown, sync: boolean) => {
			if (sync)
				return;

			requestPagesAroundViewport();

			// Throttle scroll-index persistence: only run every 500ms to avoid layout thrashing
			persistScrollIndex();
			void highlightPendingMedia();
		},
	})),
);

function measureVirtualRow(element: unknown) {
	if (element instanceof Element)
		get(rowVirtualizer).measureElement(element);
}

// Firefox and Chrome silently clamp CSS element heights at ~33.5M px, causing a blank render.
// This guard ensures the spacer div never exceeds that limit regardless of item count or column count.
const BROWSER_MAX_CSS_HEIGHT = 33_000_000;
const safeTotalSize = computed(() => Math.min(rowVirtualizer.value.getTotalSize(), BROWSER_MAX_CSS_HEIGHT));

function getScrollElement(): HTMLElement | null {
	const target = get(scrollAreaRef)?.getScrollTarget() ?? null;
	if (target !== get(scrollContainerRef)) {
		set(scrollContainerRef, target);
	}
	return target;
}

// Returns the loaded items belonging to a given row index, preserving each item's global index.
function getRowItems(rowIndex: number): { item: PlexMediaSlimDTO | null; index: number }[] {
	const cols = get(gridItems);
	const startIndex = rowIndex * cols;
	const endIndex = startIndex + cols;
	const loadedItems = mediaOverviewStore.getMediaItemsForRange(startIndex, endIndex);
	const rowItems: { item: PlexMediaSlimDTO | null; index: number }[] = [];

	// loadedItems are already in index order with sortIndex = globalIndex + 1.
	// Single pass: walk both the range and the loaded items in lockstep.
	let loadedIdx = 0;
	for (let index = startIndex; index < endIndex && index < mediaOverviewStore.totalCount; index++) {
		const next = loadedItems[loadedIdx];
		if (next && next.sortIndex === index + 1) {
			rowItems.push({ item: next, index });
			loadedIdx++;
		} else {
			rowItems.push({ item: null, index });
		}
	}

	return rowItems;
}

// Throttled scroll-index persistence — avoids DOM queries + BCR calls on every scroll tick.
let persistTimer: ReturnType<typeof setTimeout> | null = null;

function persistScrollIndex() {
	if (persistTimer)
		return;

	persistTimer = setTimeout(() => {
		persistTimer = null;
		const container = getScrollElement();
		if (!container)
			return;

		const virtualItems = rowVirtualizer.value.getVirtualItems();
		const firstVirtualRow = virtualItems.at(0);
		if (!firstVirtualRow)
			return;

		// Guard initial mount at absolute top
		if (!(container.scrollTop > 0 || firstVirtualRow.index > 0))
			return;

		const posterNodes = Array.from(container.querySelectorAll<HTMLElement>('[data-scroll-index]'));
		if (posterNodes.length === 0)
			return;

		const containerTop = container.getBoundingClientRect().top;
		let nearestIndex: number | null = null;
		let nearestDistance = Number.POSITIVE_INFINITY;

		for (const node of posterNodes) {
			const candidateIndex = Number(node.dataset.scrollIndex);
			if (!Number.isInteger(candidateIndex) || candidateIndex < 0)
				continue;

			const distance = Math.abs(node.getBoundingClientRect().top - containerTop);
			if (distance < nearestDistance) {
				nearestDistance = distance;
				nearestIndex = candidateIndex;
			}
		}

		if (nearestIndex !== null)
			mediaOverviewStore.setCurrentScrollIndex(nearestIndex + 1);
	}, 500);
}

// useElementBounding must be called at setup level so its ResizeObserver is wired correctly.
// Calling it inside watchEffect/watch creates a new instance each time with width=0, which
// causes rowCount = ceil(N/1) = N rows and getTotalSize() to exceed browser CSS height limits.
const { width: containerWidth } = useElementBounding(scrollContainerRef);

watch(containerWidth, (width) => {
	if (width <= 0)
		return;

	const isMobile = width < 600;
	const columns = isMobile
		? Math.min(3, Math.max(1, Math.floor(width / 144)))
		: Math.max(1, Math.floor(width / 232));
	const cardPadding = isMobile ? 8 : 16;
	const rowGap = isMobile ? 4 : 20;
	const cardWidth = isMobile ? width / columns : 232;
	const imageWidth = cardWidth - (cardPadding * 2);
	const cardHeight = isMobile
		? imageWidth * 1.5 + cardPadding + 36
		: 372;

	set(posterCardWidth, cardWidth);
	set(posterCardHeight, cardHeight);
	set(gridRowGap, rowGap);
	set(gridItems, columns);
	set(gridPaddingLeft, Math.max(0, (width - columns * cardWidth) / 2));

	set(posterCardWidthCss, `${cardWidth}px`);
	set(posterImageWidthCss, `${imageWidth}px`);
	set(posterCardPaddingCss, `${cardPadding}px`);
	rowVirtualizer.value.measure();
	if (!get(hasRunInitialPageReady)) {
		set(hasRunInitialPageReady, true);
		nextTick(() => onPageReady());
	}
});

function onPageReady() {
	const requestedScrollIndex = get(mediaOverviewStore.currentScrollIndex);
	if (requestedScrollIndex > 0) {
		scrollToIndex(requestedScrollIndex - 1, get(mediaOverviewStore.pendingMediaHighlightId) === null);
	}

	highlightPendingMedia();
	if (get(mediaOverviewStore.pendingMediaHighlightId) !== null) {
		return;
	}

	const lastMediaItemViewed = get(mediaOverviewStore.lastMediaItemViewed);
	if (requestedScrollIndex <= 0 && lastMediaItemViewed && lastMediaItemViewed.sortIndex > 0)
		scrollToIndex(lastMediaItemViewed.sortIndex - 1);
}

async function highlightPendingMedia() {
	const pendingMediaHighlightId = get(mediaOverviewStore.pendingMediaHighlightId);
	if (pendingMediaHighlightId === null) {
		return;
	}

	const element = await waitForElement(getScrollElement(), `[data-media-id="${pendingMediaHighlightId}"]`);
	if (!element) {
		mediaOverviewStore.consumePendingMediaHighlight(pendingMediaHighlightId);
		return;
	}

	if (get(mediaOverviewStore.pendingMediaHighlightId) === pendingMediaHighlightId) {
		triggerBoxHighlight(element);
		mediaOverviewStore.consumePendingMediaHighlight(pendingMediaHighlightId);
	}
}

function onOpenMediaDetails(mediaItem: PlexMediaSlimDTO) {
	mediaOverviewStore.setPendingMediaHighlight(mediaItem.id, mediaOverviewStore.libraryId);
	if (mediaItem.type === PlexMediaType.Movie) {
		router.push({
			name: 'movies-libraryId-details-movieId',
			params: {
				libraryId: mediaItem.plexLibraryId.toString(),
				movieId: mediaItem.id.toString(),
			},
		});
		return;
	}

	router.push({
		name: 'tvshows-libraryId-details-tvShowId',
		params: {
			libraryId: mediaItem.plexLibraryId.toString(),
			tvShowId: mediaItem.id.toString(),
		},
	});
}

// Throttled page-prefetch during scrolling — avoids firing HTTP requests on every scroll tick.
let prefetchTimer: ReturnType<typeof setTimeout> | null = null;

function requestPagesAroundViewport() {
	if (prefetchTimer)
		return;

	prefetchTimer = setTimeout(() => {
		prefetchTimer = null;
		if (mediaOverviewStore.getMediaItems.length >= mediaOverviewStore.totalCount)
			return;

		const virtualItems = get(rowVirtualizer).getVirtualItems();
		const firstVirtualRow = virtualItems.at(0);
		const lastVirtualRow = virtualItems.at(-1);
		if (!firstVirtualRow || !lastVirtualRow)
			return;

		const cols = get(gridItems);
		const firstVisibleIndex = firstVirtualRow.index * cols;
		const lastVisibleIndex = ((lastVirtualRow.index + 1) * cols) - 1;
		const prefetchBuffer = mediaOverviewStore.pageSize;
		const prefetchStart = Math.max(0, firstVisibleIndex - prefetchBuffer);
		const prefetchEnd = Math.min(mediaOverviewStore.totalCount, lastVisibleIndex + prefetchBuffer);

		useSubscription(mediaOverviewStore.requestRange(prefetchStart, prefetchEnd).subscribe());
	}, 200);
}

function scrollToIndex(index: number, highlight = true) {
	const container = getScrollElement();
	if (!container) {
		Log.error('Could not find scroll container reference: ', container);
		return;
	}

	Log.debug('Scrolling to index:', index);

	const rowIndex = Math.floor(index / get(gridItems));
	get(rowVirtualizer).scrollToIndex(rowIndex, { align: 'start' });

	if (highlight) {
		// Highlight after render
		waitForElement(container, `[data-scroll-index="${index}"]`).then((element) => {
			if (element) {
				triggerBoxHighlight(element);
			}
		});
	}
}

onMounted(() => {
	// Listen for scroll to navigation index command
	useSubscription(mediaOverviewStore.getScrollCommand().subscribe(({ index, highlight }) => {
		if (!getScrollElement()) {
			Log.error('Could not find container with reference: ', get(scrollContainerRef));
			return;
		}

		// Scroll immediately for responsiveness, then prefetch nearby pages in background
		scrollToIndex(index, highlight);
	}));
});
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';
@use '@/assets/scss/variables.scss' as *;

.poster-table-content {
  position: relative;
  width: 100%;
  min-height: 100%;
}

.poster-table-item {
  // GPU acceleration for each item — smoother scrolling
  will-change: transform;
  // Prevent layout thrashing during scroll
  contain: layout style paint;
}

.media-poster-placeholder {
  display: flex;
  flex-direction: column;
  flex: 0 0 var(--poster-card-width, 232px);
  width: var(--poster-card-width, 232px);
  min-width: var(--poster-card-width, 232px);
  max-width: var(--poster-card-width, 232px);
  margin: 0;
  padding: var(--poster-card-padding, 16px) var(--poster-card-padding, 16px) 0;
  box-sizing: border-box;

  &__image {
    width: var(--poster-image-width, 200px);
    aspect-ratio: 2 / 3;
    border-radius: 2px;
    background: rgba(0, 0, 0, 0.45);
  }

  &__quality {
    width: var(--poster-image-width, 200px);
    height: 28px;
    margin-top: 0;
    background: rgba(0, 0, 0, 0.6);
  }
}
</style>
