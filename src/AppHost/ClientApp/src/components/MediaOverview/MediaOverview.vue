<template>
	<!--	Refresh Library Screen	-->
	<MediaOverviewRefresh
		v-if="libraryStore.getIsLibrarySyncing(libraryId)"
		:library-id="libraryId"
		mode="processing" />
	<MediaOverviewRefresh
		v-else-if="showQueuedPanel"
		:library-id="libraryId"
		mode="queued"
		variant="panel" />
	<MediaOverviewLayout v-else>
		<template #header>
			<div class="media-overview-bar">
				<!--	Overview bar	-->
				<MediaOverviewBar
					:detail-mode="false"
					:library-id="libraryId"
					:refresh-disabled="isRefreshDisabled"
					@action="onAction" />
				<!-- Library Inaccessible Alert -->
				<q-banner
					v-if="isLibraryInaccessible"
					class="media-overview-inaccessible-banner bg-warning text-black"
					dense
					role="status"
					aria-live="polite"
					data-cy="media-overview-inaccessible-library-banner">
					<template #avatar>
						<q-icon
							name="mdi-alert"
							aria-hidden="true" />
					</template>
					<strong>{{ $t('components.media-overview.library-inaccessible-title') }}</strong>
					{{ $t('components.media-overview.library-inaccessible') }}
				</q-banner>
			</div>
			<QBanner
				v-if="refreshSubmitting && !isQueued"
				class="media-overview-refresh-requesting"
				dense
				role="status"
				aria-live="polite"
				data-cy="media-overview-refresh-requesting">
				<template #avatar>
					<q-icon
						name="mdi-refresh"
						aria-hidden="true" />
				</template>
				{{ t('components.media-overview.requesting-refresh') }}
			</QBanner>
			<MediaOverviewRefresh
				v-if="isQueued"
				:library-id="libraryId"
				mode="queued"
				variant="banner" />
		</template>
		<template #default>
			<template v-if="!mediaOverviewStore.loading">
				<QRow
					v-if="library && !library.isEnabled"
					class="q-mt-md"
					justify="center"
					gutter="md">
					<QCol cols="auto">
						<QAlert type="warning">
							<QRow
								align="center"
								gutter="md">
								<QCol>
									{{ t('components.media-overview.library-disabled', { library: library.title }) }}
								</QCol>
								<QCol cols="auto">
									<BaseButton
										color="positive"
										:label="t('components.media-overview.enable-library-button')"
										@click="openLibraryServerSettings" />
								</QCol>
							</QRow>
						</QAlert>
					</QCol>
				</QRow>
				<!-- Media Overview -->
				<template v-else-if="mediaOverviewStore.itemsLength && !mediaOverviewStore.hasNoSearchResults">
					<!--	Data table display	-->
					<QRow
						class="media-overview-results"
						align="start">
						<QCol>
							<template v-if="mediaOverviewStore.getMediaViewMode === ViewMode.Table">
								<MediaTable
									:disable-hover-click="mediaOverviewStore.getMediaType === PlexMediaType.Movie"
									is-scrollable />
							</template>

							<!-- Poster display -->
							<template v-else>
								<PosterTable
									:library-id="libraryId"
									:media-type="mediaOverviewStore.getMediaType" />
							</template>
						</QCol>
						<!-- Alphabet Navigation -->
						<AlphabetNavigation />
					</QRow>
				</template>
				<!-- No Media Overview - Error Messages -->
				<template v-else-if="mediaOverviewStore.mediaLoadError">
					<QRow
						class="q-mt-md"
						justify="center">
						<QCol cols="auto">
							<QAlert type="error">
								<div>{{ t('components.media-overview.media-load-failed') }}</div>
								<BaseButton
									class="q-mt-sm"
									:label="t('components.media-overview.retry-media-load')"
									@click="retryMediaLoad" />
							</QAlert>
						</QCol>
					</QRow>
				</template>
				<template v-else>
					<QRow
						class="q-mt-md"
						justify="center"
						gutter="md">
						<QCol cols="auto">
							<QAlert
								type="warning">
								<template v-if="mediaOverviewStore.allMediaMode">
									{{ t('components.media-overview.no-media-items-available') }}
								</template>
								<template v-else-if="mediaOverviewStore.hasNoSearchResults">
									{{ t('components.media-overview.no-search-results', { query: mediaOverviewStore.filterQuery }) }}
								</template>
								<template v-else-if="mediaOverviewStore.hasNoFilterResults">
									{{ t('components.media-overview.no-filter-results') }}
								</template>
								<template v-else-if="library?.syncedAt === null">
									{{ t('components.media-overview.library-not-yet-synced') }}
								</template>
								<template
									v-else-if="!mediaOverviewStore.itemsLength && !mediaOverviewStore.filterQuery && !mediaOverviewStore.hasActiveFilter">
									{{ t('components.media-overview.no-data') }}
								</template>
								<template v-else>
									{{ t('components.media-overview.could-not-display') }}
								</template>
							</QAlert>
						</QCol>
					</QRow>
				</template>
			</template>

			<!-- Media Selection Dialog -->
			<MediaSelectionDialog />
			<!-- Media Comparison Details Dialog -->
			<MediaComparisonDetailsDialog v-if="mediaOverviewStore.getSupportsComparison" />
			<!-- Media Options Dialog -->
			<MediaOptionsDialog @closed="onOptionsClosed" />
			<!-- Loading overlay -->
			<QLoadingOverlay :loading="!libraryStore.getIsLibrarySyncing(libraryId) && mediaOverviewStore.loading" />
			<!-- Download confirmation dialog	-->
			<DownloadConfirmation @download="downloadStore.downloadMedia($event)" />
			<!-- Library refresh mode dialog -->
			<RefreshModeDialog
				:library-name="libraryStore.getLibraryName(libraryId)"
				@select="refreshLibrary" />
		</template>
	</MediaOverviewLayout>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import { filter, finalize, switchMap, take } from 'rxjs';
import { DialogType } from '@enums';
import { LibrarySyncJobStatus, type DownloadMediaDTO, PlexMediaType, ViewMode } from '@dto';
import { toDownloadSelectionKind } from '@composables/conversion';
import type { IMediaOverviewBarActions } from '@interfaces';
import { useGlobalStore } from '@store';
import {
	listenMediaOverviewDownloadCommand,
	sendMediaOverviewDownloadCommand,
	useAccountStore,
	useDialogStore,
	useDownloadStore,
	useI18n,
	useLibraryStore,
	useMediaOverviewBarDownloadCommandBus,
	useMediaOverviewStore,
	useSettingsStore,
} from '#imports';

const { t } = useI18n();
const settingsStore = useSettingsStore();
const mediaOverviewStore = useMediaOverviewStore();
const downloadStore = useDownloadStore();
const libraryStore = useLibraryStore();
const dialogStore = useDialogStore();
const accountStore = useAccountStore();
const globalStore = useGlobalStore();

const props = defineProps<{
	libraryId: number;
}>();

const library = computed(() => libraryStore.getLibrary(props.libraryId));
const isLibraryInaccessible = computed(() => {
	if (props.libraryId <= 0) {
		return false;
	}

	const library = libraryStore.getLibrary(props.libraryId);
	return !library
		|| !accountStore.getHasAccountServerAccess(library.plexServerId)
		|| !accountStore.getHasAccountLibraryAccess(library.id);
});
const refreshSubmitting = ref(false);
const refreshAttemptActive = ref(false);
const syncQueue = computed(() => libraryStore.getLibrarySyncQueue(props.libraryId));
const syncQueueKey = computed(() => {
	const queue = libraryStore.getLibrarySyncQueue(props.libraryId);
	return queue ? `${queue.status}|${queue.createdAt}|${queue.completedAt ?? ''}` : '';
});
const isQueued = computed(() => get(syncQueue)?.status === LibrarySyncJobStatus.Queued);
const isRefreshDisabled = computed(() => {
	const status = get(syncQueue)?.status;
	return get(refreshSubmitting)
		|| status === LibrarySyncJobStatus.Queued
		|| status === LibrarySyncJobStatus.Processing;
});
const showQueuedPanel = computed(() => {
	const currentLibrary = get(library);
	return get(isQueued)
		&& currentLibrary?.isEnabled === true
		&& currentLibrary.syncedAt === null
		&& !get(isLibraryInaccessible);
});

function resetProgress() {
	libraryStore.updateLibraryProgress({
		plexLibraryId: mediaOverviewStore.libraryId,
		percentage: 0,
		received: 0,
		total: 0,
		isComplete: false,
		timeStamp: '',
		timeRemaining: '',
		items: [],
		errors: [],
	});
}

function refreshLibrary(forceMediaRefresh: boolean): void {
	if (get(isRefreshDisabled)) {
		return;
	}

	set(refreshSubmitting, true);
	set(refreshAttemptActive, true);
	resetProgress();
	useSubscription(
		libraryStore.reSyncLibrary(props.libraryId, forceMediaRefresh).pipe(
			finalize(() => {
				set(refreshSubmitting, false);
			}),
		).subscribe({
			next: (queue) => {
				if (!queue) {
					set(refreshAttemptActive, false);
				}
			},
			error: () => {
				set(refreshAttemptActive, false);
			},
		}),
	);
}

function openLibraryServerSettings(): void {
	useSubscription(
		libraryStore.setLibraryEnabled(props.libraryId, true).subscribe(),
	);
}

// region Eventbus

/**
 * Listen for process download command
 */
listenMediaOverviewDownloadCommand((command) => {
	Log.info('MediaOverview => Received download command', command);
	// Only show if there is more than 1 selection
	if (command.length > 0 && command.some((x) => x.mediaIds.length > 0)) {
		if (settingsStore.isConfirmationEnabled(mediaOverviewStore.getMediaType)) {
			dialogStore.openMediaConfirmationDownloadDialog(command);
		} else {
			downloadStore.downloadMedia({
				customDestinationFolderPath: '',
				destinationFolderPathId: null,
				downloadMedias: command,
			});
		}
	}
});

useMediaOverviewBarDownloadCommandBus().on(() => {
	const downloadCommand: DownloadMediaDTO = {
		plexServerId: libraryStore.getServerByLibraryId(mediaOverviewStore.libraryId)?.id ?? 0,
		plexLibraryId: mediaOverviewStore.libraryId,
		mediaIds: mediaOverviewStore.selection.keys,
		type: mediaOverviewStore.getMediaType,
		qualities: [],
		selectionKind: toDownloadSelectionKind(mediaOverviewStore.getMediaType),
		keepCompletedInDownloadFolder: settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder,
	};
	sendMediaOverviewDownloadCommand([downloadCommand]);
});

function onAction(event: IMediaOverviewBarActions) {
	switch (event) {
		case 'back':
			break;
		case 'selection-dialog':
			dialogStore.openDialog(DialogType.MediaSelectionDialog);

			break;
		case 'refresh-library':
			if (get(isRefreshDisabled)) {
				break;
			}
			dialogStore.openDialog(DialogType.RefreshMediaDialog);
			break;
		case 'media-options-dialog':
			dialogStore.openDialog(DialogType.MediaOptionsDialog);
			break;
		default:
			Log.error('Unknown action event', event);
			break;
	}
}

function retryMediaLoad() {
	useSubscription(mediaOverviewStore.retryMediaLoad().subscribe());
}

function onOptionsClosed(hasChanged: boolean, visibilityChanged: boolean) {
	if (!hasChanged) {
		return;
	}

	if (visibilityChanged && mediaOverviewStore.allMediaMode) {
		useSubscription(mediaOverviewStore.refreshMediaDataWithMetadata().subscribe());
		return;
	}

	useSubscription(mediaOverviewStore.refreshMediaData().subscribe());
}

function refreshCompletedLibrary(): void {
	useSubscription(
		libraryStore.refreshLibrary(props.libraryId).pipe(
			switchMap(() => mediaOverviewStore.refreshMediaData()),
		).subscribe(),
	);
}

// Refresh library metadata and media only when the current queue row reports completion.
watch(
	syncQueueKey,
	(currentKey, previousKey) => {
		const currentQueue = get(syncQueue);
		if (!currentQueue) {
			set(refreshAttemptActive, false);
			return;
		}

		if (currentQueue.status === LibrarySyncJobStatus.Queued
			|| currentQueue.status === LibrarySyncJobStatus.Processing) {
			set(refreshAttemptActive, true);
			return;
		}

		if (currentQueue.status === LibrarySyncJobStatus.Completed) {
			const isCompletedReplacement = previousKey?.startsWith(`${LibrarySyncJobStatus.Completed}|`)
				&& currentKey !== previousKey;
			if (get(refreshAttemptActive) || isCompletedReplacement) {
				set(refreshAttemptActive, false);
				refreshCompletedLibrary();
			}
			return;
		}

		set(refreshAttemptActive, false);
	},
	{ immediate: true },
);

onMounted(() => {
	resetProgress();

	// The loading overlay does not prevent mounting before settings are loaded.
	useSubscription(
		globalStore.getPageSetupReady.pipe(
			filter(Boolean),
			take(1),
			switchMap(() => mediaOverviewStore.initializeLibrary(props.libraryId)),
		).subscribe({
			next: async () => {
				const requestedScrollIndex = mediaOverviewStore.currentScrollIndex;
				if (requestedScrollIndex <= 0) {
					return;
				}

				// URL/store uses one-based index (same convention as media sortIndex).
				// PosterTable scroll API expects zero-based row/item index.
				const targetIndex = requestedScrollIndex - 1;
				// Initial render can race with virtualized content mounting and page prefetch.
				// Retry a few times so refresh/back-forward restores land at the intended poster.
				const maxAttempts = 10;
				for (let attempt = 0; attempt < maxAttempts; attempt++) {
					await nextTick();
					useSubscription(mediaOverviewStore.scrollToIndex(targetIndex, false).subscribe());
					await new Promise((resolve) => setTimeout(resolve, 100));

					const container = document.querySelector<HTMLElement>('#poster-table');
					const targetNode = container?.querySelector(`[data-scroll-index="${targetIndex}"]`);
					if ((container && container.scrollTop > 0) || targetNode) {
						break;
					}
				}
			},
		}),
	);
});
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.media-overview-results {
  height: 100%;
  min-height: 0;

  > .col,
  > [class*='col-'] {
    display: flex;
    height: 100%;
    min-height: 0;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .media-overview-results {
    padding-inline-end: 44px;
  }
}

#media-container,
.media-table-container,
.detail-view-container {
  width: 100%;
  overflow: hidden;
}

.progress-table {
  margin: 0 auto;
  border-collapse: collapse;

  td {
    vertical-align: middle;
    white-space: nowrap;
  }

  .progress-bar-cell {
    width: 250px;
  }
}
</style>
