<template>
	<td
		ref="visibilityTarget"
		:data-cy="itemCy">
		<q-btn
			flat
			dense
			no-caps
			no-wrap
			align="left"
			padding="0"
			class="full-width"
			data-photo-overview-focus="title"
			:label="title"
			@click.stop="emit('open', $event)" />
	</td>
	<template v-if="item.kind === PhotoItemKind.Album">
		<td>{{ item.album.plexServerName }}</td>
		<td>{{ item.album.plexLibraryTitle }}</td>
		<td class="text-center">
			{{ item.album.assetCount }}
		</td>
		<td class="text-center">
			<QFileSize :size="item.album.assetBytes" />
		</td>
	</template>
	<template v-else-if="metadata">
		<td>{{ metadata.location }}</td>
		<td class="text-center">
			{{ metadata.kindLabel }}
		</td>
		<td class="text-center">
			<QFileSize :size="metadata.size" />
		</td>
	</template>
	<td class="text-center">
		<q-btn
			flat
			icon="mdi-download"
			:aria-label="t('general.commands.download')"
			@click.stop="emit('download')" />
	</td>
</template>

<script setup lang="ts">
import { PhotoAssetKind, PhotoItemKind } from '@dto';
import { usePhotoImagePreloadCandidate } from '@composables/usePhotoImagePreloadCandidate';
import type { PhotoOverviewItem } from '@/store/photoBrowsingStore';

const props = defineProps<{
	item: PhotoOverviewItem;
}>();

const { t } = useI18n();
const visibilityTarget = useTemplateRef<HTMLElement>('visibilityTarget');
usePhotoImagePreloadCandidate(visibilityTarget, () => props.item);

const title = computed(() => {
	const item = props.item;
	switch (item.kind) {
		case PhotoItemKind.Album: return item.album.title;
		case PhotoItemKind.Folder: return item.folder.title;
		case PhotoItemKind.Asset: return item.asset.title;
		default: return '';
	}
});
const itemCy = computed(() => {
	if (props.item.kind === PhotoItemKind.Folder) return `photo-folder-${props.item.folder.id}`;
	if (props.item.kind === PhotoItemKind.Album) return `photo-album-${props.item.album.plexLibraryId}-${props.item.album.id}`;
	return `photo-asset-${props.item.asset.plexLibraryId}-${props.item.asset.id}`;
});
const metadata = computed(() => {
	const item = props.item;
	if (item.kind === PhotoItemKind.Album) return null;
	if (item.kind === PhotoItemKind.Folder) {
		return {
			location: item.folder.relativePath,
			kindLabel: t('components.photo-browser.views.folders'),
			size: item.folder.recursiveAssetBytes,
		};
	}
	return {
		location: item.asset.relativeFolderPath || item.asset.sourceRootTitle || '',
		kindLabel: item.asset.assetKind === PhotoAssetKind.VideoClip
			? t('components.photo-browser.kind-clip')
			: t('components.photo-browser.kind-image'),
		size: item.asset.size,
	};
});

const emit = defineEmits<{
	(e: 'open', event: Event): void;
	(e: 'download'): void;
}>();
</script>
