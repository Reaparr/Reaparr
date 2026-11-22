<template>
	<QCardDialog
		:name="DialogType.AccountDialog"
		persistent
		:type="{} as IAccountDialog"
		cy="account-dialog-form"
		close-button
		@opened="accountDialogStore.openDialog"
		@closed="accountDialogStore.closeDialog">
		<!-- Dialog Header -->
		<template #title>
			<QRow>
				<QCol>
					<QText
						size="h5"
						bold="bold">
						{{ getDialogHeader }}
					</QText>
				</QCol>
			</QRow>
		</template>
		<template #default>
			<div>
				<AccountForm />
			</div>
		</template>
		<!-- Dialog Actions	-->
		<template #actions>
			<div
				class="account-dialog-actions"
				:class="{
					'account-dialog-actions--new': accountDialogStore.isNewAccount,
					'account-dialog-actions--token': !accountDialogStore.isNewAccount && accountDialogStore.isAuthTokenMode,
				}">
				<div
					v-if="!accountDialogStore.isNewAccount"
					class="account-dialog-actions__group">
					<!-- Delete account -->
					<DeleteButton
						cy="account-dialog-delete-button"
						@click="dialogStore.openDialog(DialogType.AccountConfirmationDialog)" />
					<BaseButton
						v-if="!accountDialogStore.isNewAccount && !accountDialogStore.isAuthTokenMode"
						cy="account-dialog-generate-token-button"
						icon="mdi-key-plus"
						:label="$t('components.account-dialog.generate-token-button')"
						@click="dialogStore.openDialog(DialogType.AccountGenerateTokenDialog)" />
				</div>
				<div class="account-dialog-actions__group">
					<!-- Validation button -->
					<AccountValidationButton
						:color="validationStyle.color"
						:icon="validationStyle.icon"
						:label="validationStyle.text"
						:aria-label="validationStyle.text || $t('general.commands.validated')"
						:loading="accountDialogStore.validateLoading"
						:disabled="accountDialogStore.validateLoading"
						cy="account-dialog-validate-button"
						@click="!accountDialogStore.isAuthTokenMode ? validatePlexAccount() : validatePlexToken()" />
					<!-- Save account -->
					<SaveButton
						:disabled="!accountDialogStore.isAllowedToSave"
						:label="accountDialogStore.isNewAccount ? $t('general.commands.save') : $t('general.commands.update')"
						:cy="`account-dialog-${accountDialogStore.isNewAccount ? 'save' : 'update'}-button`"
						:loading="accountDialogStore.savingLoading"
						@click="saveAccount" />
				</div>
			</div>
		</template>
	</QCardDialog>

	<!--	Account Verification Code Dialog	-->
	<AccountVerificationCodeDialog />

	<!--	Account Token Validate Dialog	-->
	<AccountTokenValidateDialog />

	<!--	Account Generate Token Dialog	-->
	<AccountGenerateTokenDialog />

	<!--	Delete Confirmation Dialog	-->
	<ConfirmationDialog
		class="q-mr-md"
		:confirm-loading="accountDialogStore.deleteLoading"
		:name="DialogType.AccountConfirmationDialog"
		:title="$t('confirmation.delete-account.title')"
		:text="$t('confirmation.delete-account.text')"
		:warning="$t('confirmation.delete-account.warning')"
		@confirm="deleteAccount" />
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import type { IAccountDialog } from '@interfaces';
import { DialogType } from '@enums';
import { useAccountDialogStore } from '@store';
import { useDialogStore, useI18n } from '#imports';

const { t } = useI18n();
const accountDialogStore = useAccountDialogStore();
const dialogStore = useDialogStore();

const getDialogHeader = computed(() => {
	const displayName = accountDialogStore.displayName;
	let title: string;
	if (accountDialogStore.isNewAccount) {
		title = t('components.account-dialog.add-account-title', {
			name: displayName,
		});
	} else {
		title = t('components.account-dialog.edit-account-title', {
			name: displayName,
		});
	}
	// Remove the colon if the display name is empty
	return displayName ? title : title.replace(':', '');
});
const validationStyle = computed(
	(): {
		color: 'default' | 'positive' | 'warning' | 'negative';
		icon: string;
		text: string;
	} => {
		if (accountDialogStore.hasValidationErrors) {
			return {
				color: 'negative',
				icon: 'mdi-alert-circle-outline',
				text: t('general.commands.validate'),
			};
		}
		if (accountDialogStore.isValidated && !accountDialogStore.hasValidationErrors) {
			return {
				color: 'positive',
				icon: 'mdi-check-bold',
				text: '',
			};
		}
		return {
			color: 'default',
			icon: 'mdi-text-box-search-outline',
			text: t('general.commands.validate'),
		};
	},
);

function validatePlexAccount() {
	useSubscription(accountDialogStore.validatePlexAccount().subscribe());
}
function validatePlexToken() {
	useSubscription(accountDialogStore.validatePlexToken().subscribe());
}
function deleteAccount() {
	useSubscription(accountDialogStore.deleteAccount().subscribe());
}
function saveAccount() {
	useSubscription(accountDialogStore.saveAccount().subscribe());
}
</script>

<style lang="scss">
.account-dialog-actions {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  width: 100%;

  &__group {
    display: flex;
    gap: 0.5rem;
  }

  .q-btn {
    min-height: 44px;
  }
}

@media (min-width: $breakpoint-sm-min) {
  .account-dialog-actions {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));

    &--new {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    &--token {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    &__group {
      display: contents;
    }

    .q-btn {
      width: 100%;
      min-width: 0;
    }

    .q-btn__content {
      min-width: 0;
      white-space: normal;
    }
  }
}

@media (max-width: $breakpoint-xs-max) {
  .account-dialog-actions {
    flex-wrap: wrap;
    justify-content: flex-start;

    &__group {
      flex: 1 1 100%;
      flex-wrap: wrap;
      min-width: 0;
    }

    .q-btn {
      flex: 0 1 auto;
      max-width: 100%;
      min-width: 44px;
      min-height: 44px;
      padding-inline: 16px;
    }

    .q-btn__content {
      min-width: 0;
      white-space: normal;
    }
  }
}
</style>
