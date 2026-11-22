<template>
	<QCardDialog
		:name="DialogType.DiscordServerInviteDialog"
		width="600px"
		close-button
		persistent
		button-align="center"
		@closed="onClosed">
		<template #title>
			{{ $t('components.discord-invite-dialog.header') }}
		</template>
		<template #default>
			<ul class="q-mt-none">
				<li>{{ $t('components.discord-invite-dialog.reasons.reason-1') }}</li>
				<li>{{ $t('components.discord-invite-dialog.reasons.reason-2') }}</li>
				<li>{{ $t('components.discord-invite-dialog.reasons.reason-3') }}</li>
				<li>{{ $t('components.discord-invite-dialog.reasons.reason-4') }}</li>
				<li>{{ $t('components.discord-invite-dialog.reasons.reason-5') }}</li>
			</ul>
		</template>
		<template #actions="{ close }">
			<BaseButton
				block

				@click="onInviteClick(close)">
				<QRow justify="center">
					<QCol cols="auto">
						<DiscordIcon class="q-mr-md" />
					</QCol>
					<QCol cols="auto">
						<QText :value="$t('components.discord-invite-dialog.discord-invite-button-text')" />
					</QCol>
				</QRow>
			</BaseButton>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { DialogType } from '@enums';
import { discordInviteLink } from '@composables';

const settingsStore = useSettingsStore();

function onInviteClick(close: () => void): void {
	window.open(discordInviteLink(), '_blank');
	settingsStore.generalSettings.hasBeenInvitedToDiscord = true;
	close();
}
function onClosed(): void {
	settingsStore.generalSettings.hasBeenInvitedToDiscord = true;
}
</script>
