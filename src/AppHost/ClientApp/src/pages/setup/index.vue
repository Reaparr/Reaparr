<template>
	<QPage class="setup-page">
		<!-- Logo	-->
		<QRow
			justify="center"
			no-gutters
			no-wrap>
			<QCol
				class="q-my-md"
				cols="auto">
				<Logo :size="128" />
			</QCol>
		</QRow>
		<!--	Horizontal Container	-->
		<QRow
			justify="center"
			class="setup-page-content">
			<QCol
				cols="12"
				lg="8">
				<!--	Vertical Container	-->
				<QRow
					class="setup-card"
					column>
					<QCol align-self="stretch">
						<QRow
							align="start"
							class="setup-content-row">
							<!-- Tabs -->
							<QCol
								cols="2"
								class="setup-tabs-column">
								<SetupTabs
									v-model="stepIndex"
									:headers="headers" />
							</QCol>
							<QCol
								align-self="stretch"
								class="setup-panels-column">
								<!-- Panels -->
								<q-tab-panels
									v-model="stepIndex"
									animated
									class="fit q-pa-md"
									transition-next="slide-up"
									transition-prev="slide-down">
									<DisclaimerSetupPanel :name="SetupPanelType.DisclaimerPanel" />
									<!-- Introduction	-->
									<IntroductionSetupPanel :name="SetupPanelType.IntroductionPanel" />
									<!-- Authorization	-->
									<AuthorizationSetupPanel :name="SetupPanelType.AuthorizationPanel" />
									<!-- Checking paths	-->
									<FolderOverviewSetupPanel :name="SetupPanelType.FolderOverviewPanel" />
									<!-- Plex Accounts	-->
									<PlexAccountsSetupPanel :name="SetupPanelType.PlexAccountsPanel" />
									<!-- Finished	-->
									<FinishSetupPanel :name="SetupPanelType.FinishPanel" />
								</q-tab-panels>
							</QCol>
						</QRow>
					</QCol>
					<!-- Stepper navigation bar	-->
					<SetupFooter
						v-model="stepIndex"
						:max-pages="headers.length"
						@finish="finishSetup" />
				</QRow>
			</QCol>
		</QRow>
	</QPage>
</template>

<script lang="ts" setup>
import Log from 'consola';
import { SetupPanelType } from '@enums';
import { switchMap, tap } from 'rxjs/operators';
import { useRouter, useI18n } from '#imports';
import { useSubscription } from '@vueuse/rxjs';

const { t } = useI18n();
const router = useRouter();
const settingsStore = useSettingsStore();
const globalStore = useGlobalStore();
const stepIndex = ref(1);

const headers = ref([
	{ name: t('pages.setup.disclaimer.header') },
	{ name: t('pages.setup.intro.header') },
	{ name: t('pages.setup.authorization.header') },
	{ name: t('pages.setup.paths.header') },
	{ name: t('pages.setup.accounts.header') },
	{ name: t('pages.setup.finished.header') }]);

function finishSetup() {
	settingsStore.$patch({
		generalSettings: {
			firstTimeSetup: false,
		},
	});

	useSubscription(settingsStore.saveSettings().pipe(switchMap(() => globalStore.setup()), tap(() => {
		Log.info('Setup process is finished, redirecting to home page now and refreshing data');
		router.push('/');
	})).subscribe());
}
</script>

<style lang="scss">
@use '@/assets/scss/variables' as *;
@use '@/assets/scss/_mixins.scss';

.setup-card {
  @extend .default-border;
  @extend .default-border-radius;
}

@media (max-width: $breakpoint-xs-max) {
  .setup-page {
    height: 100%;
    overflow-y: auto;
    overscroll-behavior: contain;
  }

.setup-content-row,
.setup-panels-column {
  min-width: 0;
}
  .setup-page-content {
    padding-inline: 0.5rem;
  }

  .setup-content-row {
    flex-direction: column;
  }

  .setup-tabs-column,
  .setup-panels-column {
    width: 100% !important;
    max-width: 100% !important;
    flex: 0 0 100% !important;
  }

  .setup-tabs-column :deep(.q-tabs) {
    width: 100%;
    flex-direction: row;
  }

  .setup-tabs-column :deep(.q-tabs__content) {
    width: 100%;
    overflow-x: auto;
  }

  .setup-tabs-column :deep(.q-tab) {
    min-width: 80px;
    min-height: 48px;
    height: auto;
  }

  .setup-panels-column :deep(.q-tab-panel) {
    padding: 0.75rem;
  }
}

.setup-tab {
  height: 12vh;
}

body {
  &.body--dark {
    .setup-card {
      background-color: $dark-xl-background-color;
    }
  }

  &.body--light {
    .setup-card {
      background-color: $light-xl-background-color;
    }
  }
}
</style>
