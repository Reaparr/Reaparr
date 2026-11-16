<template>
	<QSection
		data-cy="settings-debug-section">
		<template #header>
			{{ t('pages.settings.advanced.debug.header') }}
		</template>
		<HelpGroup>
			<!--	Toggle Debug Mode	-->
			<HelpRow
				:label="$t('help.settings.advanced.debug-section.debug-mode.label')"
				:title="$t('help.settings.advanced.debug-section.debug-mode.title')"
				:text="$t('help.settings.advanced.debug-section.debug-mode.text')">
				<q-toggle
					v-model:model-value="settingsStore.debugSettings.debugModeEnabled"
					size="lg"
					@update:model-value="onDebugSettingChanged" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@store';

const telemetry = useTelemetry();
const { t } = useI18n();
const settingsStore = useSettingsStore();

function onDebugSettingChanged(): void {
	telemetry.debugSettingsChanged();
}
</script>
