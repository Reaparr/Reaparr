<template>
	<q-drawer
		id="navigation-drawer"
		v-model="showDrawer"
		class="navigation-drawer"
		:width="drawerWidth"
		:breakpoint="1023"
		:behavior="$q.screen.gt.sm ? 'desktop' : 'mobile'"
		show-if-above
		bordered
		style="overflow-x: hidden">
		<QCol class="server-drawer-container">
			<q-scroll>
				<!-- Server drawer -->
				<ServerDrawer />
			</q-scroll>
		</QCol>
		<QCol class="menu-items">
			<q-separator />
			<!-- Menu items -->
			<QExpansionList :items="getNavItems" />
		</QCol>
	</q-drawer>
</template>

<script setup lang="ts">
import type { QExpansionListProps } from '@interfaces/components/QExpansionListProps';
import { useSettingsStore, useDownloadStore } from '@store';

const showDrawer = defineModel<boolean>('showDrawer', { default: false });
const settingsStore = useSettingsStore();
const downloadStore = useDownloadStore();
const { t } = useI18n();
const $q = useQuasar();
const drawerWidth = computed(() => {
	if (!$q.screen.lt.md || !$q.screen.width)
		return 400;

	return Math.min(400, $q.screen.width * 0.92);
});

const getNavItems = computed((): QExpansionListProps[] => {
	const mainItems: QExpansionListProps[] = [
		{
			title: t('components.navigation-drawer.downloads'),
			icon: 'mdi-download',
			link: '/downloads',
			type: 'badge',
			count: downloadStore.getActiveDownloadList().length,
		},
		{
			title: t('components.navigation-drawer.settings'),
			icon: 'mdi-cog',
			link: '/settings',
			children: [
				{
					title: t('components.navigation-drawer.accounts'),
					icon: 'mdi-account',
					link: '/settings/accounts',
				},
				{
					title: t('components.navigation-drawer.paths'),
					icon: 'mdi-folder',
					link: '/settings/paths',
				},
				{
					title: t('components.navigation-drawer.ui'),
					icon: 'mdi-television-guide',
					link: '/settings/ui',
				},
				{
					title: t('components.navigation-drawer.integrations'),
					icon: 'mdi-antenna',
					link: '/settings/integrations',
				},
				{
					title: t('components.navigation-drawer.advanced'),
					icon: 'mdi-wrench',
					link: '/settings/advanced',
				},
				{
					title: t('components.navigation-drawer.logs'),
					icon: 'mdi-text-box-search-outline',
					link: '/settings/logs',
				},
			],
		},
	];

	if (settingsStore.debugMode) {
		mainItems.push({
			title: t('components.navigation-drawer.debug'),
			icon: 'mdi-bug-outline',
			children: [
				{
					title: t('components.navigation-drawer.scratchpad'),
					icon: 'mdi-note-edit',
					link: '/debug-pages/scratchpad',
				},
				{
					title: t('components.navigation-drawer.dialogs'),
					icon: 'mdi-dock-window',
					link: '/debug-pages/dialogs',
				},
				{
					title: t('components.navigation-drawer.buttons'),
					icon: 'mdi-button-pointer',
					link: '/debug-pages/buttons',
				},
			],
		});
	}
	return mainItems;
});
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss';

.navigation-drawer {
  display: flex;
  flex-direction: column;
  justify-content: space-between;

  .server-drawer-container {
    overflow-y: auto;
    overflow-x: hidden;

    flex-grow: 3;
  }

  .menu-items {
    flex-grow: 0;
  }
}

@media (max-width: 1023px) {
  .navigation-drawer {
    width: min(400px, 92vw) !important;
    max-width: 92vw;
    background-color: variables.$dark-lg-background-color;
    backdrop-filter: blur(10px);
    overflow-x: hidden;

    .server-drawer-container {
      min-height: 0;
      overflow: hidden;
      flex: 1 1 auto;
    }

    .q-scrollarea__content {
      width: 100%;
      min-width: 0;
      max-width: 100%;
    }

    .q-expansion-item,
    .q-list {
      width: 100%;
      min-width: 0;
      max-width: 100%;
    }

    .menu-items {
      flex: 0 0 auto;
    }
  }

  body.body--light .navigation-drawer {
    background-color: variables.$light-lg-background-color !important;
  }

  // Keep the app bar above mobile drawers so the toggle remains reachable.
  .q-drawer:has(> .navigation-drawer) {
    top: variables.$app-bar-height !important;
    height: calc(100dvh - #{variables.$app-bar-height}) !important;
  }

  .q-drawer-container:has(> .q-drawer > .navigation-drawer) > .q-drawer__backdrop {
    top: variables.$app-bar-height !important;
    height: calc(100dvh - #{variables.$app-bar-height}) !important;
  }
}

@media (min-width: 1024px) {
  .navigation-drawer {
    border-right: 1px solid variables.$separator-dark-color;
  }

  body.body--light .navigation-drawer {
    border-right-color: variables.$separator-color;
  }
}
</style>
