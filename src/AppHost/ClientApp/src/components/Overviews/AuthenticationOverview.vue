<template>
	<HelpRow
		:label="$t('help.settings.accounts.app-username.label')"
		:title="$t('help.settings.accounts.app-username.title')"
		:text="$t('help.settings.accounts.app-username.text')">
		<q-input
			v-model="authStore.username"
			:disable="loading"
			:rules="getUsernameRules"
			color="red"
			full-width
			outlined
			required
			hide-bottom-space
			name="reaparr-username"
			autocomplete="reaparr-username"
			data-cy="app-username-input" />
	</HelpRow>
	<!-- Password Input -->
	<HelpRow
		:label="$t('help.settings.accounts.app-password.label')"
		:title="$t('help.settings.accounts.app-password.title')"
		:text="$t('help.settings.accounts.app-password.text')">
		<PasswordInputField
			v-model="authStore.password"
			v-model:has-focus="passwordInputFocus"
			:disable="loading"
			hide-mask-button
			show-strength
			name="reaparr-password"
			autocomplete="reaparr-password"
			cy="app-password-input" />
	</HelpRow>

	<!-- Password Strength -->
	<HelpRow
		v-if="passwordInputFocus"
		hide-label>
		<PasswordStrength
			v-model:is-valid="authStore.isPasswordValid"
			:value="authStore.password" />
	</HelpRow>

	<!-- Confirm Password -->
	<HelpRow
		:label="$t('help.settings.accounts.app-confirm-password.label')">
		<PasswordInputField
			v-model="authStore.confirmPassword"
			:disable="loading"
			hide-mask-button
			cy="app-confirm-password-input"
			@blur="confirmPasswordLostFocus = true" />
	</HelpRow>

	<!-- Password Equality -->
	<HelpRow
		v-if="authStore.hasPasswordChanged && confirmPasswordLostFocus && !authStore.equalPassword"
		hide-label
		:label="$t('help.settings.accounts.app-confirm-password.label')">
		<QAlert
			cy="password-equality-alert"
			type="error">
			{{ $t('components.authentication-overview.validation.passwords-are-not-equal') }}
		</QAlert>
	</HelpRow>

	<HelpRow hide-label>
		<ValidationButton
			class="q-pa-md"
			block
			cy="save-credentials-button"
			default-icon="mdi-content-save"
			:is-validated="isValid"
			:loading="loading"
			:label="$t('general.commands.save')"
			:disabled="loading || !authStore.canUpdateCredentials"
			@click="onUpdateCredentials" />
	</HelpRow>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useAuthenticationStore } from '@store';
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { finalize } from 'rxjs';

const { t } = useI18n();
const authStore = useAuthenticationStore();

const loading = ref(true);
const isValid = ref(false);
const passwordInputFocus = ref(false);
const confirmPasswordLostFocus = ref(false);

const getUsernameRules = computed(() => [
	(v: string): boolean | string => !!v || t('components.authentication-overview.validation.username-is-required'),
	(v: string): boolean | string => (v && v.length >= 8) || t('components.authentication-overview.validation.username-length', {
		count: 8,
	}),
]);

function onUpdateCredentials() {
	if (get(loading) || !authStore.canUpdateCredentials) return;
	set(loading, true);
	set(isValid, false);
	useSubscription(authStore.updateCredentials().pipe(
		finalize(() => set(loading, false)),
	).subscribe({
		next: (result) => {
			set(isValid, result.isSuccess);
		},
		error: () => {
			set(isValid, false);
		},
	}));
}

onMounted(() =>
	useSubscription(authStore.refreshCredentials().pipe(
		finalize(() => set(loading, false)),
	).subscribe({
		error: () => set(isValid, false),
	})),
);
</script>
