<template>
	<q-form
		ref="accountForm"
		greedy
		autofocus
		autocapitalize="off"
		spellcheck="false"
		@validation-success="setValidationState(true)"
		@validation-error="setValidationState(false)">
		<HelpGroup>
			<!-- Is account enabled -->
			<HelpRow
				:label="$t('help.account-form.is-enabled.label')"
				:title="$t('help.account-form.is-enabled.title')"
				:text="$t('help.account-form.is-enabled.text')">
				<q-toggle
					v-model="accountDialogStore.isEnabled"
					class="q-ma-sm pt-0"
					color="red"
					data-cy="account-form-is-enabled" />
			</HelpRow>

			<!-- Is main account -->
			<HelpRow
				:label="$t('help.account-form.is-main.label')"
				:title="$t('help.account-form.is-main.title')"
				:text="$t('help.account-form.is-main.text')">
				<q-toggle
					v-model="accountDialogStore.isMain"
					class="q-ma-sm pt-0"
					color="red"
					data-cy="account-form-is-main" />
			</HelpRow>

			<!-- Display Name -->
			<HelpRow
				:label="$t('help.account-form.display-name.label')"
				:title="$t('help.account-form.display-name.title')"
				:text="$t('help.account-form.display-name.text')">
				<q-input
					id="plex-display-name"
					v-model="accountDialogStore.displayName"
					:rules="getDisplayNameRules"
					color="red"
					full-width
					outlined
					required
					hide-bottom-space
					name="plex-display-name"
					autocomplete="name"
					data-cy="account-form-display-name-input" />
			</HelpRow>
		</HelpGroup>
		<q-tabs
			v-model="tab"
			class="q-my-lg"
			align="justify">
			<q-tab
				data-cy="account-dialog-credentials-mode-button"
				:name="credentialsTab"
				:label="$t('components.account-form.credentials-tab')"
				:disable="!accountDialogStore.isNewAccount && !!accountDialogStore.customAuthenticationToken" />
			<q-tab
				data-cy="account-dialog-auth-token-mode-button"
				:name="tokenTab"
				:label="$t('components.account-form.token-tab')"
				:disable="!accountDialogStore.isNewAccount && !accountDialogStore.customAuthenticationToken" />
		</q-tabs>
		<q-tab-panels
			v-model="tab"
			animated>
			<q-tab-panel
				:name="credentialsTab"
				class="account-dialog-panel">
				<HelpGroup>
					<!-- Username -->
					<HelpRow
						:label="$t('help.account-form.username.label')"
						:title="$t('help.account-form.username.title')"
						:text="$t('help.account-form.username.text')">
						<q-input
							id="plex-username"
							v-model="accountDialogStore.username"
							:rules="getUsernameRules"
							color="red"
							full-width
							outlined
							required
							hide-bottom-space
							name="plex-username"
							autocomplete="username"
							data-cy="account-form-username-input" />
					</HelpRow>

					<!-- Password -->
					<HelpRow
						:label="$t('help.account-form.password.label')"
						:title="$t('help.account-form.password.title')"
						:text="$t('help.account-form.password.text')">
						<PasswordInputField
							id="plex-password"
							v-model="accountDialogStore.password"
							disable-validation
							class="q-my-md"
							name="plex-password"
							cy="account-form-password-input" />
					</HelpRow>
				</HelpGroup>
			</q-tab-panel>

			<q-tab-panel
				:name="tokenTab"
				class="account-dialog-panel">
				<HelpGroup>
					<!-- Plex Token -->
					<HelpRow
						:label="$t('help.account-form.auth-token.label')"
						:title="$t('help.account-form.auth-token.title')"
						:text="$t('help.account-form.auth-token.text')">
						<PasswordInputField
							v-model="accountDialogStore.customAuthenticationToken"
							:rules="getAuthTokenRules"
							class="q-my-md"
							name="plex-auth-token"
							cy="account-form-auth-token-input" />
					</HelpRow>
				</HelpGroup>
			</q-tab-panel>
		</q-tab-panels>
	</q-form>
</template>

<script setup lang="ts">
import { useAccountDialogStore } from '@store';

const { t } = useI18n();
const accountDialogStore = useAccountDialogStore();

const tokenTab = 'token';
const credentialsTab = 'credentials';

const tab = computed({
	get: () => accountDialogStore.isAuthTokenMode ? tokenTab : credentialsTab,
	set: (value: string) => accountDialogStore.switchInputMode(value === tokenTab),
});

const getDisplayNameRules = computed(() => [
	(v: string): boolean | string => !!v || t('components.account-form.validation.display-name-required'),
	(v: string): boolean | string => (v && v.length >= 4) || t('components.account-form.validation.display-name-length', {
		count: 4,
	}),
]);

const getUsernameRules = computed(() => [(v: string): boolean | string => !!v || t('components.account-form.validation.username-is-required')]);

const getAuthTokenRules = computed(() => [
	(v: string): boolean | string => !!v || t('components.account-form.validation.auth-token-required'),
	(v: string): boolean | string => (v && v.length == 20) || t('components.account-form.validation.auth-token-length', {
		count: 20,
	}),
	(v: string): boolean | string => (v && /^[a-zA-Z0-9_-]+$/.test(v)) || t('components.account-form.validation.auth-token-format'),
]);

function setValidationState(state: boolean) {
	accountDialogStore.$patch({
		isInputValid: state,
	});
}
</script>

<style lang="scss">
.account-dialog-panel {
  min-height: 11rem;
}
</style>
