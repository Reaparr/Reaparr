<template>
	<q-btn-dropdown
		class="activity-button"
		color="default"
		flat
		fab-mini
		rounded
		data-cy="background-activity-button"
		:menu-offset="[0, 6]">
		<template #label>
			<QGlowBadge
				v-if="activeCount > 0"
				:value="activeCount"
				data-cy="background-activity-button-badge" />
			<div class="activity-icon-container">
				<QSpinnerOval
					v-if="loading"
					class="spinner"
					color="primary"
					data-cy="background-activity-button-loading"
					:size="`${size + 2}px`" />

				<svg
					class="activity-icon"
					fill="currentColor"
					:viewBox="`0 0 48 48`"
					xmlns="http://www.w3.org/2000/svg">
					<path
						clip-rule="evenodd"
						d="M30.223 7.50024C30.8383 7.48916 31.3979 7.85502 31.6346 8.42308L37.5 22.5H44V25.5H36.5C35.8944 25.5 35.3483 25.1359 35.1154 24.5769L30.3234 13.0762L20.4045 39.5267C20.195 40.0853 19.6744 40.4667 19.0786 40.4979C18.4828 40.5292 17.9252 40.2044 17.6584 39.6708L10.5729 25.5H4V22.5H11.5C12.0682 22.5 12.5876 22.821 12.8416 23.3292L18.8033 35.2525L28.8455 8.47332C29.0616 7.8971 29.6077 7.51133 30.223 7.50024Z"
						fill="currentColor"
						fill-rule="evenodd" />
				</svg>
			</div>
		</template>
		<template #default>
			<q-list>
				<q-item
					v-for="(item, index) in menuItems"
					:key="index"
					v-close-popup
					:data-cy="item.cy"
					clickable>
					<q-item-section @click="item.action">
						{{ item.label }}
					</q-item-section>
				</q-item>
			</q-list>
		</template>
	</q-btn-dropdown>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { JobStatus, JobTypes, LibrarySyncJobStatus } from '@dto';
import { useSubscription } from '@vueuse/rxjs';
import { useBackgroundJobsStore, useDialogStore, useLibraryStore } from '@store';
import { useI18n } from '#imports';

const size = 32;

const checkPlexServerConnections = ref<Record<string, number[]>>({});

const { t } = useI18n();
const backgroundJobStore = useBackgroundJobsStore();
const dialogStore = useDialogStore();
const libraryStore = useLibraryStore();

const hasActiveLibrarySync = computed(() => libraryStore.syncQueues.some((queue) =>
	queue.status === LibrarySyncJobStatus.Queued || queue.status === LibrarySyncJobStatus.Processing));
const loading = computed(() => get(activeCount) > 0);
const activeCount = computed(() => {
	let count = 0;
	if (Object.keys(get(checkPlexServerConnections)).length > 0) {
		count++;
	}

	if (get(hasActiveLibrarySync)) {
		count++;
	}

	return count;
});
const menuItems = computed(() => {
	const items: {
		label: string;
		icon: string;
		cy: string;
		action?: () => void;
	}[] = [];

	if (get(checkPlexServerConnections) && Object.keys(get(checkPlexServerConnections)).length > 0) {
		items.push({
			label: t('components.background-activity-toggle-button.checking-plex-server-connections'),
			icon: 'mdi-server-network',
			cy: JobTypes.CheckAllConnectionsStatusByPlexServerJob + 'activity-button',
			action: () => dialogStore.openCheckServerConnectionsDialog({ plexServersWithConnectionIds: get(checkPlexServerConnections) }),
		});
	}

	if (get(hasActiveLibrarySync)) {
		items.push({
			label: t('components.background-activity-toggle-button.syncing-media'),
			icon: 'mdi-server-network',
			cy: JobTypes.LibrarySyncJob + 'activity-button',
			action: () => dialogStore.openSyncServerMediaDialog(),
		});
	}

	if (items.length === 0) {
		items.push({
			label: t('components.background-activity-toggle-button.no-active-background-activity'),
			icon: 'mdi-server-network',
			cy: 'no-background-activity-button',
		});
	}

	return items;
});

onMounted(() => {
	useSubscription(
		backgroundJobStore.getCheckPlexServerConnectionsJobUpdate(JobStatus.Started)
			.subscribe(({ data }) => {
				set(checkPlexServerConnections, data.plexServersWithConnectionIds ?? {});
			}),
	);

	useSubscription(
		backgroundJobStore.getCheckPlexServerConnectionsJobUpdate(JobStatus.Completed)
			.subscribe(() => {
				// Clear
				set(checkPlexServerConnections, {});
			}),
	);
});
</script>

<style lang="scss">
.activity-button {
  .q-btn-dropdown__arrow {
    display: none;
  }

  .activity-icon-container {
    position: absolute;
    top: 0;
    left: 0;

    .spinner {
      position: absolute;
      top: 0.25rem;
      left: 0.25rem;
      padding: 0 2px 2px 0;
    }

    .activity-icon {
      position: absolute;
      width: 1.5rem;
      height: 1.5rem;
      top: 0.5rem;
      left: 0.5rem;
    }
  }
}
</style>
