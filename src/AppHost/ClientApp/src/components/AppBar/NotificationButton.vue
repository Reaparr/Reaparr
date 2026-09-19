<template>
	<q-btn
		dense
		round
		flat
		icon="mdi-bell"
		aria-label="Notifications"
		data-cy="notifications-button"
		@click="toggleNotificationDrawer">
		<QGlowBadge
			v-if="notificationsStore.getVisibleNotifications.length > 0"
			color="positive"
			:value="notificationsStore.getVisibleNotifications.length" />
	</q-btn>
</template>

<script setup lang="ts">
import { useNotificationsStore } from '@store';

const notificationsStore = useNotificationsStore();

const emit = defineEmits<{ (e: 'toggle'): void }>();

function toggleNotificationDrawer() {
	emit('toggle');
}
</script>
