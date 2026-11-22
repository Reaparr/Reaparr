<template>
	<QRow
		class="q-mb-md"
		justify="between">
		<QCol cols="auto">
			<QText size="h5">
				{{ $t('components.server-dialog.tabs.server-connections.section-header') }}
			</QText>
		</QCol>
		<QCol cols="auto">
			<IconButton
				icon="mdi-cloud-sync"
				:loading="checkAllLoading"
				:cy="'check-all-connections-btn'"
				:tooltip-text="$t('components.server-dialog.tabs.server-connections.check-all-connections')"
				class="q-mr-md"
				@click="checkAllConnections" />
		</QCol>
	</QRow>

	<q-list>
		<!-- Plex Connections -->
		<ServerConnectionDisplayRow
			v-for="connection in connections.plexApiConnections"
			:key="connection.id"
			:connection="connection" />
		<!-- Separator -->
		<QSeparator />
		<!-- Custom Connections -->
		<ServerConnectionDisplayRow
			v-for="connection in connections.customConnections"
			:key="connection.id"
			:connection="connection" />
		<!-- Add Connection -->
		<q-item>
			<!-- Connection Url -->
			<q-item-section tag="label">
				<BaseButton
					:label="$t('components.server-dialog.tabs.server-connections.add-connection-button')"
					@click="dialogStore.openAddConnectionDialog({ plexServerId, plexServerConnectionId: 0 })" />
			</q-item-section>
			<q-space />
		</q-item>
		<AddConnectionDialog />
	</q-list>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import type { PlexServerConnectionDTO } from '@dto';
import { useServerConnectionStore, useDialogStore } from '@store';

const props = defineProps<{
	plexServerId: number;
	isVisible: boolean;
}>();

const dialogStore = useDialogStore();
const serverConnectionStore = useServerConnectionStore();

const checkAllLoading = computed(() =>
	serverConnectionStore.isAnyConnectionLoadingForServer(props.plexServerId),
);
const connections = computed((): {
	plexApiConnections: PlexServerConnectionDTO[];
	customConnections: PlexServerConnectionDTO[];
} => {
	const allConnections = serverConnectionStore.getServerConnectionsByServerId(props.plexServerId);
	return {
		customConnections: allConnections.filter((x) => x.isCustom),
		plexApiConnections: allConnections.filter((x) => !x.isCustom),
	};
});

function checkAllConnections(): void {
	useSubscription(
		serverConnectionStore.checkServerStatus(props.plexServerId).subscribe(),
	);
}
</script>
