<template>
	<q-item class="server-connection-row">
		<!-- Radio Button -->
		<q-item-section
			class="server-connection-row__radio"
			avatar
			tag="label">
			<q-radio
				v-model="preferredConnectionId"
				:val="connection.id"
				color="red" />
		</q-item-section>
		<!-- Connection Icon -->
		<q-item-section
			class="server-connection-row__icon"
			avatar
			tag="label">
			<QConnectionIcon :type="connection.type" />
		</q-item-section>
		<!-- Connection Status -->
		<q-item-section
			class="server-connection-row__status"
			side>
			<QStatus :value="connection.latestConnectionStatus?.isSuccessful ?? false" />
		</q-item-section>
		<!-- Connection Url -->
		<q-item-section
			tag="label"
			class="connection-url-section">
			<span class="connection-url ml-2">{{ connection.url }}</span>
		</q-item-section>
		<q-space />
		<q-item-section
			v-if="connection.isCustom"
			class="server-connection-row__edit"
			side>
			<EditIconButton
				@click="dialogStore.openAddConnectionDialog({ plexServerId, plexServerConnectionId: connection.id })" />
		</q-item-section>

		<q-item-section
			class="server-connection-row__check"
			side>
			<CheckConnectionButton
				:loading="loading"
				:cy="`check-connection-btn-${connection.id}`"
				@click="checkPlexConnection(connection.id)" />
		</q-item-section>
	</q-item>
	<!-- Progress Text -->
	<CheckServerStatusProgressDisplay
		v-if="progressData"
		:progress="progressData" />
</template>

<script setup lang="ts">
import type { PlexServerConnectionDTO, ServerConnectionCheckStatusProgressDTO } from '@dto';
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { useDialogStore, useServerConnectionStore, useServerStore, useSignalrStore } from '@store';
import { map } from 'rxjs/operators';

const serverStore = useServerStore();
const signalrStore = useSignalrStore();
const dialogStore = useDialogStore();
const serverConnectionStore = useServerConnectionStore();

const progressData = ref<ServerConnectionCheckStatusProgressDTO | null>(null);

const props = defineProps<{
	connection: PlexServerConnectionDTO;
}>();

const plexServerId = computed<number>(() => props.connection.plexServerId);

const loading = computed(() => serverConnectionStore.getConnectionLoading(props.connection.id));
const preferredConnectionId = computed<number>({
	get: () => serverStore.getServer(get(plexServerId))?.preferredConnectionId ?? -1,
	set: (value) => {
		useSubscription(serverConnectionStore.setPreferredPlexServerConnection(get(plexServerId), value).subscribe());
	},
});

function checkPlexConnection(plexServerConnectionId: number) {
	useSubscription(
		serverConnectionStore.checkServerConnection(plexServerConnectionId).subscribe(),
	);
}
onMounted(() => useSubscription(
	signalrStore
		.getServerConnectionProgressByPlexServerId(get(plexServerId))
		.pipe(map((x) => x.find((y) => y.plexServerConnectionId === props.connection.id)))
		.subscribe((data) => {
			set(progressData, data ?? null);
		}),
));
</script>

<style lang="scss">
@media (max-width: $breakpoint-xs-max) {
  .server-connection-row {
    display: grid;
    grid-template-columns: auto auto auto 1fr auto;
    grid-template-areas:
      "radio icon status spacer edit"
      "url url url url check";
    gap: 0.5rem;
    padding-inline: 0;

    > .q-space {
      grid-area: spacer;
    }

    &__radio {
      grid-area: radio;
    }

    &__icon {
      grid-area: icon;
    }

    &__status {
      grid-area: status;
    }

    &__edit {
      grid-area: edit;
    }

    &__check {
      grid-area: check;
    }

    &__radio,
    &__icon {
      width: 44px;
      min-width: 44px;
      max-width: 44px;
      min-height: 44px;
      padding-right: 0;
    }

    &__status,
    &__edit,
    &__check {
      padding-left: 0;
    }

    .connection-url-section {
      grid-area: url;
      min-width: 0;
    }

    .connection-url {
      margin-left: 0;
      overflow-wrap: anywhere;
      word-break: break-word;
    }
  }
}
</style>
