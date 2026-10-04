<template>
	<QCardDialog
		:name="DialogType.UpdateAvailableDialog"
		content-height="80"
		close-button
		cy="updatedialog-cy">
		<template #title>
			<div class="update-dialog__title">
				<span>{{ t('components.update-available-dialog.title') }}</span>
				<span class="update-dialog__title-versions">
					<span class="update-dialog__title-version">
						{{ t('components.update-available-dialog.current-version', { version: globalStore.version }) }}
					</span>
					<span class="update-dialog__title-version">
						{{ t('components.update-available-dialog.latest-version', { version: latestVersion }) }}
					</span>
				</span>
			</div>
		</template>

		<template #default>
			<div class="update-dialog">
				<div class="update-dialog__sidebar">
					<QScroll class="update-dialog__sidebar-scroll">
						<div class="update-dialog__sidebar-body">
							<q-tabs
								v-model="releaseIndex"
								vertical
								active-color="primary"
								indicator-color="primary"
								class="update-dialog__tabs"
								dense>
								<q-tab
									v-for="(release, index) in updateStore.releaseNotes"
									:key="index"
									:name="index"
									class="update-dialog__tab">
									<div class="update-dialog__tab-content">
										<span class="update-dialog__tab-version">{{ release.version }}</span>
										<QDateTime
											:text="release.releaseDate"
											short-date />
									</div>
								</q-tab>
							</q-tabs>
						</div>
					</QScroll>
				</div>
				<div class="update-dialog__content">
					<div
						v-if="!updateStore.hasUpdateAvailable"
						class="update-dialog__empty">
						{{ t('components.update-available-dialog.no-releases') }}
					</div>
					<q-tab-panels
						v-else
						v-model="releaseIndex"
						animated
						vertical
						class="update-dialog__panels">
						<q-tab-panel
							v-for="(release, index) in updateStore.releaseNotes"
							:key="index"
							:name="index"
							class="update-dialog__panel">
							<QScroll class="update-dialog__panel-scroll">
								<div class="update-dialog__panel-body">
									<div class="update-dialog__panel-header">
										<div class="column">
											<span class="update-dialog__panel-version">{{ release.version }}</span>
											<QDateTime
												:text="release.releaseDate"
												long-date
												time />
										</div>
									</div>

									<div
										v-if="release.notes"
										class="i18n-formatting update-dialog__notes">
										<VueMarkdown :source="release.notes" />
									</div>
								</div>
							</QScroll>
						</q-tab-panel>
					</q-tab-panels>
				</div>
			</div>
		</template>

		<template #actions>
			<QRow
				justify="between"
				gutter="sm">
				<QCol>
					<QLinearProgress
						v-if="updateStore.isDownloading"
						:value="updateStore.downloadProgress / 100"
						color="primary"
						class="update-dialog__progress" />
				</QCol>
				<QCol cols="auto">
					<!-- Update On Docker -->
					<ExternalLinkButton
						v-if="globalStore.isDockerMode"
						href="https://hub.docker.com/r/reaparr/reaparr/tags"
						icon="mdi-docker"
						:label="t('components.update-available-dialog.docker-action')" />

					<!-- Update On Desktop -->
					<BaseButton
						v-else
						unelevated
						icon="mdi-update"
						:label="desktopUpdateButtonLabel"
						:disable="updateStore.isDownloading || updateStore.isApplyingUpdate"
						@click="runDesktopUpdate()" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { useUpdateStore } from '@store';
import { DialogType } from '@enums';
import { useI18n } from 'vue-i18n';
import VueMarkdown from 'vue-markdown-render';
import QScroll from '@components/Common/QScroll.vue';

const { t } = useI18n();

const globalStore = useGlobalStore();
const updateStore = useUpdateStore();

const releaseIndex = ref(0);
const latestVersion = computed(() => updateStore.releaseNotes.at(0)?.version ?? '?');
const desktopUpdateButtonLabel = computed(() => {
	if (updateStore.isApplyingUpdate) {
		return t('components.update-available-dialog.restarting');
	}

	if (updateStore.isDownloading) {
		return t('components.update-available-dialog.downloading', { percentage: Math.round(updateStore.downloadProgress) });
	}

	return t('components.update-available-dialog.download-update');
});

function runDesktopUpdate() {
	useSubscription(updateStore.downloadAndApplyUpdate().subscribe());
}
</script>

<style scoped lang="scss">
:deep([data-cy='updatedialog-cy'] .dialog-container-content.dialog-container-content-80) {
	overflow: hidden !important;
}

.update-dialog {
	display: grid;
	grid-template-columns: minmax(10.5rem, 13rem) minmax(0, 1fr);
	gap: 1rem;
	height: 100%;
	min-height: 0;
	overflow: hidden;
}

.update-dialog__sidebar {
	display: flex;
	min-width: 0;
	min-height: 0;
	height: 100%;
	overflow: hidden;
}

.update-dialog__sidebar-scroll,
.update-dialog__panel-scroll {
	flex: 1;
	min-height: 0;
}

.update-dialog__sidebar-body {
	padding: 0.75rem;
}

.update-dialog__panel-body {
	padding: 1rem;
}

.update-dialog__tabs {
	align-items: stretch;
	min-height: 0;
}

.update-dialog__tab {
	justify-content: flex-start;
}

.update-dialog__tab-content {
	display: flex;
	flex-direction: column;
	align-items: flex-start;
	gap: 0.35rem;
	width: 100%;
	padding: 0.5rem 0.75rem;
	text-align: left;
}

.update-dialog__tab + .update-dialog__tab {
	margin-top: 0.5rem;
}

.update-dialog__tab-version,
.update-dialog__panel-version {
	font-size: 1rem;
	font-weight: 600;
}

.update-dialog__content {
	display: flex;
	min-width: 0;
	min-height: 0;
	height: 100%;
	overflow: hidden;
}

.update-dialog__panels {
	display: flex;
	flex: 1;
	min-width: 0;
	min-height: 0;
	height: 100%;
	overflow: hidden;
}

.update-dialog__panel {
	min-height: 0;
	height: 100%;
	padding: 0;
	overflow: hidden;
}

.update-dialog__panel-scroll {
	min-height: 0;
	height: 100%;
}

.update-dialog__panel-header {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 1rem;
	margin-bottom: 1rem;
}

.update-dialog__title {
	display: flex;
	flex-direction: column;
	gap: 0.2rem;
}

.update-dialog__title-versions {
	display: flex;
	flex-wrap: wrap;
	gap: 0.6rem;
	font-size: 0.85rem;
	font-weight: 400;
	opacity: 0.8;
}

.update-dialog__title-version + .update-dialog__title-version::before {
	content: '•';
	margin-right: 0.6rem;
}

.update-dialog__notes {
	padding-bottom: 1rem;
}

:deep(.update-dialog__notes h1) {
	display: none;
}

:deep(.update-dialog__notes h3) {
	margin: 0.35rem 0 0.15rem;
	text-align: left;
}

:deep(.update-dialog__notes ul) {
	margin: 0;
	padding: 0 0 0 1rem;
}

:deep(.update-dialog__notes ul + h3) {
	margin-top: 0.45rem;
}

:deep(.update-dialog__notes li) {
	margin: 0;
}

:deep(.update-dialog__notes li > p) {
	margin: 0;
}

:deep(.update-dialog__notes li + li) {
	margin-top: 0.15rem;
}

:deep(.update-dialog__notes p) {
	margin: 0 0 0.6rem;
}

.update-dialog__empty {
	display: grid;
	place-items: center;
	width: 100%;
	padding: 1rem;
	text-align: center;
}

.update-dialog__progress {
	width: 100%;
	margin-bottom: 0.5rem;
}

:deep(.update-dialog__tab .q-tab__content) {
	width: 100%;
	align-items: stretch;
}

:deep(.update-dialog__panels .q-panel),
:deep(.update-dialog__panels .q-tab-panel) {
	height: 100%;
	min-height: 0;
	overflow: hidden;
}
</style>
