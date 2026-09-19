<template>
	<q-list v-if="mediaRows.length > 0">
		<q-item>
			<QRow align="center">
				<QCol cols="auto">
					<q-checkbox
						class="movie-media-list-root-checkbox"
						:aria-label="`${t('general.commands.selection')} ${mediaItem.title}`"
						data-cy="movie-media-list-root-checkbox"
						:model-value="rootSelected"
						@update:model-value="rootSetSelected($event)" />
				</QCol>
				<QCol class="q-ml-md">
					<QSubHeader bold>
						{{ mediaItem?.title ?? t('general.error.unknown') }}
					</QSubHeader>
				</QCol>
				<QCol
					v-if="selectedCount"
					cols="auto">
					<span
						class="text-weight-bold"
						data-cy="movie-media-list-root-total-selected-count">
						{{ t('components.media-list.selected-count', { selectedCount }) }}
					</span>
				</QCol>
			</QRow>
		</q-item>
		<MediaQTable
			:rows="mediaRows"
			:selection="selection"
			:download-media-factory="toMoviePartDownloadMedia"
			@selection="onSelection" />
	</q-list>
	<q-list v-else>
		<q-item>
			<q-item-section>
				<QText
					size="h4"
					align="center">
					{{ $t('components.media-list.no-media-found') }}
				</QText>
			</q-item-section>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import type { DownloadMediaDTO, PlexMediaDTO, PlexMediaQualityDTO, PlexMediaSlimDTO } from '@dto';
import { PlexMediaType } from '@dto';
import type { ISelection } from '@interfaces';
import { useMediaOverviewStore, useSettingsStore } from '@store';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';

const settingsStore = useSettingsStore();
const mediaOverviewStore = useMediaOverviewStore();
const { t } = useI18n();

const props = defineProps<{
	mediaItem: PlexMediaDTO;
}>();

const selection = ref<ISelection>({
	indexKey: props.mediaItem.id,
	keys: [],
	allSelected: false,
});

const mediaRows = computed((): PlexMediaSlimDTO[] => {
	return props.mediaItem.mediaData.map((mediaData) => ({
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
		type: PlexMediaType.Movie,
		hasThumb: false,
		plexApiRatingKey: props.mediaItem.plexApiRatingKey,
		plexApiMetaDataKey: props.mediaItem.plexApiMetaDataKey,
		comparisonId: props.mediaItem.comparisonId,
		qualities: [{
			dataId: mediaData.id,
			mediaDataType: PlexMediaType.Movie,
			mediaId: props.mediaItem.id,
			quality: mediaData.videoResolution,
		} satisfies PlexMediaQualityDTO,
		],
	}));
});

const selectedCount = computed((): number => get(selection).keys.length);

const rootSelected = computed((): boolean | null => get(selection).allSelected);

function rootSetSelected(value: boolean) {
	set(selection, {
		indexKey: props.mediaItem.id,
		keys: value ? get(mediaRows).map((x) => x.id) : [],
		allSelected: value,
	});
}

function onSelection(payload: ISelection) {
	set(selection, {
		indexKey: props.mediaItem.id,
		keys: payload.keys,
		allSelected: payload.allSelected,
	});
}

function toMoviePartDownloadMedia(row: PlexMediaSlimDTO): DownloadMediaDTO[] {
	return [
		{
			mediaIds: [props.mediaItem.id],
			type: PlexMediaType.Movie,
			plexServerId: props.mediaItem.plexServerId,
			plexLibraryId: props.mediaItem.plexLibraryId,
			qualities: row.qualities,
			keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
		},
	];
}

watch(selectedCount, () => {
	mediaOverviewStore.$patch({
		downloadButtonVisible: get(selectedCount) > 0,
	});
});

useMediaOverviewBarDownloadCommandBus().on(() => {
	const selectedRows = get(mediaRows).filter((row) => get(selection).keys.includes(row.id));
	const downloadMedia = selectedRows.flatMap((row) => toMoviePartDownloadMedia(row));
	if (downloadMedia.length === 0) {
		return;
	}

	sendMediaOverviewDownloadCommand(downloadMedia);
});
</script>

<style lang="scss">
.movie-media-list-root-checkbox {
  min-width: 44px;
  min-height: 44px;
}
</style>
