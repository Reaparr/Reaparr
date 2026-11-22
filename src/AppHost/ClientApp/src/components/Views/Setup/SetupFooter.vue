<template>
	<QCol
		align-self="stretch"
		cols="12"
		class="setup-footer">
		<q-separator class="q-mb-md" />
		<QRow
			justify="between"
			align="center"
			class="setup-footer__row q-my-md">
			<!-- Language Selector + Background Toggle -->
			<QCol
				cols="2"
				class="setup-footer__utilities">
				<QRow no-wrap>
					<LanguageSelect
						class="q-ml-md"
						dense
						@update:model-value="onLanguageChanged" />
					<AnimatedBackgroundToggleButton
						class="q-ml-sm"
						@click="onBackgroundChanged" />
				</QRow>
			</QCol>
			<!-- Navigation buttons -->
			<QCol class="setup-footer__navigation">
				<QRow
					justify="center"
					align="center">
					<!-- Back button -->
					<QCol
						v-if="!isBackDisabled"
						class="setup-footer__button q-mx-md"
						cols="3">
						<NavigationPreviousButton
							:disabled="isBackDisabled"
							cy="setup-page-previous-button"
							@click="back" />
					</QCol>
					<!-- Next Button -->
					<QCol
						v-if="!isNextDisabled"
						:cols="isBackDisabled ? '9' : '3'"
						class="setup-footer__button q-mx-md">
						<ConfirmButton
							v-if="model === 1"
							cy="setup-disclaimer-accept-button"
							:label="$t('pages.setup.disclaimer.i-agree-disclaimer-button')"
							block
							@click="onDisclaimerAgree" />
						<NavigationNextButton
							v-else
							block
							:tooltip-text="!stepValidation.allowed ? stepValidation.tooltip : ''"
							:disabled="!stepValidation.allowed"
							cy="setup-page-next-button"
							@click="next" />
					</QCol>
				</QRow>
			</QCol>
			<!--	Skip button	-->
			<QCol
				class="setup-footer__finish q-mx-md"
				cols="auto">
				<!--	Finish button	-->
				<NavigationFinishSetupButton
					v-if="isFinishButtonVisible"
					cy="setup-page-skip-setup-button"
					@click="emits('finish')" />
			</QCol>
		</QRow>
	</QCol>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { SetupPanelType } from '@enums';
import { useFolderPathStore } from '#imports';

const props = defineProps<{ maxPages: number }>();

const model = defineModel<number>({
	default: 1,
});

const settingsStore = useSettingsStore();
const authStore = useAuthenticationStore();
const folderPathStore = useFolderPathStore();
const accountStore = useAccountStore();
const telemetry = useTelemetry();

const stepValidation = computed((): { allowed: boolean; tooltip?: string } => {
	switch (get(model)) {
		case SetupPanelType.DisclaimerPanel:
			return {
				allowed: false,
			};
		case SetupPanelType.AuthorizationPanel:
			return {
				allowed: !authStore.isDefaultCredentials,
				tooltip: 'You must change the default credentials before proceeding.',
			};

		case SetupPanelType.FolderOverviewPanel:
			return {
				allowed: folderPathStore.areDefaultFolderPathsValid,
				tooltip: 'All folder paths must be valid and writable.',
			};

		case SetupPanelType.PlexAccountsPanel:
			return {
				allowed: accountStore.getAccounts.length > 0,
				tooltip: 'Add at least 1 Plex account to proceed.',
			};

		default:
			return {
				allowed: true,
			};
	}
});
const isBackDisabled = computed(() => {
	return get(model) === 1;
});
const isNextDisabled = computed(() => {
	return get(model) === props.maxPages;
});
const isFinishButtonVisible = computed(() => {
	return get(model) === props.maxPages;
});

function next(capture = true) {
	if (get(model) < props.maxPages) {
		const nextStep = get(model) + 1;
		if (capture) {
			telemetry.setupNextRequested(nextStep);
		}
		set(model, nextStep);
	}
}
function back() {
	if (get(model) > 1) {
		const previousStep = get(model) - 1;
		telemetry.setupPreviousRequested(previousStep);
		set(model, previousStep);
	}
}
function onLanguageChanged(): void {
	telemetry.setupLanguageChanged();
}
function onBackgroundChanged(): void {
	telemetry.setupBackgroundChanged();
}
function onDisclaimerAgree() {
	telemetry.setupDisclaimerAccepted();
	settingsStore.$patch({
		generalSettings: {
			hasAgreedToDisclaimer: true,
		},
	});
	next(false);
}

const emits = defineEmits<{
	(e: 'finish'): void;
}>();
</script>

<style lang="scss">
.setup-footer {
  max-height: 76px;
}

@media (max-width: $breakpoint-xs-max) {
  .setup-footer {
    max-height: none;
  }

  .setup-footer__row {
    gap: 0.5rem;
    flex-wrap: wrap;
    padding-inline: 0.5rem;
  }

  .setup-footer__utilities {
    order: 2;
    width: auto;
    flex: 0 0 auto;
  }

  .setup-footer__navigation {
    order: 1;
    flex: 1 1 100%;
  }

  .setup-footer__button {
    min-width: 0;
    margin-inline: 0.25rem;
    flex: 1 1 0;
  }

  .setup-footer__button .q-btn,
  .setup-footer__finish .q-btn {
    min-height: 44px;
  }
}
</style>
