<template>
	<div class="media-overview-bar-container">
		<q-toolbar class="media-overview-bar">
			<div class="media-overview-bar__main">
				<!-- Title -->
				<q-toolbar-title class="media-overview-bar__title">
					<QRow
						align="center"
						justify="around">
						<Transition
							appear
							enter-active-class="animated fadeInLeft"
							leave-active-class="animated fadeOutLeft">
							<QCol
								v-if="detailMode || (photoMode && photoStore.mode === 'album')"
								cols="auto">
								<q-btn
									flat
									icon="mdi-arrow-left"
									:aria-label="$t('general.commands.back')"
									:data-cy="photoMode ? 'photo-album-back' : undefined"
									size="xl"
									@click="$emit('action', 'back')" />
							</QCol>
						</Transition>
						<QCol cols="auto">
							<MediaOverviewBarHeader
								:library-id="libraryId"
								:detail-mode="detailMode"
								:media-detail-item="mediaDetailItem"
								:photo-mode="photoMode"
								@family-change="emit('photo-family-change', $event)" />
						</QCol>
						<!-- Search Bar -->
						<QCol
							class="media-overview-bar__search"
							align-self="center">
							<MediaOverviewSearchBar
								:library-id="libraryId"
								:photo-mode="photoMode"
								@photo-search="emit('photo-search', $event)" />
						</QCol>
					</QRow>
				</q-toolbar-title>
				<div class="media-overview-bar__mobile-search">
					<MediaOverviewSearchBar
						:library-id="libraryId"
						:photo-mode="photoMode"
						@photo-search="emit('photo-search', $event)" />
				</div>
			</div>

			<div class="media-overview-bar__actions">
				<!-- Download button -->
				<VerticalButton
					v-if="showDownloadButton"
					:height="barHeight"
					:label="$t('general.commands.download')"
					:width="verticalButtonWidth"
					icon="mdi-download"
					cy="media-overview-bar-download-button"
					@click="download()" />

				<!-- Photo selection clear button -->
				<VerticalButton
					v-if="showClearSelectionButton"
					:height="barHeight"
					:label="$t('components.photo-browser.clear-selection')"
					:width="verticalButtonWidth"
					icon="mdi-close-circle-outline"
					cy="media-overview-bar-clear-selection-button"
					@click="emit('photo-clear-selection')" />

				<!--	Selection Dialog Button	-->
				<VerticalButton
					v-if="showSelectionButton"
					:height="barHeight"
					:label="$t('general.commands.selection')"
					:width="verticalButtonWidth"
					icon="mdi-select-marker"
					@click="$emit('action', 'selection-dialog')" />

				<!--	Sort button	-->
				<VerticalButton
					v-if="!detailMode"
					:height="barHeight"
					:icon="activeSortIcon"
					:label="$t('general.commands.sort')"
					:width="verticalButtonWidth"
					:color="isSorted ? 'positive' : undefined"
					cy="media-overview-sort-btn">
					<q-menu
						anchor="bottom left"
						auto-close
						self="top left">
						<q-list>
							<!--	Clear Sort	-->
							<q-item
								v-if="isSorted"
								clickable
								data-cy="sort-clear-btn"
								@click="clearSort()">
								<q-item-section avatar>
									<q-icon name="mdi-sort-variant-remove" />
								</q-item-section>
								<q-item-section>{{ $t('general.sort.clear') }}</q-item-section>
							</q-item>
							<q-separator v-if="isSorted" />
							<!--	Sort options	-->
							<q-item
								v-for="option in sortOptions"
								:key="option.field"
								:data-cy="`sort-option-${option.field}-btn`"
								clickable
								style="min-width: 200px"
								@click="changeSort(option.field)">
								<q-item-section avatar>
									<q-icon
										v-if="option.direction !== SortDirection.NoSort"
										:name="option.direction === SortDirection.Asc ? 'mdi-arrow-up' : 'mdi-arrow-down'" />
								</q-item-section>
								<q-item-section>{{ option.label }}</q-item-section>
							</q-item>
						</q-list>
					</q-menu>
				</VerticalButton>

				<!--	Refresh library button	-->
				<VerticalButton
					v-if="showRefreshButton"
					:height="barHeight"
					:label="$t('general.commands.refresh')"
					:width="verticalButtonWidth"
					cy="media-overview-refresh-library-btn"
					icon="mdi-refresh"
					:loading="photoMode && photoStore.isRefreshing"
					:disabled="refreshDisabled"
					@click="$emit('action', 'refresh-library')" />

				<!--	Media Options button	-->
				<VerticalButton
					v-if="showMediaOptionsButton"
					:height="barHeight"
					:label="$t('general.commands.media-options')"
					:width="verticalButtonWidth"
					cy="media-overview-options-btn"
					icon="mdi-tune"
					@click="$emit('action', 'media-options-dialog')" />

				<!--	View mode	-->
				<VerticalButton
					v-if="!detailMode && supportsViewMode"
					:height="barHeight"
					:label="$t('general.commands.view')"
					:width="verticalButtonWidth"
					cy="change-view-mode-btn"
					icon="mdi-eye">
					<q-menu
						anchor="bottom left"
						auto-close
						self="top left">
						<q-list>
							<template
								v-for="viewOption in viewOptions"
								:key="viewOption.viewMode">
								<q-separator v-if="photoMode && libraryId > 0 && viewOption.viewMode === ViewMode.Poster" />
								<q-item
									:data-cy="`view-mode-${viewOption.viewMode.toLowerCase()}-btn`"
									clickable
									style="min-width: 200px"
									@click="changeView(viewOption.viewMode)">
									<q-item-section avatar>
										<q-icon
											v-if="isSelected(viewOption.viewMode)"
											name="mdi-check" />
									</q-item-section>
									<q-item-section>{{ viewOption.label }}</q-item-section>
								</q-item>
							</template>
						</q-list>
					</q-menu>
				</VerticalButton>
			</div>

			<q-btn
				class="media-overview-bar__mobile-menu-trigger"
				flat
				round
				dense
				icon="mdi-dots-vertical"
				:aria-label="$t('components.app-bar.more-actions')"
				data-cy="media-overview-bar-mobile-menu">
				<q-menu
					class="media-overview-bar__mobile-menu-panel"
					anchor="bottom right"
					self="top right"
					:offset="[0, 8]">
					<q-list class="media-overview-bar__mobile-actions">
						<!-- Download button -->
						<q-item
							v-if="showDownloadButton"
							v-close-popup
							clickable
							data-cy="media-overview-bar-mobile-download-button"
							@click="download()">
							<q-item-section avatar>
								<q-icon name="mdi-download" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.download') }}</q-item-section>
						</q-item>
						<!-- Photo selection clear button -->
						<q-item
							v-if="showClearSelectionButton"
							v-close-popup
							clickable
							data-cy="media-overview-bar-mobile-clear-selection-button"
							@click="emit('photo-clear-selection')">
							<q-item-section avatar>
								<q-icon name="mdi-close-circle-outline" />
							</q-item-section>
							<q-item-section>{{ $t('components.photo-browser.clear-selection') }}</q-item-section>
						</q-item>

						<!-- Selection Dialog Button -->
						<q-item
							v-if="showSelectionButton"
							v-close-popup
							clickable
							data-cy="media-overview-bar-mobile-selection-button"
							@click="$emit('action', 'selection-dialog')">
							<q-item-section avatar>
								<q-icon name="mdi-select-marker" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.selection') }}</q-item-section>
						</q-item>

						<!-- Sort button -->
						<q-item
							v-if="!detailMode"
							clickable
							data-cy="media-overview-bar-mobile-sort-button">
							<q-item-section avatar>
								<q-icon :name="activeSortIcon" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.sort') }}</q-item-section>
							<q-item-section side>
								<q-icon name="mdi-chevron-right" />
							</q-item-section>
							<q-menu
								anchor="top right"
								self="top right">
								<q-list style="min-width: 220px">
									<q-item
										v-if="isSorted"
										v-close-popup
										clickable
										data-cy="sort-clear-btn"
										@click="clearSort()">
										<q-item-section avatar>
											<q-icon name="mdi-sort-variant-remove" />
										</q-item-section>
										<q-item-section>{{ $t('general.sort.clear') }}</q-item-section>
									</q-item>
									<q-separator v-if="isSorted" />
									<q-item
										v-for="option in sortOptions"
										:key="option.field"
										v-close-popup
										:data-cy="`sort-option-${option.field}-btn`"
										clickable
										@click="changeSort(option.field)">
										<q-item-section avatar>
											<q-icon
												v-if="option.direction !== SortDirection.NoSort"
												:name="option.direction === SortDirection.Asc ? 'mdi-arrow-up' : 'mdi-arrow-down'" />
										</q-item-section>
										<q-item-section>{{ option.label }}</q-item-section>
									</q-item>
								</q-list>
							</q-menu>
						</q-item>

						<!-- Refresh library button -->
						<q-item
							v-if="showRefreshButton"
							v-close-popup
							:disable="refreshDisabled"
							clickable
							data-cy="media-overview-bar-mobile-refresh-button"
							@click="$emit('action', 'refresh-library')">
							<q-item-section avatar>
								<q-icon name="mdi-refresh" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.refresh') }}</q-item-section>
						</q-item>

						<!-- Media Options button -->
						<q-item
							v-if="showMediaOptionsButton"
							v-close-popup
							clickable
							data-cy="media-overview-bar-mobile-options-button"
							@click="$emit('action', 'media-options-dialog')">
							<q-item-section avatar>
								<q-icon name="mdi-tune" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.media-options') }}</q-item-section>
						</q-item>

						<!-- View mode -->
						<q-item
							v-if="!detailMode && supportsViewMode"
							clickable
							data-cy="change-view-mode-btn">
							<q-item-section avatar>
								<q-icon name="mdi-eye" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.view') }}</q-item-section>
							<q-item-section side>
								<q-icon name="mdi-chevron-right" />
							</q-item-section>
							<q-menu
								anchor="top right"
								self="top right">
								<q-list style="min-width: 220px">
									<template
										v-for="viewOption in viewOptions"
										:key="viewOption.viewMode">
										<q-separator v-if="photoMode && libraryId > 0 && viewOption.viewMode === ViewMode.Poster" />
										<q-item
											v-close-popup
											:data-cy="`view-mode-${viewOption.viewMode.toLowerCase()}-btn`"
											clickable
											@click="changeView(viewOption.viewMode)">
											<q-item-section avatar>
												<q-icon
													v-if="isSelected(viewOption.viewMode)"
													name="mdi-check" />
											</q-item-section>
											<q-item-section>{{ viewOption.label }}</q-item-section>
										</q-item>
									</template>
								</q-list>
							</q-menu>
						</q-item>
					</q-list>
				</q-menu>
			</q-btn>
		</q-toolbar>
	</div>
</template>

<script lang="ts" setup>
import type { PlexMediaDTO } from '@dto';
import { PlexMediaType, SortDirection, SortType, ViewMode } from '@dto';
import type { IMediaOverviewBarActions, ISortOption } from '@interfaces';
import type { IMediaOverviewSort } from '@composables/event-bus';
import { createDefaultMediaOverviewSort, toggleMediaOverviewSort } from '@composables';
import { useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';
import { useLibraryStore, useMediaOverviewStore, usePhotoBrowsingStore, useSettingsStore } from '@store';
import type { PhotoBrowseView } from '@/store/photoBrowsingStore';
import { useSubscription } from '@vueuse/rxjs';
import { get } from '@vueuse/core';

type ViewOption = {
	label: string;
	viewMode: ViewMode | PhotoBrowseView;
};

const mediaOverviewStore = useMediaOverviewStore();
const libraryStore = useLibraryStore();
const photoStore = usePhotoBrowsingStore();
const downloadCommandBus = useMediaOverviewBarDownloadCommandBus();
const settingsStore = useSettingsStore();
const { t } = useI18n();

const props = withDefaults(defineProps<{
	libraryId?: number;
	detailMode?: boolean;
	mediaDetailItem?: PlexMediaDTO | null;
	refreshDisabled?: boolean;
	photoMode?: boolean;
}>(), {
	libraryId: 0,
	detailMode: false,
	refreshDisabled: false,
	photoMode: false,
});

const emit = defineEmits<{
	(e: 'action', payload: IMediaOverviewBarActions): void;
	(e: 'photo-view', value: PhotoBrowseView): void;
	(e: 'photo-search', value: string): void;
	(e: 'photo-sort', value: IMediaOverviewSort): void;
	(e: 'photo-download' | 'photo-clear-selection'): void;
	(e: 'photo-family-change', value: PlexMediaType): void;
}>();

const $q = useQuasar();
const barHeight = computed(() => $q.screen.lt.md ? 72 : 85);
const verticalButtonWidth = computed(() => $q.screen.lt.md ? 96 : 120);

const supportsViewMode = computed(() => props.photoMode || [
	PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.MusicArtist, PlexMediaType.PhotoAlbum, PlexMediaType.OtherVideos,
].includes(mediaOverviewStore.getMediaType));
const showDownloadButton = computed(() => props.photoMode
	? photoStore.selectedCount > 0
	: mediaOverviewStore.showDownloadButton);
const showClearSelectionButton = computed(() => props.photoMode && photoStore.selectedCount > 0);
const showSelectionButton = computed(() => !props.photoMode && mediaOverviewStore.showSelectionButton);
const showRefreshButton = computed(() => !props.detailMode && (props.photoMode ? props.libraryId > 0 : !mediaOverviewStore.allMediaMode));
const showMediaOptionsButton = computed(() => !props.photoMode && !props.detailMode && mediaOverviewStore.allMediaMode);
const isSorted = computed(() => {
	if (props.photoMode) {
		return photoStore.sort.field !== SortType.Title || photoStore.sort.sort === SortDirection.Desc;
	}
	return mediaOverviewStore.getIsSorted;
});

function isSelected(viewMode: ViewMode | PhotoBrowseView): boolean {
	if (viewMode === ViewMode.Poster || viewMode === ViewMode.Table) {
		return (props.photoMode
			? settingsStore.displaySettings.photoAlbumViewMode
			: mediaOverviewStore.getMediaViewMode) === viewMode;
	}
	return props.photoMode && photoStore.view === viewMode;
}

const activeSortIcon = computed((): string => {
	if (!get(isSorted)) {
		return 'mdi-sort';
	}
	const sort = props.photoMode ? photoStore.sort.sort : mediaOverviewStore.getActiveSort.sort;
	return sort === SortDirection.Asc ? 'mdi-sort-descending' : 'mdi-sort-ascending';
});

const sortOptions = computed((): ISortOption[] => mediaOverviewStore.getSortOptions(
	props.photoMode ? photoStore.supportedSorts : undefined,
	props.photoMode ? photoStore.sort : undefined,
));

const viewOptions = computed((): ViewOption[] => {
	const options: ViewOption[] = [
		{
			label: t('components.media-overview-bar.view-mode.poster'),
			viewMode: ViewMode.Poster,
		},
		{
			label: t('components.media-overview-bar.view-mode.table'),
			viewMode: ViewMode.Table,
		},
	];
	if (props.photoMode) {
		const contentOptions: ViewOption[] = [
			...(libraryStore.getLibrary(props.libraryId)?.photoAlbumCount === 0
				? []
				: [{ label: t('components.photo-browser.views.albums'), viewMode: 'albums' as const }]),
			...(props.libraryId > 0
				? [{ label: t('components.photo-browser.views.folders'), viewMode: 'folders' as const }]
				: []),
			{ label: t('components.photo-browser.views.assets'), viewMode: 'assets' },
		];
		return [...contentOptions, ...options];
	}
	return options;
});

function download(): void {
	if (props.photoMode) {
		emit('photo-download');
		return;
	}
	downloadCommandBus.emit('download');
}

function clearSort(): void {
	if (props.photoMode) {
		emit('photo-sort', createDefaultMediaOverviewSort());
		return;
	}
	useSubscription(mediaOverviewStore.clearSort().subscribe());
}

function changeSort(field: SortType): void {
	if (props.photoMode) {
		emit('photo-sort', toggleMediaOverviewSort(photoStore.sort, field));
		return;
	}
	mediaOverviewStore.toggleSortMedia(field);
}

function changeView(viewMode: ViewMode | PhotoBrowseView): void {
	if (viewMode === ViewMode.Poster || viewMode === ViewMode.Table) {
		settingsStore.updateDisplayMode(props.photoMode ? PlexMediaType.PhotoAlbum : mediaOverviewStore.getMediaType, viewMode);
		return;
	}
	if (props.photoMode) {
		emit('photo-view', viewMode);
	}
}
</script>

<style lang="scss">
@use '@/assets/scss/variables' as *;
@use '@/assets/scss/mixins';

.media-overview-bar {
  @extend .fade-out-border;
  height: $media-overview-bar-height;
  flex: 0 0 auto;
}

.media-overview-bar__main {
  display: flex;
  min-width: 0;
  flex: 1 1 auto;
}

.media-overview-bar__title {
  overflow: visible;
}

.media-overview-bar__actions {
  display: flex;
  flex: 0 0 auto;
  align-items: stretch;
}

.media-overview-bar__mobile-menu-trigger,
.media-overview-bar__mobile-search {
  display: none;
}

.q-fab__label {
  max-height: none;
}

.media-overview-bar-container {
  width: 100%;
  max-width: 100%;
  min-width: 0;
  container: media-overview-bar / inline-size;
}

.media-overview-bar__mobile-menu-panel {
  width: min(360px, calc(100vw - 16px));
  max-width: calc(100vw - 16px);
}

.media-overview-bar__mobile-actions {
  min-width: 220px;
}

.media-overview-bar__mobile-actions .q-item {
  min-height: 44px;
}

@mixin compact-media-overview-bar {
  .media-overview-bar-container > .media-overview-bar {
    width: 100%;
    min-width: 0;
    min-height: $media-overview-bar-height;
    height: auto;
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    padding: 0;
  }

  .media-overview-bar__main {
    display: flex;
    flex-direction: row;
    align-items: center;
    width: auto;
    min-width: 0;
    flex: 1 1 auto;
  }

  .media-overview-bar__search,
  .media-overview-bar__actions {
    display: none;
  }

  .media-overview-bar__title {
    flex: 0 0 auto;
    padding-inline: 0;
  }

  .media-overview-bar__mobile-search {
    display: block;
    flex: 1 1 auto;
    width: auto;
    padding: 0.25rem 0.25rem 0.5rem;
  }

  .media-overview-bar__mobile-menu-trigger {
    display: inline-flex;
    align-self: center;
    flex: 0 0 auto;
    min-width: 44px !important;
    min-height: 44px !important;
    margin-top: 0;
    margin-right: 0.25rem;
  }

  .media-overview-bar__title,
  .media-overview-bar__title > .row {
    height: auto;
  }

  .media-overview-bar__title > .row {
    flex-wrap: nowrap;
    gap: 0;
  }

  .media-overview-bar__title > .row > .col,
  .media-overview-bar__title > .row > [class*='col-'] {
    min-width: 0;
    max-width: 100%;
  }

  .media-overview-bar__title > .row > .col {
    flex: 1 1 auto;
    width: auto;
  }

  .media-overview-bar__title > .row > .col-auto {
    flex: 0 1 auto;
    width: auto;
  }
}

@media (max-width: $breakpoint-sm-max) {
  @include compact-media-overview-bar;
}

@container media-overview-bar (max-width: 800px) {
  @include compact-media-overview-bar;
}

@media (max-width: $breakpoint-xs-max) {
  .media-overview-bar__main {
    width: 100%;
  }
}
</style>
