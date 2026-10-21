<template>
	<MediaPosterCard
		v-bind="$attrs"
		:title="mediaItem.title"
		:media-type="mediaType"
		:image-url="imageUrl"
		:loading="thumbnailLoading"
		@click="openMobileActions"
		@contextmenu="onContextMenu">
		<template #image-actions>
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
		</template>

		<template #metadata="{ compact }">
			<QText
				v-if="mediaType === PlexMediaType.TvShow && !compact"
				:value="$t('components.media-poster-image-content.seasons-count', { count: mediaItem.childCount })"
				bold="bold"
				align="center"
				size="subtitle1" />
			<QText
				v-if="mediaType === PlexMediaType.TvShow && !compact"
				:value="$t('components.media-poster-image-content.episode-count', { count: mediaItem.grandChildCount })"
				bold="bold"
				align="center"
				size="subtitle1" />
			<QText
				v-if="mediaType === PlexMediaType.MusicArtist && !compact"
				:value="`${$t('components.music-details.album-count')}: ${mediaItem.childCount}`"
				align="center"
				size="subtitle1" />
			<QText
				v-if="mediaType === PlexMediaType.MusicArtist && !compact"
				:value="`${$t('components.music-details.track-count')}: ${mediaItem.grandChildCount}`"
				align="center"
				size="subtitle1" />
			<QText
				v-if="mediaType === PlexMediaType.PhotoAlbum && !compact"
				:value="`${$t('components.photo-details.asset-count')}: ${mediaItem.childCount}`"
				align="center"
				size="subtitle1" />
			<QText
				v-if="mediaOverviewStore.allMediaMode"
				align="center"
				:size="compact ? 'subtitle2' : 'subtitle1'"
				:value="serverStore.getServerName(mediaItem.plexServerId)" />
			<QText
				v-if="mediaOverviewStore.allMediaMode && !compact"
				align="center"
				size="subtitle2"
				class="media-poster-library-name"
				:value="libraryStore.getLibraryName(mediaItem.plexLibraryId)" />
		</template>

		<template #actions>
			<div :class="['media-poster--actions-inner', hasDetailsAction ? 'media-poster--actions-around' : 'media-poster--actions-center']">
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
		</template>

		<template #badge>
			<div
				v-if="supportsComparison"
				class="comparison-state-button"
				@click.stop>
				<MediaComparisonStateButton
					:comparison-state="getPlexMediaComparisonState(mediaItem)"
					:media-type="mediaType"
					show-tooltip
					dense
					:clickable="comparisonBadgeClickable"
					:cy="`comparison-chip-${getPlexMediaComparisonState(mediaItem)}`"
					@click="openComparisonDetails" />
			</div>
		</template>

		<template
			v-if="mediaOverviewStore.getIsSorted"
			#sort>
			<QFileSize
				v-if="mediaOverviewStore.getActiveSort.field === SortType.MediaSize"
				:size="mediaItem.mediaSize"
				align="center" />
			<QDuration
				v-else-if="mediaOverviewStore.getActiveSort.field === SortType.Duration"
				:value="mediaItem.duration"
				align="center"
				short />
			<QDateTime
				v-else-if="mediaOverviewStore.getActiveSort.field === SortType.AddedAt"
				:text="mediaItem.addedAt"
				align="center"
				short-date />
			<QDateTime
				v-else-if="mediaOverviewStore.getActiveSort.field === SortType.UpdatedAt"
				:text="mediaItem.updatedAt ?? ''"
				align="center"
				short-date />
			<QText
				v-else-if="mediaOverviewStore.getActiveSort.field === SortType.Year"
				:value="mediaItem.year"
				align="center" />
		</template>

		<template #footer>
			<MediaQuality
				v-if="supportsMediaQuality"
				class="media-poster-quality-bar"
				:qualities="mediaItem.qualities"
				:clickable="mediaType !== PlexMediaType.MusicArtist"
				@download="onDownload" />
			<QLoadingOverlay :loading="loading" />
		</template>

		<template #menu>
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
		</template>
	</MediaPosterCard>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import type { Subscription } from 'rxjs';
import { PlexMediaComparisonState, PlexMediaType, SortType } from '@dto';
import type { DownloadMediaDTO, PlexMediaQualityDTO, PlexMediaSlimDTO } from '@dto';
import {
	useDialogStore,
	useMediaOverviewStore,
	useMediaStore,
	useServerStore,
	useSettingsStore,
	useLibraryStore,
} from '@store';
import { getPlexMediaComparisonState, toDownloadMedia } from '@composables';
import Convert from '@class/Convert';
import MediaPosterCard from './MediaPosterCard.vue';

defineOptions({ inheritAttrs: false });

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

const hasDetailsAction = computed(() => Convert.mediaTypeToDetailsPath(props.mediaItem) !== null);
const supportsComparison = computed(() => [PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.MusicArtist].includes(get(mediaType)));
const supportsMediaQuality = computed(() => [PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.MusicArtist, PlexMediaType.OtherVideos].includes(get(mediaType)));

const comparisonBadgeClickable = computed(() => {
	return [
		PlexMediaComparisonState.Missing,
		PlexMediaComparisonState.HigherQuality,
		PlexMediaComparisonState.Partial,
		PlexMediaComparisonState.PartialAndHigherQuality,
	].includes(getPlexMediaComparisonState(props.mediaItem));
});

function onDownload(mediaQualities: PlexMediaQualityDTO[]) {
	if (get(mediaType) === PlexMediaType.MusicArtist && mediaQualities.length > 0)
		return;

	const downloadCommand: DownloadMediaDTO = {
		...toDownloadMedia(props.mediaItem)[0]!,
		qualities: mediaQualities,
	};

	emit('download', [downloadCommand]);
}

function openMobileActions(event: MouseEvent) {
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
	if (isActive)
		loadThumbnail(props.mediaItem);
}, { immediate: true });

onUnmounted(() => {
	if (currentSubscription)
		currentSubscription.unsubscribe();
});
</script>

<style lang="scss">
.comparison-state-button {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 9999;
  pointer-events: auto;
  max-width: calc(100% - 16px);
}
</style>
