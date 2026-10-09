<template>
	<section
		class="other-video-files"
		:aria-label="t('components.other-video-media-list.originals')">
		<div
			v-if="originals.length"
			class="other-video-files__selection q-pa-sm">
			<q-checkbox
				class="other-video-files__control"
				:model-value="rootSelected"
				:aria-label="t('components.other-video-media-list.select-video', { title: mediaItem.title })"
				data-cy="other-video-root-checkbox"
				@update:model-value="selectAll($event)" />
			<strong>{{ t('components.other-video-media-list.originals') }}</strong>
			<span data-cy="other-video-selected-count">{{ t('components.other-video-media-list.selected-count', { count: rootSelected ? 1 : 0 }) }}</span>
		</div>
		<q-list
			v-if="originals.length"
			bordered
			separator>
			<q-item
				v-for="original in originals"
				:key="original.id"
				class="other-video-files__item">
				<q-item-section>
					<div
						v-for="file in original.files"
						:key="file.id"
						class="other-video-files__file">
						<strong class="other-video-files__name">{{ file.fileName }}</strong>
						<div class="other-video-files__metadata">
							<QFileSize :size="file.size" />
							<QDuration
								v-if="file.duration > 0"
								:value="file.duration"
								short />
							<MediaVideoQuality
								v-if="file.videoResolution !== VideoQuality.Unknown"
								:quality="file.videoResolution" />
							<span v-if="file.videoCodec">{{ file.videoCodec }}</span>
							<span v-if="file.audioCodec">{{ file.audioCodec }}</span>
						</div>
					</div>
				</q-item-section>
			</q-item>
		</q-list>
		<p
			v-else
			role="status"
			data-cy="other-video-files-empty">
			{{ t('components.other-video-media-list.empty') }}
		</p>
	</section>
</template>

<script setup lang="ts">
import { computed, ref, watch, onBeforeUnmount } from 'vue';
import { PlexMediaType, VideoQuality, type PlexMediaDTO, type PlexMediaDataDTO, type DownloadMediaDTO } from '@dto';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';

const props = defineProps<{ mediaItem: PlexMediaDTO }>();
const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const settingsStore = useSettingsStore();
const rootSelected = ref(false);
const originals = computed(() => {
	const groups = new Map<number, { id: number; files: PlexMediaDataDTO[] }>();
	for (const file of props.mediaItem.mediaData) {
		const existing = groups.get(file.plexApiMediaId);
		if (existing)
			existing.files.push(file);
		else
			groups.set(file.plexApiMediaId, { id: file.id, files: [file] });
	}
	return Array.from(groups.values());
});

function selectAll(value: boolean): void {
	rootSelected.value = value;
}
watch(() => props.mediaItem, () => {
	rootSelected.value = false;
});
watch(rootSelected, (selected) => {
	mediaOverviewStore.downloadButtonVisible = selected;
}, { immediate: true });
useMediaOverviewBarDownloadCommandBus().on(() => {
	if (!rootSelected.value)
		return;
	const command: DownloadMediaDTO = {
		type: PlexMediaType.OtherVideos,
		mediaIds: [props.mediaItem.id],
		plexLibraryId: props.mediaItem.plexLibraryId,
		plexServerId: props.mediaItem.plexServerId,
		keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
		qualities: [],
	};
	sendMediaOverviewDownloadCommand([command]);
});
onBeforeUnmount(() => {
	mediaOverviewStore.downloadButtonVisible = false;
});
</script>

<style scoped lang="scss">
.other-video-files__selection,
.other-video-files__metadata {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}
.other-video-files__control {
  min-width: 44px;
  min-height: 44px;
}
.other-video-files__item .q-item__section--main {
  min-width: 0;
}
.other-video-files__name {
  overflow-wrap: anywhere;
}
.other-video-files__file + .other-video-files__file {
  margin-top: 0.75rem;
}
</style>
