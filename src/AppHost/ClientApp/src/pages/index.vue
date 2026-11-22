<template>
	<QPage>
		<PhotoBrowser
			v-if="isPhotoHome"
			:library-id="0" />
		<MediaOverview
			v-else
			:library-id="0" />
	</QPage>
</template>

<script setup lang="ts">
import { PlexMediaType } from '@dto';
import { useSettingsStore } from '@store';

const settingsStore = useSettingsStore();

const isPhotoHome = computed(() => settingsStore.displaySettings.allOverviewViewMode === PlexMediaType.PhotoAlbum);
</script>
