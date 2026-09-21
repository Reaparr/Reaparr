<template>
	<span
		class="media-comparison-state-button"
		:class="{
			'media-comparison-state-button--with-label': showLabel,
			'media-comparison-state-button--dense': dense,
			'media-comparison-state-button--clickable': clickable,
			'media-comparison-state-button--flat': flat,
			[`media-comparison-state-button--${comparisonBadge.tone}`]: true,
		}">
		<BaseButton
			:icon="comparisonBadge.icon"
			:label="resolvedLabel"
			:size="size"
			:flat="flat"
			:round="round"
			:rounded="rounded"
			:outline="outline"
			:aria-label="translateMediaComparisonState(comparisonState)"
			:tooltip-text="showTooltip ? translateMediaComparisonState(comparisonState) : ''"
			:cy="cy"
			@click="onClick" />
	</span>
</template>

<script setup lang="ts">
import { PlexMediaComparisonState } from '@dto';
import { translateMediaComparisonState } from '@composables';

const props = withDefaults(defineProps<{
	comparisonState: PlexMediaComparisonState;
	showTooltip?: boolean;
	showLabel?: boolean;
	size?: string;
	dense?: boolean;
	flat?: boolean;
	round?: boolean;
	rounded?: boolean;
	outline?: boolean;
	clickable?: boolean;
	cy?: string;
}>(), {
	showTooltip: false,
	showLabel: false,
	size: '1rem',
	dense: false,
	flat: false,
	round: false,
	rounded: true,
	outline: false,
	clickable: false,
	cy: undefined,
});

const emit = defineEmits<{
	(e: 'click'): void;
}>();

const comparisonBadge = computed((): {
	icon: string;
	tone: 'not-compared'
		| 'owned'
		| 'missing'
		| 'higher-quality'
		| 'pending'
		| 'partial'
		| 'partial-and-higher-quality'
		| 'unknown';
} => {
	switch (props.comparisonState) {
		case PlexMediaComparisonState.NotCompared:
			return {
				icon: 'mdi-alert-circle-outline',
				tone: 'not-compared',
			};
		case PlexMediaComparisonState.Owned:
			return {
				icon: 'mdi-check-circle-outline',
				tone: 'owned',
			};
		case PlexMediaComparisonState.Missing:
			return {
				icon: 'mdi-video-off-outline',
				tone: 'missing',
			};
		case PlexMediaComparisonState.HigherQuality:
			return {
				icon: 'mdi-arrow-up-circle',
				tone: 'higher-quality',
			};
		case PlexMediaComparisonState.Pending:
			return {
				icon: 'mdi-progress-clock',
				tone: 'pending',
			};
		case PlexMediaComparisonState.Partial:
			return {
				icon: 'mdi-circle-slice-4',
				tone: 'partial',
			};
		case PlexMediaComparisonState.PartialAndHigherQuality:
			return {
				icon: 'mdi-layers-triple-outline',
				tone: 'partial-and-higher-quality',
			};
		case PlexMediaComparisonState.Unknown:
		default:
			return {
				icon: 'mdi-crosshairs-question',
				tone: 'unknown',
			};
	}
});

const resolvedLabel = computed(() => props.showLabel ? translateMediaComparisonState(props.comparisonState) : undefined);

function onClick() {
	if (props.clickable)
		emit('click');
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.media-comparison-state-button {
  white-space: nowrap;
  color: #cfd8dc;

  .q-btn {
    color: currentColor !important;
    background: rgba(10, 14, 22, 0.88);
    border: none !important;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.55);
    backdrop-filter: blur(4px);
    text-transform: none;

    .q-icon,
    .block {
      color: currentColor !important;
    }
  }
}

.media-comparison-state-button--flat {
  .q-btn {
    background: transparent;
    box-shadow: none;
    backdrop-filter: none;
  }
}

.media-comparison-state-button--clickable {
  .q-btn {
    cursor: pointer;
  }
}

.media-comparison-state-button--owned {
  color: #7fc9a0;
}

.media-comparison-state-button--missing {
  color: #ff9f43;
}

.media-comparison-state-button--higher-quality {
  color: #8ea7ff;
}

.media-comparison-state-button--pending {
  color: #90caf9;
}

.media-comparison-state-button--partial {
  color: #ffc857;
}

.media-comparison-state-button--partial-and-higher-quality {
  color: #d8a7ff;
}

.media-comparison-state-button--unknown {
  color: #ff8a80;
}

.media-comparison-state-button--with-label {
  min-width: max-content;
}

.media-comparison-state-button--dense {
  .q-btn {
    min-height: 2em;
    padding: 0.25em;
  }
}

@media (max-width: $breakpoint-sm-max) {
  .media-comparison-state-button--dense {
    .q-btn {
      min-width: 44px !important;
      min-height: 44px !important;
    }
  }
}
</style>
