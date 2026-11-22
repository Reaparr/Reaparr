<template>
	<q-list
		class="media-overview-bar-header no-background"
		data-cy="media-overview-bar-header">
		<q-item
			v-ripple
			class="media-overview-bar-header__item"
			:clickable="isAggregate">
			<q-item-section avatar>
				<QMediaTypeIcon
					class="media-overview-bar-header__desktop-icon"
					:media-type="mediaType"
					:size="36" />
				<q-btn
					class="media-overview-bar-header__mobile-trigger"
					flat
					round
					dense
					:aria-label="$t('components.media-overview-bar-header.details')"
					data-cy="media-overview-bar-header-mobile-trigger"
					@click.stop>
					<QMediaTypeIcon
						:media-type="mediaType"
						:size="36" />
					<q-menu
						anchor="bottom left"
						self="top left"
						:offset="[0, 8]">
						<q-list class="media-overview-bar-header__mobile-menu">
							<q-item
								class="media-overview-bar-header__mobile-summary"
								dense>
								<q-item-section>
									<q-item-label>
										<template v-if="server && library">
											<span :class="{ 'inaccessible-item-text': !accountStore.getHasAccountServerAccess(server.id) }">
												{{ serverStore.getServerName(server.id) }}
											</span>
											{{ $t('general.delimiter.dash') }}
											<span :class="{ 'inaccessible-library-title': !hasLibraryAccess }">
												{{ libraryStore.getLibraryName(library.id) }}
											</span>
										</template>
										<template v-else>
											{{ mediaTypeToAllText(mediaType) }}
										</template>
									</q-item-label>
									<q-item-label
										v-if="showMetadata"
										caption>
										{{ formatted(mediaMetaData) }}
									</q-item-label>
								</q-item-section>
							</q-item>
							<template v-if="isAggregate">
								<q-separator />
								<MediaOverviewTypeSelector
									:selected-type="mediaType"
									@select="selectFamily" />
							</template>
						</q-list>
					</q-menu>
				</q-btn>
			</q-item-section>
			<q-item-section class="media-overview-bar-header__details">
				<q-item-label v-if="server && library">
					<span :class="{ 'inaccessible-item-text': !accountStore.getHasAccountServerAccess(server.id) }">
						{{ serverStore.getServerName(server.id) }}
					</span>
					{{ $t('general.delimiter.dash') }}
					<span
						:class="{ 'inaccessible-library-title': !hasLibraryAccess }"
						data-cy="media-overview-library-title">
						{{ libraryStore.getLibraryName(library.id) }}
					</span>
				</q-item-label>
				<q-item-label v-else>
					{{ mediaTypeToAllText(mediaType) }}
				</q-item-label>
				<q-item-label
					v-if="showMetadata"
					caption>
					{{ formatted(mediaMetaData) }}
				</q-item-label>
			</q-item-section>
			<q-menu
				v-if="isAggregate"
				anchor="bottom left"
				auto-close
				self="top left">
				<MediaOverviewTypeSelector
					:selected-type="mediaType"
					@select="selectFamily" />
			</q-menu>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { type PlexLibraryDTO, type PlexMediaDTO, PlexMediaType } from '@dto';
import prettyBytes from 'pretty-bytes';
import { useAccountStore, useLibraryStore, useServerStore, useLocalizationStore, useMediaOverviewStore, usePhotoBrowsingStore } from '@store';
import { useI18n } from '#imports';

interface IMediaOverviewMetadata {
	movieCount: number;
	tvShowCount: number;
	seasonCount: number;
	episodeCount: number;
	artistCount: number;
	albumCount: number;
	trackCount: number;
	photoCount: number;
	photoAlbumCount: number;
	clipCount: number;
	videoCount: number;
	fileSize: number;
}

const props = withDefaults(defineProps<{
	libraryId?: number;
	detailMode?: boolean;
	mediaDetailItem?: PlexMediaDTO | null;
	photoMode?: boolean;
}>(), {
	libraryId: 0,
	detailMode: false,
	photoMode: false,
});

const accountStore = useAccountStore();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();
const localizationStore = useLocalizationStore();
const mediaOverviewStore = useMediaOverviewStore();
const photoStore = usePhotoBrowsingStore();
const { t } = useI18n();

const mediaType = computed(() => props.photoMode
	? PlexMediaType.PhotoAlbum
	: props.mediaDetailItem?.type ?? mediaOverviewStore.getMediaType);
const isAggregate = computed(() => props.photoMode ? props.libraryId === 0 : mediaOverviewStore.allMediaMode);
const isLoading = computed(() => props.photoMode ? photoStore.isRefreshing : mediaOverviewStore.loading);
const library = computed(() => libraryStore.getLibrary(props.libraryId));
const server = computed(() => serverStore.getServer(get(library)?.plexServerId ?? -1));
const hasLibraryAccess = computed(() => {
	const currentLibrary = get(library);
	const currentServer = get(server);
	return !!currentLibrary
		&& !!currentServer
		&& accountStore.getHasAccountServerAccess(currentServer.id)
		&& accountStore.getHasAccountLibraryAccess(currentLibrary.id);
});
const mediaMetaData = computed<IMediaOverviewMetadata>(() => {
	const currentLibrary = get(library);
	if (props.photoMode) return getPhotoMetadata(currentLibrary);
	if (props.mediaDetailItem) return getDetailMetadata(props.mediaDetailItem);
	return getLibraryMetadata(currentLibrary);
});
const hasMedia = computed(() => {
	if (props.photoMode) {
		const currentLibrary = get(library);
		return (currentLibrary?.count ?? 0) > 0
			|| photoStore.albums.totalCount > 0
			|| photoStore.assets.totalCount > 0
			|| photoStore.folders.totalCount > 0;
	}
	return !!props.mediaDetailItem || mediaOverviewStore.totalCount > 0;
});
const showMetadata = computed(() => (props.photoMode && get(library) !== null) || (!get(isLoading) && get(hasMedia)));

function getPhotoMetadata(currentLibrary: PlexLibraryDTO | null): IMediaOverviewMetadata {
	return {
		movieCount: 0,
		tvShowCount: 0,
		seasonCount: 0,
		episodeCount: 0,
		artistCount: 0,
		albumCount: currentLibrary?.photoAlbumCount ?? photoStore.albums.totalCount,
		trackCount: 0,
		photoCount: currentLibrary?.photoImageCount ?? photoStore.assets.totalCount,
		photoAlbumCount: currentLibrary?.photoAlbumCount ?? photoStore.albums.totalCount,
		clipCount: currentLibrary?.photoClipCount ?? 0,
		videoCount: 0,
		fileSize: currentLibrary?.mediaSize ?? photoStore.assets.totalBytes,
	};
}
function getDetailMetadata(mediaDetailItem: PlexMediaDTO): IMediaOverviewMetadata {
	const movieFileCount = mediaDetailItem.mediaData.length || mediaDetailItem.qualities.length || 1;
	return {
		movieCount: mediaDetailItem.type === PlexMediaType.Movie ? movieFileCount : 0,
		tvShowCount: mediaDetailItem.type === PlexMediaType.TvShow ? 1 : 0,
		seasonCount: mediaDetailItem.childCount,
		episodeCount: mediaDetailItem.grandChildCount,
		artistCount: 0,
		albumCount: 0,
		trackCount: 0,
		photoCount: 0,
		photoAlbumCount: 0,
		clipCount: 0,
		videoCount: mediaDetailItem.type === PlexMediaType.OtherVideos ? 1 : 0,
		fileSize: mediaDetailItem.mediaSize,
	};
}
function getLibraryMetadata(currentLibrary: PlexLibraryDTO | null): IMediaOverviewMetadata {
	return {
		movieCount: mediaOverviewStore.allMovieCount,
		tvShowCount: mediaOverviewStore.allTvShowCount,
		seasonCount: mediaOverviewStore.allSeasonCount,
		episodeCount: mediaOverviewStore.allEpisodeCount,
		artistCount: currentLibrary?.musicArtistCount ?? 0,
		albumCount: currentLibrary?.musicAlbumCount ?? 0,
		trackCount: currentLibrary?.musicTrackCount ?? 0,
		photoCount: currentLibrary?.photoImageCount ?? 0,
		photoAlbumCount: currentLibrary?.photoAlbumCount ?? 0,
		clipCount: currentLibrary?.photoClipCount ?? 0,
		videoCount: currentLibrary?.otherVideoCount ?? 0,
		fileSize: mediaOverviewStore.allFileSize,
	};
}
function formatPhotoMetadata(metadata: IMediaOverviewMetadata, currentLibrary: PlexLibraryDTO | null, aggregateAlbumCount = metadata.photoAlbumCount): string {
	if (currentLibrary) {
		return t('components.media-overview-bar-header.photos-library-metadata', {
			albumCount: metadata.photoAlbumCount,
			photoCount: metadata.photoCount,
			clipCount: metadata.clipCount,
			fileSize: toFileSize(metadata.fileSize),
		});
	}
	return t('components.media-overview-bar-header.photos-metadata', {
		albumCount: aggregateAlbumCount,
		fileSize: toFileSize(metadata.fileSize),
	});
}
function formatted(metadata: IMediaOverviewMetadata): string {
	const currentLibrary = get(library);
	if (props.photoMode) return formatPhotoMetadata(metadata, currentLibrary);
	switch (get(mediaType)) {
		case PlexMediaType.Movie:
			return t('components.media-overview-bar-header.movies-metadata', {
				movieCount: metadata.movieCount,
				fileSize: toFileSize(metadata.fileSize),
			});
		case PlexMediaType.TvShow:
			return t('components.media-overview-bar-header.tv-shows-metadata', {
				tvShowCount: metadata.tvShowCount,
				seasonCount: metadata.seasonCount,
				episodeCount: metadata.episodeCount,
				fileSize: toFileSize(metadata.fileSize),
			});
		case PlexMediaType.MusicArtist:
			if (props.mediaDetailItem) {
				return t('components.media-overview-bar-header.music-detail-metadata', {
					albumCount: props.mediaDetailItem.childCount,
					trackCount: props.mediaDetailItem.grandChildCount,
					fileSize: toFileSize(metadata.fileSize),
				});
			}
			if (currentLibrary) {
				return t('components.media-overview-bar-header.music-library-metadata', {
					artistCount: metadata.artistCount,
					albumCount: metadata.albumCount,
					trackCount: metadata.trackCount,
					fileSize: toFileSize(metadata.fileSize),
				});
			}
			return t('components.media-overview-bar-header.music-metadata', {
				artistCount: mediaOverviewStore.totalCount,
				fileSize: toFileSize(metadata.fileSize),
			});
		case PlexMediaType.PhotoAlbum:
			if (props.mediaDetailItem) {
				return t('components.media-overview-bar-header.photo-detail-metadata', {
					assetCount: props.mediaDetailItem.childCount,
					fileSize: toFileSize(metadata.fileSize),
				});
			}
			return formatPhotoMetadata(metadata, currentLibrary, mediaOverviewStore.totalCount);
		case PlexMediaType.OtherVideos:
			return t('components.media-overview-bar-header.other-videos-metadata', {
				videoCount: currentLibrary ? metadata.videoCount : mediaOverviewStore.totalCount,
				fileSize: toFileSize(metadata.fileSize),
			});
		default:
			return t('general.error.unknown');
	}
}
function toFileSize(size: number): string {
	if (size === 0) {
		return '-';
	}
	return prettyBytes(size, { locale: localizationStore.getLanguageLocale?.bcp47Code || 'en-US' });
}
function mediaTypeToAllText(type: PlexMediaType): string {
	switch (type) {
		case PlexMediaType.Movie:
			return t('components.media-overview-bar.all-media-mode.movies');
		case PlexMediaType.TvShow:
			return t('components.media-overview-bar.all-media-mode.tv-shows');
		case PlexMediaType.MusicArtist:
			return t('components.media-overview-bar.all-media-mode.music');
		case PlexMediaType.PhotoAlbum:
			return t('components.media-overview-bar.all-media-mode.photos');
		case PlexMediaType.OtherVideos:
			return t('components.media-overview-bar.all-media-mode.other-videos');
		default:
			return t('general.error.unknown');
	}
}
function selectFamily(type: PlexMediaType): void {
	if (props.photoMode) {
		emit('family-change', type);
		return;
	}
	mediaOverviewStore.changeAllMediaOverviewType(type);
}

const emit = defineEmits<{
	(e: 'family-change', type: PlexMediaType): void;
}>();
</script>

<style lang="scss">
.inaccessible-item-text,
.inaccessible-library-title {
  text-decoration: line-through;
  opacity: 0.62;
}

.inaccessible-library-title {
  color: var(--q-grey-6);
}

.media-overview-bar-header__item {
  align-items: center;

  > .q-item__section--avatar {
    align-self: center;
    justify-content: center;
  }
}

.media-overview-bar-header__mobile-trigger {
  display: none;
}

.media-overview-bar-header__mobile-menu {
  width: min(320px, calc(100vw - 16px));
  max-width: calc(100vw - 16px);
}

.media-overview-bar-header__mobile-summary .q-item__label {
  white-space: normal;
  overflow-wrap: anywhere;
}

@media (max-width: $breakpoint-sm-max) {
  .media-overview-bar-header {
    padding-inline: 4px;
  }

  .media-overview-bar-header__item {
    padding: 0;
    > .q-item__section--avatar {
      width: 44px;
      min-width: 44px;
      max-width: 44px;
      padding-right: 0;
      flex: 0 0 44px;
    }
  }

  .media-overview-bar-header__desktop-icon {
    display: none;
  }

  .media-overview-bar-header__details {
    display: none;
  }

  .media-overview-bar-header__mobile-trigger {
    display: inline-flex;
    min-width: 44px !important;
    min-height: 44px !important;
    padding: 0;
    align-self: center;
    align-items: center;
    justify-content: center;
  }
}
</style>
