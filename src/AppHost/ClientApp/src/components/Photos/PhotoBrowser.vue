<template>
	<MediaOverviewRefresh
		v-if="isProcessing"
		:library-id="props.libraryId"
		mode="processing" />
	<MediaOverviewRefresh
		v-else-if="showQueuedPanel"
		:library-id="props.libraryId"
		mode="queued"
		variant="panel" />
	<MediaOverviewLayout
		v-else
		class="photo-browser"
		data-cy="photo-browser"
		:aria-busy="photoStore.isRefreshing">
		<template
			v-if="validRoute"
			#header>
			<MediaOverviewBar
				:library-id="props.libraryId"
				:refresh-disabled="refreshDisabled"
				photo-mode
				@action="handleOverviewAction"
				@photo-view="changeView"
				@photo-search="changeSearch"
				@photo-sort="changeSort"
				@photo-download="downloadSelection"
				@photo-clear-selection="photoStore.clearSelection"
				@photo-family-change="changeFamily" />
			<MediaOverviewRefresh
				v-if="isQueued"
				:library-id="props.libraryId"
				mode="queued"
				variant="banner" />
		</template>
		<QAlert
			v-if="!validRoute"
			type="error"
			role="alert"
			data-cy="photo-invalid-route">
			{{ t('components.photo-details.invalid-route') }}
		</QAlert>
		<template v-else>
			<QAlert
				v-if="photoStore.selectionStale"
				type="warning"
				role="status"
				data-cy="photo-stale-selection">
				{{ t('components.photo-browser.stale-selection') }}
			</QAlert>
			<QAlert
				v-if="neverSynced && !isProcessing && !isQueued"
				type="info"
				role="status"
				data-cy="photo-never-synced">
				{{ t('components.photo-browser.never-synced') }}
			</QAlert>
			<QBanner
				v-if="photoStore.reloadError"
				class="q-mb-sm"
				role="status"
				aria-live="polite"
				data-cy="photo-reload-error">
				{{ t('components.photo-browser.reload-error') }}
				<template #action>
					<q-btn
						flat
						:label="t('components.photo-browser.retry')"
						@click="retry" />
				</template>
			</QBanner>
			<QAlert
				v-if="photoStore.disabledLibrary"
				type="warning"
				role="alert"
				data-cy="photo-disabled-library">
				{{ t('components.photo-browser.disabled-library') }}
			</QAlert>
			<QAlert
				v-else-if="photoStore.error && !photoStore.hasAnyData"
				type="error"
				role="alert"
				data-cy="photo-browser-error">
				{{ t('components.photo-browser.load-error') }}
				<q-btn
					flat
					:label="t('components.photo-browser.retry')"
					@click="retry" />
			</QAlert>
			<nav
				v-if="photoStore.view === 'albums' || photoStore.mode === 'album'"
				aria-label="Photo album breadcrumbs"
				class="photo-browser__breadcrumbs"
				data-cy="photo-album-breadcrumbs">
				<QBreadcrumbs>
					<QBreadcrumbsEl
						:label="t('pages.photos.index.header')"
						:to="props.libraryId > 0 ? '/photos' : undefined" />
					<QBreadcrumbsEl
						v-if="props.libraryId > 0"
						:label="libraryStore.getLibraryName(props.libraryId)"
						:to="props.albumId ? `/photos/${props.libraryId}` : undefined" />
				</QBreadcrumbs>
			</nav>
			<nav
				v-if="showFolderBreadcrumbs"
				aria-label="Photo folder breadcrumbs"
				class="photo-browser__breadcrumbs"
				data-cy="photo-folder-breadcrumbs">
				<QBreadcrumbs>
					<QBreadcrumbsEl
						:label="t('components.photo-browser.folder-roots')"
						:to="folderLocation(null)" />
					<QBreadcrumbsEl
						v-for="(breadcrumb, index) in photoStore.breadcrumbs"
						:key="`${breadcrumb.id}-${index}`"
						:label="breadcrumb.title"
						:to="index < photoStore.breadcrumbs.length - 1 ? folderLocation(breadcrumb.id) : undefined" />
				</QBreadcrumbs>
			</nav>
			<PhotoOverviewList
				ref="overviewListRef"
				:context="overviewContext"
				:item-count="photoStore.overviewCount"
				:item-at="photoStore.overviewItemAt"
				:loading="photoStore.isRefreshing"
				:error="false"
				:request-range="photoStore.requestRange"
				:range-context-key="rangeContextKey"
				:is-selected="isOverviewItemSelected"
				:all-selected="photoStore.allVisibleSelected"
				:selected-count="photoStore.selectedCount"
				:selection-loading="photoStore.selectionLoading"
				:selection-error="photoStore.selectionError"
				:initial-scroll-index="restoreScrollIndex"
				:on-scroll-index="photoStore.setScrollIndex"
				@open="openOverviewItem"
				@download="downloadOverviewItem"
				@select="selectOverviewItem"
				@select-all="selectAll"
				@retry="retry" />
			<QLoadingOverlay :loading="photoStore.loading && !photoStore.hasAnyData" />
			<PhotoAssetDialog @download="downloadOverviewItem" />
			<DownloadConfirmation @download="downloadStore.downloadMedia($event)" />
			<RefreshModeDialog
				v-if="props.libraryId > 0"
				:library-name="libraryStore.getLibraryName(props.libraryId)"
				@select="refreshLibrary" />
		</template>
	</MediaOverviewLayout>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { DialogType } from '@enums';
import { LibrarySyncJobStatus, PhotoItemKind, PlexMediaType, SortType, type DownloadMediaDTO, type PhotoAlbumBrowseDTO, type PhotoAssetBrowseDTO, type PhotoFolderBrowseDTO } from '@dto';
import type { IMediaOverviewBarActions } from '@interfaces';
import type { IMediaOverviewSort } from '@composables/event-bus';
import { MEDIA_OVERVIEW_SORT_FIELDS, createDefaultMediaOverviewSort, parseFlexSortDsl, serializeFlexSortDsl } from '@composables';
import type { IPhotoBrowseActivation, IPhotoSelector, PhotoBrowseView, PhotoOverviewItem } from '@/store/photoBrowsingStore';
import { useDialogStore, useDownloadStore, useGlobalStore, useLibraryStore, usePhotoBrowsingStore, useSettingsStore } from '@store';

const props = withDefaults(defineProps<{
	libraryId?: number;
	albumId?: number;
}>(), {
	libraryId: 0,
	albumId: undefined,
});

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const photoStore = usePhotoBrowsingStore();
const libraryStore = useLibraryStore();
const dialogStore = useDialogStore();
const downloadStore = useDownloadStore();
const settingsStore = useSettingsStore();
const globalStore = useGlobalStore();
type FocusableOverviewList = {
	focusItem: (index: number) => Promise<boolean>;
	focusFallback: () => void;
};
const overviewListRef = ref<FocusableOverviewList | null>(null);
const ready = shallowRef(false);
const owner = {};
useSubscription(globalStore.getPageSetupReady.subscribe((value) => set(ready, value)));

const queue = computed(() => props.libraryId > 0 ? libraryStore.getLibrarySyncQueue(props.libraryId) : null);
const isProcessing = computed(() => get(queue)?.status === LibrarySyncJobStatus.Processing);
const isQueued = computed(() => get(queue)?.status === LibrarySyncJobStatus.Queued);
const refreshDisabled = computed(() => get(isProcessing) || get(isQueued));
const neverSynced = computed(() => props.libraryId > 0 && libraryStore.getLibrary(props.libraryId)?.syncedAt === null);
const showQueuedPanel = computed(() => get(isQueued)
	&& get(neverSynced)
	&& libraryStore.getLibrary(props.libraryId)?.isEnabled === true
	&& !photoStore.disabledLibrary);
const hasNoAlbums = computed(() => libraryStore.getLibrary(props.libraryId)?.photoAlbumCount === 0);

function scalarQuery(name: string): string | undefined {
	const value = route.query[name];
	return typeof value === 'string' ? value : undefined;
}

const parsedRoute = computed<IPhotoBrowseActivation | null>(() => {
	const viewValue = scalarQuery('view') ?? 'albums';
	const view = (['albums', 'folders', 'assets'] as const).find((choice) => choice === viewValue);
	const query = route.query.q;
	if (!view || (query !== undefined && typeof query !== 'string')) return null;
	const searchQuery = query ?? '';
	const effectiveView: PhotoBrowseView = props.albumId ? 'assets' : view;
	const supportedSortFields: readonly SortType[] = effectiveView === 'folders' && !searchQuery.trim()
		? [SortType.Title]
		: MEDIA_OVERVIEW_SORT_FIELDS;
	const sort = parseFlexSortDsl(scalarQuery('sort'), supportedSortFields);
	if (!sort) return null;
	const folderValue = scalarQuery('folderId');
	let folderId: number | null = null;
	if (folderValue !== undefined) {
		if (!/^[1-9]\d*$/.test(folderValue)) return null;
		folderId = Number(folderValue);
		if (!Number.isSafeInteger(folderId)) return null;
	}

	const libraryId = props.libraryId ?? 0;
	if (libraryId <= 0 && effectiveView === 'folders') return null;
	let mode: IPhotoBrowseActivation['mode'] = 'aggregate';
	if (props.albumId) mode = 'album';
	else if (libraryId > 0) mode = 'library';
	return {
		mode,
		libraryId,
		albumId: props.albumId,
		view: effectiveView,
		folderId,
		searchQuery,
		sort,
	};
});
const validRoute = computed(() => get(parsedRoute) !== null);
const shouldRedirectToFolders = computed(() => {
	const context = get(parsedRoute);
	return context?.mode === 'library' && context.view === 'albums' && get(hasNoAlbums);
});
const overviewContext = computed<'albums' | 'folders' | 'assets' | 'album' | 'search'>(() => {
	if (photoStore.searchQuery) return 'search';
	if (photoStore.mode === 'album') return 'album';
	return photoStore.view;
});
const rangeContextKey = computed(() => [
	photoStore.mode,
	photoStore.libraryId,
	photoStore.albumId ?? '',
	photoStore.folderId ?? '',
	photoStore.view,
	photoStore.searchQuery,
	serializeFlexSortDsl(photoStore.sort),
].join(':'));
const restoreScrollIndex = shallowRef(photoStore.scrollIndex);
const showFolderBreadcrumbs = computed(() => photoStore.mode === 'library' && photoStore.view === 'folders' && !photoStore.searchQuery && photoStore.breadcrumbs.length > 0);

function syncRoute(): void {
	const context = get(parsedRoute);
	const cleanQuery = cleanPhotoQuery();
	if (context && Object.keys(cleanQuery).length !== Object.keys(route.query).length)
		void router.replace({ query: cleanQuery }).catch(() => undefined);
	if (!get(ready) || !context) {
		photoStore.deactivate(owner);
		return;
	}
	if (get(shouldRedirectToFolders)) {
		updateQuery({
			view: 'folders',
			folderId: null,
			...(context.sort.field !== SortType.Title ? { sort: null } : {}),
		}, true);
		return;
	}
	const activation = photoStore.activate(context, owner);
	set(restoreScrollIndex, photoStore.scrollIndex);
	useSubscription(activation.subscribe());
}

function updateQuery(updates: Record<string, string | number | null | undefined>, replace = false): void {
	const query: Record<string, string | (string | null)[] | null | undefined> = cleanPhotoQuery();
	for (const [key, value] of Object.entries(updates)) {
		if (value === null || value === undefined || value === '') Reflect.deleteProperty(query, key);
		else query[key] = String(value);
	}
	const navigate = replace ? router.replace : router.push;
	void navigate({ query }).catch(() => undefined);
}

function cleanPhotoQuery(): Record<string, string | (string | null)[] | null | undefined> {
	const query: Record<string, string | (string | null)[] | null | undefined> = { ...route.query };
	for (const key of ['view', 'folderId', 'albumPage', 'assetPage', 'folderPage', 'searchPage', 'sort', 'q']) Reflect.deleteProperty(query, key);
	for (const key of ['view', 'folderId', 'sort', 'q']) {
		const value = route.query[key];
		if (value !== undefined) query[key] = value;
	}
	return query;
}

function changeView(view: PhotoBrowseView): void {
	const currentSort = get(parsedRoute)?.sort ?? photoStore.sort;
	const searchQuery = scalarQuery('q')?.trim() ?? '';
	const resetSort = view === 'folders' && !searchQuery && currentSort.field !== SortType.Title;
	updateQuery({
		view,
		folderId: view === 'folders' ? scalarQuery('folderId') : null,
		...(resetSort ? { sort: null } : {}),
	});
}

function changeSearch(value: string): void {
	const query = value.trim();
	const context = get(parsedRoute);
	const resetSort = !query && context?.view === 'folders' && context.sort.field !== SortType.Title;
	updateQuery({ q: query || null, ...(resetSort ? { sort: null } : {}) }, true);
}

function changeSort(sort: IMediaOverviewSort): void {
	const defaultSort = createDefaultMediaOverviewSort();
	updateQuery({ sort: sort.field === defaultSort.field && sort.sort === defaultSort.sort ? null : serializeFlexSortDsl(sort) });
}

function openFolder(folderId: number): void {
	void router.push(folderLocation(folderId)).catch(() => undefined);
}

function folderLocation(folderId: number | null) {
	return {
		path: route.path,
		query: { ...cleanPhotoQuery(), view: 'folders', folderId: folderId?.toString() },
	};
}

function openAlbum(album: { plexLibraryId: number; id: number }): void {
	void router.push(`/photos/${album.plexLibraryId}/details/${album.id}`);
}

function backToAlbums(): void {
	const previous = window.history.state?.back;
	const path = typeof previous === 'string' ? previous.split('?')[0] ?? '' : '';
	if (path === '/' || path === '/photos' || path.startsWith('/photos/')) {
		router.back();
		return;
	}
	void router.push({ path: `/photos/${props.libraryId}`, query: { view: 'albums' } });
}

async function changeFamily(type: PlexMediaType): Promise<void> {
	if (type === PlexMediaType.PhotoAlbum) return;
	photoStore.leavePhotos(owner);
	await router.replace({ query: cleanPhotoQuery() });
	settingsStore.displaySettings.allOverviewViewMode = type;
	if (route.path !== '/') await router.push({ path: '/', query: cleanPhotoQuery() });
}

function overviewSource(item: PhotoOverviewItem): PhotoAlbumBrowseDTO | PhotoFolderBrowseDTO | PhotoAssetBrowseDTO {
	switch (item.kind) {
		case PhotoItemKind.Album: return item.album;
		case PhotoItemKind.Folder: return item.folder;
		case PhotoItemKind.Asset: return item.asset;
	}
}

function isOverviewItemSelected(item: PhotoOverviewItem): boolean {
	const source = overviewSource(item);
	return photoStore.isSelected({ id: source.id, plexServerId: source.plexServerId, plexLibraryId: source.plexLibraryId, kind: item.kind });
}

function selectOverviewItem(payload: { item: PhotoOverviewItem; selected: boolean }): void {
	if (payload.item.kind === PhotoItemKind.Album) photoStore.toggleAlbum(payload.item.album, payload.selected);
	else if (payload.item.kind === PhotoItemKind.Folder) photoStore.toggleFolder(payload.item.folder, payload.selected);
	else photoStore.toggleAsset(payload.item.asset, payload.selected);
}

function openOverviewItem(payload: { item: PhotoOverviewItem; event: Event }): void {
	if (payload.item.kind === PhotoItemKind.Folder) {
		openFolder(payload.item.folder.id);
		return;
	}
	if (payload.item.kind === PhotoItemKind.Album) {
		openAlbum(payload.item.album);
		return;
	}
	useSubscription(photoStore.openAsset(payload.item.asset, payload.item.index).subscribe());
}

function selectorForItem(item: PhotoOverviewItem): IPhotoSelector {
	const source = overviewSource(item);
	const kind = item.kind;
	return {
		id: source.id,
		plexServerId: source.plexServerId,
		plexLibraryId: source.plexLibraryId,
		title: source.title,
		kind,
		type: kind === PhotoItemKind.Album ? PlexMediaType.PhotoAlbum : PlexMediaType.PhotoImage,
		stale: false,
	};
}

function downloadOverviewItem(item: PhotoOverviewItem): void {
	confirmDownload(photoStore.getDownloadSelectors([selectorForItem(item)]));
}

function downloadSelection(): void {
	confirmDownload(photoStore.getDownloadSelectors());
}

function confirmDownload(command: DownloadMediaDTO[]): void {
	if (command.length === 0) return;
	if (settingsStore.isConfirmationEnabled(command[0]?.type ?? PlexMediaType.PhotoImage)) {
		dialogStore.openMediaConfirmationDownloadDialog(command);
		return;
	}
	downloadStore.downloadMedia({ downloadMedias: command, customDestinationFolderPath: '', destinationFolderPathId: null });
}

function selectAll(selected: boolean): void {
	useSubscription(photoStore.selectAll(selected).subscribe());
}

function handleOverviewAction(action: IMediaOverviewBarActions): void {
	if (action === 'refresh-library') openRefreshDialog();
	else if (action === 'back') backToAlbums();
}

function openRefreshDialog(): void {
	if (get(refreshDisabled)) return;
	dialogStore.openDialog(DialogType.RefreshMediaDialog);
}

function refreshLibrary(forceMediaRefresh: boolean): void {
	if (props.libraryId <= 0 || get(refreshDisabled)) return;
	useSubscription(libraryStore.reSyncLibrary(props.libraryId, forceMediaRefresh).subscribe());
}

function retry(): void {
	useSubscription(photoStore.loadCurrent(true).subscribe());
}

watch(() => [get(ready), route.fullPath, props.libraryId, props.albumId, get(hasNoAlbums)], syncRoute, { immediate: true });
watch(() => settingsStore.displaySettings.photoAlbumViewMode, () => {
	set(restoreScrollIndex, photoStore.focusIndex ?? photoStore.scrollIndex);
});
let focusRestoreGeneration = 0;
watch(() => [photoStore.focusIndex, get(rangeContextKey)], ([index]) => {
	const generation = ++focusRestoreGeneration;
	if (typeof index !== 'number')
		return;
	void nextTick(async () => {
		const list = get(overviewListRef);
		if (!list || generation !== focusRestoreGeneration)
			return;
		const focused = await list.focusItem(index);
		if (generation !== focusRestoreGeneration)
			return;
		if (!focused)
			list.focusFallback();
		photoStore.acknowledgeFocus();
	});
});
onBeforeRouteLeave((to) => {
	const remainsInPhotos = to.path === '/photos' || to.path.startsWith('/photos/')
		|| (to.path === '/' && settingsStore.displaySettings.allOverviewViewMode === PlexMediaType.PhotoAlbum);
	if (!remainsInPhotos) photoStore.leavePhotos(owner);
});
onBeforeUnmount(() => photoStore.deactivate(owner));
</script>

<style scoped lang="scss">
.photo-browser :deep(.media-overview-content) {
  display: flex;
  flex-direction: column;
}

.photo-browser__breadcrumbs {
  flex: 0 0 auto;
  padding: 0.5rem 1rem;
}
</style>
