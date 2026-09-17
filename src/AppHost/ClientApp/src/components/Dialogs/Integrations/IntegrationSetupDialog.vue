<template>
	<QCardDialog
		:name="DialogType.IntegrationSetupDialog"
		width="520px"
		persistent
		close-button
		cy="integration-setup-dialog"
		@opened="open"
		@closed="close">
		<template #title>
			<div class="row items-center q-gutter-sm">
				<QImg
					:src="integrationLogo"
					:alt="store.draft.type"
					fit="contain"
					width="28px"
					height="28px" />
				<span>{{ setupTexts.title }}</span>
			</div>
		</template>
		<template #default>
			<QAlert
				v-if="store.setupError"
				type="error"
				data-cy="integration-setup-request-error">
				{{ formatErrorResponse(store.setupError) }}
			</QAlert>
			<QStepper
				v-model="activeStep"
				vertical
				flat
				animated
				class="integration-setup-stepper">
				<QStep
					:name="1"
					:title="setupTexts.steps.connect"
					icon="mdi-lan-connect"
					done-icon="mdi-check"
					:active-icon="getStepActiveIcon(steps.connect.status)"
					error-icon="mdi-alert"
					:active-color="getStepActiveColor(steps.connect.status)"
					done-color="positive"
					error-color="negative"
					:done="steps.connect.status === 'success'"
					:header-nav="false"
					:error="steps.connect.status === 'error'"
					:data-status="steps.connect.status"
					data-cy="integration-setup-step-connect">
					<QAlert
						v-if="steps.connect.error"
						type="error"
						class="integration-setup-error">
						<div
							v-for="(error, index) in formatStepErrors(steps.connect.error)"
							:key="index"
							class="integration-setup-error__line">
							{{ error }}
						</div>
					</QAlert>
					<div v-else-if="steps.connect.status === 'running'">
						{{ setupTexts.progress.connecting }}
					</div>
					<div
						v-else-if="steps.connect.status === 'success'"
						class="text-positive">
						{{ setupTexts.progress.connected }}
					</div>
				</QStep>
				<QStep
					:name="2"
					:title="setupTexts.steps.downloadClient"
					icon="mdi-download"
					done-icon="mdi-check"
					:active-icon="getStepActiveIcon(steps.downloadClient.status)"
					error-icon="mdi-alert"
					:active-color="getStepActiveColor(steps.downloadClient.status)"
					done-color="positive"
					error-color="negative"
					:done="steps.downloadClient.status === 'success'"
					:header-nav="false"
					:error="steps.downloadClient.status === 'error'"
					:data-status="steps.downloadClient.status"
					data-cy="integration-setup-step-download-client">
					<QAlert
						v-if="steps.downloadClient.error"
						type="error"
						class="integration-setup-error">
						<div
							v-for="(error, index) in formatStepErrors(steps.downloadClient.error)"
							:key="index"
							class="integration-setup-error__line">
							{{ error }}
						</div>
					</QAlert>
					<div v-else-if="steps.downloadClient.status === 'running'">
						{{ setupTexts.progress.downloadClient }}
					</div>
					<div
						v-else-if="steps.downloadClient.status === 'success'"
						class="text-positive">
						{{ setupTexts.progress.downloadClientReady }}
					</div>
				</QStep>
				<QStep
					:name="3"
					:title="setupTexts.steps.indexer"
					icon="mdi-database-search"
					done-icon="mdi-check"
					:active-icon="getStepActiveIcon(steps.indexer.status)"
					error-icon="mdi-alert"
					:active-color="getStepActiveColor(steps.indexer.status)"
					done-color="positive"
					error-color="negative"
					:done="steps.indexer.status === 'success'"
					:header-nav="false"
					:error="steps.indexer.status === 'error'"
					:data-status="steps.indexer.status"
					data-cy="integration-setup-step-indexer">
					<QAlert
						v-if="steps.indexer.error"
						type="error"
						class="integration-setup-error">
						<div
							v-for="(error, index) in formatStepErrors(steps.indexer.error)"
							:key="index"
							class="integration-setup-error__line">
							{{ error }}
						</div>
					</QAlert>
					<div v-else-if="steps.indexer.status === 'running'">
						{{ setupTexts.progress.indexer }}
					</div>
					<div
						v-else-if="steps.indexer.status === 'success'"
						class="text-positive">
						{{ setupTexts.progress.indexerReady }}
					</div>
				</QStep>
				<QStep
					:name="4"
					:title="setupTexts.steps.validation"
					icon="mdi-connection"
					done-icon="mdi-check"
					:active-icon="getStepActiveIcon(steps.validation.status)"
					error-icon="mdi-alert"
					:active-color="getStepActiveColor(steps.validation.status)"
					done-color="positive"
					error-color="negative"
					:done="steps.validation.status === 'success'"
					:header-nav="false"
					:error="steps.validation.status === 'error'"
					:data-status="steps.validation.status"
					data-cy="integration-setup-step-validation">
					<QAlert
						v-if="steps.validation.error"
						type="error"
						class="integration-setup-error">
						<div
							v-for="(error, index) in formatStepErrors(steps.validation.error)"
							:key="index"
							class="integration-setup-error__line">
							{{ error }}
						</div>
					</QAlert>
					<QAlert
						v-if="steps.validation.error"
						type="warning"
						class="integration-setup-error">
						{{ setupTexts.progress.reverseProxyWarning }}
					</QAlert>
					<div v-else-if="steps.validation.status === 'running'">
						{{ setupTexts.progress.validation }}
					</div>
					<div
						v-else-if="steps.validation.status === 'success'"
						class="text-positive">
						{{ setupTexts.progress.validationReady }}
					</div>
				</QStep>
				<QStep
					:name="5"
					:title="setupTexts.steps.done"
					icon="mdi-flag-checkered"
					done-icon="mdi-check"
					:active-icon="getStepActiveIcon(steps.done.status)"
					:active-color="getStepActiveColor(steps.done.status)"
					:done="steps.done.status === 'success'"
					:header-nav="false"
					:data-status="steps.done.status"
					data-cy="integration-setup-step-done" />
			</QStepper>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import Log from 'consola';
import { get, set } from '@vueuse/core';
import { DialogType } from '@enums';
import { IntegrationSetupProgressStage, IntegrationType, type IntegrationSetupProgressDTO } from '@dto';
import { useDialogStore, useIntegrationStore, useSignalrStore } from '@store';
import { formatErrorResponse } from '@composables/common';
import { useSubscription } from '@vueuse/rxjs';

const store = useIntegrationStore();
const signalrStore = useSignalrStore();
const dialogStore = useDialogStore();
const activeStep = ref(1);
const closeIntegrationDialogOnClose = ref(false);
const integrationLogo = computed(() => store.draft.type === IntegrationType.Radarr ? '/img/logo/radarr.png' : '/img/logo/sonarr.png');
const setupTexts = computed(() => {
	const values = { type: store.draft.type };
	return {
		title: $t('components.integration-setup-dialog.setup.title', values),
		steps: {
			connect: $t('components.integration-setup-dialog.setup.steps.connect', values),
			downloadClient: $t('components.integration-setup-dialog.setup.steps.download-client', values),
			indexer: $t('components.integration-setup-dialog.setup.steps.indexer', values),
			validation: $t('components.integration-setup-dialog.setup.steps.validation', values),
			done: $t('components.integration-setup-dialog.setup.steps.done', values),
		},
		progress: {
			connecting: $t('components.integration-setup-dialog.setup.progress.connecting', values),
			connected: $t('components.integration-setup-dialog.setup.progress.connected', values),
			downloadClient: $t('components.integration-setup-dialog.setup.progress.download-client', values),
			downloadClientReady: $t('components.integration-setup-dialog.setup.progress.download-client-ready', values),
			indexer: $t('components.integration-setup-dialog.setup.progress.indexer', values),
			indexerReady: $t('components.integration-setup-dialog.setup.progress.indexer-ready', values),
			validation: $t('components.integration-setup-dialog.setup.progress.validation', values),
			validationReady: $t('components.integration-setup-dialog.setup.progress.validation-ready', values),
			reverseProxyWarning: $t('components.integration-setup-dialog.setup.progress.reverse-proxy-warning', values),
		},
	};
});
type StepStatus = 'pending' | 'running' | 'success' | 'error';

function getStepActiveIcon(status: StepStatus): string {
	if (status === 'error') return 'mdi-alert';
	if (status === 'success') return 'mdi-check';
	return 'mdi-loading';
}

function getStepActiveColor(status: StepStatus): 'positive' | 'negative' | 'info' | 'primary' {
	if (status === 'error') return 'negative';
	if (status === 'success') return 'positive';
	if (status === 'running') return 'info';
	return 'primary';
}

type Step = { status: StepStatus; error: string };
const steps = reactive<{ connect: Step; downloadClient: Step; indexer: Step; validation: Step; done: Step }>({
	connect: { status: 'pending', error: '' },
	downloadClient: { status: 'pending', error: '' },
	indexer: { status: 'pending', error: '' },
	validation: { status: 'pending', error: '' },
	done: { status: 'pending', error: '' },
});

function open(closeParent: unknown): void {
	set(closeIntegrationDialogOnClose, closeParent === true);
	reset();
}

function reset(): void {
	activeStep.value = 1;
	for (const step of Object.values(steps)) {
		step.status = 'pending';
		step.error = '';
	}
}

function formatStepErrors(error: string): string[] {
	const sections = error
		.split(/;\s+(?=Failed to validate)/)
		.map((section) => section.trim())
		.filter(Boolean);

	return sections.map((section) => {
		const summary = section.replace(/\s*;\s*\[.*$/s, '').trim();
		const messages = [
			...section.matchAll(/"errorMessage"\s*:\s*"([^"]+)"/g),
			...section.matchAll(/"detailedDescription"\s*:\s*"([^"]+)"/g),
		].map(([, message]) => message).filter((message): message is string => Boolean(message));
		const details = [...new Set(messages)].join(' · ');
		return details ? `${summary}: ${details}` : section.replace(/\s+/g, ' ');
	});
}

function updateStep(progress: IntegrationSetupProgressDTO): void {
	const setupStage = {
		[IntegrationSetupProgressStage.Connecting]: { step: steps.connect, number: 1 },
		[IntegrationSetupProgressStage.DownloadClient]: { step: steps.downloadClient, number: 2 },
		[IntegrationSetupProgressStage.Indexer]: { step: steps.indexer, number: 3 },
		[IntegrationSetupProgressStage.Validation]: { step: steps.validation, number: 4 },
		[IntegrationSetupProgressStage.Done]: { step: steps.done, number: 5 },
	}[progress.stage];
	if (!setupStage) {
		Log.warn('Unknown integration setup progress stage', progress.stage);
		return;
	}

	if (hasPreviousError(progress.stage)) return;
	const isDone = progress.stage === IntegrationSetupProgressStage.Done;
	setupStage.step.status = isDone ? 'success' : progress.isRunning ? 'running' : progress.isSuccess ? 'success' : 'error';
	setupStage.step.error = isDone ? '' : progress.error ?? '';
	set(activeStep, setupStage.number);
}

function hasPreviousError(stage: IntegrationSetupProgressStage): boolean {
	if (stage === IntegrationSetupProgressStage.Connecting) return false;
	if (steps.connect.status === 'error') return true;
	if (stage === IntegrationSetupProgressStage.DownloadClient) return false;
	if (steps.downloadClient.status === 'error') return true;
	if (stage === IntegrationSetupProgressStage.Indexer) return false;
	if (steps.indexer.status === 'error') return true;
	return stage === IntegrationSetupProgressStage.Done && steps.validation.status === 'error';
}

useSubscription(signalrStore.integrationSetupProgressSubject.subscribe((progress) => {
	if (progress.integrationId === store.detail?.id) updateStep(progress);
}));

onMounted(reset);
watch(() => store.isSettingUp, (isSettingUp) => {
	if (isSettingUp) reset();
});

function refresh(): void {
	useSubscription(store.refresh().subscribe());
}

function close(): void {
	const closeParent = get(closeIntegrationDialogOnClose);
	set(closeIntegrationDialogOnClose, false);
	reset();
	refresh();
	if (closeParent)
		dialogStore.closeDialog(DialogType.IntegrationDialog);
}
</script>

<style lang="scss">
.integration-setup-stepper {
  background: transparent;

  .mdi-loading {
    animation: integration-setup-loading 1s linear infinite;
  }
}

.integration-setup-error {
  margin: 0.5rem 0;
  overflow-wrap: anywhere;
  text-align: left;

  &__line + &__line {
    margin-top: 0.5rem;
  }
}

@keyframes integration-setup-loading {
  from {
    transform: rotate(0deg);
  }

  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .integration-setup-stepper .mdi-loading {
    animation: none;
  }
}
</style>
