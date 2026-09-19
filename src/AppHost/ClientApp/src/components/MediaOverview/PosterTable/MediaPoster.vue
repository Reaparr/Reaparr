<template>
	<q-card
		class="media-poster-card"
		flat
		@click="openMobileActions"
		@contextmenu="onContextMenu">
		<q-card-section>
			<div class="media-poster-image-wrapper">
				<q-btn
					v-if="$q.screen.lt.md"
					class="media-poster-mobile-actions-trigger"
					flat
					round
					dense
					icon="mdi-dots-vertical"
					:aria-label="`${mediaItem.title} — ${$t('general.commands.media-options')}`"
					data-cy="media-poster-menu-trigger"
					@click.stop="openMobileActionsMenu" />
				<QGlowContainer>
					<q-skeleton
						v-if="thumbnailLoading"
						class="media-poster-skeleton"
						animation="fade"
						square
						dark />
					<template v-else>
						<q-img
							v-if="imageUrl"
							loading="lazy"
							:src="imageUrl"
							fit="cover"
							:ratio="2 / 3"
							no-spinner
							crossorigin="anonymous"
							class="media-poster--image"
							:alt="mediaItem.title" />
						<div
							v-else
							class="media-poster--content media-poster--fallback">
							<div class="media-poster--fallback-icon">
								<QMediaTypeIcon
									:size="60"
									:media-type="mediaType" />
							</div>
							<div class="media-poster--section media-poster--section-compact">
								<QText
									:value="mediaItem.title"
									bold="bold"
									align="center"
									:size="titleSize"
									class="media-poster--title" />
								<QText
									v-if="mediaOverviewStore.allMediaMode"
									align="center"
									size="subtitle2"
									:value="serverStore.getServerName(mediaItem.plexServerId)" />
							</div>
						</div>

						<!-- Hover overlay (always rendered, positioned absolutely over image/fallback) -->
						<div class="media-poster--overlay white--text">
							<div class="media-poster--content">
								<div class="media-poster--section">
									<QText
										:value="mediaItem.title"
										bold="bold"
										align="center"
										:size="titleSize"
										class="media-poster--title" />
									<QText
										v-if="mediaType === PlexMediaType.TvShow"
										:value="$t('components.media-poster-image-content.seasons-count', { count: mediaItem.childCount })"
										bold="bold"
										align="center"
										size="subtitle1" />
									<QText
										v-if="mediaType === PlexMediaType.TvShow"
										:value="$t('components.media-poster-image-content.episode-count', { count: mediaItem.grandChildCount })"
										bold="bold"
										align="center"
										size="subtitle1" />
									<QText
										v-if="mediaOverviewStore.allMediaMode"
										align="center"
										size="subtitle1"
										:value="serverStore.getServerName(mediaItem.plexServerId)" />
									<QText
										v-if="mediaOverviewStore.allMediaMode"
										align="center"
										size="subtitle2"
										class="media-poster-library-name"
										:value="libraryStore.getLibraryName(mediaItem.plexLibraryId)" />
								</div>

								<div
									:class="['media-poster--actions', hasDetailsAction ? 'media-poster--actions-around' : 'media-poster--actions-center']">
									<BaseButton
										icon="mdi-download"
										size="xl"
										flat
										:outline="false"
										@click="onDownload([])" />
									<BaseButton
										v-if="hasDetailsAction"
										icon="mdi-magnify"
										:outline="false"
										size="xl"
										flat
										@click="emit('open-media-details', mediaItem)" />
								</div>
							</div>
						</div>
					</template>

					<!-- Comparison State Button (always visible, rendered outside image element) -->
					<MediaComparisonStateButton
						class="comparison-state-button"
						:comparison-state="getPlexMediaComparisonState(mediaItem)"
						show-tooltip
						dense
						:clickable="comparisonBadgeClickable"
						:cy="`comparison-chip-${getPlexMediaComparisonState(mediaItem)}`"
						@click="openComparisonDetails" />
				</QGlowContainer>

				<div
					v-if="mediaOverviewStore.getIsSorted"
					class="media-poster-sort-overlay">
					<QFileSize
						v-if="mediaOverviewStore.getActiveSort.field === MediaSortField.MediaSize"
						:size="mediaItem.mediaSize"
						align="center" />
					<QDuration
						v-else-if="mediaOverviewStore.getActiveSort.field === MediaSortField.Duration"
						:value="mediaItem.duration"
						align="center"
						short />
					<QDateTime
						v-else-if="mediaOverviewStore.getActiveSort.field === MediaSortField.AddedAt"
						:text="mediaItem.addedAt"
						align="center"
						short-date />
					<QDateTime
						v-else-if="mediaOverviewStore.getActiveSort.field === MediaSortField.UpdatedAt"
						:text="mediaItem.updatedAt ?? ''"
						align="center"
						short-date />
					<QText
						v-else-if="mediaOverviewStore.getActiveSort.field === MediaSortField.Year"
						:value="mediaItem.year"
						align="center" />
				</div>
			</div>

			<MediaQuality
				class="media-poster-quality-bar"
				:qualities="mediaItem.qualities"
				clickable
				@download="onDownload" />
		</q-card-section>
		<QLoadingOverlay :loading="loading" />
		<q-menu
			v-if="$q.screen.lt.md"
			v-model="mobileMenuVisible"
			no-parent-event
			anchor="bottom middle"
			self="bottom middle"
			data-cy="media-poster-menu">
			<q-list class="media-poster-menu">
				<q-item
					v-if="hasDetailsAction"
					v-close-popup
					class="media-poster-menu__item"
					clickable
					data-cy="media-poster-menu-details"
					@click="emit('open-media-details', mediaItem)">
					<q-item-section avatar>
						<q-icon name="mdi-magnify" />
					</q-item-section>
					<q-item-section>{{ $t('general.commands.view') }}</q-item-section>
				</q-item>
				<q-item
					v-close-popup
					class="media-poster-menu__item"
					clickable
					data-cy="media-poster-menu-download"
					@click="onDownload([])">
					<q-item-section avatar>
						<q-icon name="mdi-download" />
					</q-item-section>
					<q-item-section>{{ $t('general.commands.download') }}</q-item-section>
				</q-item>
			</q-list>
		</q-menu>
	</q-card>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import type { Subscription } from 'rxjs';
import { PlexMediaComparisonState, PlexMediaType } from '@dto';
import type { DownloadMediaDTO, PlexMediaQualityDTO, PlexMediaSlimDTO } from '@dto';
import { MediaSortField } from '@enums';
import {
	useDialogStore,
	useMediaOverviewStore,
	useMediaStore,
	useServerStore,
	useSettingsStore,
	useLibraryStore,
} from '@store';
import { getPlexMediaComparisonState } from '@composables';

const mediaOverviewStore = useMediaOverviewStore();
const mediaStore = useMediaStore();
const serverStore = useServerStore();
const settingsStore = useSettingsStore();
const libraryStore = useLibraryStore();
const dialogStore = useDialogStore();
const $q = useQuasar();
const props = withDefaults(defineProps<{
	mediaItem: PlexMediaSlimDTO;
	active?: boolean;
}>(), {
	active: true,
});

const emit = defineEmits<{
	(e: 'download', downloadMediaCommands: DownloadMediaDTO[]): void;
	(e: 'open-media-details', payload: PlexMediaSlimDTO): void;
}>();

const loading = ref(false);
const thumbnailLoading = ref(true);
const imageUrl = ref('');
const thumbWidth = 200;
const thumbHeight = 300;
let currentSubscription: Subscription | null = null;
const mobileMenuVisible = shallowRef(false);

const mediaType = computed(() => props.mediaItem?.type ?? PlexMediaType.Unknown);

const hasDetailsAction = computed(() => get(mediaType) === PlexMediaType.TvShow || get(mediaType) === PlexMediaType.Movie);

const comparisonBadgeClickable = computed(() => {
	return [
		PlexMediaComparisonState.Missing,
		PlexMediaComparisonState.HigherQuality,
		PlexMediaComparisonState.Partial,
		PlexMediaComparisonState.PartialAndHigherQuality,
	].includes(getPlexMediaComparisonState(props.mediaItem));
});

const titleSize = computed(() => {
	const len = props.mediaItem?.title?.length ?? 0;
	if (len <= 25) return 'h5';
	if (len <= 50) return 'h6';
	return 'subtitle1';
});

function onDownload(mediaQualities: PlexMediaQualityDTO[]) {
	const downloadCommand: DownloadMediaDTO = {
		type: get(mediaType),
		mediaIds: [props.mediaItem.id],

		plexLibraryId: props.mediaItem.plexLibraryId,
		plexServerId: props.mediaItem.plexServerId,
		qualities: mediaQualities,
		keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
	};

	emit('download', [downloadCommand]);
}

function openMobileActions(event: Event) {
	if (!$q.screen.lt.md || isMobileMenuExcludedTarget(event.target))
		return;

	set(mobileMenuVisible, true);
}

function openMobileActionsMenu() {
	if ($q.screen.lt.md)
		set(mobileMenuVisible, true);
}

function onContextMenu(event: MouseEvent) {
	if (!$q.screen.lt.md)
		return;

	event.preventDefault();
	openMobileActions(event);
}

function isMobileMenuExcludedTarget(target: EventTarget | null): boolean {
	return target instanceof Element && !!target.closest('button, a, .media-poster-quality-bar');
}

function openComparisonDetails() {
	if (get(comparisonBadgeClickable))
		dialogStore.openMediaComparisonDetailsDialog(props.mediaItem);
}

function loadThumbnail(mediaItem: PlexMediaSlimDTO) {
	if (currentSubscription) {
		currentSubscription.unsubscribe();
		currentSubscription = null;
	}

	set(thumbnailLoading, true);

	if (!mediaItem?.hasThumb || mediaItem.plexApiMetaDataKey === 0 || mediaItem.plexApiRatingKey === 0) {
		set(imageUrl, '');
		set(thumbnailLoading, false);
		return;
	}

	const useLowQualityPoster = settingsStore.generalSettings.useLowQualityPosterImages;
	currentSubscription = mediaStore.getMediaThumbnailUrl({
		plexServerId: mediaItem.plexServerId,
		plexKey: mediaItem.plexApiRatingKey.toString(),
		metaDataKey: mediaItem.plexApiMetaDataKey,
		width: useLowQualityPoster ? thumbWidth : thumbWidth * 1.5,
		height: useLowQualityPoster ? thumbHeight : thumbHeight * 1.5,
	}).subscribe({
		next: (url) => {
			set(imageUrl, url);
			set(thumbnailLoading, false);
		},
		error: () => {
			set(imageUrl, '');
			set(thumbnailLoading, false);
		},
	});
}

watch([() => props.mediaItem.id, () => props.active], ([, isActive]) => {
	if (isActive) {
		loadThumbnail(props.mediaItem);
	}
}, { immediate: true });

onUnmounted(() => {
	if (currentSubscription)
		currentSubscription.unsubscribe();
});
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

.media-poster-menu__item {
  min-height: 44px;
}

.q-img__content > div {
  padding: 0;
}

.comparison-state-button {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 9999;
  pointer-events: auto;
  max-width: calc(100% - 16px);
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
    width: 100%;
    padding: 8px;
    text-align: center;
  }

  &--actions-center {
    justify-content: center;
  }

  &--actions-around {
    justify-content: space-around;
  }
}
.media-poster-image-wrapper .media-poster--image {
  height: auto;
}

.media-poster-card:hover {
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

.media-poster-image-wrapper {
  position: relative;
  display: block;
  width: 100%;
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
</style>
