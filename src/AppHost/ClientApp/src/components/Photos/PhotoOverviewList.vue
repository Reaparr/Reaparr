<template>
	<section
		ref="overviewRoot"
		class="photo-overview-list column no-wrap"
		data-cy="photo-overview-list"
		tabindex="-1"
		:aria-busy="props.loading || props.selectionLoading">
		<QLinearProgress
			v-if="props.loading && props.context !== 'folders'"
			indeterminate
			class="q-mb-sm" />
		<QBanner
			v-if="props.error"
			class="q-mb-sm"
			role="alert"
			data-cy="photo-overview-error">
			{{ t('components.photo-browser.load-error') }}
			<template #action>
				<q-btn
					flat
					:label="t('components.photo-browser.retry')"
					@click="emit('retry')" />
			</template>
		</QBanner>
		<QAlert
			v-if="!props.loading && !props.error && props.itemCount === 0"
			type="info"
			role="status"
			data-cy="photo-overview-empty">
			{{ emptyLabel }}
		</QAlert>
		<template v-else-if="props.itemCount > 0">
			<div
				v-if="isTableView"
				class="photo-overview-list__table"
				data-cy="photo-overview-table">
				<MediaOverviewTable
					ref="tableRef"
					:item-count="props.itemCount"
					:item-at="props.itemAt"
					:columns="tableColumns"
					:row-key="rowKey"
					:row-title="rowTitle"
					:request-range="props.requestRange"
					:range-context-key="props.rangeContextKey"
					:is-row-selected="isItemSelected"
					:all-selected="props.allSelected"
					:selected-count="props.selectedCount"
					:selection-loading="props.selectionLoading"
					:selection-error="props.selectionError"
					:initial-scroll-index="props.initialScrollIndex"
					:on-scroll-index="props.onScrollIndex"
					@select-all="emit('select-all', $event)"
					@row-selection="onRowSelection">
					<template #controlled-row="{ row }: { row: PhotoOverviewItem }">
						<PhotoOverviewTableRow
							:item="row"
							@open="onOpen(row, $event)"
							@download="emit('download', row)" />
					</template>
				</MediaOverviewTable>
			</div>
			<PosterTable
				v-else
				ref="posterRef"
				class="poster-table"
				:item-count="props.itemCount"
				:request-range="props.requestRange"
				:range-context-key="props.rangeContextKey"
				:initial-scroll-index="props.initialScrollIndex"
				:on-scroll-index="props.onScrollIndex"
				scroll-id="photo-overview-scroll">
				<template #item="{ index }">
					<PhotoOverviewPosterCard
						v-if="props.itemAt(index)"
						:item="props.itemAt(index)!"
						@open="onOpen(props.itemAt(index)!, $event)"
						@download="emit('download', props.itemAt(index)!)" />
					<div
						v-else
						class="media-poster-placeholder"
						aria-hidden="true">
						<div class="media-poster-placeholder__image" />
						<div class="media-poster-placeholder__quality" />
					</div>
				</template>
			</PosterTable>
		</template>
	</section>
</template>

<script setup lang="ts">
import type { Observable } from 'rxjs';
import { get } from '@vueuse/core';
import { useSettingsStore } from '@store';
import { PhotoItemKind, ViewMode } from '@dto';
import type { PhotoOverviewItem } from '@/store/photoBrowsingStore';

type FocusableViewport = {
	focusItem: (index: number) => Promise<boolean>;
};

const props = withDefaults(defineProps<{
	context: 'albums' | 'folders' | 'assets' | 'album' | 'search';
	itemCount: number;
	itemAt: (index: number) => PhotoOverviewItem | null;
	loading?: boolean;
	error?: boolean;
	requestRange?: (start: number, end: number) => Observable<void>;
	rangeContextKey?: string;
	isSelected?: (item: PhotoOverviewItem) => boolean;
	allSelected?: boolean;
	selectedCount?: number;
	selectionLoading?: boolean;
	selectionError?: boolean;
	initialScrollIndex?: number;
	onScrollIndex?: (index: number) => void;
}>(), {
	loading: false,
	error: false,
	requestRange: undefined,
	rangeContextKey: '',
	isSelected: undefined,
	allSelected: false,
	selectedCount: 0,
	selectionLoading: false,
	selectionError: false,
	initialScrollIndex: 0,
	onScrollIndex: undefined,
});

const overviewRoot = ref<HTMLElement | null>(null);
const tableRef = ref<FocusableViewport | null>(null);
const posterRef = ref<FocusableViewport | null>(null);

const { t } = useI18n();
const settingsStore = useSettingsStore();

const isTableView = computed(() => settingsStore.displaySettings.photoAlbumViewMode === ViewMode.Table);
const emptyLabel = computed(() => {
	switch (props.context) {
		case 'albums':
			return t('components.photo-browser.empty-albums');
		case 'folders':
			return t('components.photo-browser.empty-folders');
		default:
			return t('components.photo-browser.empty-assets');
	}
});
const tableColumns = computed(() => {
	if (props.context === 'albums') {
		return [
			{ name: 'title', label: t('components.media-list.columns.title'), field: 'title', align: 'left' as const },
			{ name: 'server', label: t('components.photo-browser.server'), field: 'plexServerName', align: 'left' as const },
			{ name: 'library', label: t('components.photo-browser.library'), field: 'plexLibraryTitle', align: 'left' as const },
			{ name: 'assetCount', label: t('components.photo-browser.views.assets'), field: 'assetCount', align: 'center' as const },
			{ name: 'assetBytes', label: t('components.photo-browser.size'), field: 'assetBytes', align: 'center' as const },
			{ name: 'actions', label: t('components.media-list.columns.actions'), field: 'id', align: 'center' as const },
		];
	}
	return [
		{ name: 'title', label: t('components.media-list.columns.title'), field: 'title', align: 'left' as const },
		{ name: 'location', label: t('components.photo-browser.location'), field: 'relativeFolderPath', align: 'left' as const },
		{ name: 'kind', label: t('components.photo-browser.asset-kind'), field: 'assetKind', align: 'center' as const },
		{ name: 'size', label: t('components.photo-browser.size'), field: 'size', align: 'center' as const },
		{ name: 'actions', label: t('components.media-list.columns.actions'), field: 'id', align: 'center' as const },
	];
});

function rowKey(item: PhotoOverviewItem): string {
	if (item.kind === PhotoItemKind.Album) return `album:${item.album.plexServerId}:${item.album.plexLibraryId}:${item.album.id}`;
	if (item.kind === PhotoItemKind.Folder) return `folder:${item.folder.plexServerId}:${item.folder.plexLibraryId}:${item.folder.id}`;
	return `asset:${item.asset.plexServerId}:${item.asset.plexLibraryId}:${item.asset.id}`;
}
function rowTitle(item: PhotoOverviewItem): string {
	if (item.kind === PhotoItemKind.Album) return item.album.title;
	if (item.kind === PhotoItemKind.Folder) return item.folder.title;
	return item.asset.title;
}
function isItemSelected(item: PhotoOverviewItem): boolean {
	return props.isSelected?.(item) ?? false;
}
function onRowSelection(payload: { row: PhotoOverviewItem; selected: boolean }): void {
	emit('select', { item: payload.row, selected: payload.selected });
}
function onOpen(item: PhotoOverviewItem, event: Event): void {
	emit('open', { item, event });
}
async function focusItem(index: number): Promise<boolean> {
	await nextTick();
	const viewport = get(isTableView) ? get(tableRef) : get(posterRef);
	return viewport?.focusItem?.(index) ?? false;
}
function focusFallback(): void {
	const root = get(overviewRoot);
	const target = root?.querySelector<HTMLElement>('[data-photo-overview-focus], [data-cy="photo-select-all"]');
	(target ?? root)?.focus({ preventScroll: true });
}

const emit = defineEmits<{
	(e: 'open', payload: { item: PhotoOverviewItem; event: Event }): void;
	(e: 'download', item: PhotoOverviewItem): void;
	(e: 'select', payload: { item: PhotoOverviewItem; selected: boolean }): void;
	(e: 'select-all', selected: boolean): void;
	(e: 'retry'): void;
}>();

defineExpose({ focusItem, focusFallback });
</script>

<style scoped lang="scss">
.photo-overview-list {
  flex: 1 1 auto;
  min-height: 0;
}

.photo-overview-list__table,
.photo-overview-list > :deep(.poster-table) {
  flex: 1 1 auto;
  min-height: 0;
}
</style>
