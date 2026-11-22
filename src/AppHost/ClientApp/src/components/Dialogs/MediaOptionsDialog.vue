<template>
	<QCardDialog
		width="800px"
		:name="DialogType.MediaOptionsDialog"
		:loading="false"
		close-button
		@opened="onOpen"
		@closed="onClosing">
		<template #title>
			{{
				$t('components.media-options-dialog.title')
			}}
		</template>
		<!--	Help text	-->
		<template #default>
			<HelpRow
				:col-label="colLabel"
				:col-content="colContent"
				disable-responsive
				:title="$t('help.media-options.hide-offline-servers.title')"
				:label="$t('help.media-options.hide-offline-servers.label')"
				:text="$t('help.media-options.hide-offline-servers.text')">
				<q-toggle
					v-model="settingsStore.generalSettings.hideMediaFromOfflineServers"
					color="red"
					data-cy="media-options-hide-offline-toggle"
					@update:model-value="onGeneralSettingChanged" />
			</HelpRow>
			<HelpRow
				:col-label="colLabel"
				:col-content="colContent"
				disable-responsive
				:title="$t('help.media-options.hide-owned-media.title')"
				:label="$t('help.media-options.hide-owned-media.label')"
				:text="$t('help.media-options.hide-owned-media.text')">
				<q-toggle
					v-model="settingsStore.generalSettings.hideMediaFromOwnedServers"
					color="red"
					data-cy="media-options-hide-owned-toggle"
					@update:model-value="onGeneralSettingChanged" />
			</HelpRow>
			<HelpRow
				:col-label="colLabel"
				:col-content="colContent"
				disable-responsive
				:title="$t('help.media-options.use-low-quality-poster-images.title')"
				:label="$t('help.media-options.use-low-quality-poster-images.label')"
				:text="$t('help.media-options.use-low-quality-poster-images.text')">
				<q-toggle
					v-model="settingsStore.generalSettings.useLowQualityPosterImages"
					color="red"
					@update:model-value="onGeneralSettingChanged" />
			</HelpRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get, breakpointsQuasar, useBreakpoints } from '@vueuse/core';
import { DialogType } from '@enums';
import { useSettingsStore } from '@store';
import type { ColLevels } from '@props';

const originalValues: Record<string, boolean> = {};

const telemetry = useTelemetry();
const settingsStore = useSettingsStore();
const breakpoints = useBreakpoints(breakpointsQuasar);

const colLabel = computed((): ColLevels => {
	if (get(breakpoints.smallerOrEqual('sm'))) {
		return 10;
	}

	return 7;
});
const colContent = computed((): ColLevels => {
	if (get(breakpoints.smallerOrEqual('sm'))) {
		return 2;
	}

	return 2;
});

function onOpen() {
	originalValues.hideMediaFromOfflineServers = settingsStore.generalSettings.hideMediaFromOfflineServers;
	originalValues.hideMediaFromOwnedServers = settingsStore.generalSettings.hideMediaFromOwnedServers;
	originalValues.useLowQualityPosterImages = settingsStore.generalSettings.useLowQualityPosterImages;
}
function onGeneralSettingChanged(): void {
	telemetry.generalSettingsChanged();
}
function onClosing() {
	const visibilityChanged = (
		originalValues.hideMediaFromOfflineServers !== settingsStore.generalSettings.hideMediaFromOfflineServers
		|| originalValues.hideMediaFromOwnedServers !== settingsStore.generalSettings.hideMediaFromOwnedServers
	);
	const hasChanged = (
		visibilityChanged
		|| originalValues.useLowQualityPosterImages !== settingsStore.generalSettings.useLowQualityPosterImages
	);

	emits('closed', hasChanged, visibilityChanged);
}

const emits = defineEmits<{
	(e: 'closed', hasChanged: boolean, visibilityChanged: boolean): void;
}>();
</script>
