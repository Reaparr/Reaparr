<template>
	<HelpRow
		class="server-command-row"
		:label="$t('help.server-dialog.server-commands.inspect-server.label')"
		:title="$t('help.server-dialog.server-commands.inspect-server.title')"
		:text="$t('help.server-dialog.server-commands.inspect-server.text')">
		<BaseButton
			:disabled="syncLoading || deleteLoading"
			class="server-command-button"
			:loading="inspectLoading"
			:label="$t('general.commands.inspect-server')"
			@click="inspectServer" />
	</HelpRow>
	<HelpRow
		class="server-command-row"
		:label="$t('help.server-dialog.server-commands.sync-server-libraries.label')"
		:title="$t('help.server-dialog.server-commands.sync-server-libraries.title')"
		:text="$t('help.server-dialog.server-commands.sync-server-libraries.text')">
		<BaseButton
			:disabled="inspectLoading || deleteLoading"
			class="server-command-button"
			:loading="syncLoading"
			:label="$t('general.commands.sync-server-libraries')"
			@click="dialogStore.openDialog(DialogType.RefreshMediaDialog)" />
	</HelpRow>
	<HelpRow
		class="server-command-row"
		:label="$t('help.server-dialog.server-commands.delete-server.label')"
		:title="$t('help.server-dialog.server-commands.delete-server.title')"
		:text="$t('help.server-dialog.server-commands.delete-server.text')">
		<DeleteButton
			:disabled="inspectLoading || syncLoading"
			:loading="deleteLoading"
			class="server-command-button"
			:label="$t('general.commands.delete-server')"
			@click="dialogStore.openDialog(DialogType.ServerDeleteConfirmationDialog)" />
	</HelpRow>
	<RefreshModeDialog
		scope="server"
		:server-name="serverStore.getServerName(plexServerId)"
		@select="syncServerLibraries" />
	<ConfirmationDialog
		:confirm-loading="deleteLoading"
		:disable-cancel="deleteLoading"
		:name="DialogType.ServerDeleteConfirmationDialog"
		:title="$t('confirmation.delete-server.title')"
		:text="$t('confirmation.delete-server.text')"
		:warning="$t('confirmation.delete-server.warning')"
		:confirm-label="$t('general.commands.delete-server')"
		class="q-mr-md"
		@confirm="deleteServer" />
</template>

<script setup lang="ts">
import { set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { DialogType } from '@enums';
import { plexServerApi } from '@api';
import { useDialogStore, useServerStore } from '@store';
import { ref, onUnmounted } from '#imports';

const dialogStore = useDialogStore();
const serverStore = useServerStore();

const props = defineProps<{
	plexServerId: number;
	isVisible: boolean;
}>();

const syncLoading = ref(false);
const inspectLoading = ref(false);
const deleteLoading = ref(false);

function syncServerLibraries(forceMediaRefresh: boolean): void {
	const plexServerId = props.plexServerId;
	set(syncLoading, true);
	useSubscription(
		plexServerApi
			.syncPlexServerMediaEndpoint(plexServerId, {
				forceLibrarySync: true,
				forceMediaRefresh,
			})
			.subscribe({
				next: (result) => {
					set(syncLoading, false);
					if (!result.isSuccess) {
						return;
					}
					dialogStore.closeDialog(DialogType.ServerSettingsDialog);
					dialogStore.openSyncServerMediaDialog(plexServerId);
				},
				error: () => {
					set(syncLoading, false);
				},
			}),
	);
}

function inspectServer(): void {
	set(inspectLoading, true);
	useSubscription(
		plexServerApi.queueInspectPlexServerJobEndpoint(props.plexServerId).subscribe({
			next: () => {
				set(inspectLoading, false);
			},
			error: () => {
				set(inspectLoading, false);
			},
		}),
	);
}

function deleteServer(): void {
	set(deleteLoading, true);
	useSubscription(
		serverStore.deleteServer(props.plexServerId).subscribe({
			next: (result) => {
				set(deleteLoading, false);
				if (!result.isSuccess) {
					return;
				}
				dialogStore.closeDialog(DialogType.ServerDeleteConfirmationDialog);
				dialogStore.closeDialog(DialogType.ServerSettingsDialog);
			},
			error: () => {
				set(deleteLoading, false);
			},
		}),
	);
}

onUnmounted(() => {
	set(syncLoading, false);
	set(inspectLoading, false);
	set(deleteLoading, false);
});
</script>

<style lang="scss">
@media (max-width: $breakpoint-sm-max) {
  .server-command-button {
    min-height: 44px;
  }
}

@media (min-width: $breakpoint-lg-min) {
  .server-command-row {
    > .help-row-label,
    > .help-row-default-slot {
      flex: 0 0 50%;
      max-width: 50%;
    }
  }
}
</style>
