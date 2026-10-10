<template>
	<QPage>
		<template v-if="!loading && mediaItemDetail">
			<MediaOverviewBar
				:media-type="mediaItemDetail.type"
				:library-id="libraryId"
				:media-detail-item="mediaItemDetail"
				detail-mode
				@action="onAction" />
			<QScroll class="page-content-minus-media-overview-bar">
				<QRow class="music-detail-header">
					<QCol cols="auto">
						<MediaPosterImage
							class="q-ma-md"
							:thumb-width="200"
							:thumb-height="300"
							:actions="false"
							:media-item="mediaItemDetail" />
					</QCol>
					<QCol>
						<q-card class="music-detail-info">
							<q-card-section>
								<h1 class="text-h4 text-weight-bold q-mt-none q-mb-md">
									{{ mediaItemDetail.title }}
								</h1>
								<dl class="music-detail-metadata">
									<div>
										<dt>{{ t('components.music-details.album-count') }}</dt>
										<dd>{{ albumCount }}</dd>
									</div>
									<div>
										<dt>{{ t('components.music-details.track-count') }}</dt>
										<dd>{{ trackCount }}</dd>
									</div>
									<div>
										<dt>{{ t('components.music-details.duration') }}</dt>
										<dd><QDuration :value="mediaItemDetail.duration" /></dd>
									</div>
									<div>
										<dt>{{ t('components.music-details.size') }}</dt>
										<dd><QFileSize :size="mediaItemDetail.mediaSize" /></dd>
									</div>
									<div v-if="mediaItemDetail.year > 0">
										<dt>{{ t('components.music-details.year') }}</dt>
										<dd>{{ mediaItemDetail.year }}</dd>
									</div>
									<div
										v-for="date in dates"
										:key="date.key">
										<dt>{{ date.label }}</dt>
										<dd>
											<QDateTime
												:text="date.value"
												long-date />
										</dd>
									</div>
								</dl>
								<p
									v-if="mediaItemDetail.summary"
									class="q-mt-md music-detail-summary">
									{{ mediaItemDetail.summary }}
								</p>
							</q-card-section>
						</q-card>
					</QCol>
				</QRow>
				<MusicMediaList :media-item="mediaItemDetail" />
			</QScroll>
		</template>

		<div
			v-else-if="errorMessage"
			class="q-pa-lg text-center"
			data-cy="music-details-error">
			<QAlert type="error">
				{{ errorMessage }}
			</QAlert>
			<q-btn
				v-if="validRoute"
				class="q-mt-md"
				:label="t('components.media-overview.retry-media-load')"
				@click="loadArtist" />
			<q-btn
				flat
				class="q-mt-md"
				:label="t('general.commands.back')"
				@click="backToArtists" />
		</div>

		<MediaComparisonDetailsDialog />
		<DownloadConfirmation @download="downloadStore.downloadMedia($event)" />
		<QLoadingOverlay :loading="loading" />
	</QPage>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { Subject, of, switchMap, catchError, take } from 'rxjs';
import { useSubscription } from '@vueuse/rxjs';
import type { PlexMediaDTO } from '@dto';
import { PlexMediaType } from '@dto';
import type { IMediaOverviewBarActions } from '@interfaces';
import { useDialogStore, useDownloadStore, useMediaOverviewStore, useMediaStore, useSettingsStore } from '@store';
import { listenMediaOverviewDownloadCommand } from '@composables/event-bus';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const mediaStore = useMediaStore();
const mediaOverviewStore = useMediaOverviewStore();
const dialogStore = useDialogStore();
const downloadStore = useDownloadStore();
const settingsStore = useSettingsStore();
const loading = ref(true);
const errorMessage = ref('');
const mediaItemDetail = ref<PlexMediaDTO | null>(null);
const requests = new Subject<void>();
const libraryId = computed(() => +(route.params.id as string));
const artistId = computed(() => +(route.params.artistId as string));
const validRoute = computed(() => [get(libraryId), get(artistId)].every((id) => Number.isSafeInteger(id) && id > 0));
const albumCount = computed(() => get(mediaItemDetail)?.children.length ?? 0);
const trackCount = computed(() => get(mediaItemDetail)?.children.reduce((count, album) => count + album.children.length, 0) ?? 0);
const dates = computed(() => [
	{ key: 'originallyAvailableAt', label: t('components.music-details.originally-available'), value: get(mediaItemDetail)?.originallyAvailableAt },
	{ key: 'addedAt', label: t('components.music-details.added-at'), value: get(mediaItemDetail)?.addedAt },
	{ key: 'updatedAt', label: t('components.music-details.updated-at'), value: get(mediaItemDetail)?.updatedAt },
].filter((date): date is { key: string; label: string; value: string } => !!date.value && Number.isFinite(Date.parse(date.value)) && !date.value.startsWith('0001-')));

function backToArtists(): void {
	router.push(get(libraryId) > 0 ? `/music/${get(libraryId)}` : '/music');
}

function onAction(event: IMediaOverviewBarActions): void {
	if (event === 'back')
		backToArtists();
}

function loadArtist(): void {
	set(loading, get(validRoute));
	set(errorMessage, get(validRoute) ? '' : t('components.music-details.invalid-route'));
	set(mediaItemDetail, null);
	mediaOverviewStore.$patch({
		libraryId: get(libraryId),
		downloadButtonVisible: false,
		isDetailView: true,
		selection: { keys: [], allSelected: false, indexKey: get(libraryId) },
	});
	requests.next();
}

useSubscription(requests.pipe(
	switchMap(() => get(validRoute)
		? mediaStore.getMediaDataDetailById(get(artistId), PlexMediaType.MusicArtist).pipe(take(1), catchError(() => of(null)))
		: of(null)),
).subscribe((mediaDetail) => {
	const validItem = mediaDetail?.type === PlexMediaType.MusicArtist
		&& mediaDetail.id === get(artistId)
		&& mediaDetail.plexLibraryId === get(libraryId);
	set(mediaItemDetail, validItem ? mediaDetail : null);
	if (validItem)
		mediaOverviewStore.lastMediaItemViewed = mediaDetail;
	else if (get(validRoute))
		set(errorMessage, t('components.music-details.load-error'));
	set(loading, false);
}));

listenMediaOverviewDownloadCommand((command) => {
	const first = command[0];
	const current = get(mediaItemDetail);
	if (!current || get(loading) || command.some((item) => item.plexLibraryId !== current.plexLibraryId
		|| item.plexServerId !== current.plexServerId
		|| ![PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack].includes(item.type)))
		return;
	if (!first) return;
	if (settingsStore.isConfirmationEnabled(first.type)) {
		dialogStore.openMediaConfirmationDownloadDialog(command);
	} else {
		downloadStore.downloadMedia({
			downloadMedias: command,
			customDestinationFolderPath: '',
			destinationFolderPathId: null,
		});
	}
});

watch([libraryId, artistId], loadArtist, { immediate: true, flush: 'sync' });

onBeforeUnmount(() => {
	requests.complete();
	set(mediaItemDetail, null);
	mediaOverviewStore.$patch({ downloadButtonVisible: false, isDetailView: false });
});

definePageMeta({ scrollToTop: false });
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

.music-detail-info {
	@extend .background-sm;
	min-height: 300px;
	margin: 1rem 1rem 1rem 0;
}

.music-detail-metadata {
	display: grid;
	grid-template-columns: repeat(2, minmax(0, 1fr));
	gap: 0.75rem 1.5rem;
	margin: 0;

	div { display: grid; grid-template-columns: minmax(8rem, auto) 1fr; gap: 1rem; }
	dt { font-weight: 600; }
	dd { margin: 0; }
}

.music-detail-summary { white-space: pre-wrap; }

@media (max-width: $breakpoint-xs-max) {
	.music-detail-header {
		flex-direction: column;
		align-items: center;
		> .col, > .col-auto { flex: 0 0 100%; width: 100%; max-width: 100%; }
		> .col-auto { display: flex; justify-content: center; }
	}
	.music-detail-info { margin: 0 0.5rem 1rem; }
	.music-detail-metadata {
		grid-template-columns: 1fr;
		div { grid-template-columns: minmax(7rem, auto) 1fr; }
	}
	.page-content-minus-media-overview-bar,
	.page-content-minus-media-overview-bar .q-scrollarea__content {
		width: 100% !important;
		min-width: 0 !important;
		max-width: 100% !important;
	}
}
</style>
