<template>
	<q-list data-cy="media-overview-type-selector">
		<q-item
			v-for="type in overviewTypes.filter((candidate) => candidate !== selectedType)"
			:key="type"
			v-close-popup
			v-ripple
			clickable
			:data-cy="`media-overview-type-${type}`"
			@click="emit('select', type)">
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
</template>

<script setup lang="ts">
import { PlexMediaType } from '@dto';

defineProps<{
	selectedType: PlexMediaType;
}>();

const overviewTypes = [PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.MusicArtist, PlexMediaType.PhotoAlbum, PlexMediaType.OtherVideos];

const { t } = useI18n();

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

const emit = defineEmits<{
	(e: 'select', type: PlexMediaType): void;
}>();
</script>
