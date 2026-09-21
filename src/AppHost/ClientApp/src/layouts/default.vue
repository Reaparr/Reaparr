<template>
	<!--	Instead of multiple layouts we merge into one default layout to prevent full
        page change (flashing white background) during transitions.	-->
	<q-layout
		view="hHh LpR lFf">
		<PageLoadOverlay :loading="isLoading">
			<!--	Use for everything else	-->
			<template v-if="!isEmptyLayout">
				<AppBar
					:show-navigation-drawer-state="showNavigationDrawerState"
					@show-navigation="toggleNavigationsDrawer"
					@show-notifications="toggleNotificationsDrawer" />
				<NavigationDrawer
					v-model:show-drawer="showNavigationDrawerState" />
				<NotificationsDrawer
					v-model:show-drawer="showNotificationsDrawerState" />
			</template>
			<!--	page-load-completed is only visible once the page is done loading. This is used for Cypress E2E	-->
			<q-page-container data-cy="page-load-completed">
				<slot />
			</q-page-container>
			<!--	Dialogs	-->
			<HelpDialog />
			<AlertDialog
				v-for="alertItem in alerts"
				:key="alertItem.id"
				:alert="alertItem" />
			<CheckServerConnectionsDialog />
			<DiscordInviteDialog />
			<SyncServerMediaDialog />
			<RefreshAccountAccessDialog />
			<UpdateAvailableDialog />
			<IntegrationSetupDialog />
		</PageLoadOverlay>
		<!--	Background	-->
		<Background :hide-background="isEmptyLayout || isLoading" />
	</q-layout>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import type { IAlert } from '@interfaces';
import { DialogType } from '@enums';
import {
	useHelpStore,
	useAlertStore,
	useGlobalStore,
	useDialogStore,
	useSettingsStore,
	useAuthenticationStore,
	useNuxtApp,
} from '#imports';

const nuxtApp = useNuxtApp();
const route = useRoute();
const router = useRouter();

const helpStore = useHelpStore();
const alertStore = useAlertStore();
const dialogStore = useDialogStore();
const settingsStore = useSettingsStore();
const globalStore = useGlobalStore();
const authStore = useAuthenticationStore();

const alerts = ref<IAlert[]>([]);
const $q = useQuasar();
const showNavigationDrawerState = ref($q.screen.gt.sm);
const showNotificationsDrawerState = ref(false);

const pageLoading = ref(true);
const pageApiLoading = ref(true);

const isLoading = computed((): boolean => get(pageLoading) || get(pageApiLoading));
const isEmptyLayout = computed((): boolean => route.fullPath.includes('setup') || route.fullPath.includes('login'),
);

function toggleNavigationsDrawer() {
	set(showNavigationDrawerState, !get(showNavigationDrawerState));
}

function toggleNotificationsDrawer() {
	set(showNotificationsDrawerState, !get(showNotificationsDrawerState));
}

watch(() => $q.screen.gt.sm, (isDesktop) => {
	set(showNavigationDrawerState, isDesktop);
	set(showNotificationsDrawerState, false);
});

watch(() => route.fullPath, () => {
	if (!$q.screen.gt.sm) {
		set(showNotificationsDrawerState, false);
		set(showNavigationDrawerState, false);
	}
});

nuxtApp.hook('page:start', () => {
	Log.debug('page:start');
	set(pageLoading, true);
});

nuxtApp.hook('page:finish', () => {
	Log.debug('page:finish');
	set(pageLoading, false);
	if (authStore.isLoggedIn) {
		setTimeout(() => {
			if (settingsStore.generalSettings.firstTimeSetup && !route.fullPath.includes('setup')) {
				router.push('/setup');
			} else if (!settingsStore.generalSettings.hasBeenInvitedToDiscord && !route.fullPath.includes('setup')) {
				dialogStore.openDialog(DialogType.DiscordServerInviteDialog);
			}
		}, 1000);
	}
});

onMounted(() => {
	useSubscription(
		globalStore.getPageSetupReady.subscribe({
			next: (ready) => {
				if (ready) {
					Log.debug('PageSetup API calls have finished');
					if (authStore.isLoggedIn && route.fullPath.includes('login')) {
						// Keep the overlay up through the router transition — page:finish will clear pageLoading,
						// and we clear pageApiLoading only after the navigation resolves so there is no flash.
						router.push('/').finally(() => set(pageApiLoading, false));
					} else {
						set(pageApiLoading, false);
					}
				} else {
					set(pageApiLoading, true);
				}
			},
			error: (err) => {
				Log.error('Error while loading API data', err);
				set(pageApiLoading, true);
			},
		}),
	);

	useSubscription(
		helpStore.getHelpDialog.subscribe((newHelpObject) => {
			if (newHelpObject) {
				dialogStore.openHelpInfoDialog(newHelpObject);
			}
		}),
	);

	useSubscription(
		alertStore.getAlerts.subscribe((newAlerts) => {
			if (newAlerts) {
				set(alerts, newAlerts);
				// Allow the alert dialog to render first before opening it
				nextTick(() => {
					for (const newAlert of get(alerts)) {
						dialogStore.openAlertInfoDialog(newAlert);
					}
				});
			}
		}),
	);

	useEventListener(window, 'resize', () => {
		if (document.body.classList.contains('window-resizing')) {
			return;
		}
		document.body.classList.add('window-resizing');
		setTimeout(() => document.body.classList.remove('window-resizing'), 100);
	});
});
</script>
