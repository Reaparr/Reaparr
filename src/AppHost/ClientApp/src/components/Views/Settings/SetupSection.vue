<template>
	<QSection>
		<template #header>
			{{ $t('pages.settings.advanced.setup.header') }}
		</template>
		<HelpGroup>
			<!--	Go To Setup Page	-->
			<HelpRow
				:label="$t('help.settings.advanced.setup-section.go-to-setup-page.label')"
				:title="$t('help.settings.advanced.setup-section.go-to-setup-page.title')"
				:text="$t('help.settings.advanced.setup-section.go-to-setup-page.text')">
				<GoToButton
					class="settings-action-button"
					cy="go-to-setup-button"
					:label="$t('general.commands.go-to-setup-page')"
					to="/setup"
					block />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>
