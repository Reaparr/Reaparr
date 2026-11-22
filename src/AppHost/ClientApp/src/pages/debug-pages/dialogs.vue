<template>
	<QPage>
		<QSection>
			<template #header>
				{{ t('pages.debug.dialogs.header') }}
			</template>
			<q-markup-table>
				<q-tr>
					<q-td>
						<DebugButton
							:label="$t('pages.debug.dialogs.buttons.server-dialog')"
							@click="dialogStore.openServerSettingsDialog(1)" />
						<ServerDialog />
					</q-td>
				</q-tr>
				<q-tr>
					<q-td>
						<DebugButton
							:label="$t('pages.debug.dialogs.buttons.download-confirmation')"
							@click="openDownloadConfirmationDialog" />
						<DownloadConfirmation />
					</q-td>
				</q-tr>
				<q-tr>
					<q-td>
						<DebugButton
							:label="$t('pages.debug.dialogs.buttons.help-dialog')"
							@click="openHelpDialog" />
					</q-td>
				</q-tr>
				<!-- Download Details Task -->
				<q-tr>
					<q-td>
						<DebugButton
							label="Download Details Task"
							@click="dialogStore.openDownloadTaskDetailsDialog('')" />
						<DownloadDetailsDialog />
					</q-td>
				</q-tr>
				<!-- Media Selection Dialog -->
				<q-tr>
					<q-td>
						<DebugButton
							label="Media Selection Dialog"
							@click="dialogStore.openDialog(DialogType.MediaSelectionDialog)" />
						<MediaSelectionDialog />
					</q-td>
				</q-tr>
				<q-tr>
					<q-td>
						<DebugButton
							:label="$t('general.commands.add-alert')"
							@click="addAlert" />
					</q-td>
				</q-tr>

				<q-tr>
					<q-td>
						<DebugButton
							:label="$t('general.commands.media-options')"
							@click="dialogStore.openDialog(DialogType.MediaOptionsDialog)" />
						<MediaOptionsDialog />
					</q-td>
				</q-tr>

				<q-tr>
					<q-td>
						<DebugButton
							data-cy="directory-browser-dialog-button"
							:label="$t('pages.debug.dialogs.buttons.directory-browser')"
							@click="dialogStore.openDirectoryBrowserDialog(folderPath)" />
						<DirectoryBrowser
							:saving="false"
							:error-message="''"
							@confirm="dialogStore.closeDialog(DialogType.DirectoryBrowserDialog)" />
					</q-td>
				</q-tr>

				<q-tr>
					<q-td>
						<DebugButton
							data-cy="directory-browser-dialog-button"
							:label="$t('pages.debug.dialogs.buttons.discord-invite')"
							@click="dialogStore.openDialog(DialogType.DiscordServerInviteDialog)" />
						<DirectoryBrowser
							:saving="false"
							:error-message="''"
							@confirm="dialogStore.closeDialog(DialogType.DirectoryBrowserDialog)" />
					</q-td>
				</q-tr>

				<q-tr>
					<q-td>
						<DebugButton
							data-cy="sync-server-media-dialog-button"
							:label="$t('pages.debug.dialogs.buttons.sync-server-media-dialog')"
							@click="dialogStore.openDialog(DialogType.SyncServerMediaDialog)" />
						<DirectoryBrowser
							:saving="false"
							:error-message="''"
							@confirm="dialogStore.closeDialog(DialogType.DirectoryBrowserDialog)" />
					</q-td>
				</q-tr>

				<q-tr>
					<q-td>
						<DebugButton
							data-cy="refresh-plex-account-access-dialog"
							:label="$t('pages.debug.dialogs.buttons.refresh-plex-account-access-dialog')"
							@click="dialogStore.openRefreshPlexAccountAccessDialog([])" />
						<DirectoryBrowser
							:saving="false"
							:error-message="''"
							@confirm="dialogStore.closeDialog(DialogType.DirectoryBrowserDialog)" />
					</q-td>
				</q-tr>
			</q-markup-table>
		</QSection>
		<!-- Account Dialogs -->
		<QSection>
			<template #header>
				{{ $t('pages.debug.dialogs.account-dialog-header') }}
			</template>
			<template #default>
				<q-markup-table>
					<!--	Account Dialog	-->
					<q-tr>
						<q-td>
							<DebugButton
								data-cy="directory-browser-dialog-button"
								label="Account Dialog"
								@click="dialogStore.openAccountDialog({ accountId: 0 })" />
							<AccountDialog />
						</q-td>
					</q-tr>
					<!-- 2FA Code Dialog -->
					<q-tr>
						<q-td>
							<DebugButton
								data-cy="open-verification-dialog-button"
								:label="$t('pages.debug.dialogs.buttons.verification-dialog')"
								@click="dialogStore.openDialog(DialogType.AccountVerificationCodeDialog)" />
						</q-td>
					</q-tr>
					<!-- Generate Account Token Dialog -->
					<q-tr>
						<q-td>
							<DebugButton
								cy="account-dialog-generate-token-button"
								:label="$t('components.account-dialog.generate-token-button')"
								@click="dialogStore.openDialog(DialogType.AccountGenerateTokenDialog)" />
						</q-td>
					</q-tr>
					<!-- Validate Account Dialog -->
					<q-tr>
						<q-td>
							<DebugButton
								cy="account-dialog-validate-button"
								label="Validate Account"
								@click="dialogStore.openDialog(DialogType.AccountTokenValidateDialog)" />
						</q-td>
					</q-tr>
					<!--	Delete Confirmation Dialog	-->
					<q-tr>
						<q-td>
							<DebugButton
								data-cy="account-dialog-delete-button"
								label="Delete Account Confirmation"
								@click="dialogStore.openDialog(DialogType.AccountConfirmationDialog)" />
							<ConfirmationDialog
								class="q-mr-md"
								:name="DialogType.AccountConfirmationDialog"
								:title="$t('confirmation.delete-account.title')"
								:text="$t('confirmation.delete-account.text')"
								:warning="$t('confirmation.delete-account.warning')" />
						</q-td>
					</q-tr>
				</q-markup-table>
			</template>
		</QSection>

		<!-- Connection Dialogs -->
		<QSection>
			<template #header>
				{{ $t('pages.debug.dialogs.connection-dialog-header') }}
			</template>
			<template #default>
				<q-markup-table>
					<q-tr>
						<q-td>
							<DebugButton
								data-cy="check-server-connections-dialog-button"
								:label="t('pages.debug.dialogs.buttons.check-server-connections-dialog')"
								@click="openCheckServerConnectionsDialog" />
						</q-td>
					</q-tr>
					<q-tr>
						<q-td>
							<DebugButton
								data-cy="add-connection-dialog-button"
								:label="$t('components.server-dialog.tabs.server-connections.add-connection-button')"
								@click="dialogStore.openAddConnectionDialog({ plexServerId: 1, plexServerConnectionId: 0 })" />
							<AddConnectionDialog />
						</q-td>
					</q-tr>
				</q-markup-table>
			</template>
		</QSection>
	</QPage>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { type DownloadMediaDTO, PlexMediaType } from '@dto';
import { generateDefaultFolderPaths } from '@factories';
import { DialogType } from '@enums';
import { useAlertStore, useHelpStore, useDialogStore } from '@store';

const folderPath = generateDefaultFolderPaths({})[0]!;

const { t } = useI18n();
const helpStore = useHelpStore();
const alertStore = useAlertStore();
const dialogStore = useDialogStore();

function openDownloadConfirmationDialog(): void {
	const demo: DownloadMediaDTO[] = [
		{
			plexServerId: 1,
			plexLibraryId: 9,
			mediaIds: [24, 25, 26, 27, 28],
			type: PlexMediaType.TvShow,
			qualities: [],
			keepCompletedInDownloadFolder: false,
		},
	];
	dialogStore.openMediaConfirmationDownloadDialog(demo);
}
function openHelpDialog(): void {
	helpStore.openHelpDialog({
		label: t('help.settings.ui.language.language-selection.label'),
		title: t('help.settings.ui.language.language-selection.title'),
		text: t('help.settings.ui.language.language-selection.text'),
	});
}
function openCheckServerConnectionsDialog(): void {
	dialogStore.openDialog(DialogType.CheckServerConnectionDialogName);
}
function addAlert(): void {
	alertStore.showAlert({ id: 0, title: 'Alert Title 1', text: 'random alert' });
	alertStore.showAlert({ id: 0, title: 'Alert Title 2', text: 'random alert' });
	alertStore.showAlert({ id: 0, title: 'Alert Title 3', text: 'random alert' });
	alertStore.showAlert({ id: 0, title: 'Alert Title 4', text: 'random alert' });
	alertStore.showAlert({ id: 0, title: 'Alert Title 5', text: 'random alert' });
}
</script>
