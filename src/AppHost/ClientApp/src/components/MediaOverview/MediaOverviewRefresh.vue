<template>
	<template v-if="props.mode === 'queued'">
		<QAlert
			v-if="props.variant === 'banner'"
			class="media-overview-library-sync-queued-banner"
			:type="NotificationLevel.Information"
			role="status"
			aria-live="polite"
			cy="media-overview-library-sync-queued-banner">
			<QRow
				align="start"
				wrap
				class="media-overview-library-sync-queued-layout">
				<QCol class="media-overview-library-sync-queued-copy">
					<QText
						size="h6"
						class="media-overview-library-sync-queued-title"
						data-cy="media-overview-library-sync-queued-title">
						{{ t('components.media-overview.sync-queued-title') }}
					</QText>
					<QText
						class="media-overview-library-sync-queued-message"
						data-cy="media-overview-library-sync-queued-message"
						:value="queuedMessage" />
					<div class="media-overview-library-sync-queued-automatic q-mt-xs">
						{{ t('components.media-overview.sync-queued-automatic') }}
					</div>
				</QCol>
				<QCol
					cols="auto"
					class="media-overview-library-sync-queued-actions">
					<BaseButton
						class="media-overview-library-sync-queued-action"
						block
						outline
						cy="cancel-queued-library-sync-button"
						:label="t('components.media-overview.cancel-queued-refresh')"
						:loading="cancelling"
						@click="cancelSync" />
					<BaseButton
						class="media-overview-library-sync-queued-action"
						block
						flat
						cy="view-library-sync-activity-button"
						:label="t('components.media-overview.view-sync-activity')"
						@click="openActivity" />
				</QCol>
			</QRow>
		</QAlert>
		<QRow
			v-else
			align="start"
			class="q-pt-xl"
			cy="refresh-library-container"
			full-height>
			<QCol text-align="center">
				<QAlert
					class="q-mx-md media-overview-library-sync-queued-panel"
					:type="NotificationLevel.Information"
					role="status"
					aria-live="polite"
					cy="media-overview-library-sync-queued-panel">
					<QRow
						align="start"
						wrap
						class="media-overview-library-sync-queued-layout">
						<QCol class="media-overview-library-sync-queued-copy">
							<div
								class="media-overview-library-sync-queued-title text-h6"
								data-cy="media-overview-library-sync-queued-title">
								{{ t('components.media-overview.sync-queued-title') }}
							</div>
							<div
								class="media-overview-library-sync-queued-message"
								data-cy="media-overview-library-sync-queued-message">
								{{ queuedMessage }}
							</div>
							<div class="media-overview-library-sync-queued-automatic q-mt-xs">
								{{ t('components.media-overview.sync-queued-automatic') }}
							</div>
						</QCol>
						<QCol
							cols="auto"
							class="media-overview-library-sync-queued-actions">
							<BaseButton
								class="media-overview-library-sync-queued-action"
								size="sm"
								block
								flat
								cy="view-library-sync-activity-button"
								:label="t('components.media-overview.view-sync-activity')"
								@click="openActivity" />
							<BaseButton
								class="media-overview-library-sync-queued-action"
								size="sm"
								block
								outline
								cy="cancel-queued-library-sync-button"
								:label="t('components.media-overview.cancel-queued-refresh')"
								:loading="cancelling"
								@click="cancelSync" />
						</QCol>
					</QRow>
				</QAlert>
			</QCol>
		</QRow>
	</template>
	<template v-else-if="props.variant === 'banner'">
		<QBanner
			class="media-overview-library-sync-processing-banner"
			role="status"
			aria-live="polite"
			data-cy="media-overview-library-sync-processing-banner">
			<div class="row items-center q-col-gutter-md">
				<div class="col">
					<div class="text-weight-medium">
						{{ t('components.media-overview.sync-processing-title') }}
					</div>
					<div class="text-caption">
						{{ refreshingText }}
					</div>
					<QProgressBar
						class="q-mt-xs"
						:value="libraryProgress?.percentage ?? 0"
						:indeterminate="progressIndeterminate"
						cy="media-overview-library-sync-processing-progress" />
				</div>
				<div class="col-auto">
					<CancelButton
						cy="cancel-library-sync-button"
						:loading="cancelling"
						@click="cancelSync" />
				</div>
			</div>
		</QBanner>
	</template>
	<QRow
		v-else
		align="start"
		class="q-pt-xl"
		cy="refresh-library-container"
		full-height>
		<QCol text-align="center">
			<ProgressComponent
				:percentage="libraryProgress?.percentage ?? -1"
				:text="refreshingText"
				circular-mode
				:indeterminate="progressIndeterminate"
				class="q-my-lg" />
			<div>
				<QCountdown
					class="q-my-md"
					:value="libraryProgress?.timeRemaining ?? ''" />
				<QRow justify="around">
					<QCol cols="6">
						<QRow
							v-for="item in libraryProgress?.items"
							:key="item.mediaType"
							gutter="sm"
							justify="between"
							class="library-media-sync-progress-row q-my-sm">
							<QCol cols="auto">
								<QMediaTypeIcon
									class="q-pr-sm"
									:media-type="item.mediaType"
									:size="20" />
							</QCol>
							<QCol>
								<QProgressBar
									:value="item.percentage"
									:indeterminate="item.total < 0"
									:cy="`library-media-sync-progress-row-${item.mediaType}-progress-bar`" />
							</QCol>
							<QCol cols="1">
								<QText
									v-if="item.total >= 0"
									:value="`${item.received}/${item.total}`"
									align="right"
									:cy="`library-media-sync-progress-row-${item.mediaType}-count`" />
							</QCol>
						</QRow>
						<QRow
							v-if="(libraryProgress?.percentage ?? 0) >= 100"
							justify="around">
							<QCol cols="auto">
								<QText value="Updating database with all new data, please wait" />
							</QCol>
						</QRow>
					</QCol>
				</QRow>
				<QRow
					justify="around"
					class="q-mt-lg">
					<QCol cols="auto">
						<CancelButton
							cy="cancel-library-sync-button"
							:loading="cancelling"
							@click="cancelSync" />
					</QCol>
				</QRow>
			</div>
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import { useDialogStore, useLibraryStore, useServerStore } from '@store';
import { useI18n } from '#imports';
import { NotificationLevel } from '@dto';
import { LibrarySyncWaitingReason } from '@enums';

const props = withDefaults(defineProps<{
	libraryId: number;
	mode?: 'processing' | 'queued';
	variant?: 'panel' | 'banner';
}>(), {
	mode: 'processing',
	variant: 'panel',
});

const cancelling = ref(false);

const { t } = useI18n();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();
const dialogStore = useDialogStore();

const library = computed(() => libraryStore.getLibrary(props.libraryId));
const libraryProgress = computed(() => libraryStore.getLibraryProgress(props.libraryId));
const progressIndeterminate = computed(() => {
	const progress = libraryProgress.value;
	return !progress || progress.items.length === 0 || progress.items.some((item) => item.total < 0);
});
const queue = computed(() => libraryStore.getLibrarySyncQueue(props.libraryId));
const refreshingText = computed(() => {
	const server = libraryStore.getServerByLibraryId(props.libraryId);
	return t('components.media-overview.is-refreshing', {
		library: get(library) ? libraryStore.getLibraryName(props.libraryId) : t('general.commands.unknown'),
		server: server ? serverStore.getServerName(server.id) : t('general.commands.unknown'),
	});
});
const queuedMessage = computed(() => {
	const currentQueue = get(queue);
	const reason = libraryStore.getLibrarySyncWaitingReason(props.libraryId);
	if (reason === LibrarySyncWaitingReason.Offline) {
		return t('components.media-overview.sync-queued-offline');
	}

	if (reason === LibrarySyncWaitingReason.Busy && currentQueue) {
		const processing = libraryStore.getProcessingLibrarySyncQueue(currentQueue.plexServerId);
		if (processing) {
			return t('components.media-overview.sync-queued-busy', {
				library: libraryStore.getLibraryName(processing.plexLibraryId),
				server: serverStore.getServerName(currentQueue.plexServerId),
			});
		}
	}

	return t('components.media-overview.sync-queued-waiting');
});

function openActivity(): void {
	const currentQueue = get(queue);
	if (currentQueue) {
		dialogStore.openSyncServerMediaDialog(currentQueue.plexServerId);
		return;
	}

	dialogStore.openSyncServerMediaDialog();
}
function cancelSync(): void {
	if (get(cancelling)) {
		return;
	}

	set(cancelling, true);
	useSubscription(
		libraryStore.cancelLibrarySync(props.libraryId).subscribe({
			complete: () => {
				set(cancelling, false);
			},
			error: () => {
				set(cancelling, false);
			},
		}),
	);
}
</script>

<style lang="scss" scoped>
.media-overview-library-sync-queued-banner,
.media-overview-library-sync-queued-panel {
  box-sizing: border-box;
  max-width: 100%;
  margin: 0.5rem;
  padding: 0.625rem 0.75rem;

  :deep(.q-alert-wrapper) {
    align-items: flex-start;
    column-gap: 0.5rem;
    min-width: 0;
  }

  :deep(.q-alert-content) {
    flex: 1 1 auto;
    min-width: 0;
    padding: 0;
  }
}

.media-overview-library-sync-queued-panel.q-mx-md {
  margin: 0.5rem;
}

.media-overview-library-sync-queued-layout {
  box-sizing: border-box;
  column-gap: 0.75rem;
  min-width: 0;
  row-gap: 0.375rem;
  width: 100%;
}

.media-overview-library-sync-queued-copy {
  flex: 1 1 16rem;
  min-width: min(100%, 14rem);
  max-width: 100%;
  overflow-wrap: anywhere;
  text-align: left;
  word-break: break-word;
}

.media-overview-library-sync-queued-title {
  font-size: 1.1rem;
  font-weight: 600;
  line-height: 1.25;
  overflow-wrap: anywhere;
  word-break: break-word;

  :deep(.q-text) {
    font-size: inherit;
    font-weight: inherit;
    line-height: inherit;
  }
}

.media-overview-library-sync-queued-message,
.media-overview-library-sync-queued-automatic {
  overflow-wrap: anywhere;
  word-break: break-word;
}

.media-overview-library-sync-queued-message {
  :deep(.q-text-container) {
    max-width: 100%;
    min-width: 0;
    width: 100%;
  }

  :deep(.q-text) {
    overflow-wrap: anywhere;
    word-break: break-word;
  }
}

.media-overview-library-sync-queued-actions {
  align-items: flex-end;
  box-sizing: border-box;
  display: flex;
  flex: 0 1 15rem;
  flex-direction: column;
  gap: 0.375rem;
  margin-inline-start: auto;
  max-width: 100%;
  min-width: min(100%, 10rem);
  width: min(100%, 15rem);
}

.media-overview-library-sync-queued-actions :deep(.media-overview-library-sync-queued-action) {
  box-sizing: border-box;
  max-width: 100%;
  min-width: 0;
  width: 100%;
}
</style>
