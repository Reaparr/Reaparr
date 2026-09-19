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
								v-if="detailMode"
								cols="auto">
								<q-btn
									flat
									icon="mdi-arrow-left"
									:aria-label="$t('general.commands.back')"
									size="xl"
									@click="$emit('action', 'back')" />
							</QCol>
						</Transition>
						<QCol cols="auto">
							<MediaOverviewBarHeader
								:library-id="libraryId"
								:detail-mode="detailMode"
								:media-detail-item="mediaDetailItem" />
						</QCol>
						<!-- Search Bar -->
						<QCol
							class="media-overview-bar__search"
							align-self="center">
							<MediaOverviewSearchBar :library-id="libraryId" />
						</QCol>
					</QRow>
				</q-toolbar-title>
				<div class="media-overview-bar__mobile-search">
					<MediaOverviewSearchBar :library-id="libraryId" />
				</div>
			</div>

			<div class="media-overview-bar__actions">
				<!-- Download button -->
				<VerticalButton
					v-if="mediaOverviewStore.showDownloadButton"
					:height="barHeight"
					:label="$t('general.commands.download')"
					:width="verticalButtonWidth"
					icon="mdi-download"
					cy="media-overview-bar-download-button"
					@click="downloadCommandBus.emit('download')" />

				<!--	Selection Dialog Button	-->
				<VerticalButton
					v-if="mediaOverviewStore.showSelectionButton"
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
					:color="mediaOverviewStore.getIsSorted ? 'positive' : undefined"
					cy="media-overview-sort-btn">
					<q-menu
						anchor="bottom left"
						auto-close
						self="top left">
						<q-list>
							<!--	Clear Sort	-->
							<q-item
								v-if="mediaOverviewStore.getIsSorted"
								clickable
								cy="sort-clear-btn"
								@click="useSubscription(mediaOverviewStore.clearSort().subscribe())">
								<q-item-section avatar>
									<q-icon name="mdi-sort-variant-remove" />
								</q-item-section>
								<q-item-section>{{ $t('general.sort.clear') }}</q-item-section>
							</q-item>
							<q-separator v-if="mediaOverviewStore.getIsSorted" />
							<!--	Sort options	-->
							<q-item
								v-for="option in mediaOverviewStore.getSortOptions()"
								:key="option.field"
								:data-cy="`sort-option-${option.field}-btn`"
								clickable
								style="min-width: 200px"
								@click="mediaOverviewStore.toggleSortMedia(option.field)">
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
					v-if="!mediaOverviewStore.allMediaMode && !detailMode"
					:height="barHeight"
					:label="$t('general.commands.refresh')"
					:width="verticalButtonWidth"
					cy="media-overview-refresh-library-btn"
					icon="mdi-refresh"
					@click="$emit('action', 'refresh-library');" />

				<!--	Media Options button	-->
				<VerticalButton
					v-if="mediaOverviewStore.allMediaMode && !detailMode"
					:height="barHeight"
					:label="$t('general.commands.media-options')"
					:width="verticalButtonWidth"
					cy="media-overview-options-btn"
					icon="mdi-tune"
					@click="$emit('action', 'media-options-dialog');" />

				<!--	View mode	-->
				<VerticalButton
					v-if="!detailMode"
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
							<q-item
								v-for="(viewOption, i) in viewOptions"
								:key="i"
								:data-cy="`view-mode-${viewOption.viewMode.toLowerCase()}-btn`"
								clickable
								style="min-width: 200px"
								@click="changeView(viewOption.viewMode)">
								<!-- View mode options -->
								<q-item-section avatar>
									<q-icon
										v-if="isSelected(viewOption.viewMode)"
										name="mdi-check" />
								</q-item-section>
								<!--	Is selected icon	-->
								<q-item-section> {{ viewOption.label }}</q-item-section>
							</q-item>
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
				aria-label="More actions"
				data-cy="media-overview-bar-mobile-menu">
				<q-menu
					class="media-overview-bar__mobile-menu-panel"
					anchor="bottom right"
					self="top right"
					:offset="[0, 8]">
					<q-list class="media-overview-bar__mobile-actions">
						<!-- Download button -->
						<q-item
							v-if="mediaOverviewStore.showDownloadButton"
							v-close-popup
							clickable
							data-cy="media-overview-bar-mobile-download-button"
							@click="downloadCommandBus.emit('download')">
							<q-item-section avatar>
								<q-icon name="mdi-download" />
							</q-item-section>
							<q-item-section>{{ $t('general.commands.download') }}</q-item-section>
						</q-item>

						<!-- Selection Dialog Button -->
						<q-item
							v-if="mediaOverviewStore.showSelectionButton"
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
										v-if="mediaOverviewStore.getIsSorted"
										v-close-popup
										clickable
										cy="sort-clear-btn"
										@click="useSubscription(mediaOverviewStore.clearSort().subscribe())">
										<q-item-section avatar>
											<q-icon name="mdi-sort-variant-remove" />
										</q-item-section>
										<q-item-section>{{ $t('general.sort.clear') }}</q-item-section>
									</q-item>
									<q-separator v-if="mediaOverviewStore.getIsSorted" />
									<q-item
										v-for="option in mediaOverviewStore.getSortOptions()"
										:key="option.field"
										v-close-popup
										:data-cy="`sort-option-${option.field}-btn`"
										clickable
										@click="mediaOverviewStore.toggleSortMedia(option.field)">
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
							v-if="!mediaOverviewStore.allMediaMode && !detailMode"
							v-close-popup
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
							v-if="mediaOverviewStore.allMediaMode && !detailMode"
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
							v-if="!detailMode"
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
									<q-item
										v-for="(viewOption, i) in viewOptions"
										:key="i"
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
import { ViewMode } from '@dto';
import { SortDirection } from '@enums';
import type { IMediaOverviewBarActions, IViewOptions } from '@interfaces';
import {
	useMediaOverviewBarDownloadCommandBus,
	useMediaOverviewStore,
	useSettingsStore,
} from '#imports';

const mediaOverviewStore = useMediaOverviewStore();
const downloadCommandBus = useMediaOverviewBarDownloadCommandBus();

const settingsStore = useSettingsStore();

withDefaults(defineProps<{
	libraryId?: number;
	detailMode?: boolean;
	mediaDetailItem?: PlexMediaDTO | null;
}>(), {
	libraryId: 0,
	detailMode: false,
});

defineEmits<{
	(e: 'action', payload: IMediaOverviewBarActions): void;
}>();

const $q = useQuasar();
const barHeight = computed(() => $q.screen.lt.md ? 72 : 85);
const verticalButtonWidth = computed(() => $q.screen.lt.md ? 96 : 120);

function isSelected(viewMode: ViewMode) {
	return mediaOverviewStore.getMediaViewMode === viewMode;
}

const activeSortIcon = computed((): string => {
	if (!mediaOverviewStore.getIsSorted) {
		return 'mdi-sort';
	}
	const sort = mediaOverviewStore.getActiveSort.sort;
	return sort === SortDirection.Asc ? 'mdi-sort-descending' : 'mdi-sort-ascending';
});

const viewOptions = computed((): IViewOptions[] => {
	return [
		{
			label: 'Poster View',
			viewMode: ViewMode.Poster,
		},
		{
			label: 'Table View',
			viewMode: ViewMode.Table,
		},
	];
});

function changeView(viewMode: ViewMode) {
	settingsStore.updateDisplayMode(mediaOverviewStore.getMediaType, viewMode);
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
