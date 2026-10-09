<template>
	<q-list
		v-if="albums.length > 0"
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
				<q-item-section side>
					<QDuration
						short
						:value="album.duration" />
				</q-item-section>
			</template>

			<div
				v-if="album.children.length > 0"
				class="music-media-list__tracks">
				<div
					v-for="track in album.children"
					:key="track.id"
					class="music-track-row"
					:data-cy="`music-track-${album.id}-${track.id}`">
					<q-checkbox
						class="music-media-list__checkbox"
						:data-cy="`music-track-checkbox-${album.id}-${track.id}`"
						:aria-label="t('components.music-media-list.select-track', { title: track.title })"
						:model-value="isTrackSelected(album.id, track.id)"
						@update:model-value="setTrackSelected(album, track.id, Boolean($event))" />
					<div class="music-track-row__title">
						<span class="text-caption text-grey-6">{{ track.sortIndex }}</span>
						<span>{{ track.title }}</span>
					</div>
					<div class="music-track-row__metadata">
						<div
							v-if="track.mediaData.length === 0"
							class="music-track-original">
							<QDuration
								short
								:value="track.duration" />
							<QFileSize :size="track.mediaSize" />
						</div>
						<div
							v-for="original in track.mediaData"
							:key="original.id"
							class="music-track-original">
							<span v-if="original.audioCodec">{{ original.audioCodec }}</span>
							<QDuration
								short
								:value="original.duration || track.duration" />
							<QFileSize :size="original.size || track.mediaSize" />
							<span
								v-if="original.fileName"
								class="music-track-row__filename">
								{{ original.fileName }}
							</span>
						</div>
					</div>
				</div>
			</div>
			<div
				v-else
				class="q-pa-md text-grey-6">
				{{ t('components.music-media-list.no-tracks') }}
			</div>
		</q-expansion-item>
	</q-list>

	<div
		v-else
		class="q-pa-lg text-center"
		data-cy="music-media-list-empty">
		{{ t('components.music-media-list.no-albums') }}
	</div>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import type { DownloadMediaDTO, PlexMediaDTO } from '@dto';
import { useMediaOverviewStore } from '@store';
import { sendMediaOverviewDownloadCommand, useMediaOverviewBarDownloadCommandBus } from '@composables/event-bus';
import { toDownloadMedia } from '@composables/conversion';

const props = defineProps<{
	mediaItem: PlexMediaDTO;
}>();

const { t } = useI18n();
const mediaOverviewStore = useMediaOverviewStore();
const artistSelected = ref(false);
const selectedAlbumIds = ref(new Set<number>());
const selectedTrackIds = ref(new Map<number, Set<number>>());

const albums = computed(() => props.mediaItem.children);

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
		return toDownloadMedia(props.mediaItem);
	}

	const result: DownloadMediaDTO[] = [];
	for (const album of get(albums)) {
		if (album.children.length === 0)
			continue;
		if (get(selectedAlbumIds).has(album.id)) {
			result.push(...toDownloadMedia(album));
			continue;
		}

		const trackIds = get(selectedTrackIds).get(album.id);
		for (const track of album.children) {
			if (trackIds?.has(track.id)) {
				result.push(...toDownloadMedia(track));
			}
		}
	}
	return result;
}

watch(() => props.mediaItem, resetSelection, { deep: false });
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
	grid-template-columns: 52px minmax(12rem, 1fr) minmax(18rem, 1fr);
	align-items: center;
	gap: 0.75rem;
	min-height: 52px;
	padding: 0.25rem 1rem 0.25rem 2rem;
	border-bottom: 1px solid rgb(255 255 255 / 8%);

	&__title {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		min-width: 0;
	}

	&__metadata {
		display: grid;
		justify-items: end;
		gap: 0.35rem;
		min-width: 0;
	}

	&__filename {
		max-width: 22rem;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
}

.music-track-original {
	display: flex;
	align-items: center;
	justify-content: flex-end;
	gap: 0.75rem;
	min-width: 0;
	flex-wrap: wrap;
}

@media (max-width: $breakpoint-sm-max) {
	.music-track-row {
		grid-template-columns: 44px minmax(0, 1fr);
		padding: 0.5rem;

		&__metadata {
			grid-column: 2;
			justify-items: start;
		}

		.music-track-original {
			justify-content: flex-start;
		}

		&__filename {
			max-width: 100%;
			white-space: normal;
			overflow-wrap: anywhere;
		}
	}
}
</style>
