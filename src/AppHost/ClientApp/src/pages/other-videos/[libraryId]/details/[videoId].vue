<template>
	<QPage data-cy="other-video-details">
		<template v-if="mediaItem && !loading && !error">
			<MediaOverviewBar
				:library-id="libraryId"
				:media-detail-item="mediaItem"
				detail-mode
				@action="onAction" />
			<QScroll class="page-content-minus-media-overview-bar">
				<div class="other-video-details__content q-pa-md">
					<header class="other-video-details__header">
						<MediaPosterImage
							:media-item="mediaItem"
							:actions="false" />
						<div class="other-video-details__info">
							<h1 class="text-h4 q-mt-none">
								{{ mediaItem.title }}
							</h1>
							<p
								v-if="mediaItem.summary"
								class="other-video-details__summary">
								{{ mediaItem.summary }}
							</p>
							<dl class="other-video-details__metadata">
								<dt>{{ t('components.other-video-details.duration') }}</dt>
								<dd><QDuration :value="mediaItem.duration" /></dd>
								<dt>{{ t('components.other-video-details.size') }}</dt>
								<dd><QFileSize :size="mediaItem.mediaSize" /></dd>
								<dt>{{ t('components.other-video-details.file-count') }}</dt>
								<dd>{{ mediaItem.mediaData.length }}</dd>
								<template v-if="mediaItem.year > 0">
									<dt>{{ t('components.other-video-details.year') }}</dt><dd>{{ mediaItem.year }}</dd>
								</template>
								<template
									v-for="date in dates"
									:key="date.key">
									<dt>{{ date.label }}</dt><dd>
										<QDateTime
											:text="date.value"
											short-date />
									</dd>
								</template>
							</dl>
						</div>
					</header>
					<OtherVideoMediaList
						:key="`${libraryId}-${mediaItem.id}`"
						:media-item="mediaItem" />
				</div>
			</QScroll>
		</template>
		<p
			v-else-if="loading"
			role="status"
			class="q-pa-md"
			data-cy="other-video-details-loading">
			{{ t('components.other-video-details.loading') }}
		</p>
		<div
			v-else
			class="q-pa-md">
			<p
				role="alert"
				data-cy="other-video-details-error">
				{{ validRoute ? t('components.other-video-details.load-error') : t('components.other-video-details.invalid-route') }}
			</p>
			<q-btn
				v-if="validRoute"
				class="other-video-details__control"
				:label="t('components.media-overview.retry-media-load')"
				@click="requestDetail" />
			<q-btn
				flat
				class="other-video-details__control"
				:label="t('general.commands.back')"
				@click="backToVideos" />
		</div>
		<DownloadConfirmation @download="downloadStore.downloadMedia($event)" />
	</QPage>
</template>

<script setup lang="ts">
import { Subject, of, switchMap, catchError, take } from 'rxjs';
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { PlexMediaType, type PlexMediaDTO } from '@dto';
import type { IMediaOverviewBarActions } from '@interfaces';
import { useDialogStore, useDownloadStore, useMediaOverviewStore, useMediaStore, useSettingsStore } from '@store';
import { listenMediaOverviewDownloadCommand } from '@composables/event-bus';

definePageMeta({ scrollToTop: false });
const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const mediaStore = useMediaStore();
const mediaOverviewStore = useMediaOverviewStore();
const settingsStore = useSettingsStore();
const dialogStore = useDialogStore();
const downloadStore = useDownloadStore();
const libraryId = computed(() => Number(route.params.libraryId));
const videoId = computed(() => Number(route.params.videoId));
const validRoute = computed(() => [get(libraryId), get(videoId)].every((id) => Number.isSafeInteger(id) && id > 0));
const mediaItem = ref<PlexMediaDTO | null>(null);
const loading = ref(false);
const error = ref(false);
const requests = new Subject<void>();
const dates = computed(() => [
	{ key: 'originallyAvailableAt', label: t('components.other-video-details.originally-available'), value: get(mediaItem)?.originallyAvailableAt },
	{ key: 'addedAt', label: t('components.other-video-details.added-at'), value: get(mediaItem)?.addedAt },
	{ key: 'updatedAt', label: t('components.other-video-details.updated-at'), value: get(mediaItem)?.updatedAt },
].filter((date): date is { key: string; label: string; value: string } => !!date.value && Number.isFinite(Date.parse(date.value)) && !date.value.startsWith('0001-')));

function backToVideos(): void {
	router.push(get(libraryId) > 0 ? `/other-videos/${get(libraryId)}` : '/other-videos');
}
function onAction(action: IMediaOverviewBarActions): void {
	if (action === 'back')
		backToVideos();
}
function requestDetail(): void {
	set(mediaItem, null);
	set(error, false);
	set(loading, get(validRoute));
	mediaOverviewStore.$patch({
		libraryId: get(libraryId), isDetailView: true, downloadButtonVisible: false,
		selection: { keys: [], allSelected: false, indexKey: get(libraryId) },
	});
	requests.next();
}
useSubscription(requests.pipe(
	switchMap(() => get(validRoute)
		? mediaStore.getMediaDataDetailById(get(videoId), PlexMediaType.OtherVideos).pipe(take(1), catchError(() => of(null)))
		: of(null)),
).subscribe((item) => {
	const validItem = item?.type === PlexMediaType.OtherVideos && item.id === get(videoId) && item.plexLibraryId === get(libraryId);
	set(mediaItem, validItem ? item : null);
	set(error, !validItem);
	set(loading, false);
	if (validItem)
		mediaOverviewStore.lastMediaItemViewed = item;
}));
watch([libraryId, videoId], requestDetail, { immediate: true, flush: 'sync' });
listenMediaOverviewDownloadCommand((command) => {
	const current = get(mediaItem);
	if (!current || get(loading) || !command.length || command.some((item) => item.type !== PlexMediaType.OtherVideos
		|| item.plexLibraryId !== current.plexLibraryId || item.plexServerId !== current.plexServerId))
		return;
	if (settingsStore.isConfirmationEnabled(PlexMediaType.OtherVideos))
		dialogStore.openMediaConfirmationDownloadDialog(command);
	else
		downloadStore.downloadMedia({ downloadMedias: command, customDestinationFolderPath: '', destinationFolderPathId: null });
});
onBeforeUnmount(() => {
	requests.complete();
	mediaOverviewStore.$patch({ isDetailView: false, downloadButtonVisible: false });
});
</script>

<style scoped lang="scss">
.other-video-details__header {
  display: flex;
  gap: 1.5rem;
  margin-bottom: 1rem;
}
.other-video-details__info {
  flex: 1;
  min-width: 0;
  overflow-wrap: anywhere;
}
.other-video-details__summary {
  white-space: pre-wrap;
}
.other-video-details__metadata {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.5rem 1rem;
}
.other-video-details__metadata dd {
  margin: 0;
}
.other-video-details__control {
  min-width: 44px;
  min-height: 44px;
}
@media (max-width: 599px) {
  .other-video-details__header {
    flex-direction: column;
    align-items: center;
  }
  .other-video-details__info {
    width: 100%;
  }
}
</style>
