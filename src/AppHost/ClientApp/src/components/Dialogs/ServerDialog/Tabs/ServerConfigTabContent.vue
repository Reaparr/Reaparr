<template>
	<HelpGroup v-if="plexServer">
		<!-- Enabled -->
		<HelpRow
			class="server-config-row"
			label="Enabled"
			title="Enable this Plex server"
			text="Disabled servers are excluded from queries/syncs and hidden from normal views.">
			<QToggle
				size="lg"
				:model-value="serverStore.getServer(plexServer.id)!.isEnabled"
				@click="dialogStore.openDialog(DialogType.ServerHideConfirmationDialog)" />
		</HelpRow>

		<!-- Owned -->
		<HelpRow
			class="server-config-row"
			label="Owned"
			title="Mark server as owned"
			text="Owned servers are excluded from Sonarr/Radarr source candidate lists.">
			<QToggle
				size="lg"
				:model-value="serverStore.getServer(plexServer.id)!.owned"
				@update:model-value="onServerOwnedChanged" />
		</HelpRow>

		<!-- Stream Downloader -->
		<HelpRow
			class="server-config-row"
			:label="$t('help.server-dialog.server-config.allow-stream-downloader.label')"
			:title="$t('help.server-dialog.server-config.allow-stream-downloader.title')"
			:text="$t('help.server-dialog.server-config.allow-stream-downloader.text')">
			<QToggle
				size="lg"
				:model-value="allowStreamDownloader"
				@update:model-value="onAllowStreamDownloaderChanged" />
		</HelpRow>

		<!-- Download Speed Limit -->
		<HelpRow
			class="server-config-row"
			:label="$t('help.server-dialog.server-config.download-speed-limit.label')"
			:title="$t('help.server-dialog.server-config.download-speed-limit.title')"
			:text="$t('help.server-dialog.server-config.download-speed-limit.text')">
			<DownloadLimitInput :machine-identifier="plexServer.machineIdentifier" />
		</HelpRow>
	</HelpGroup>

	<QAlert
		v-else
		type="error">
		{{ $t('components.server-dialog.tabs.server-config.plex-server-was-null') }}
	</QAlert>
	<!-- Hide Server Confirm Dialog -->
	<ConfirmationDialog
		:confirm-loading="confirmHideDialog"
		:name="DialogType.ServerHideConfirmationDialog"
		:title="$t('confirmation.disable-server.title')"
		:text="$t('confirmation.disable-server.text')"
		class="q-mr-md"
		@confirm="onDisableServer" />
</template>

<script setup lang="ts">
import Log from 'consola';
import type { PlexServerDTO } from '@dto';
import { set } from '@vueuse/core';
import { useDialogStore, useServerStore, useSettingsStore } from '@store';
import { DialogType } from '@enums';

const props = defineProps<{
	plexServer: PlexServerDTO | null;
}>();

const settingsStore = useSettingsStore();
const serverStore = useServerStore();
const confirmHideDialog = ref(false);
const dialogStore = useDialogStore();

const allowStreamDownloader = computed(
	() => settingsStore.getServerSettings(props.plexServer?.machineIdentifier)?.allowStreamDownloader ?? false,
);

function onAllowStreamDownloaderChanged(value: boolean) {
	if (!props.plexServer) {
		Log.error('props.plexServer is null');
		return;
	}

	settingsStore.updateAllowStreamDownloader(props.plexServer.machineIdentifier, value);
}

function onDisableServer() {
	if (!props.plexServer) {
		Log.error('props.plexServer is null');
		return;
	}

	set(confirmHideDialog, true);

	useSubscription(serverStore.setServerEnabled(props.plexServer.id, false).subscribe(() => {
		set(confirmHideDialog, false);
		dialogStore.closeDialog(DialogType.ServerHideConfirmationDialog);
		dialogStore.closeDialog(DialogType.ServerSettingsDialog);
	}));
}

function onServerOwnedChanged(value: boolean) {
	if (!props.plexServer) {
		Log.error('props.plexServer is null');
		return;
	}

	useSubscription(serverStore.setServerOwned(props.plexServer.id, value).subscribe());
}
</script>

<style lang="scss">
@media (min-width: $breakpoint-lg-min) {
  .server-config-row {
    > .help-row-label,
    > .help-row-default-slot {
      flex: 0 0 50%;
      max-width: 50%;
    }
  }
}
</style>
