<template>
	<q-drawer
		v-model="showDrawer"
		:width="drawerWidth"
		:breakpoint="1023"
		side="right"
		overlay
		class="notification-drawer">
		<QCol class="notification-container">
			<q-scroll>
				<!-- Render All Notifications	-->
				<template v-if="notificationsStore.getVisibleNotifications.length > 0">
					<q-alert
						v-for="notification in notificationsStore.getVisibleNotifications"
						:key="notification.id"
						:min-width="200"
						:max-width="450"
						:type="notification.level"
						dense
						dismissible
						outlined
						elevation="10"
						@click="notificationsStore.hideNotification(notification.id)">
						<span
							class="text-wrap"
							style="overflow-wrap: anywhere">
							{{ notification.message }}
						</span>
					</q-alert>
				</template>
				<!-- No Notifications	-->
				<template v-else>
					<q-list>
						<q-item @click="clearAllNotifications">
							<q-item-section avatar>
								<q-icon name="mdi-check-circle-outline" />
							</q-item-section>
							<q-item-section>
								{{ t('components.notifications-drawer.no-notifications') }}
							</q-item-section>
						</q-item>
					</q-list>
				</template>
			</q-scroll>
		</QCol>
		<!-- Menu items -->
		<QCol
			v-if="notificationsStore.getVisibleNotifications.length > 0"
			class="clear-notifications-container">
			<q-list>
				<q-item
					clickable
					@click="clearAllNotifications">
					<q-item-section avatar>
						<q-icon name="mdi-close-circle" />
					</q-item-section>
					<q-item-section>
						{{ t('components.notifications-drawer.clear-notifications') }}
					</q-item-section>
				</q-item>
			</q-list>
		</QCol>
	</q-drawer>
</template>

<script setup lang="ts">
import { useNotificationsStore } from '@store';
import { set } from '@vueuse/core';

const notificationsStore = useNotificationsStore();
const { t } = useI18n();

const showDrawer = defineModel<boolean>('showDrawer', { default: false });
const $q = useQuasar();
const drawerWidth = computed(() => {
	if (!$q.screen.lt.md || !$q.screen.width)
		return 450;

	return Math.min(450, $q.screen.width * 0.92);
});

useEventListener(document, 'keydown', (event) => {
	if (event.key === 'Escape')
		set(showDrawer, false);
});

function clearAllNotifications() {
	notificationsStore.clearAllNotifications();
	set(showDrawer, false);
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.notification-drawer {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  position: absolute;
  top: calc($app-bar-height + 2px);
  right: 0;
  bottom: 0;
  left: auto;
  height: auto !important;

  .notification-container {
    overflow-y: auto;
    overflow-x: hidden;
    flex-grow: 3;
  }

  .clear-notifications-container {
    flex-grow: 0;
  }
}

.q-drawer {
  background-color: transparent;
}

@media (max-width: 1023px) {
  .notification-drawer {
    width: min(450px, 92vw) !important;
    max-width: 92vw;

    .notification-container {
      min-height: 0;
      overflow: hidden;
      flex: 1 1 auto;

      .q-scrollarea__content,
      .q-alert,
      .q-list {
        width: 100%;
        min-width: 0;
        max-width: 100%;
      }
    }

    .clear-notifications-container {
      flex: 0 0 auto;
    }
  }

  .body--dark .notification-drawer {
    background-color: var(--q-dark-page, #121212) !important;
    color: #fff;
  }

  .body--light .notification-drawer {
    background-color: var(--q-light-page, #fff) !important;
    color: #000;
  }
}
</style>
