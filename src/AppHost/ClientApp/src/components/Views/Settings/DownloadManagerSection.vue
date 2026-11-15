<template>
	<QSection>
		<template #header>
			{{ $t('pages.settings.advanced.download-manager.header') }}
		</template>
		<HelpGroup>
			<!--	Max segmented downloads	-->
			<HelpRow
				:label="$t('help.settings.advanced.download-manager-section.download-segments.label')"
				:title="$t('help.settings.advanced.download-manager-section.download-segments.title')"
				:text="$t('help.settings.advanced.download-manager-section.download-segments.text')">
				<QSlider
					:model-value="settingsStore.downloadManagerSettings.downloadSegments"
					label
					label-always
					:min="1"
					:max="8"
					:step="1"
					class="q-mt-lg"
					@change="onDownloadSegmentsChanged($event)" />
			</HelpRow>
			<!-- Keep completed in download folder -->
			<HelpRow
				:label="$t('help.settings.advanced.download-manager-section.keep-in-downloads.label')"
				:title="$t('help.settings.advanced.download-manager-section.keep-in-downloads.title')"
				:text="$t('help.settings.advanced.download-manager-section.keep-in-downloads.text')">
				<QToggle
					:model-value="settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder"
					@update:model-value="onKeepCompletedChanged($event)" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@store';

const telemetry = useTelemetry();
const settingsStore = useSettingsStore();

function onDownloadSegmentsChanged(value: number): void {
	settingsStore.downloadManagerSettings.downloadSegments = value;
	telemetry.downloadManagerSettingsChanged();
}

function onKeepCompletedChanged(value: boolean): void {
	settingsStore.downloadManagerSettings.keepCompletedInDownloadFolder = value;
	telemetry.downloadManagerSettingsChanged();
}
</script>
