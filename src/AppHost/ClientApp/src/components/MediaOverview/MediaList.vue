<template>
	<q-list v-if="mediaItem?.children?.length ?? -1 > 0">
		<!--	Root item	-->
		<q-item>
			<QRow align="center">
				<QCol cols="auto">
					<q-checkbox
						class="media-list-checkbox"
						:aria-label="`${t('general.commands.selection')} ${mediaItem?.title ?? t('general.error.unknown')}`"
						data-cy="media-list-root-checkbox"
						:model-value="rootSelected"
						@update:model-value="rootSetSelected($event)" />
				</QCol>
				<QCol class="q-ml-md">
					<QSubHeader bold>
						{{ mediaItem?.title ?? t('general.error.unknown') }}
					</QSubHeader>
				</QCol>
				<!--	Total selected count	-->
				<QCol
					v-if="selectedCount"
					cols="auto">
					<span
						class="text-weight-bold"
						data-cy="media-list-root-total-selected-count">
						{{ t('components.media-list.selected-count', { selectedCount }) }}
					</span>
				</QCol>
			</QRow>
		</q-item>
		<!-- Season display -->
		<q-expansion-item
			v-for="(child, index) in mediaItem?.children ?? []"
			:key="child.id"
			:model-value="itemExpanded[index]"
			expand-separator
			hide-expand-icon
			:default-opened="defaultOpened"
			:group="defaultOpened ? undefined : 'media-list'"
			:label="child.title"
			@update:model-value="itemExpanded[index] = $event">
			<!-- Header	-->
			<template #header="{ expanded }">
				<QRow align="center">
					<QCol cols="auto">
						<q-checkbox
							class="media-list-checkbox"
							:aria-label="`${t('general.commands.selection')} ${child.title}`"
							:data-cy="`media-list-child-checkbox-${child.id}`"
							:model-value="isSelected(child.id)"
							@update:model-value="setSelected(child.id, child.children, $event)" />
					</QCol>
					<QCol class="q-ml-md">
						<QSubHeader bold>
							{{ child.title }}
							<span class="text-weight-light q-ml-md">
								{{ t('components.media-list.episode-count', { episodeCount: child.childCount }) }}
							</span>
						</QSubHeader>
					</QCol>
					<QCol
						v-if="getSelected(child.id)?.keys.length"
						cols="auto">
						<span
							class="text-weight-bold">
							{{
								t('components.media-list.selected-count', {
									selectedCount: getSelected(child.id)?.keys.length ?? -1,
								})
							}}
						</span>
					</QCol>
					<QCol cols="auto">
						<q-icon
							size="lg"
							:name="expanded ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
					</QCol>
				</QRow>
			</template>
			<!-- Body	-->
			<template #default>
				<MediaQTable
					v-if="useQTable"
					:rows="child.children"
					:selection="getSelected(child.id)"
					@selection="onSelection(child.id, $event)" />
				<MediaTable
					v-else
					:rows="child.children"
					:selection="getSelected(child.id)"
					row-key="id"
					disable-hover-click
					:disable-highlight="disableHighlight"
					:disable-intersection="disableIntersection"
					@selection="onSelection(child.id, $event)" />
			</template>
		</q-expansion-item>
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
import Log from 'consola';
import { get, set } from '@vueuse/core';
import { type DownloadMediaDTO, type PlexMediaDTO, type PlexMediaSlimDTO, PlexMediaType } from '@dto';
import type { ISelection } from '@interfaces';
import {
	useI18n,
	useMediaOverviewBarDownloadCommandBus,
	toDownloadMedia,
	sendMediaOverviewDownloadCommand,
	useMediaOverviewStore,
	useSettingsStore,
} from '#imports';

const mediaOverviewStore = useMediaOverviewStore();
const settingsStore = useSettingsStore();

const defaultOpened = ref(false);
const { t } = useI18n();
const props = withDefaults(
	defineProps<{
		mediaItem?: PlexMediaDTO | null;
		loading?: boolean;
		disableIntersection: boolean;
		disableHighlight: boolean;
		useQTable: boolean;
	}>(),
	{
		mediaItem: null,
		loading: false,
	},
);

const selected = ref<ISelection[]>([]);
const itemExpanded = ref<boolean[]>([]);

const rootSelected = computed((): boolean | null => {
	const allSelected = get(selected).map((x) => x.allSelected);
	if (allSelected.every((x) => x === true)) {
		return true;
	}

	if (allSelected.every((x) => x === false)) {
		return false;
	}

	return null;
});

// region Selection

const selectedCount = computed((): number => {
	return get(selected)?.reduce((acc, x) => (x.keys?.length ?? 0) + acc, 0);
});

function rootSetSelected(value: boolean) {
	for (const child of props.mediaItem?.children ?? []) {
		setSelected(child.id, child.children, value);
	}
}

function isSelected(id: number): boolean | null {
	if (get(selected).length === 0) {
		return false;
	}
	const result = get(selected).find((x) => x.indexKey === id);
	if (result === undefined) {
		return false;
	}
	return result.allSelected;
}

function getSelected(id: number): ISelection | null {
	if (get(selected).length === 0) {
		return null;
	}
	const result = get(selected).find((x) => x.indexKey === id);
	if (result === undefined) {
		return null;
	}
	return result;
}

function setSelected(id: number, children: PlexMediaSlimDTO[], value: boolean) {
	const selection = get(selected).find((x) => x.indexKey === id);
	if (selection) {
		if (value) {
			selection.allSelected = true;
			selection.keys = children.map((x) => x.id);
		} else {
			selection.allSelected = false;
			selection.keys = [];
		}
	}
}

function onSelection(id: number, payload: ISelection) {
	const list = get(selected) ?? [];
	const i = list.findIndex((x) => x.indexKey === id);
	if (i === -1) {
		list.push({ indexKey: id, keys: payload.keys, allSelected: payload.allSelected });
		return;
	}

	const item = list[i];
	if (!item)
		return;

	item.allSelected = payload.allSelected;
	item.keys = payload.keys;
}

// endregion

function expandAll() {
	defaultOpened.value = true;
}

// region EventBus
watch(selectedCount, () =>
	mediaOverviewStore.$patch({
		downloadButtonVisible: get(selectedCount) > 0,
	}),
);

useMediaOverviewBarDownloadCommandBus().on(() => {
	if (!props.mediaItem) {
		Log.error('No media item selected');
		return;
	}

	const mediaItem = props.mediaItem;

	// All selected thus entire TvShow
	if (rootSelected.value) {
		sendMediaOverviewDownloadCommand(toDownloadMedia(mediaItem));
		return;
	}

	const downloadMedia: DownloadMediaDTO[] = [];
	const seasonIds: number[] = [];
	const episodesIds: number[] = [];

	for (const selection of get(selected)) {
		if (selection.allSelected) {
			seasonIds.push(selection.indexKey);
		} else {
			episodesIds.push(...selection.keys.map((x) => +x));
		}
	}

	if (seasonIds.length > 0) {
		downloadMedia.push({
			type: PlexMediaType.Season,
			mediaIds: seasonIds,
			plexLibraryId: mediaItem.plexLibraryId,
			plexServerId: mediaItem.plexServerId,
			qualities: mediaItem.qualities,
			keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
		});
	}

	if (episodesIds.length > 0) {
		downloadMedia.push({
			type: PlexMediaType.Episode,
			mediaIds: episodesIds,
			plexLibraryId: mediaItem.plexLibraryId,
			plexServerId: mediaItem.plexServerId,
			qualities: mediaItem.qualities,
			keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
		});
	}

	sendMediaOverviewDownloadCommand(downloadMedia);
});
// endregion
onMounted(() => {
	const children = props.mediaItem?.children ?? [];
	if (children.length === 0) {
		return;
	}

	set(selected, children.map((x) => ({ indexKey: x.id, keys: [], allSelected: false })) ?? []);
	get(itemExpanded).fill(false, 0, children.length - 1);
});

defineExpose({
	expandAll,
});
</script>

<style lang="scss">
.media-list-checkbox {
	min-width: 44px;
	min-height: 44px;
}
</style>
