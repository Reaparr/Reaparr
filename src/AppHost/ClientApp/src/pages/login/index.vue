<template>
	<QPage>
		<QCard
			dark
			class="login-card">
			<QCardSection>
				<!-- Logo -->
				<QRow justify="center">
					<QCol cols="auto">
						<Logo :size="64" />
					</QCol>
				</QRow>
				<!-- Header -->
				<QRow justify="center">
					<QCol cols="auto">
						<QText
							size="h5"
							class="q-my-md"
							:value="$t('pages.login.header')" />
					</QCol>
				</QRow>
				<QRow
					class="login-options-row"
					justify="between">
					<QCol
						v-if="invalidCredentials || lockedOut"
						cols="12"
						class="q-my-md">
						<!-- Invalid Credentials Error -->
						<QAlert
							v-if="invalidCredentials"
							type="error"
							class="q-ma-none"
							cy="login-invalid-credentials-alert">
							{{ $t('pages.login.invalid-credentials') }}
						</QAlert>
						<!-- Locked Out Error -->
						<QAlert
							v-if="lockedOut"
							type="error"
							cy="login-locked-out-alert">
							{{ $t('pages.login.locked-out') }}
						</QAlert>
					</QCol>
					<QCol cols="12">
						<!-- Username Login Field -->
						<q-input
							v-model="username"
							color="red"
							full-width
							outlined
							required
							hide-bottom-space
							data-cy="login-username-input" />
					</QCol>
					<QCol cols="12">
						<!-- Password Login Field -->
						<PasswordInputField
							v-model="password"
							disable-validation
							class="q-my-md"
							cy="login-password-input" />
					</QCol>
					<QCol cols="6">
						<!-- Remember Me -->
						<q-checkbox
							v-model="rememberMe"
							data-cy="login-remember-me-input"
							:label="$t('pages.login.remember-me')" />
					</QCol>
					<QCol cols="auto">
						<!-- Remember Me -->
						<ExternalLink
							href="https://www.reaparr.rocks/docs/faq#forgot-creds"
							class="link">
							<QText :value="$t('pages.login.forgot-your-password')" />
						</ExternalLink>
					</QCol>
				</QRow>
			</QCardSection>

			<QCardActions
				align="center"
				class="q-pt-none">
				<BaseButton
					block
					cy="login-submit-button"
					label="Login"
					class="login-button"
					@click="onLogin" />
			</QCardActions>
		</QCard>
	</QPage>
</template>

<script setup lang="ts">
import Log from 'consola';
import { get, set } from '@vueuse/core';
import { useAuthenticationStore } from '@store';
import { useSubscription } from '@vueuse/rxjs';

const authStore = useAuthenticationStore();
const username = ref('');
const password = ref('');
const rememberMe = ref(false);
const invalidCredentials = ref(false);
const lockedOut = ref(false);

function onLogin() {
	useSubscription(authStore.login(get(username), get(password), get(rememberMe)).subscribe((statusCode) => {
		Log.info('Login Status Code:', statusCode);
		set(invalidCredentials, statusCode === 401);
		set(lockedOut, statusCode === 403);
	}));
}
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

.login-card {
  @extend .background-md;
  margin: 6rem auto 0;
  max-width: 400px;

  .login-button {
    margin: 0 0.5rem 0.5rem;
  }
}
@media (max-width: $breakpoint-sm-max) {
  .login-card {
    width: min(400px, calc(100% - 2rem));
    margin-top: clamp(1rem, 10vh, 6rem);
  }
}

@media (max-width: $breakpoint-xs-max) {
  .login-card .row {
    gap: 0.5rem;
  }

  .login-options-row .col-6,
  .login-options-row .col-auto {
    width: 100%;
    max-width: 100%;
    flex: 1 1 100%;
  }
}
</style>
