<template>
	<div class="alphabet-navigation-container">
		<div class="alphabet-navigation">
			<q-btn
				v-for="[displayValue, scrollIndex] in mediaOverviewStore.scrollDict"
				:key="displayValue"
				class="navigation-btn"
				:label="getDisplayValue(displayValue)"
				:loading="clickedLabel === displayValue && mediaOverviewStore.navLoading"
				flat
				square
				no-wrap
				:data-cy="`letter-${displayValue}-alphabet-navigation-btn`"
				@click="onLetterClick(displayValue, scrollIndex)">
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
import { set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { useMediaOverviewStore } from '@store';
import { MediaSortField } from '@enums';
import { getVideoQualityFromValue, translateVideoQuality } from '@composables';

const mediaOverviewStore = useMediaOverviewStore();
const clickedLabel = ref<string | null>(null);

watch(() => mediaOverviewStore.navLoading, (isLoading) => {
	if (!isLoading) {
		set(clickedLabel, null);
	}
});

function onLetterClick(label: string, scrollIndex: number) {
	set(clickedLabel, label);
	mediaOverviewStore.clearPendingMediaHighlight();
	useSubscription(mediaOverviewStore.scrollToIndex(scrollIndex).subscribe());
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
