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
						<td
							colspan="2"
							class="media-info-column">
							<div class="media-info-detail-grid">
								<div class="media-info-detail-column">
									<div class="media-info-detail-field">
										<div class="media-info-detail-label">
											{{ t('components.music-details.duration') }}
										</div>
										<div class="media-info-detail-value">
											<QDuration :value="mediaItemDetail.duration" />
										</div>
									</div>
									<div class="media-info-detail-field">
										<div class="media-info-detail-label">
											{{ t('components.music-details.size') }}
										</div>
										<div class="media-info-detail-value">
											<QFileSize :size="mediaItemDetail.mediaSize" />
										</div>
									</div>
									<div
										v-if="mediaItemDetail.year > 0"
										class="media-info-detail-field">
										<div class="media-info-detail-label">
											{{ t('components.music-details.year') }}
										</div>
										<div class="media-info-detail-value">
											{{ mediaItemDetail.year }}
										</div>
									</div>
									<div
										v-if="mediaItemDetail.genres?.length"
										class="media-info-detail-field"
										data-cy="music-detail-genres">
										<div class="media-info-detail-label">
											{{ t('components.music-details.genres') }}
										</div>
										<div class="media-info-detail-value">
											{{ mediaItemDetail.genres?.map((genre) => genre.name).join(', ') }}
										</div>
									</div>
								</div>
								<div class="media-info-detail-column">
									<div
										v-for="date in dates"
										:key="date.key"
										class="media-info-detail-field">
										<div class="media-info-detail-label">
											{{ date.label }}
										</div>
										<div class="media-info-detail-value">
											<QDateTime
												:text="date.value"
												long-date />
										</div>
									</div>
								</div>
							</div>
						</td>
					</tr>
				</template>
				<MusicMediaList :media-item="mediaItemDetail" />
			</MediaDetailsLayout>
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
const dates = computed(() => [
	{ key: 'originallyAvailableAt', label: t('components.music-details.originally-available'), value: get(mediaItemDetail)?.originallyAvailableAt },
	{ key: 'addedAt', label: t('components.music-details.added-at'), value: get(mediaItemDetail)?.addedAt },
	{ key: 'updatedAt', label: t('components.music-details.updated-at'), value: get(mediaItemDetail)?.updatedAt },
].filter((date): date is { key: string; label: string; value: string } => !!date.value && Number.isFinite(Date.parse(date.value)) && !date.value.startsWith('0001-')));

function backToArtists(): void {
	const id = get(libraryId);
	router.push(Number.isSafeInteger(id) && id > 0 ? `/music/${id}` : '/music');
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
	if (command.some((item) => settingsStore.isConfirmationEnabled(item.type))) {
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
