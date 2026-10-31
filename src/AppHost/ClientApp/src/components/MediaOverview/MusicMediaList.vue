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
					flat
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
			class="music-album-item"
			:data-cy="`music-album-${album.id}`">
			<template #header>
				<div class="music-album-row">
					<q-checkbox
						class="music-media-list__checkbox"
						:data-cy="`music-album-checkbox-${album.id}`"
						:disable="album.children.length === 0"
						:aria-label="t('components.music-media-list.select-album', { title: album.title })"
						:model-value="albumSelectionState(album)"
						@click.stop
						@update:model-value="setAlbumSelected(album, Boolean($event))" />
					<QRow
						justify="between">
						<QCol
							cols="4">
							<MediaComparisonStateButton
								class="q-mr-sm"
								:comparison-state="getPlexMediaComparisonState(album)"
								:media-type="album.type"
								show-tooltip
								dense
								flat
								:cy="`music-comparison-album-${album.id}`"
								@click.stop />
							<span>{{ album.title }}&nbsp;</span>
							<span>({{ t('components.music-media-list.track-count', { count: album.children.length }) }})</span>
						</QCol>
						<QCol
							v-if="album.recordLabel"
							cols="2">
							{{ t('components.music-media-list.record-label') }}: {{ album.recordLabel }}
						</QCol>
						<QCol
							cols="1"
							class="music-album-row__duration"
							@click.stop>
							<QDuration
								short
								:value="album.duration" />
						</QCol>

						<QCol
							v-if="album.originallyAvailableAt"
							cols="1">
							{{ t('components.music-media-list.release-date') }}:
							<QDateTime
								:text="album.originallyAvailableAt"
								short-date />
						</QCol>
					</QRow>
				</div>
			</template>

			<div
				v-if="album.children.length > 0"
				class="music-media-list__tracks">
				<MediaQTable
					:rows="album.children"
					:selection="trackSelection(album, album.children)"
					:download-media-factory="downloadTrackMedia"
					@selection="setAlbumTrackSelection(album, album.children, $event)" />
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
import { orderBy } from 'lodash-es';
import {
	PlexMediaComparisonState,
	PlexMediaType,
	type DownloadMediaDTO,
	type PlexMediaDTO,
	type PlexMediaQualityDTO,
	type PlexMediaSlimDTO,
} from '@dto';
import type { ISelection } from '@interfaces';
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
	[PlexMediaComparisonState.Missing, PlexMediaComparisonState.Partial, PlexMediaComparisonState.HigherQuality, PlexMediaComparisonState.PartialAndHigherQuality].includes(getPlexMediaComparisonState(props.mediaItem)));

function openArtistComparison(): void {
	if (get(artistComparisonClickable))
		dialogStore.openMediaComparisonDetailsDialog(props.mediaItem);
}

const artistSelected = ref(false);
const selectedAlbumIds = ref(new Set<number>());
const selectedTrackIds = ref(new Map<number, Set<number>>());

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
		const tracks = orderBy(
			children,
			[
				(track) => track.discNumber ?? Number.POSITIVE_INFINITY,
				(track) => track.trackNumber ?? Number.POSITIVE_INFINITY,
				'sortIndex',
				'id',
			],
		);
		return { ...album, children: tracks };
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

watch(() => props.mediaItem, resetSelection);

function downloadMedia(item: PlexMediaDTO | PlexMediaSlimDTO, qualities: PlexMediaQualityDTO[]): DownloadMediaDTO {
	return { ...toDownloadMedia(item)[0]!, qualities };
}

function trackSelection(album: PlexMediaDTO, tracks: PlexMediaDTO[]): ISelection {
	const keys = tracks.filter((track) => isTrackSelected(album.id, track.id)).map((track) => track.id);
	return {
		indexKey: album.id,
		keys,
		allSelected: keys.length === tracks.length && tracks.length > 0 ? true : keys.length === 0 ? false : null,
	};
}

function setAlbumTrackSelection(album: PlexMediaDTO, tracks: PlexMediaDTO[], selection: ISelection): void {
	const albumsSelection = selectedAlbumsAfterArtistDemotion();
	const inheritedWholeAlbum = get(artistSelected) || get(selectedAlbumIds).has(album.id);
	const tracksSelection = new Map(get(selectedTrackIds));
	const albumTracks = inheritedWholeAlbum
		? new Set(album.children.map((track) => track.id))
		: new Set(tracksSelection.get(album.id));
	const selectedKeys = new Set(selection.keys);

	set(artistSelected, false);
	albumsSelection.delete(album.id);
	for (const track of tracks) {
		if (selectedKeys.has(track.id))
			albumTracks.add(track.id);
		else
			albumTracks.delete(track.id);
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

function downloadTrackMedia(row: PlexMediaSlimDTO): DownloadMediaDTO[] {
	return [downloadMedia(row, [])];
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

function selectedDownloadMedia(): DownloadMediaDTO[] {
	if (get(artistSelected) && get(albums).some((album) => album.children.length > 0)) {
		return [downloadMedia(props.mediaItem, [])];
	}

	const result: DownloadMediaDTO[] = [];
	for (const album of get(albums)) {
		if (album.children.length === 0)
			continue;
		if (get(selectedAlbumIds).has(album.id)) {
			result.push(downloadMedia(album, []));
			continue;
		}

		const trackIds = get(selectedTrackIds).get(album.id);
		for (const track of album.children) {
			if (trackIds?.has(track.id))
				result.push(downloadMedia(track, []));
		}
	}
	return result;
}

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
@use '@/assets/scss/variables.scss' as *;

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

.music-album-row {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr) 44px auto;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  min-width: 0;

  &__details {
    min-width: 0;
  }

  &__metadata {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem;
    min-width: 0;
    overflow-wrap: anywhere;
  }

  &__duration {
    justify-self: end;
    white-space: nowrap;
  }
}
</style>
