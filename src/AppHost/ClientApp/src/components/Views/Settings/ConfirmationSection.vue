<template>
	<QSection :header="$t('pages.settings.ui.confirmation-settings.header')">
		<HelpGroup class="q-mt-md">
			<!--	Ask Download Movie Confirmation	-->
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-movie-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-movie-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-movie-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadMovieConfirmation"
					size="lg"
					data-cy="ask-download-movie-confirmation" />
			</HelpRow>
			<!--	Ask Download TvShow Confirmation	-->
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-tvshow-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-tvshow-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-tvshow-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadTvShowConfirmation"
					size="lg"
					data-cy="ask-download-tvshow-confirmation" />
			</HelpRow>
			<!--	Ask Download Season Confirmation	-->
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-season-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-season-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-season-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadSeasonConfirmation"
					size="lg"
					data-cy="ask-download-season-confirmation" />
			</HelpRow>
			<!--	Ask Download Episode Confirmation	-->
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-episode-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-episode-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-episode-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadEpisodeConfirmation"
					size="lg"
					data-cy="ask-download-episode-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-music-artist-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-music-artist-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-music-artist-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadMusicArtistConfirmation"
					size="lg"
					data-cy="ask-download-music-artist-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-music-album-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-music-album-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-music-album-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadMusicAlbumConfirmation"
					size="lg"
					data-cy="ask-download-music-album-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-music-track-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-music-track-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-music-track-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadMusicTrackConfirmation"
					size="lg"
					data-cy="ask-download-music-track-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-photo-album-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-photo-album-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-photo-album-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadPhotoAlbumConfirmation"
					size="lg"
					data-cy="ask-download-photo-album-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-photo-image-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-photo-image-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-photo-image-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadPhotoImageConfirmation"
					size="lg"
					data-cy="ask-download-photo-image-confirmation" />
			</HelpRow>
			<HelpRow
				:label="$t('help.settings.ui.confirmation-settings.download-other-videos-confirmation.label')"
				:title="$t('help.settings.ui.confirmation-settings.download-other-videos-confirmation.title')"
				:text="$t('help.settings.ui.confirmation-settings.download-other-videos-confirmation.text')">
				<q-toggle
					v-model:model-value="settingsStore.confirmationSettings.askDownloadOtherVideosConfirmation"
					size="lg"
					data-cy="ask-download-other-videos-confirmation" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@store';

const settingsStore = useSettingsStore();
</script>
