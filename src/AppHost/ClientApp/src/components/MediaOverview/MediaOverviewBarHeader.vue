<template>
	<q-list class="media-overview-bar-header no-background">
		<q-item
			v-ripple
			class="media-overview-bar-header__item"
			:clickable="mediaOverviewStore.allMediaMode">
			<q-item-section avatar>
				<QMediaTypeIcon
					class="media-overview-bar-header__desktop-icon"
					:media-type="mediaOverviewStore.getMediaType"
					:size="36" />
				<q-btn
					class="media-overview-bar-header__mobile-trigger"
					flat
					round
					dense
					aria-label="Media overview details"
					data-cy="media-overview-bar-header-mobile-trigger"
					@click.stop>
					<QMediaTypeIcon
						:media-type="mediaOverviewStore.getMediaType"
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
											{{ mediaTypeToAllText(mediaOverviewStore.getMediaType) }}
										</template>
									</q-item-label>
									<q-item-label
										v-if="!mediaOverviewStore.loading && hasMedia"
										caption>
										{{ formatted(mediaMetaData) }}
									</q-item-label>
								</q-item-section>
							</q-item>
							<template v-if="mediaOverviewStore.allMediaMode">
								<q-separator />
								<q-item
									v-for="(type, i) in [PlexMediaType.Movie, PlexMediaType.TvShow].filter(x => x !== mediaOverviewStore.getMediaType)"
									:key="i"
									v-close-popup
									v-ripple
									clickable
									@click="mediaOverviewStore.changeAllMediaOverviewType(type)">
									<q-item-section avatar>
										<QMediaTypeIcon
											:media-type="type"
											:size="36"
											class="q-mr-md" />
									</q-item-section>
									<q-item-section>
										<QText
											size="h5"
											:value="mediaTypeToAllText(type)" />
									</q-item-section>
								</q-item>
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
					{{ mediaTypeToAllText(mediaOverviewStore.getMediaType) }}
				</q-item-label>
				<q-item-label
					v-if="!mediaOverviewStore.loading && hasMedia"
					caption>
					{{ formatted(mediaMetaData) }}
				</q-item-label>
			</q-item-section>
			<q-menu
				v-if="mediaOverviewStore.allMediaMode"
				anchor="bottom left"
				auto-close
				self="top left">
				<q-list>
					<q-item
						v-for="(type, i) in [PlexMediaType.Movie, PlexMediaType.TvShow].filter(x => x !== mediaOverviewStore.getMediaType)"
						:key="i"
						v-ripple
						clickable
						@click="mediaOverviewStore.changeAllMediaOverviewType(type)">
						<q-item-section avatar>
							<QMediaTypeIcon
								:media-type="type"
								:size="36"
								class="q-mr-md" />
						</q-item-section>
						<q-item-section>
							<QText
								size="h5"
								:value="mediaTypeToAllText(type)" />
						</q-item-section>
					</q-item>
				</q-list>
			</q-menu>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { type PlexMediaDTO, PlexMediaType } from '@dto';
import prettyBytes from 'pretty-bytes';
import {
	useAccountStore,
	useLibraryStore,
	useServerStore,
	useLocalizationStore,
	useMediaOverviewStore,
	useI18n,
} from '#imports';

const accountStore = useAccountStore();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();
const localizationStore = useLocalizationStore();
const mediaOverviewStore = useMediaOverviewStore();

const props = withDefaults(defineProps<{
	libraryId?: number;
	detailMode?: boolean;
	mediaDetailItem?: PlexMediaDTO | null;

}>(), {
	libraryId: 0,
	detailMode: false,
});

const server = computed(() => serverStore.getServer(get(library)?.plexServerId ?? -1));
const library = computed(() => libraryStore.getLibrary(props.libraryId));
const hasLibraryAccess = computed(() => {
	const currentLibrary = get(library);
	const currentServer = get(server);
	return !!currentLibrary
		&& !!currentServer
		&& accountStore.getHasAccountServerAccess(currentServer.id)
		&& accountStore.getHasAccountLibraryAccess(currentLibrary.id);
});

const { t } = useI18n();

const mediaMetaData = computed(() => {
	if (props.mediaDetailItem) {
		const movieFileCount = props.mediaDetailItem.mediaData.length || props.mediaDetailItem.qualities.length || 1;
		return {
			movieCount: props.mediaDetailItem.type === PlexMediaType.Movie ? movieFileCount : 0,
			tvShowCount: props.mediaDetailItem.type === PlexMediaType.TvShow ? 1 : 0,
			seasonCount: props.mediaDetailItem.childCount,
			episodeCount: props.mediaDetailItem.grandChildCount,
			fileSize: props.mediaDetailItem.mediaSize,
		};
	}

	return {
		movieCount: mediaOverviewStore.allMovieCount,
		tvShowCount: mediaOverviewStore.allTvShowCount,
		seasonCount: mediaOverviewStore.allSeasonCount,
		episodeCount: mediaOverviewStore.allEpisodeCount,
		fileSize: mediaOverviewStore.allFileSize,
	};
});

const hasMedia = computed(() => !!props.mediaDetailItem || mediaOverviewStore.totalCount > 0);

function formatted({ movieCount, tvShowCount, seasonCount, episodeCount, fileSize }: {
	movieCount: number;
	tvShowCount: number;
	seasonCount: number;
	episodeCount: number;
	fileSize: number;
}): string {
	switch (mediaOverviewStore.getMediaType) {
		case PlexMediaType.Movie:
			return t('components.media-overview-bar-header.movies-metadata', {
				movieCount,
				fileSize: toFileSize(fileSize),
			});
		case PlexMediaType.TvShow:
			return t('components.media-overview-bar-header.tv-shows-metadata', {
				tvShowCount,
				seasonCount,
				episodeCount,
				fileSize: toFileSize(fileSize),
			});
		default:
			return `Media type ${mediaOverviewStore.getMediaType} is not supported in the media count`;
	}
}

function toFileSize(size: number): string {
	if (size === 0) {
		return '-';
	}
	return prettyBytes(size, { locale: localizationStore.getLanguageLocale?.bcp47Code || 'en-US' });
}

function mediaTypeToAllText(mediaType: PlexMediaType): string {
	switch (mediaType) {
		case PlexMediaType.Movie:
			return t('components.media-overview-bar.all-media-mode.movies');
		case PlexMediaType.TvShow:
			return t('components.media-overview-bar.all-media-mode.tv-shows');
		default:
			return t('general.error.unknown');
	}
}
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
