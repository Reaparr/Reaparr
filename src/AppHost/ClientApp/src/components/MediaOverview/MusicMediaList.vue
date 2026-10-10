<template>
	<q-list
		class="music-media-list"
		data-cy="music-media-list">
		<q-item class="music-media-list__root">
			<q-item-section avatar>
				<q-checkbox
					class="music-media-list__checkbox"
					data-cy="music-artist-checkbox"
					:disable="!albums.some(album => album.children.length > 0)"
					:aria-label="t('components.music-media-list.select-artist', { title: mediaItem.title })"
					:model-value="rootSelectionState"
					@update:model-value="setArtistSelected(Boolean($event))" />
			</q-item-section>
			<q-item-section>
				<q-item-label class="text-h6 text-weight-bold">
					{{ mediaItem.title }}
				</q-item-label>
			</q-item-section>
			<q-item-section side>
				<MediaComparisonStateButton
					:comparison-state="getPlexMediaComparisonState(mediaItem)"
					:media-type="mediaItem.type"
					show-tooltip
					dense
					:clickable="artistComparisonClickable"
					:cy="`music-comparison-artist-${mediaItem.id}`"
					@click="openArtistComparison" />
			</q-item-section>
			<q-item-section
				v-if="selectedCount > 0"
				side>
				{{ t('components.music-media-list.selected-count', { count: selectedCount }) }}
			</q-item-section>
		</q-item>

		<q-expansion-item
			v-for="album in albums"
			:key="album.id"
			expand-separator
			:label="album.title"
			:data-cy="`music-album-${album.id}`">
			<template #header>
				<q-item-section avatar>
					<q-checkbox
						class="music-media-list__checkbox"
						:data-cy="`music-album-checkbox-${album.id}`"
						:disable="album.children.length === 0"
						:aria-label="t('components.music-media-list.select-album', { title: album.title })"
						:model-value="albumSelectionState(album)"
						@click.stop
						@update:model-value="setAlbumSelected(album, Boolean($event))" />
				</q-item-section>
				<q-item-section>
					<q-item-label class="text-weight-bold">
						{{ album.title }}
					</q-item-label>
					<q-item-label caption>
						{{ t('components.music-media-list.track-count', { count: album.children.length }) }}
					</q-item-label>
				</q-item-section>
				<q-item-section
					side
					@click.stop>
					<MediaComparisonStateButton
						:comparison-state="getPlexMediaComparisonState(album)"
						:media-type="album.type"
						show-tooltip
						dense
						:cy="`music-comparison-album-${album.id}`" />
					<QDuration
						short
						:value="album.duration" />
				</q-item-section>
			</template>

			<div
				v-if="album.children.length > 0"
				class="music-media-list__tracks">
				<q-expansion-item
					v-for="disc in album.discs"
					:key="disc.number ?? 'unknown'"
					default-opened
					expand-separator
					:label="disc.number === null ? t('components.music-media-list.unknown-disc') : t('components.music-media-list.disc-label', { number: disc.number })"
					:data-cy="`music-disc-${album.id}-${disc.number ?? 'unknown'}`">
					<div
						v-for="track in disc.tracks"
						:key="track.id"
						class="music-track-row"
						:data-cy="`music-track-${album.id}-${track.id}`">
						<q-checkbox
							class="music-media-list__checkbox"
							:data-cy="`music-track-checkbox-${album.id}-${track.id}`"
							:aria-label="t('components.music-media-list.select-track', { title: track.title })"
							:model-value="isTrackSelected(album.id, track.id)"
							@update:model-value="setTrackSelected(album, track.id, Boolean($event))" />
						<div class="music-track-row__details">
							<div class="music-track-row__title">
								<span
									v-if="track.trackNumber != null"
									class="text-caption text-grey-6"
									:data-cy="`music-track-number-${album.id}-${track.id}`">{{ track.trackNumber }}</span>
								<span>{{ track.title }}</span>
							</div>
							<div
								class="music-track-originals"
								role="radiogroup"
								:aria-label="t('general.labels.select-original', { title: track.title })">
								<label class="music-track-original music-track-original--automatic">
									<q-radio
										class="music-track-original__control"
										:data-cy="`music-track-original-${album.id}-${track.id}-automatic`"
										:model-value="selectedOriginals.get(trackKey(album.id, track.id)) ?? 'automatic'"
										val="automatic"
										:aria-label="t('general.labels.automatic-original')"
										@update:model-value="setSelectedOriginal(album, track, $event)" />
									<strong>{{ t('general.labels.automatic-original') }}</strong>
								</label>
								<label
									v-for="original in trackOriginals(track)"
									:key="original.plexApiMediaId"
									class="music-track-original">
									<q-radio
										class="music-track-original__control"
										:data-cy="`music-track-original-${album.id}-${track.id}-${original.plexApiMediaId}`"
										:model-value="selectedOriginals.get(trackKey(album.id, track.id)) ?? 'automatic'"
										:val="original.plexApiMediaId"
										:aria-label="`${t('general.labels.original')} ${original.files.map(file => file.fileName).join(', ')}`"
										@update:model-value="setSelectedOriginal(album, track, $event)" />
									<div class="music-track-original__content">
										<strong>{{ t('general.labels.original') }}</strong>
										<div
											v-for="file in original.files"
											:key="file.id"
											class="music-track-original__file">
											<span
												v-if="file.fileName"
												class="music-track-row__filename">{{ file.fileName }}</span>
											<span v-if="file.audioCodec">{{ file.audioCodec }}</span>
											<span v-if="file.videoCodec">{{ file.videoCodec }}</span>
											<QDuration
												short
												:value="file.duration || track.duration" />
											<QFileSize :size="file.size || track.mediaSize" />
										</div>
									</div>
								</label>
							</div>
						</div>
						<MediaComparisonStateButton
							:comparison-state="getPlexMediaComparisonState(track)"
							:media-type="track.type"
							show-tooltip
							dense
							:cy="`music-comparison-track-${album.id}-${track.id}`" />
					</div>
				</q-expansion-item>
			</div>
			<div
				v-else
				class="q-pa-md text-grey-6">
				{{ t('components.music-media-list.no-tracks') }}
			</div>
		</q-expansion-item>
	</q-list>

	<div
		v-if="albums.length === 0"
		class="q-pa-lg text-center"
		data-cy="music-media-list-empty">
		{{ t('components.music-media-list.no-albums') }}
	</div>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { groupBy, orderBy } from 'lodash-es';
import { PlexMediaComparisonState, PlexMediaType, type DownloadMediaDTO, type PlexMediaDataDTO, type PlexMediaDTO, type PlexMediaQualityDTO } from '@dto';
import { useDialogStore, useMediaOverviewStore } from '@store';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';
import { getPlexMediaComparisonState, toDownloadMedia } from '@composables/conversion';

const props = defineProps<{
	mediaItem: PlexMediaDTO;
}>();

const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const dialogStore = useDialogStore();
const artistComparisonClickable = computed(() =>
	[PlexMediaComparisonState.Missing, PlexMediaComparisonState.Partial].includes(getPlexMediaComparisonState(props.mediaItem)));

function openArtistComparison(): void {
	if (get(artistComparisonClickable))
		dialogStore.openMediaComparisonDetailsDialog(props.mediaItem);
}

const artistSelected = ref(false);
const selectedAlbumIds = ref(new Set<number>());
const selectedTrackIds = ref(new Map<number, Set<number>>());
const selectedOriginals = ref(new Map<string, number>());

const albums = computed(() => props.mediaItem.children.filter((album) =>
	album.type === PlexMediaType.MusicAlbum
	&& album.parentId === props.mediaItem.id
	&& album.plexLibraryId === props.mediaItem.plexLibraryId
	&& album.plexServerId === props.mediaItem.plexServerId)
	.map((album) => {
		const children = album.children.filter((track) =>
			track.type === PlexMediaType.MusicTrack
			&& track.parentId === album.id
			&& track.plexLibraryId === album.plexLibraryId
			&& track.plexServerId === album.plexServerId);
		const discs = orderBy(
			Object.values(groupBy(children, (track) => track.discNumber ?? 'unknown'))
				.map((tracks) => ({
					number: tracks[0]!.discNumber ?? null,
					tracks: orderBy(tracks, [(track) => track.trackNumber ?? Number.POSITIVE_INFINITY, 'sortIndex', 'id']),
				})),
			[(disc) => disc.number ?? Number.POSITIVE_INFINITY]);
		return { ...album, children, discs };
	}));

const selectedCount = computed(() => {
	if (get(artistSelected)) {
		const trackCount = get(albums).reduce((count, album) => count + album.children.length, 0);
		return trackCount;
	}

	return get(albums).reduce((count, album) => {
		if (get(selectedAlbumIds).has(album.id)) {
			return count + album.children.length;
		}
		return count + (get(selectedTrackIds).get(album.id)?.size ?? 0);
	}, 0);
});

const rootSelectionState = computed((): boolean | null => {
	if (get(artistSelected)) {
		return true;
	}
	return get(selectedCount) > 0 ? null : false;
});

function resetSelection(): void {
	set(artistSelected, false);
	set(selectedAlbumIds, new Set());
	set(selectedTrackIds, new Map());
}

function trackKey(albumId: number, trackId: number): string {
	return `${albumId}:${trackId}`;
}

function trackOriginals(track: PlexMediaDTO): Array<{ plexApiMediaId: number; files: PlexMediaDataDTO[] }> {
	const groups = new Map<number, PlexMediaDataDTO[]>();
	for (const file of track.mediaData) {
		const files = groups.get(file.plexApiMediaId);
		if (files)
			files.push(file);
		else
			groups.set(file.plexApiMediaId, [file]);
	}
	return Array.from(groups, ([plexApiMediaId, files]) => ({ plexApiMediaId, files }));
}

function setSelectedOriginal(album: PlexMediaDTO, track: PlexMediaDTO, value: number | 'automatic'): void {
	const next = new Map(get(selectedOriginals));
	const key = trackKey(album.id, track.id);
	if (value === 'automatic' || !trackOriginals(track).some((original) => original.plexApiMediaId === value))
		next.delete(key);
	else
		next.set(key, value);
	set(selectedOriginals, next);
}

function selectedQualities(album: PlexMediaDTO, tracks: PlexMediaDTO[]): PlexMediaQualityDTO[] {
	const result: PlexMediaQualityDTO[] = [];
	for (const track of tracks) {
		const plexApiMediaId = get(selectedOriginals).get(trackKey(album.id, track.id));
		const original = plexApiMediaId === undefined ? undefined : trackOriginals(track).find((group) => group.plexApiMediaId === plexApiMediaId);
		const representative = original?.files[0];
		if (representative) {
			result.push({ mediaId: track.id, dataId: representative.id, mediaDataType: track.type, quality: representative.videoResolution });
		}
	}
	return result;
}

function downloadMedia(item: PlexMediaDTO, qualities: PlexMediaQualityDTO[]): DownloadMediaDTO {
	return { ...toDownloadMedia(item)[0]!, qualities };
}

function setArtistSelected(value: boolean): void {
	resetSelection();
	set(artistSelected, value);
}

function albumSelectionState(album: PlexMediaDTO): boolean | null {
	if (get(artistSelected) || get(selectedAlbumIds).has(album.id)) {
		return true;
	}
	return (get(selectedTrackIds).get(album.id)?.size ?? 0) > 0 ? null : false;
}

function selectedAlbumsAfterArtistDemotion(): Set<number> {
	return get(artistSelected)
		? new Set(get(albums).map((album) => album.id))
		: new Set(get(selectedAlbumIds));
}

function setAlbumSelected(album: PlexMediaDTO, value: boolean): void {
	const albumsSelection = selectedAlbumsAfterArtistDemotion();
	set(artistSelected, false);
	const tracksSelection = new Map(get(selectedTrackIds));
	tracksSelection.delete(album.id);
	if (value) {
		albumsSelection.add(album.id);
	} else {
		albumsSelection.delete(album.id);
	}
	set(selectedAlbumIds, albumsSelection);
	set(selectedTrackIds, tracksSelection);
}

function isTrackSelected(albumId: number, trackId: number): boolean {
	return get(artistSelected)
		|| get(selectedAlbumIds).has(albumId)
		|| (get(selectedTrackIds).get(albumId)?.has(trackId) ?? false);
}

function setTrackSelected(album: PlexMediaDTO, trackId: number, value: boolean): void {
	const inheritedWholeAlbum = get(artistSelected) || get(selectedAlbumIds).has(album.id);
	const albumsSelection = selectedAlbumsAfterArtistDemotion();
	set(artistSelected, false);
	albumsSelection.delete(album.id);
	const tracksSelection = new Map(get(selectedTrackIds));
	const albumTracks = inheritedWholeAlbum
		? new Set(album.children.map((track) => track.id))
		: new Set(tracksSelection.get(album.id));
	if (value) {
		albumTracks.add(trackId);
	} else {
		albumTracks.delete(trackId);
	}
	if (albumTracks.size === album.children.length && album.children.length > 0) {
		albumsSelection.add(album.id);
		tracksSelection.delete(album.id);
	} else if (albumTracks.size > 0) {
		tracksSelection.set(album.id, albumTracks);
	} else {
		tracksSelection.delete(album.id);
	}
	set(selectedAlbumIds, albumsSelection);
	set(selectedTrackIds, tracksSelection);
}

function selectedDownloadMedia(): DownloadMediaDTO[] {
	if (get(artistSelected)) {
		return [downloadMedia(props.mediaItem, get(albums).flatMap((album) => selectedQualities(album, album.children)))];
	}

	const result: DownloadMediaDTO[] = [];
	for (const album of get(albums)) {
		if (album.children.length === 0)
			continue;
		if (get(selectedAlbumIds).has(album.id)) {
			result.push(downloadMedia(album, selectedQualities(album, album.children)));
			continue;
		}

		const trackIds = get(selectedTrackIds).get(album.id);
		for (const track of album.children) {
			if (trackIds?.has(track.id))
				result.push(downloadMedia(track, selectedQualities(album, [track])));
		}
	}
	return result;
}

watch(() => props.mediaItem, () => {
	resetSelection();
	set(selectedOriginals, new Map());
}, { deep: false });
watch(selectedCount, (count) => {
	mediaOverviewStore.$patch({ downloadButtonVisible: count > 0 });
}, { immediate: true });

useMediaOverviewBarDownloadCommandBus().on(() => {
	const downloadMedia = selectedDownloadMedia();
	if (downloadMedia.length > 0) {
		sendMediaOverviewDownloadCommand(downloadMedia);
	}
});

onBeforeUnmount(() => {
	mediaOverviewStore.$patch({ downloadButtonVisible: false });
});
</script>

<style lang="scss">
.music-media-list {
	&__checkbox {
		min-width: 44px;
		min-height: 44px;
	}

	&__root {
		min-height: 56px;
	}

	&__tracks {
		border-top: 1px solid rgb(255 255 255 / 12%);
	}
}

.music-track-row {
	display: grid;
	grid-template-columns: 52px minmax(0, 1fr) auto;
	align-items: start;
	gap: 0.75rem;
	padding: 0.5rem 1rem 0.75rem 2rem;
	border-bottom: 1px solid rgb(255 255 255 / 8%);

	&__details,
	&__title,
	&__filename {
		min-width: 0;
	}

	&__title {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		min-height: 44px;
	}

	&__filename {
		overflow-wrap: anywhere;
	}
}

.music-track-originals {
	display: grid;
	gap: 0.5rem;
}

.music-track-original {
	display: flex;
	align-items: flex-start;
	gap: 0.5rem;
	min-width: 0;
	min-height: 44px;
	padding: 0.35rem;
	border: 1px solid rgb(255 255 255 / 12%);
	border-radius: 4px;

	&__control {
		min-width: 44px;
		min-height: 44px;
	}

	&__content {
		min-width: 0;
		padding-top: 0.35rem;
	}

	&__file {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
		min-width: 0;
	}

	&__file + &__file {
		margin-top: 0.5rem;
	}
}

@media (max-width: $breakpoint-sm-max) {
	.music-track-row {
		grid-template-columns: 44px minmax(0, 1fr) auto;
		padding: 0.5rem 0.25rem;
	}

	.music-track-original {
		padding-inline: 0;
	}
}
</style>
