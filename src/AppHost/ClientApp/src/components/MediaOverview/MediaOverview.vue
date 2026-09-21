<template>
	<!--	Refresh Library Screen	-->
	<MediaOverviewRefresh
		v-if="libraryStore.getIsLibrarySyncing(libraryId)"
		:library-id="libraryId" />
	<div
		v-else
		class="media-overview-layout">
		<div class="media-overview-bar">
			<!--	Overview bar	-->
			<MediaOverviewBar
				:detail-mode="false"
				:library-id="libraryId"
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
		<div class="media-overview-content">
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
									:disable-hover-click="mediaOverviewStore.getMediaType !== PlexMediaType.TvShow"
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
			<MediaComparisonDetailsDialog />
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
		</div>
	</div>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useSubscription } from '@vueuse/rxjs';
import { type DownloadMediaDTO, PlexMediaType, ViewMode } from '@dto';
import { DialogType } from '@enums';
import type { IMediaOverviewBarActions } from '@interfaces';
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
	resetProgress();
	mediaOverviewStore.loading = true;
	useSubscription(
		libraryStore.reSyncLibrary(mediaOverviewStore.libraryId, forceMediaRefresh).subscribe(),
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

function onOptionsClosed(hasChanged: boolean) {
	if (hasChanged) {
		useSubscription(mediaOverviewStore.refreshMediaData().subscribe());
	}
}

// Refresh media data when a library sync completes.
watch(
	() => libraryStore.getIsLibrarySyncing(props.libraryId),
	(isSyncing, wasSyncing) => {
		if (wasSyncing && !isSyncing) {
			useSubscription(mediaOverviewStore.refreshMediaData().subscribe());
		}
	},
);

onMounted(() => {
	resetProgress();

	// Initialize the library in the store
	useSubscription(
		mediaOverviewStore.initializeLibrary(props.libraryId).subscribe({
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

.media-overview-layout {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.media-overview-content {
  position: relative;
  flex: 1 1 auto;
  min-height: 0;
  overflow: hidden;
}

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
