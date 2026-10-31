<template>
	<q-card
		class="media-poster-card"
		:class="{ 'media-poster-card--without-overlay': !overlay }"
		ref="card"
		flat
		@click="onClick"
		@contextmenu="onContextMenu"
		@keydown="onKeydown">
		<q-card-section>
			<div class="media-poster-image-wrapper">
				<QGlowContainer>
					<div class="media-poster-image-surface">
						<q-skeleton
							v-if="loading"
							class="media-poster-skeleton"
							animation="fade"
							square
							dark />
						<template v-else>
							<div
								v-if="$slots.image"
								class="media-poster--image-slot">
								<slot name="image" />
							</div>
							<q-img
								v-else-if="imageUrl"
								loading="lazy"
								:src="imageUrl"
								fit="cover"
								:ratio="2 / 3"
								no-spinner
								crossorigin="anonymous"
								class="media-poster--image"
								:alt="title" />
							<div
								v-else
								class="media-poster--content media-poster--fallback">
								<div class="media-poster--fallback-icon">
									<q-icon
										v-if="fallbackIcon"
										:name="fallbackIcon"
										size="60px"
										aria-hidden="true" />
									<QMediaTypeIcon
										v-else
										:size="60"
										:media-type="mediaType" />
								</div>
								<div class="media-poster--section media-poster--section-compact">
									<QText
										:value="title"
										bold="bold"
										align="center"
										:size="titleSize"
										class="media-poster--title" />
									<slot
										name="metadata"
										:compact="true" />
								</div>
							</div>

							<div :class="overlay ? 'media-poster--overlay white--text' : 'media-poster--controls'">
								<div class="media-poster--content">
									<div
										v-if="overlay"
										class="media-poster--section">
										<QText
											:value="title"
											bold="bold"
											align="center"
											:size="titleSize"
											class="media-poster--title" />
										<slot
											name="metadata"
											:compact="false" />
									</div>
									<div
										v-if="$slots.actions"
										class="media-poster--actions">
										<slot
											name="actions"
											:compact="false"
											:activate="activateCard" />
									</div>
								</div>
							</div>
						</template>

						<slot name="badge" />
						<div
							v-if="$slots['image-actions'] || (overlay && $q.screen.lt.md && $slots.actions)"
							class="media-poster-image-actions">
							<slot name="image-actions">
								<q-btn
									v-if="overlay && $q.screen.lt.md && $slots.actions"
									class="media-poster-mobile-actions-trigger"
									flat
									round
									dense
									icon="mdi-dots-vertical"
									:aria-label="`${title} — ${$t('general.commands.media-options')}`"
									data-cy="media-poster-menu-trigger"
									@click.stop="mobileMenuVisible = true" />
							</slot>
						</div>
					</div>
				</QGlowContainer>

				<div
					v-if="$slots.sort"
					class="media-poster-sort-overlay">
					<slot name="sort" />
				</div>
			</div>

			<slot name="footer" />
		</q-card-section>
		<slot name="menu">
			<q-menu
				v-if="overlay && $q.screen.lt.md && $slots.actions"
				v-model="mobileMenuVisible"
				no-parent-event
				anchor="bottom middle"
				self="bottom middle"
				data-cy="media-poster-menu">
				<q-list class="q-pa-sm">
					<slot
						name="actions"
						:compact="true"
						:activate="activateCard" />
				</q-list>
			</q-menu>
		</slot>
	</q-card>
</template>

<script setup lang="ts">
import type { PlexMediaType } from '@dto';
import { get } from '@vueuse/core';

const props = withDefaults(defineProps<{
	title: string;
	mediaType: PlexMediaType;
	imageUrl?: string;
	loading?: boolean;
	fallbackIcon?: string;
	overlay?: boolean;
}>(), {
	loading: false,
	overlay: true,
});
const titleSize = computed(() => {
	const len = props.title?.length ?? 0;
	if (len <= 25) return 'h5';
	if (len <= 50) return 'h6';
	return 'subtitle1';
});
const $q = useQuasar();
const mobileMenuVisible = ref(false);
const card = ref<{ $el: HTMLElement } | null>(null);

function activateCard(): void {
	get(card)?.$el.click();
}

defineSlots<{
	image?: () => unknown;
	metadata?: (props: { compact: boolean }) => unknown;
	actions?: (props: { compact: boolean; activate: () => void }) => unknown;
	'image-actions'?: () => unknown;
	badge?: () => unknown;
	sort?: () => unknown;
	footer?: () => unknown;
	menu?: () => unknown;
}>();
const emit = defineEmits<{
	(e: 'click' | 'contextmenu', event: MouseEvent): void;
}>();
function onClick(event: MouseEvent) {
	emit('click', event);
}

function onContextMenu(event: MouseEvent) {
	emit('contextmenu', event);
}

function onKeydown(event: KeyboardEvent) {
	if (event.target !== event.currentTarget)
		return;

	if (event.key !== 'Enter' && event.key !== ' ' && event.key !== 'Spacebar' && event.key !== 'Space')
		return;

	event.preventDefault();
	if (event.currentTarget instanceof HTMLElement)
		event.currentTarget.click();
}
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';
@use '@/assets/scss/variables.scss' as *;

.media-poster-menu__item {
  min-height: 44px;
}

.q-img__content > div {
  padding: 0;
}

.media-poster-card {
  flex: 0 0 var(--poster-card-width, 232px) !important;
  width: var(--poster-card-width, 232px) !important;
  min-width: var(--poster-card-width, 232px) !important;
  max-width: var(--poster-card-width, 232px) !important;
}

.media-poster-card .q-card__section {
  padding: var(--poster-card-padding, 16px) var(--poster-card-padding, 16px) 0;
}

.media-poster-skeleton {
  @extend .background-sm;
  width: var(--poster-image-width, 200px);
  aspect-ratio: 2 / 3;
  height: auto;
  padding: 0;
}

.media-poster {
  @extend .background-sm;

  width: var(--poster-image-width, 200px);

  &--image {
    aspect-ratio: 2 / 3;
    height: auto;
    padding: 0;
  }

  &--overlay {
    @extend .background-xl;
    position: absolute;
    inset: 0;
    opacity: 0;
    margin: 0;
    pointer-events: none;
    transition: opacity 0.2s ease-in-out;
  }

  &--content {
    overflow: hidden;
    width: var(--poster-image-width, 200px);
    aspect-ratio: 2 / 3;
    height: auto;
  }

  &--section {
    padding: 36px 16px 16px 16px;
  }

  &--section-compact {
    padding-top: 0;
    padding-bottom: 0;
  }

  &--fallback {
    @extend .background-sm;
    padding: 0 !important;
    transition: opacity 0.2s ease-in-out;
  }

  &--fallback-icon {
    display: flex;
    justify-content: center;
    padding: 16px;
  }

  &--title {
    .q-text {
      display: -webkit-box;
      -webkit-line-clamp: 4;
      line-clamp: 4;
      -webkit-box-orient: vertical;
      overflow: hidden;
      text-overflow: ellipsis;
      word-break: break-word;
      overflow-wrap: break-word;
    }
  }

  &--actions {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    padding: 8px;
    text-align: center;
  }

  &--actions-center,
  &--actions-around {
    display: flex;
    align-items: center;
    width: 100%;
  }

  &--actions-center {
    justify-content: center;
  }

  &--actions-around {
    justify-content: space-around;
  }
}

.media-poster-image-wrapper {
  position: relative;
  display: block;
  width: 100%;
}

.media-poster-image-surface {
  position: relative;
  width: var(--poster-image-width, 200px);
  aspect-ratio: 2 / 3;
  overflow: hidden;
}

.media-poster--image-slot {
  width: var(--poster-image-width, 200px);
  height: 100%;
  overflow: hidden;
}

.media-poster--image-slot > * {
  width: 100% !important;
  height: 100% !important;
}

.media-poster-image-actions {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

.media-poster-image-actions > * {
  pointer-events: auto;
}

.media-poster--controls {
  position: absolute;
  inset: 0;
  pointer-events: none;

  .media-poster--actions {
    pointer-events: auto;
  }
}

.media-poster-card:not(.media-poster-card--without-overlay):hover {
  .media-poster--fallback {
    opacity: 0;
  }

  .media-poster--overlay {
    opacity: 0.8;
    pointer-events: auto;

    .q-btn {
      opacity: 1;
    }
  }
}

@media (max-width: $breakpoint-sm-max) {
  .media-poster-card .media-poster--overlay,
  .media-poster-card:hover .media-poster--overlay {
    display: none;
  }

  .media-poster-card .media-poster--fallback,
  .media-poster-card:hover .media-poster--fallback {
    opacity: 1;
  }
}

@media (max-width: 599px) {
  .media-poster--section {
    padding: 16px 8px 8px;
  }

  .media-poster--section-compact {
    padding: 0 8px;
  }

  .media-poster--title .q-text {
    font-size: clamp(0.7rem, 0.55rem + 0.8vw, 1.25rem);
    line-height: 1.2;
  }

  .media-poster-quality-bar {
    min-width: 0;
    overflow: hidden;
    padding: 0;

    > * {
      min-width: 0;
      max-width: 100%;
      flex: 1 1 auto;
      overflow: hidden;
    }

    .q-chip {
      min-width: 0;
      max-width: 100%;
      margin: 0;
      padding-inline: 4px;
      font-size: 12px !important;
    }

    .q-chip__content,
    .q-text {
      min-width: 0;
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .q-chip .q-text {
      font-size: inherit !important;
    }
  }
}

.media-poster-quality-bar {
  @extend .background-sm;
}

.media-poster-sort-overlay {
  @extend .background-lg;

  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 4px 8px;
  text-align: center;
  color: #fff;
  pointer-events: none;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.media-poster-library-name {
  padding-top: 2px;
  opacity: 0.7;
}
.media-poster-mobile-actions-trigger {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 9999;
  min-width: 44px;
  min-height: 44px;
  touch-action: manipulation;
}
</style>
