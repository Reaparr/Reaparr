<template>
	<QCardDialog
		:name="DialogType.AccountVerificationCodeDialog"
		persistent
		width="640px"
		cy="2fa-code-verification-dialog">
		<template #title>
			{{ t('components.account-verification-code-dialog.title') }}
		</template>
		<template #top-row>
			<QSubHeader>{{ t('components.account-verification-code-dialog.sub-title') }}</QSubHeader>
		</template>
		<template #default>
			<div>
				<!--	Verification Code input	-->
				<QRow justify="center">
					<QCol cols="auto">
						<VOtpInput
							id="verification-code"
							ref="otpInput"
							v-model:value="accountDialogStore.verificationCode"
							input-classes="otp-input"
							separator=""
							:num-inputs="6"
							:should-auto-focus="true"
							input-type="number"
							inputmode="numeric"
							name="verification-code"
							autocomplete="one-time-code"
							data-cy="2fa-code-verification-input"
							:conditional-class="['one', 'two', 'three', 'four', 'five', 'six']"
							@on-complete="onComplete" />
					</QCol>
				</QRow>
				<QRow
					v-if="errors.length > 0"
					data-cy="2fa-code-verification-error"
					justify="center">
					<QCol cols="auto">
						<span style="color: red; font-weight: bold">
							{{
								$t('components.account-verification-code-dialog.error')
							}}
						</span>
					</QCol>
				</QRow>
			</div>
		</template>
		<template #actions="{ close }">
			<QRow justify="between">
				<!-- Close	-->
				<QCol cols="auto">
					<CancelButton @click="close" />
				</QCol>
				<!--	Confirm	-->
				<QCol cols="auto">
					<ConfirmButton
						cy="2fa-code-verification-confirm-button"
						:loading="loading"
						:disabled="accountDialogStore.verificationCode.length < 6"
						@click="onComplete" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import VOtpInput from 'vue3-otp-input';
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { DialogType } from '@enums';
import { useAccountDialogStore, useI18n } from '#imports';
import type { ErrorDTO } from '@dto';

const { t } = useI18n();

const accountDialogStore = useAccountDialogStore();

const otpInput = ref<InstanceType<typeof VOtpInput>>();

const loading = ref(false);
const errors = ref<ErrorDTO[]>([]);

function onComplete() {
	set(errors, []);
	set(loading, true);
	useSubscription(
		accountDialogStore.validateVerificationCode().subscribe({
			error(err) {
				set(errors, err);
			},
			complete: () => {
				set(loading, false);
				if (accountDialogStore.hasValidationErrors) {
					set(errors, [
						{
							message: t('components.account-verification-code-dialog.error'),
						} as ErrorDTO,
					]);
					get(otpInput)?.clearInput();
				}
			},
		}),
	);
}
</script>

<style lang="scss">
@use '@/assets/scss/mixins.scss';

.otp-input {
  @extend .default-border;
  @extend .background-sm;
  width: 80px;
  height: 80px;
  padding: 5px;
  margin: 10px;
  font-size: 40px;
  border-radius: 4px;
  text-align: center;
  /* Background colour of an input field with value */
  &.is-complete {
    @extend .success-border;
    background-color: #ff0000;
  }
}

.otp-input::-webkit-inner-spin-button,
.otp-input::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

input::placeholder {
  font-size: 35px;
  text-align: center;
  font-weight: 600;
}

@media (max-width: $breakpoint-sm-max) {
  .otp-input {
    width: min(80px, calc((100vw - 64px) / 6));
    height: min(80px, calc((100vw - 64px) / 6));
    margin: clamp(1px, 0.4vw, 10px);
    font-size: clamp(1.5rem, 1.15rem + 1.4vw, 2.5rem);
  }

  input::placeholder {
    font-size: clamp(1.75rem, 1.35rem + 1.8vw, 2.1875rem);
  }
}
</style>
