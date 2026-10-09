<template>
	<section data-cy="photo-media-list">
		<div class="photo-media-list__selection row items-center q-gutter-sm">
			<q-checkbox
				class="photo-media-list__checkbox"
				data-cy="photo-album-checkbox"
				:model-value="albumState"
				:disable="assets.length === 0"
				:aria-label="t('components.photo-media-list.select-album', { title: mediaItem.title })"
				@update:model-value="selectAlbum($event)" />
			<span
				role="status"
				data-cy="photo-selected-count">
				{{ t('components.photo-media-list.selected-count', { count: selectedCount }) }}
			</span>
			<q-btn
				flat
				class="photo-media-list__reset"
				data-cy="photo-reset-selection"
				:disable="!albumSelected && selectedCount === 0"
				:label="t('components.photo-media-list.reset-selection')"
				@click="resetSelection" />
		</div>
		<p
			v-if="assets.length === 0"
			role="status"
			class="q-pa-md"
			data-cy="photo-assets-empty">
			{{ t('components.photo-media-list.empty') }}
		</p>
		<q-list
			v-else
			separator>
			<q-item
				v-for="asset in assets"
				:key="asset.id"
				class="photo-media-list__asset"
				:data-cy="`photo-asset-${asset.id}`">
				<q-item-section side>
					<q-checkbox
						class="photo-media-list__checkbox"
						:data-cy="`photo-asset-checkbox-${asset.id}`"
						:model-value="albumSelected || selectedIds.includes(asset.id)"
						:aria-label="t('components.photo-media-list.select-asset', { title: asset.title })"
						@update:model-value="selectAsset(asset.id, $event)" />
				</q-item-section>
				<q-item-section side>
					<MediaPosterImage
						:media-item="asset"
						:thumb-width="72"
						:thumb-height="72"
						:actions="false" />
				</q-item-section>
				<q-item-section class="photo-media-list__metadata">
					<q-item-label class="text-weight-bold">
						{{ asset.title }}
					</q-item-label>
					<q-item-label>
						<QFileSize :size="asset.mediaSize" />
					</q-item-label>
					<div
						v-for="file in asset.mediaData"
						:key="file.id"
						class="photo-media-list__file">
						<span>{{ file.fileName }}</span>
						<QFileSize :size="file.size" />
						<QDuration
							v-if="file.duration > 0"
							:value="file.duration" />
						<span v-if="file.videoCodec">{{ file.videoCodec }}</span>
						<span v-if="file.audioCodec">{{ file.audioCodec }}</span>
					</div>
				</q-item-section>
			</q-item>
		</q-list>
	</section>
</template>

<script setup lang="ts">
import { sortBy } from 'lodash-es';
import { get, set } from '@vueuse/core';
import { PlexMediaType, type PlexMediaDTO } from '@dto';
import { useMediaOverviewStore } from '@store';
import { toDownloadMedia } from '@composables/conversion/download-actions.conversion';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';

const props = defineProps<{ mediaItem: PlexMediaDTO }>();
const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const albumSelected = ref(false);
const selectedIds = ref<number[]>([]);
const assets = computed(() => sortBy(props.mediaItem.children.filter((child) =>
	child.type === PlexMediaType.PhotoImage
	&& child.parentId === props.mediaItem.id
	&& child.plexLibraryId === props.mediaItem.plexLibraryId
	&& child.plexServerId === props.mediaItem.plexServerId), ['sortIndex', 'id']));
const selectedCount = computed(() => get(albumSelected) ? get(assets).length : get(selectedIds).length);
const albumState = computed((): boolean | null => {
	if (get(albumSelected) || (get(assets).length > 0 && get(selectedCount) === get(assets).length)) {
		return true;
	}
	return get(selectedCount) > 0 ? null : false;
});

function resetSelection() {
	set(albumSelected, false);
	set(selectedIds, []);
}

function selectAlbum(selected: boolean) {
	if (get(assets).length === 0) {
		resetSelection();
		return;
	}
	set(albumSelected, selected);
	set(selectedIds, []);
}

function selectAsset(id: number, selected: boolean) {
	const ids = get(albumSelected) ? get(assets).map((asset) => asset.id) : get(selectedIds);
	set(albumSelected, false);
	set(selectedIds, selected ? [...new Set([...ids, id])] : ids.filter((key) => key !== id));
}

watch(() => props.mediaItem, resetSelection, { flush: 'sync', deep: true });
watch([albumSelected, selectedCount], () => {
	mediaOverviewStore.$patch({ downloadButtonVisible: get(albumSelected) || get(selectedCount) > 0 });
}, { immediate: true, flush: 'sync' });

useMediaOverviewBarDownloadCommandBus().on(() => {
	const items = get(albumSelected)
		? [props.mediaItem]
		: get(assets).filter((asset) => get(selectedIds).includes(asset.id));
	if (items.length > 0) {
		sendMediaOverviewDownloadCommand(items.flatMap(toDownloadMedia));
	}
});
onBeforeUnmount(() => mediaOverviewStore.$patch({ downloadButtonVisible: false }));
</script>

<style scoped lang="scss">
.photo-media-list__checkbox,
.photo-media-list__reset {
  min-width: 44px;
  min-height: 44px;
}
.photo-media-list__metadata {
  min-width: 0;
  overflow-wrap: anywhere;
}
.photo-media-list__file {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.5rem;
}
.photo-media-list__file > span:first-child {
  flex-basis: 100%;
}
@media (max-width: 599px) {
  .photo-media-list__asset {
    padding: 8px 4px;
  }
  .photo-media-list__asset :deep(.q-item__section--side) {
    padding-right: 4px;
  }
}
</style>
