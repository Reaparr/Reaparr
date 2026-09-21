<template>
	<QSection>
		<template #header>
			{{ $t('pages.settings.advanced.database.header') }}
		</template>
		<HelpGroup>
			<!--	Reset Database	-->
			<HelpRow
				:label="$t('help.settings.advanced.reset-db.label')"
				:title="$t('help.settings.advanced.reset-db.title')"
				:text="$t('help.settings.advanced.reset-db.text')">
				<WarningButton
					class="settings-action-button"
					cy="reset-database-button"
					:label="$t('general.commands.reset-db')"
					block
					@click="dialogStore.openDialog(DialogType.ResetDatabaseConfirmationDialog)" />
				<ConfirmationDialog
					:title="$t('confirmation.reset-db.title')"
					:text="$t('confirmation.reset-db.text')"
					:name="DialogType.ResetDatabaseConfirmationDialog"
					@confirm="resetDatabaseCommand" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import { settingsApi } from '@api';
import { DialogType } from '@enums';
import { useDialogStore, useRouter } from '#imports';

const router = useRouter();

const dialogStore = useDialogStore();

const resetDatabaseCommand = (): void => {
	useSubscription(
		settingsApi.resetDatabaseEndpoint().subscribe((value) => {
			if (value.isSuccess) {
				router.push('/setup').then(() => {
					// Refresh the page when we go to the home page to make sure we get all new data.
					location.reload();
				});
			}
		}),
	);
};
</script>
