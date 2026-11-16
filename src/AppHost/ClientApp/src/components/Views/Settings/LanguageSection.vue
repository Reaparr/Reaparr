<template>
	<QSection :header="$t('pages.settings.ui.language.header')">
		<HelpGroup class="q-mt-md">
			<HelpRow
				:label="$t('help.settings.ui.language.language-selection.label')"
				:title="$t('help.settings.ui.language.language-selection.title')"
				:text="$t('help.settings.ui.language.language-selection.text')">
				<LanguageSelect @update:model-value="onLanguageChanged" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
const telemetry = useTelemetry();

function onLanguageChanged(): void {
	telemetry.languageSettingsChanged();
}
</script>
