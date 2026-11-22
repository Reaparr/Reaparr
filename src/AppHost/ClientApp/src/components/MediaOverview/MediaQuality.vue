<template>
	<!-- Show all available qualities as chips -->
	<div
		v-if="displayQualities.length"
		class="media-quality-container">
		<!-- Show all available qualities as chips -->
		<template
			v-for="(quality, j) in displayQualities"
			:key="j">
			<MediaAudioQuality
				v-if="isAudioMediaType(quality.mediaDataType)"
				:quality="quality.audioQuality ?? AudioQuality.Unknown"
				:truncated="truncated"
				:count="displayQualities.length"
				:clickable="clickable"
				@click="$emit('download', [quality])" />
			<MediaVideoQuality
				v-else
				:quality="quality.videoQuality"
				:clickable="clickable"
				:truncated="truncated"
				:count="qualities.length"
				@click="$emit('download', [quality])" />
		</template>
	</div>
</template>

<script setup lang="ts">
import { AudioQuality, PlexMediaType, type PlexMediaQualityDTO } from '@dto';
import type { IMediaActionEmits } from '@interfaces';

const props = withDefaults(defineProps<{
	qualities: PlexMediaQualityDTO[];
	truncated?: boolean; // Whether to truncate the display of qualities
	// Whether the chips are clickable
	clickable?: boolean;
}>(), {
	truncated: true,
	clickable: false,
});

const displayQualities = computed(() => {
	const parentQualities = new Set<string>();
	return props.qualities.filter((quality) => {
		if (![PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum].includes(quality.mediaDataType))
			return true;

		const key = `${quality.mediaDataType}:${quality.audioQuality ?? AudioQuality.Unknown}`;
		if (parentQualities.has(key))
			return false;
		parentQualities.add(key);
		return true;
	});
});

function isAudioMediaType(mediaType: PlexMediaType): boolean {
	return [PlexMediaType.MusicArtist, PlexMediaType.MusicAlbum, PlexMediaType.MusicTrack].includes(mediaType);
}

defineEmits<IMediaActionEmits>();
</script>

<style lang="scss" scoped>
.media-quality-container {
  text-align: center;
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  justify-content: center;
}

@media (max-width: $breakpoint-sm-max) {
  .media-quality-container {
    min-width: 0;
    max-width: 100%;
    gap: clamp(0.2rem, 0.8vw, 0.4rem);
  }

  .media-quality-container > * {
    min-width: 0;
    max-width: 100%;
  }
}
</style>
