<template>
	<div
		class="alphabet-navigation-container"
		data-cy="alphabet-navigation-container">
		<div
			ref="navigation"
			class="alphabet-navigation"
			:class="{ 'alphabet-navigation--dragging': isDragging }"
			data-cy="alphabet-navigation"
			@pointerdown="onPointerDown"
			@pointermove="onPointerMove"
			@pointerup="onPointerUp"
			@pointercancel="onPointerCancel">
			<q-btn
				v-for="entry in navigationEntries"
				:key="entry.displayValue"
				class="navigation-btn"
				:label="entry.label"
				:loading="clickedLabel === entry.displayValue && mediaOverviewStore.navLoading"
				flat
				square
				no-wrap
				:data-cy="`letter-${entry.displayValue}-alphabet-navigation-btn`"
				@click="onLetterClick(entry.displayValue, entry.scrollIndex)">
				<template #loading>
					<QSpinnerPuff
						size="1em"
						color="primary" />
				</template>
			</q-btn>
		</div>
	</div>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { useMediaOverviewStore } from '@store';
import { MediaSortField } from '@enums';
import { getVideoQualityFromValue, translateVideoQuality } from '@composables';

const mediaOverviewStore = useMediaOverviewStore();
const $q = useQuasar();

type AlphabetNavigationEntry = {
	displayValue: string;
	scrollIndex: number;
	label: string;
};

const navigationRef = useTemplateRef<HTMLElement>('navigation');
const clickedLabel = shallowRef<string | null>(null);
const isDragging = shallowRef(false);
const dragStartY = shallowRef<number | null>(null);
const draggedEntry = shallowRef<AlphabetNavigationEntry | null>(null);

const navigationEntries = computed<AlphabetNavigationEntry[]>(() => {
	return Array.from(mediaOverviewStore.scrollDict.entries()).map(([displayValue, scrollIndex]) => ({
		displayValue,
		scrollIndex,
		label: getDisplayValue(displayValue),
	}));
});

watch(() => mediaOverviewStore.navLoading, (isLoading) => {
	if (!isLoading) {
		set(clickedLabel, null);
	}
});

function onLetterClick(label: string, scrollIndex: number, highlight = true) {
	set(clickedLabel, label);
	mediaOverviewStore.clearPendingMediaHighlight();
	useSubscription(mediaOverviewStore.scrollToIndex(scrollIndex, highlight).subscribe());
}

function onPointerDown(event: PointerEvent) {
	if (!$q.screen.lt.sm || (event.pointerType === 'mouse' && event.button !== 0)) {
		return;
	}

	set(isDragging, false);
	set(dragStartY, event.clientY);
	set(draggedEntry, null);
}

function onPointerMove(event: PointerEvent) {
	const startY = get(dragStartY);
	if (startY === null || Math.abs(event.clientY - startY) < 8) {
		return;
	}

	if (!get(isDragging)) {
		set(isDragging, true);
		get(navigationRef)?.setPointerCapture(event.pointerId);
	}
	event.preventDefault();

	const entries = get(navigationEntries);
	const element = get(navigationRef);
	if (!element || entries.length === 0) return;

	const rect = element.getBoundingClientRect();
	if (rect.height <= 0 || element.scrollHeight <= 0) return;

	const pointerOffset = event.clientY - rect.top + element.scrollTop;
	const position = Math.min(0.999, Math.max(0, pointerOffset / element.scrollHeight));
	set(draggedEntry, entries[Math.floor(position * entries.length)] ?? null);
}

function onPointerUp() {
	const entry = get(draggedEntry);
	if (get(isDragging) && entry) onLetterClick(entry.displayValue, entry.scrollIndex, false);
	resetDragState();
}

function onPointerCancel() {
	resetDragState();
}

function resetDragState() {
	set(isDragging, false);
	set(dragStartY, null);
	set(draggedEntry, null);
}

function getDisplayValue(value: string): string {
	if (mediaOverviewStore.getActiveSort.field !== MediaSortField.Quality) {
		return value;
	}

	const quality = getVideoQualityFromValue(value);
	return quality === undefined ? value : translateVideoQuality(quality);
}
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';
@use '@/assets/scss/variables' as *;

.alphabet-navigation-container {
  height: 100%;
  min-height: 0;
  max-height: none;
  display: flex;
  align-content: stretch;
  align-items: stretch;
  align-self: stretch;
  justify-content: center;
  flex: 0 0 30px;

  .alphabet-navigation {
    height: 100%;
    min-height: 0;
    display: flex;
    justify-content: space-around;
    flex: 0 0 100%;
    flex-direction: column;
    overflow-y: auto;
    scrollbar-width: none;

    &::-webkit-scrollbar {
      display: none;
    }

    .navigation-btn {
      @extend .fade-out-border;
      flex: 1 1 25px;
      text-align: center;
      font-weight: bold;
      background: transparent !important;

      &:hover {
        &::before {
          opacity: 0.2 !important;
        }
      }
    }
  }
}

@media (max-width: $breakpoint-xs-max) {
  .alphabet-navigation-container {
    position: absolute;
    inset-block: 0;
    inset-inline-end: 0;
    z-index: 2;
    width: 44px;
    height: auto;
    max-height: none;
    flex-basis: 44px;
    transform: none;
    border-radius: 0.75rem 0 0 0.75rem;
    background: rgba(0, 0, 0, 0.72);
    backdrop-filter: blur(8px);
  }

  .alphabet-navigation-container .alphabet-navigation {
    width: 100%;
    flex: 1 1 auto;
    justify-content: flex-start;
    overflow-x: hidden;
    overflow-y: auto;
    overscroll-behavior: contain;
    touch-action: pan-y;

    &--dragging .navigation-btn {
      background: rgba(255, 255, 255, 0.08) !important;
    }
  }

  .alphabet-navigation-container .navigation-btn {
    min-width: 44px;
    min-height: 44px !important;
    height: 44px;
    flex: 0 0 44px;
    padding: 0 !important;
    font-size: 0.72rem;
    line-height: 1;
  }
}

body {
  &.body--dark {
    .navigation-btn {
      color: red;
    }
  }

  &.body--light {
    .navigation-btn {
      color: darkred;
    }
  }
}
</style>
