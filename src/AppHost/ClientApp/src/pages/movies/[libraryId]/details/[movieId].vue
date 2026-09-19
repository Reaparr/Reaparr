<template>
	<QPage>
		<template v-if="!loading && mediaItemDetail">
			<!--	Overview bar	-->
			<MediaOverviewBar
				:media-type="mediaItemDetail.type"
				:library-id="libraryId"
				:media-detail-item="mediaItemDetail"
				detail-mode
				@action="onAction" />
			<QScroll class="page-content-minus-media-overview-bar">
				<!--	Header	-->
				<QRow class="media-detail-header">
					<QCol
						cols="auto">
						<!--	Poster	-->
						<MediaPosterImage
							class="q-ma-md"
							:thumb-width="thumbWidth"
							:thumb-height="thumbHeight"
							:actions="false"
							:media-item="mediaItemDetail" />
					</QCol>
					<QCol>
						<q-card
							class="media-info-container"
							:style="{ height: thumbHeight + 'px' }">
							<!-- Media info -->
							<q-card-section>
								<q-markup-table wrap-cells>
									<tbody>
										<tr class="q-tr--no-hover">
											<td
												colspan="2"
												class="media-info-column media-title">
												{{ mediaItemDetail?.title ?? t('general.commands.unknown') }}
											</td>
										</tr>
										<tr>
											<td class="media-info-column">
												{{ t('components.details-overview.total-duration') }}
											</td>
											<td class="media-info-column">
												<QDuration :value="mediaItemDetail?.duration ?? -1" />
											</td>
										</tr>
										<tr>
											<td class="media-info-column">
												{{ t('components.details-overview.media-count-label') }}
											</td>
											<td class="media-info-column">
												{{ movieMediaCountFormatted }}
											</td>
										</tr>
										<tr>
											<td class="media-info-column">
												{{ t('components.details-overview.summary') }}
											</td>
											<td class="media-info-column">
												{{ mediaItemDetail?.summary ?? '' }}
											</td>
										</tr>
									</tbody>
								</q-markup-table>
							</q-card-section>
						</q-card>
					</QCol>
				</QRow>

				<!--	Media Table	-->
				<QRow no-gutters>
					<QCol>
						<MovieMediaList :media-item="mediaItemDetail" />
					</QCol>
				</QRow>
			</QScroll>
		</template>

		<!-- Download confirmation dialog	-->
		<DownloadConfirmation @download="downloadStore.downloadMedia($event)" />

		<QLoadingOverlay :loading="loading" />
	</QPage>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { forkJoin } from 'rxjs';
import { take } from 'rxjs/operators';
import { type PlexMediaDTO, PlexMediaType } from '@dto';
import { useRouter } from 'vue-router';
import type { IMediaOverviewBarActions } from '@interfaces';
import { useDialogStore, useDownloadStore, useMediaOverviewStore, useMediaStore, useSettingsStore } from '@store';
import {
	definePageMeta, listenMediaOverviewDownloadCommand,
	useI18n,
	useSubscription,
} from '#imports';

definePageMeta({
	scrollToTop: false,
});

const route = useRoute();

const mediaStore = useMediaStore();
const dialogStore = useDialogStore();
const mediaOverviewStore = useMediaOverviewStore();
const downloadStore = useDownloadStore();
const settingsStore = useSettingsStore();

const router = useRouter();

const { t } = useI18n();
const loading = ref(true);
const mediaItemDetail = ref<PlexMediaDTO | null>(null);
const thumbWidth = ref(200);
const thumbHeight = ref(300);
const defaultImage = ref(false);

const libraryId = computed(() => +(route.params.libraryId as string));
const mediaId = computed(() => +(route.params.movieId as string));

const movieFileCount = computed((): number => {
	const item = get(mediaItemDetail);
	return item?.mediaData.length || item?.qualities.length || 1;
});

const movieMediaCountFormatted = computed((): string => {
	const count = get(movieFileCount);
	if (count === 1) {
		return t('components.details-overview.one-movie-count');
	}

	return t('components.details-overview.movie-file-count', { movieFileCount: count });
});

function onAction(event: IMediaOverviewBarActions) {
	if (event === 'back') {
		router.go(-1);
	}
}

listenMediaOverviewDownloadCommand((command) => {
	const [first] = command;
	if (!first) {
		return;
	}
	const type: PlexMediaType = first.type;
	if (settingsStore.isConfirmationEnabled(type)) {
		dialogStore.openMediaConfirmationDownloadDialog(command);
	} else {
		downloadStore.downloadMedia({
			downloadMedias: command,
			customDestinationFolderPath: '',
			destinationFolderPathId: null,
		});
	}
});

onMounted(() => {
	const type = PlexMediaType.Movie;

	mediaOverviewStore.$patch({
		downloadButtonVisible: false,
		isDetailView: true,
	});

	useSubscription(
		forkJoin({
			mediaDetail: mediaStore.getMediaDataDetailById(get(mediaId), type),
		})
			.pipe(take(1))
			.subscribe({
				next: ({ mediaDetail }) => {
					set(mediaItemDetail, mediaDetail);
					mediaOverviewStore.lastMediaItemViewed = mediaDetail;
				},
				error: () => {
					set(defaultImage, true);
				},
				complete: () => {
					set(loading, false);
				},
			}),
	);
});

onBeforeUnmount(() => {
	set(mediaItemDetail, null);
	set(loading, true);
	mediaOverviewStore.$patch({
		downloadButtonVisible: false,
		isDetailView: false,
	});
});
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

.media-info-container {
  @extend .background-sm;
  box-shadow: 0px 3px 1px -2px rgba(0, 0, 0, 0.2), 0px 2px 2px 0px rgba(0, 0, 0, 0.14),
  0px 1px 5px 0px rgba(0, 0, 0, 0.12);

  margin-right: 1rem;

  .media-title {
    font-size: 30px;
    font-weight: bold;
  }

  .media-info-column {
    min-width: 150px;
    text-align: left;
    white-space: pre-wrap;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .media-detail-header {
    flex-direction: column;
    align-items: center;

    > .col,
    > .col-auto {
      width: 100%;
      min-width: 0;
    }
  }
  .page-content-minus-media-overview-bar,
  .page-content-minus-media-overview-bar .q-scrollarea__content {
    width: 100% !important;
    min-width: 0 !important;
    max-width: 100% !important;
  }

  .media-detail-header > .col,
  .media-detail-header > .col-auto {
    flex: 0 0 100% !important;
    width: 100% !important;
    max-width: 100% !important;
  }

  .media-detail-header > .col-auto {
    display: flex;
    justify-content: center;
  }

  .media-info-container {
    width: calc(100% - 1rem) !important;
    max-width: calc(100% - 1rem) !important;

    .q-markup-table,
    .q-table__container,
    .q-table {
      width: 100% !important;
      max-width: 100% !important;
      table-layout: fixed;
    }
  }

  .media-info-container {
    height: auto !important;
    margin: 0 0.5rem 1rem;

    .media-title {
      font-size: clamp(1.35rem, 1.1rem + 1vw, 1.5rem);
    }

    .media-info-column {
      min-width: 0;
      padding: 0.5rem;
      overflow-wrap: anywhere;
    }
  }
}
</style>
