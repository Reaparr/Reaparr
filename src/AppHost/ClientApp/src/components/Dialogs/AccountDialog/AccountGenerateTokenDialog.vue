<template>
	<QCardDialog
		:name="DialogType.AccountGenerateTokenDialog"
		persistent
		width="700px"
		cy="generate-token-dialog"
		@opened="onOpen">
		<template #title>
			{{ $t('components.account-token-validate-dialog.title') }}
		</template>
		<template #top-row>
			<QSubHeader
				v-if="accountDialogStore.isValidated"
				class="ph-no-capture">
				{{ $t('components.account-token-validate-dialog.sub-header',
					{ authToken: accountDialogStore.authenticationToken })
				}}
			</QSubHeader>
		</template>
		<template #default>
			<div>
				<QAlert
					v-if="errorMessage"
					cy="generate-token-error"
					type="error"
					role="alert">
					{{ errorMessage }}
				</QAlert>
				<!--	Verification Code input	-->
				<QRow
					v-if="needVerificationToken"
					justify="center">
					<QCol cols="auto">
						<VOtpInput
							v-model:value="verificationCodeValue"
							input-classes="otp-input"
							separator=""
							:num-inputs="6"
							:should-auto-focus="true"
							input-type="number"
							inputmode="numeric"
							data-cy="2fa-code-verification-input"
							:conditional-class="['one', 'two', 'three', 'four', 'five', 'six']"
							@on-complete="onComplete" />
					</QCol>
				</QRow>
				<q-input
					v-else
					:model-value="generatedToken"
					data-cy="generate-token-dialog-token-input"
					readonly>
					<template #append>
						<IconButton
							cy="generate-token-dialog-copy-button"
							icon="mdi-content-copy"
							@click="copyGeneratedToken" />
					</template>
				</q-input>
			</div>
		</template>
		<template #actions="{ close }">
			<QRow justify="end">
				<!--	Hide	-->
				<QCol cols="auto">
					<HideButton
						cy="generate-token-dialog-hide-button"
						@click="close" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import Log from 'consola';
import { get, set, useClipboard } from '@vueuse/core';
import { DialogType } from '@enums';
import { useAccountDialogStore } from '@store';
import VOtpInput from 'vue3-otp-input';
import { useSubscription } from '@vueuse/rxjs';

let latestRequestId = 0;

const generatedToken = ref<string>('');
const verificationCodeValue = ref<string>('');
const needVerificationToken = ref<boolean>(false);
const errorMessage = ref('');

const telemetry = useTelemetry();
const accountDialogStore = useAccountDialogStore();
const { copy } = useClipboard({ legacy: true });

function onOpen() {
	requestToken();
}
function onComplete(verificationCode: string) {
	requestToken(verificationCode);
	set(needVerificationToken, false);
	set(verificationCodeValue, '');
}
function copyGeneratedToken() {
	copy(get(generatedToken)).then(() => {
		telemetry.accountTokenCopied(accountDialogStore.id);
	}).catch((error) => Log.error('Failed to copy generated account token', error));
}
function requestToken(verificationCode: string = '') {
	const requestId = ++latestRequestId;
	set(errorMessage, '');
	set(generatedToken, '');
	useSubscription(accountDialogStore.generateToken(verificationCode).subscribe({
		next({ isSuccess, value, errors }) {
			if (requestId !== latestRequestId) {
				return;
			}

			if (!isSuccess) {
				set(errorMessage, errors.map((error) => error.message).join('\n'));
				return;
			}

			if (value?.needsVerificationCode) {
				set(needVerificationToken, true);
				return;
			}

			set(generatedToken, value?.plexAuthToken ?? '');
		},
	}));
}
</script>
