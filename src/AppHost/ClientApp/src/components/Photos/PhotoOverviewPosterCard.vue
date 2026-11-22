<template>
	<MediaPosterCard
		ref="visibilityTarget"
		:data-cy="itemCy"
		:data-photo-overview-index="item.index"
		data-photo-overview-focus="card"
		:title="title"
		:media-type="mediaType"
		:overlay="item.kind !== PhotoItemKind.Folder"
		tabindex="0"
		role="group"
		:aria-label="title"
		:fallback-icon="fallbackIcon"
		@click="emit('open', $event)">
		<template
			v-if="thumbnail"
			#image>
			<PhotoThumbnail
				:plex-server-id="thumbnail.plexServerId"
				:plex-api-rating-key="thumbnail.plexApiRatingKey"
				:plex-api-meta-data-key="thumbnail.plexApiMetaDataKey"
				:has-thumbnail="thumbnail.hasThumbnail"
				:title="title"
				:width="settingsStore.generalSettings.useLowQualityPosterImages ? 200 : 300"
				:height="settingsStore.generalSettings.useLowQualityPosterImages ? 300 : 450"
				:cy="`${itemCy}-thumbnail`" />
		</template>
		<template
			v-if="item.kind !== PhotoItemKind.Folder"
			#metadata="{ compact }">
			<div :class="['text-center', compact ? 'text-caption' : 'text-body2']">
				<div v-if="item.kind === PhotoItemKind.Album">
					{{ ownershipLabel }}
				</div>
				<div v-if="item.kind === PhotoItemKind.Asset">
					{{ locationLabel }}
				</div>
				<div v-if="item.kind === PhotoItemKind.Asset">
					{{ kindLabel }}
				</div>
			</div>
		</template>
		<template #actions="{ compact, activate }">
			<div :class="compact ? 'column' : 'media-poster--actions-inner media-poster--actions-around'">
				<BaseButton
					v-close-popup="compact"
					:class="{ 'media-poster-menu__item': compact }"
					icon="mdi-download"
					:size="compact ? 'md' : 'xl'"
					:label="compact ? t('general.commands.download') : undefined"
					flat
					:outline="false"
					:aria-label="t('general.commands.download')"
					@click.stop="emit('download')" />
				<BaseButton
					v-close-popup="compact"
					:class="{ 'media-poster-menu__item': compact }"
					icon="mdi-magnify"
					:size="compact ? 'md' : 'xl'"
					:label="compact ? t('general.commands.view') : undefined"
					flat
					:outline="false"
					:aria-label="t('general.commands.view')"
					@click.stop="activate" />
			</div>
		</template>
	</MediaPosterCard>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { PhotoAssetKind, PhotoItemKind, PlexMediaType } from '@dto';
import { useSettingsStore } from '@store';
import { usePhotoImagePreloadCandidate } from '@composables/usePhotoImagePreloadCandidate';
import type { PhotoOverviewItem } from '@/store/photoBrowsingStore';

const props = defineProps<{ item: PhotoOverviewItem }>();

const { t } = useI18n();
const settingsStore = useSettingsStore();
const visibilityTarget = useTemplateRef<{ $el: HTMLElement }>('visibilityTarget');
usePhotoImagePreloadCandidate(() => get(visibilityTarget)?.$el ?? null, () => props.item);

const title = computed(() => {
	const item = props.item;
	switch (item.kind) {
		case PhotoItemKind.Album: return item.album.title;
		case PhotoItemKind.Folder: return item.folder.title;
		case PhotoItemKind.Asset: return item.asset.title;
		default: return '';
	}
});
const mediaType = computed(() => props.item.kind === PhotoItemKind.Asset ? PlexMediaType.PhotoImage : PlexMediaType.PhotoAlbum);
const fallbackIcon = computed(() => {
	if (props.item.kind === PhotoItemKind.Folder) return 'mdi-folder-outline';
	if (props.item.kind === PhotoItemKind.Asset && props.item.asset.assetKind === PhotoAssetKind.VideoClip) return 'mdi-movie-open-outline';
	return props.item.kind === PhotoItemKind.Album ? 'mdi-image-multiple-outline' : 'mdi-image-outline';
});
const thumbnail = computed(() => {
	const item = props.item;
	switch (item.kind) {
		case PhotoItemKind.Album: return item.album;
		case PhotoItemKind.Asset: return item.asset;
		default: return null;
	}
});
const itemCy = computed(() => {
	if (props.item.kind === PhotoItemKind.Folder) return `photo-folder-${props.item.folder.id}`;
	if (props.item.kind === PhotoItemKind.Album) return `photo-album-${props.item.album.plexLibraryId}-${props.item.album.id}`;
	return `photo-asset-${props.item.asset.plexLibraryId}-${props.item.asset.id}`;
});
const ownershipLabel = computed(() => props.item.kind === PhotoItemKind.Album
	? t('components.photo-browser.ownership-label', { server: props.item.album.plexServerName, library: props.item.album.plexLibraryTitle })
	: '');
const locationLabel = computed(() => props.item.kind === PhotoItemKind.Asset
	? props.item.asset.relativeFolderPath || props.item.asset.sourceRootTitle || ''
	: '');
const kindLabel = computed(() => props.item.kind === PhotoItemKind.Asset && props.item.asset.assetKind === PhotoAssetKind.VideoClip
	? t('components.photo-browser.kind-clip')
	: t('components.photo-browser.kind-image'));

const emit = defineEmits<{
	(e: 'open', event: MouseEvent): void;
	(e: 'download'): void;
}>();
</script>
