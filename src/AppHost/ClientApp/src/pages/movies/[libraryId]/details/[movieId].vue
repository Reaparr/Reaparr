<template>
	<QPage>
		<template v-if="!loading && mediaItemDetail">
			<MediaDetailsLayout
				:library-id="libraryId"
				:media-item="mediaItemDetail"
				:summary-label="t('components.details-overview.summary')"
				@action="onAction">
				<template #details>
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
				</template>
				<MovieMediaList :media-item="mediaItemDetail" />
			</MediaDetailsLayout>
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

const loading = ref(true);
const mediaItemDetail = ref<PlexMediaDTO | null>(null);
const defaultImage = ref(false);

const route = useRoute();
const mediaStore = useMediaStore();
const dialogStore = useDialogStore();
const mediaOverviewStore = useMediaOverviewStore();
const downloadStore = useDownloadStore();
const settingsStore = useSettingsStore();
const router = useRouter();
const { t } = useI18n();

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

function onAction(event: IMediaOverviewBarActions) {
	if (event === 'back') {
		router.go(-1);
	}
}

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
