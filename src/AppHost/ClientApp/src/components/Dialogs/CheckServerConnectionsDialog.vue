<template>
	<QCardDialog
		full-height
		:name="DialogType.CheckServerConnectionDialogName"
		cy="check-server-connection-dialog"
		@closed="onClosed">
		<template #top-row>
			<!-- The total progress -->
			<ProgressComponent
				class="q-ma-md"
				circular-mode
				data-cy="check-server-connection-dialog-progress"
				:data-completed="totalPercentage === 100"
				:percentage="totalPercentage"
				:completed="totalPercentage === 100"
				:text="getProgressText"
				:indeterminate="!hasAnyProgress" />
		</template>
		<template #default>
			<div>
				<q-tree
					v-if="plexServerNodes.length > 0"
					v-model:expanded="expanded"
					:nodes="plexServerNodes"
					node-key="index"
					default-expand-all>
					<template #default-header="{ node }: { node: IPlexServerNode }">
						<QRow
							class="check-server-connections-dialog-node-row"
							justify="between"
							align="center">
							<QCol
								cols="12"
								sm="8"
								class="check-server-connections-dialog-title-col">
								<div :class="{ 'text-weight-bold': isServer(node) }">
									<!--	Plex Server Connection Icon -->
									<q-icon
										v-if="isServer(node)"
										name="mdi-server"
										size="28px"
										:data-cy="`check-server-connection-dialog-server-icon-${node.id}`"
										class="q-mr-sm" />
									<QConnectionIcon
										v-else
										:cy="`check-server-connections-dialog-connection-icon-${node.id}`"
										:type="node.connectionType" />
									<!-- Plex Server Connection Url	-->
									<span
										:class="[
											isServer(node)
												? 'check-server-connections-dialog-server-title'
												: 'check-server-connections-dialog-connection-title',
											'check-server-connections-dialog-node-title',
											'q-ml-sm',
										]"
										:data-cy="
											isServer(node)
												? `check-server-connections-dialog-server-title-${node.id}`
												: `check-server-connections-dialog-connection-title-${node.id}`
										">
										{{ node.title }}
									</span>
								</div>
							</QCol>
							<QCol
								cols="12"
								sm="4"
								class="check-server-connections-dialog-status-col"
								:style="{ 'max-width': `600px !important` }">
								<QRow
									class="check-server-connections-dialog-status-row"
									justify-end
									no-wrap
									justify="end">
									<!--	Plex Server Progress Status Icon -->
									<QCol
										cols="3"
										class="check-server-connections-dialog-status-indicator">
										<QSpinnerRadio
											v-if="!node.completed"
											:data-cy="`check-server-connections-dialog-${node.id}`"
											color="red"
											size="2em" />
										<QStatus
											v-else
											:cy="node.id.toString()"
											:value="node.connectionSuccessful" />
									</QCol>
									<!-- Plex Server Connection Progress	-->
									<QCol
										cols="9"
										class="check-server-connections-dialog-status-text">
										<template v-if="isServer(node) && (node.connectionSuccessful || node.completed)">
											<!-- No Plex Server Connection -->
											<span
												v-if="node.noConnections"
												:data-cy="`check-server-connections-dialog-result-text-no-connections-${node.id}`"
												:class="{ 'text-weight-bold': node.type === 'server' }">
												{{ t('components.check-server-connections-dialog.no-connections') }}
											</span>
											<span
												v-else-if="node.connectionSuccessful"
												:data-cy="`check-server-connections-dialog-result-text-success-${node.id}`"
												:class="{ 'text-weight-bold': node.type === 'server' }">
												{{ t('components.check-server-connections-dialog.server-connectable') }}
											</span>
											<span
												v-else-if="node.completed"
												:data-cy="`check-server-connections-dialog-result-text-completed-${node.id}`"
												:class="{ 'text-weight-bold': node.type === 'server' }">
												{{ t('components.check-server-connections-dialog.server-un-connectable') }}
											</span>
										</template>
										<template v-else-if="node.progress">
											<ConnectionProgressText :progress="node.progress" />
										</template>
									</QCol>
								</QRow>
							</QCol>
						</QRow>
					</template>
				</q-tree>
			</div>
		</template>
		<template #actions="{ close }">
			<QRow justify="end">
				<QCol cols="auto">
					<HideButton
						cy="check-server-connection-dialog-hide-btn"
						@click="close" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import { JobStatus } from '@dto';
import type { ServerConnectionCheckStatusProgressDTO, PlexConnectionTypes } from '@dto';
import { DialogType } from '@enums';
import { clamp } from 'lodash-es';
import {
	useBackgroundJobsStore,
	useDialogStore,
	useI18n,
	useServerConnectionStore,
	useServerStore,
	useSignalrStore,
} from '#imports';
import Log from 'consola';

const { t } = useI18n();
const serverStore = useServerStore();
const connectionStore = useServerConnectionStore();
const dialogStore = useDialogStore();
const backgroundJobStore = useBackgroundJobsStore();
const signalrStore = useSignalrStore();
const connectionProgress = ref<ServerConnectionCheckStatusProgressDTO[]>([]);

const expanded = ref<number[]>([]);
/**
 * The plex server ids that are being checked
 */
const plexServerIds = ref<number[]>([]);
const completedCount = computed(() => {
	return get(plexServerNodes).filter((progress) => progress.completed).length;
});

const hasAnyProgress = computed(() => get(connectionProgress).length > 0);

const totalPercentage = computed(() => {
	if (get(plexServerNodes).length === 0) {
		return 0;
	}
	return clamp(Math.round((get(completedCount) / get(plexServerNodes).length) * 100), 0, 100);
});
const plexServers = computed(() => serverStore.getServers(get(activeServerIds)));
const activeServerIds = computed(() => (get(plexServerIds).length > 0
	? get(plexServerIds)
	: [...new Set(get(connectionProgress).map((x) => x.plexServerId))]));

const getProgressText = computed(() => {
	if (get(plexServers).length === 0) {
		return t('components.check-server-connections-dialog.fetching-servers', {
			displayName: t('general.error.unknown'),
		});
	}
	if (get(totalPercentage) === 100) {
		// Close all expanded nodes
		set(expanded, []);
		return t('components.check-server-connections-dialog.completed', {
			length: get(plexServers).length,
		});
	}
	return t('components.check-server-connections-dialog.checking-progress', {
		count: get(completedCount),
		total: get(plexServerNodes).length,
	});
});

const plexServerNodes = computed((): IPlexServerNode[] => {
	let uniqueIndex = 0;

	return get(plexServers).map((server) => {
		const serverHasProgress = get(connectionProgress).some((x) => x.plexServerId === server.id);
		const connections = connectionStore.getServerConnectionsByServerId(server.id);
		const mappedConnections = connections.map((connection): IPlexServerNode => {
			const progress = getConnectionProgress(connection.id, server.id);

			return {
				id: connection.id,
				index: uniqueIndex++,
				type: 'connection',
				title: connection.url,
				local: connection.local,
				completed: progress.completed,
				connectionSuccessful: progress.connectionSuccessful,
				progress,
				connectionType: connection.type,
				children: [],
			};
		});

		const hasConnections = mappedConnections.length > 0;
		const hasInProgressConnections = hasConnections && mappedConnections.some((connection) => !connection.completed);
		const hasSuccessfulConnection = hasConnections
			? mappedConnections.some((connection) => connection.connectionSuccessful)
			: false;
		const serverCompleted = serverHasProgress
			? (hasConnections ? hasSuccessfulConnection || !hasInProgressConnections : true)
			: false;

		return {
			id: server.id,
			index: uniqueIndex++,
			type: 'server',
			title: serverStore.getServerName(server.id),
			completed: serverCompleted,
			connectionSuccessful: hasSuccessfulConnection,
			hasInProgressConnections,
			noConnections: !hasConnections,
			children: mappedConnections,
		};
	});
});

function getConnectionProgress(connectionId: number, serverId: number): ServerConnectionCheckStatusProgressDTO {
	return (
		get(connectionProgress).find((x) => x.plexServerConnectionId === connectionId) ?? {
			// Add default progress object
			plexServerConnectionId: connectionId,
			plexServerId: serverId,
			connectionSuccessful: false,
			completed: false,
			message: t('components.check-server-connections-dialog.no-progress-yet'),
			retryAttemptCount: 0,
			retryAttemptIndex: 0,
			statusCode: 0,
			timeToNextRetry: 0,
		}
	);
}

function isServer(node: IPlexServerNode): boolean {
	return node.type === 'server';
}

function onClosed(): void {
	Log.debug('Resetting CheckServerConnectionsDialog');
	set(plexServerIds, []);
	set(connectionProgress, []);
	set(expanded, []);
}

onMounted(() => {
	useSubscription(
		signalrStore
			.getAllServerConnectionProgress()
			.subscribe((connections) => {
				set(connectionProgress, get(plexServerIds).length > 0
					? connections.filter((progress) => get(plexServerIds).includes(progress.plexServerId))
					: connections);
			}),
	);

	// TODO this might be better moved to the dialog store
	useSubscription(
		backgroundJobStore.getInspectPlexServerJobUpdate(JobStatus.Started)
			.subscribe(({ data }) => {
				set(plexServerIds, [...new Set([...get(plexServerIds), ...data.plexServerIds])]);

				dialogStore.openCheckServerConnectionsDialog({
					plexServersWithConnectionIds: data.plexServerIds.reduce(
						(acc, serverId) => {
							acc[serverId] = [];
							return acc;
						},
						{} as Record<string, number[]>,
					),
				});
			}),
	);
});

interface IPlexServerNode {
	id: number;
	title: string;
	index: number;
	type: 'server' | 'connection';
	completed: boolean;
	connectionSuccessful: boolean;
	progress?: ServerConnectionCheckStatusProgressDTO;
	noConnections?: boolean;
	hasInProgressConnections?: boolean;
	local?: boolean;
	connectionType?: PlexConnectionTypes;
	children: IPlexServerNode[];
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;
.server-progress-list {
  .v-list-item__content {
    padding: 0;
  }

  &.theme--dark {
    .server-title {
      color: white;
    }
  }
}

@media (max-width: $breakpoint-sm-max) {
.check-server-connections-dialog-title-col,
.check-server-connections-dialog-status-col,
.check-server-connections-dialog-status-row,
.check-server-connections-dialog-status-text {
  min-width: 0;
}

.check-server-connections-dialog-node-title,
.check-server-connections-dialog-status-text {
  overflow-wrap: anywhere;
  word-break: break-word;
}
}
@media (max-width: $breakpoint-xs-max) {
  .check-server-connections-dialog-node-row {
    row-gap: 0.5rem;
  }
}
</style>
