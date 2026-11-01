<template>
	<q-list
		v-if="mediaRows.length > 0"
		data-cy="other-video-media-list">
		<q-item>
			<QRow align="center">
				<QCol cols="auto">
					<q-checkbox
						class="other-video-media-list-root-checkbox"
						:aria-label="`${t('general.commands.selection')} ${mediaItem.title}`"
						data-cy="other-video-root-checkbox"
						:model-value="rootSelected"
						@update:model-value="rootSetSelected($event)" />
				</QCol>
				<QCol class="q-ml-md">
					<QSubHeader bold>
						{{ mediaItem.title }}
					</QSubHeader>
				</QCol>
				<QCol
					v-if="selectedCount"
					cols="auto">
					<span
						class="text-weight-bold"
						data-cy="other-video-selected-count">
						{{ t('components.media-list.selected-count', { selectedCount }) }}
					</span>
				</QCol>
			</QRow>
		</q-item>
		<MediaQTable
			:rows="mediaRows"
			:selection="selection"
			:download-media-factory="toOtherVideoPartDownloadMedia"
			@selection="onSelection" />
	</q-list>
	<q-list v-else>
		<q-item>
			<q-item-section>
				<QText
					size="h4"
					align="center"
					data-cy="other-video-files-empty">
					{{ t('components.other-video-media-list.empty') }}
				</QText>
			</q-item-section>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { AudioQuality, PlexMediaType, type DownloadMediaDTO, type PlexMediaDTO, type PlexMediaQualityDTO, type PlexMediaSlimDTO } from '@dto';
import type { ISelection } from '@interfaces';
import { toDownloadMedia } from '@composables/conversion';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';

const props = defineProps<{ mediaItem: PlexMediaDTO }>();
const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const selection = ref<ISelection>({
	indexKey: props.mediaItem.id,
	keys: [],
	allSelected: false,
});

const mediaRows = computed((): PlexMediaSlimDTO[] => props.mediaItem.mediaData.map((mediaData) => ({
	id: mediaData.id,
	title: mediaData.fileName,
	searchTitle: mediaData.fileName,
	sortIndex: mediaData.plexApiPartId,
	year: props.mediaItem.year,
	duration: mediaData.duration,
	mediaSize: mediaData.size,
	childCount: 0,
	grandChildCount: 0,
	addedAt: props.mediaItem.addedAt,
	updatedAt: props.mediaItem.updatedAt,
	plexLibraryId: props.mediaItem.plexLibraryId,
	plexServerId: props.mediaItem.plexServerId,
	type: PlexMediaType.OtherVideos,
	hasThumb: false,
	plexApiRatingKey: props.mediaItem.plexApiRatingKey,
	plexApiMetaDataKey: props.mediaItem.plexApiMetaDataKey,
	comparisonId: props.mediaItem.comparisonId,
	qualities: [{
		dataId: mediaData.id,
		mediaDataType: PlexMediaType.OtherVideos,
		mediaId: props.mediaItem.id,
		videoQuality: mediaData.videoQuality,
		audioQuality: AudioQuality.None,
	} satisfies PlexMediaQualityDTO],
})));

const selectedCount = computed(() => get(selection).keys.length);
const rootSelected = computed((): boolean | null => get(selection).allSelected);

function rootSetSelected(value: boolean): void {
	set(selection, {
		indexKey: props.mediaItem.id,
		keys: value ? get(mediaRows).map((row) => row.id) : [],
		allSelected: value,
	});
}

function onSelection(payload: ISelection): void {
	set(selection, {
		indexKey: props.mediaItem.id,
		keys: payload.keys,
		allSelected: payload.allSelected,
	});
}

function toOtherVideoPartDownloadMedia(row: PlexMediaSlimDTO): DownloadMediaDTO[] {
	return toDownloadMedia(props.mediaItem).map((command) => ({
		...command,
		qualities: row.qualities,
	}));
}

watch(() => props.mediaItem, () => {
	set(selection, {
		indexKey: props.mediaItem.id,
		keys: [],
		allSelected: false,
	});
}, { flush: 'sync', deep: true });

watch(selectedCount, (count) => {
	mediaOverviewStore.$patch({ downloadButtonVisible: count > 0 });
}, { immediate: true });

useMediaOverviewBarDownloadCommandBus().on(() => {
	const selectedRows = get(mediaRows).filter((row) => get(selection).keys.includes(row.id));
	const downloadMedia = selectedRows.flatMap((row) => toOtherVideoPartDownloadMedia(row));
	if (downloadMedia.length > 0)
		sendMediaOverviewDownloadCommand(downloadMedia);
});

onBeforeUnmount(() => {
	mediaOverviewStore.$patch({ downloadButtonVisible: false });
});
</script>

<style lang="scss">
.other-video-media-list-root-checkbox {
	min-width: 44px;
	min-height: 44px;
}
</style>
