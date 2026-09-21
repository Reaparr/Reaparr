<template>
	<QCard
		:class="['overview-card red-glow-hover', { 'overview-card--add': mode === 'add' }]"
		:data-cy="cy"
		:aria-label="ariaLabel"
		role="button"
		tabindex="0"
		@click="activate"
		@keydown.enter="activate"
		@keydown.space.prevent="activate">
		<QIcon
			v-if="mode === 'add'"
			name="mdi-plus"
			style="font-size: 90px" />
		<QCardSection
			v-else
			class="overview-card__content q-pa-md">
			<div class="overview-card__identity row items-center no-wrap q-gutter-sm">
				<QImg
					:src="iconSource"
					:alt="icon"
					no-spinner
					fit="contain"
					width="48px"
					height="48px"
					class="overview-card__logo" />
				<div class="overview-card__details">
					<div
						v-if="title"
						class="overview-card__title text-subtitle1 ellipsis">
						{{ title }}
					</div>
					<div
						v-if="subtitle"
						class="overview-card__subtitle text-caption">
						{{ subtitle }}
					</div>
				</div>
			</div>
			<div
				v-if="chips.length"
				class="overview-card__chips row q-gutter-sm q-mt-md">
				<QGlowChip
					v-for="chip in chips"
					:key="`${chip.color}-${chip.value}`"
					:color="chip.color"
					:value="chip.value" />
			</div>
		</QCardSection>
	</QCard>
</template>

<script setup lang="ts">
import type { NamedColor } from 'quasar';

type OverviewCardMode = 'add' | 'edit';
type OverviewCardIcon = 'plex' | 'radarr' | 'sonarr';

interface OverviewCardChip {
	value: string | number;
	color: NamedColor;
}

const props = withDefaults(defineProps<{
	mode: OverviewCardMode;
	icon?: OverviewCardIcon;
	title?: string;
	subtitle?: string;
	chips?: OverviewCardChip[];
	cy?: string;
	ariaLabel?: string;
}>(), {
	icon: 'plex',
	title: '',
	subtitle: '',
	chips: () => [],
	cy: undefined,
	ariaLabel: undefined,
});

const emit = defineEmits<{ (event: 'click'): void }>();
const iconSource = computed(() => `/img/logo/${props.icon}.svg`);

function activate(): void {
	emit('click');
}
</script>

<style lang="scss">
.overview-card {
  border: 2px solid red;
  min-height: 140px;
  max-height: 140px;

  &:focus-visible {
    outline: 2px solid white;
    outline-offset: 3px;
  }

  &--add {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  &__identity {
    min-width: 0;
  }

  &__logo {
    flex: 0 0 auto;
    padding: 4px;
    border-radius: 8px;
  }

  &__details,
  &__title {
    min-width: 0;
  }

  &__subtitle {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__chips {
    min-width: 0;
  }

  @media (max-width: $breakpoint-xs-max) {
    max-height: none;

    &__content {
      min-width: 0;
    }

    &__chips {
      gap: 0.25rem;

      .q-chip {
        max-width: 100%;
        margin: 0;
      }
    }
  }
}
</style>
