<template>
	<QCardDialog
		full-height
		:type="0 as number"
		:name="DialogType.SyncServerMediaDialog"
		cy="sync-server-media-dialog"
		@opened="onOpened"
		@closed="onClosed">
		<template #top-row>
			<!-- The total progress -->
			<ProgressComponent
				v-if="hasProcessingNodes || !hasQueuedNodes"
				class="q-ma-md"
				circular-mode
				:percentage="totalPercentage"
				:completed="totalPercentage === 100"
				:text="getProgressText"
				:indeterminate="openedAt === null" />
			<div
				v-else
				class="q-pa-md text-center"
				role="status"
				aria-live="polite"
				data-cy="sync-server-media-dialog-queued-status">
				{{ t('components.sync-server-media-dialog.queued-summary') }}
			</div>
		</template>
		<template #default>
			<div>
				<q-tree
					v-if="plexServerNodes.length > 0"
					v-model:expanded="expanded"
					:nodes="plexServerNodes"
					node-key="index"
					default-expand-all>
					<template #default-header="{ node }: { node: IPlexMediaSyncServerNode }">
						<QRow
							justify="between"
							class="q-mr-lg"
							align="center">
							<QCol
								cols="4">
								<div :class="{ 'text-weight-bold': isServer(node) }">
									<!--	Row Icon -->
									<q-icon
										v-if="isServer(node)"
										name="mdi-server"
										size="28px"
										class="q-mr-sm" />
									<QMediaTypeIcon
										v-else
										:active="node.completed"
										:loading="node.status === LibrarySyncJobStatus.Processing && node.percentage > 0 && !node.completed"
										:media-type="node.mediaType" />
									<!-- Row Title	-->
									<span
										:class="[
											isServer(node)
												? 'sync-server-media-dialog-server-title'
												: 'sync-server-media-dialog-library-title',
											'q-ml-sm',
										]"
										:data-cy="
											isServer(node)
												? 'sync-server-media-dialog-server-title'
												: 'sync-server-media-dialog-library-title'
										">
										{{ node.title }}
									</span>
								</div>
							</QCol>
							<!-- Time Remaining -->
							<QCol
								cols="4"
								text-align="center">
								<template v-if="!isServer(node)">
									<QCountdown
										v-if="node.status === LibrarySyncJobStatus.Processing && node.progress?.timeRemaining"
										data-cy="sync-server-media-dialog-library-eta"
										:value="node.progress.timeRemaining" />
									<div
										v-else
										role="status"
										aria-live="polite"
										data-cy="sync-server-media-dialog-library-status">
										{{ getStatusText(node.status, node.id) }}
									</div>
									<div
										v-if="node.status === LibrarySyncJobStatus.Failed && node.errorMessage"
										class="text-negative text-caption"
										data-cy="sync-server-media-dialog-library-error">
										{{ node.errorMessage }}
									</div>
								</template>
							</QCol>
							<!-- Plex Media Sync Progress -->
							<QCol cols="4">
								<QProgressBar
									v-if="shouldShowProgress(node)"
									:value="node.percentage" />
							</QCol>
						</QRow>
					</template>
				</q-tree>
			</div>
		</template>
		<!-- Actions -->
		<template #actions="{ close }">
			<QRow justify="end">
				<QCol cols="auto">
					<HideButton
						cy="sync-server-media-dialog-hide-btn"
						@click="close" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import {
	type LibrarySyncProgressDTO,
	LibrarySyncJobStatus,
	type PlexMediaType,
} from '@dto';
import { DialogType, LibrarySyncWaitingReason } from '@enums';
import { sum, meanBy } from 'lodash-es';
import { useLibraryStore, useServerStore } from '@store';
import { useI18n } from '#imports';

interface IPlexMediaSyncServerNode {
	id: number;
	title: string;
	index: number;
	type: 'server' | 'library';
	percentage: number;
	completed: boolean;
	status?: LibrarySyncJobStatus;
	errorMessage?: string | null;
	progress?: Partial<LibrarySyncProgressDTO>;
	mediaType?: PlexMediaType;
	children: IPlexMediaSyncServerNode[];
}

const expanded = ref<number[]>([]);
/**
 * When set, only progress for the server that initiated the sync is displayed.
 * A null value keeps the background activity dialog's all-server view.
 */
const plexServerId = ref<number | null>(null);
const openedAt = ref<Date | null>(null);

const { t } = useI18n();
const serverStore = useServerStore();
const libraryStore = useLibraryStore();

const libraryNodes = computed(() => get(plexServerNodes).flatMap((x) => x.children));
const hasProcessingNodes = computed(() => get(libraryNodes).some((x) => x.status === LibrarySyncJobStatus.Processing));
const hasQueuedNodes = computed(() => get(libraryNodes).some((x) => x.status === LibrarySyncJobStatus.Queued));
const totalPercentage = computed(() => {
	const nodes = get(plexServerNodes);
	if (nodes.length === 0) {
		return 0;
	}
	return sum(nodes.map((x) => x.percentage)) / nodes.length;
});
const plexServers = computed(() => serverStore.getServers(get(plexServerNodes).map((x) => x.id)));
const getProgressText = computed(() => {
	if (get(openedAt) && get(libraryNodes).length === 0) {
		return t('components.sync-server-media-dialog.no-active-syncs');
	}

	if (get(plexServers).length === 0) {
		return t('components.sync-server-media-dialog.fetching-servers', {
			displayName: t('general.error.unknown'),
		});
	}

	const nodes = get(libraryNodes);
	const completed = nodes.filter((x) => x.status === LibrarySyncJobStatus.Completed).length;
	const failed = nodes.filter((x) => x.status === LibrarySyncJobStatus.Failed).length;
	const cancelled = nodes.filter((x) => x.status === LibrarySyncJobStatus.Cancelled).length;
	const terminal = completed + failed + cancelled;
	if (terminal === nodes.length) {
		set(expanded, []);
		return t('components.sync-server-media-dialog.finished', { completed, failed, cancelled });
	}

	return t('components.sync-server-media-dialog.checking-progress', {
		count: terminal,
		total: nodes.length,
	});
});
const plexServerNodes = computed((): IPlexMediaSyncServerNode[] => {
	let uniqueIndex = 0;

	return libraryStore
		.getLibrarySyncQueueGrouped(get(plexServerId) ?? undefined, get(openedAt) ?? undefined)
		.map((server) => {
			const percentage = server.progress.length > 0
				? meanBy(server.progress, (x) => x.percentage ?? 0)
				: 0;
			return {
				id: server.serverId,
				index: uniqueIndex++,
				type: 'server',
				title: serverStore.getServerName(server.serverId),
				percentage: percentage,
				completed: percentage === 100,
				children: server.progress.map((libraryProgress) => {
					const library = libraryStore.getLibrary(libraryProgress.plexLibraryId);
					return {
						id: libraryProgress.plexLibraryId,
						index: uniqueIndex++,
						type: 'library',
						mediaType: library?.type,
						percentage: libraryProgress.percentage ?? 0,
						title: library ? libraryStore.getLibraryName(libraryProgress.plexLibraryId) : t('general.error.unknown'),
						completed: libraryProgress.status === LibrarySyncJobStatus.Completed,
						status: libraryProgress.status,
						errorMessage: libraryProgress.errorMessage,
						progress: libraryProgress,
						children: [],
					};
				}),
			};
		});
});

function isServer(node: IPlexMediaSyncServerNode): boolean {
	return node.type === 'server';
}
function shouldShowProgress(node: IPlexMediaSyncServerNode): boolean {
	if (isServer(node)) {
		return node.children.some((child) => child.status !== LibrarySyncJobStatus.Queued);
	}

	return node.status !== LibrarySyncJobStatus.Queued;
}
function getStatusText(status?: LibrarySyncJobStatus, libraryId?: number): string {
	if (status === LibrarySyncJobStatus.Queued && libraryId !== undefined) {
		const reason = libraryStore.getLibrarySyncWaitingReason(libraryId);
		if (reason === LibrarySyncWaitingReason.Offline) {
			return t('components.sync-server-media-dialog.status.queued-offline');
		}

		if (reason === LibrarySyncWaitingReason.Busy) {
			const queue = libraryStore.getLibrarySyncQueue(libraryId);
			const processing = queue
				? libraryStore.getProcessingLibrarySyncQueue(queue.plexServerId)
				: null;
			if (queue && processing) {
				return t('components.sync-server-media-dialog.status.queued-busy', {
					library: libraryStore.getLibraryName(processing.plexLibraryId),
					server: serverStore.getServerName(queue.plexServerId),
				});
			}
		}

		return t('components.sync-server-media-dialog.status.queued-waiting');
	}

	switch (status) {
		case LibrarySyncJobStatus.Queued:
			return t('components.sync-server-media-dialog.status.queued');
		case LibrarySyncJobStatus.Processing:
			return t('components.sync-server-media-dialog.status.processing');
		case LibrarySyncJobStatus.Completed:
			return t('components.sync-server-media-dialog.status.completed');
		case LibrarySyncJobStatus.Failed:
			return t('components.sync-server-media-dialog.status.failed');
		case LibrarySyncJobStatus.Cancelled:
			return t('components.sync-server-media-dialog.status.cancelled');
		default:
			return t('components.sync-server-media-dialog.status.unknown');
	}
}
function onOpened(serverId?: number): void {
	set(openedAt, new Date());
	set(plexServerId, serverId ?? null);
}
function onClosed(): void {
	set(openedAt, null);
	set(plexServerId, null);
	set(expanded, []);
}
</script>
