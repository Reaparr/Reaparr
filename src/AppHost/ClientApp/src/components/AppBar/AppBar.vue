<template>
	<q-header class="app-bar">
		<q-toolbar
			v-if="$q.screen.gt.sm"
			class="app-bar">
			<q-toolbar-title>
				<q-btn
					flat
					dense
					:data-cy="'navigation-drawer-toggle'"
					:icon="showNavigationDrawerState ? 'mdi-arrow-collapse-left' : 'mdi-arrow-collapse-right'"
					class="q-mr-sm"
					@click.stop="showNavigationDrawer" />
				<q-btn
					to="/"
					flat
					class="q-pa-sm">
					<div
						class="row items-center no-wrap"
						style="height: 2rem;">
						<img
							src="/img/logo/reaparr-full.svg"
							alt="Reaparr"
							style="height: 125%; width: auto;">
						<img
							src="/img/logo/reaparr-title.svg"
							alt="Reaparr"
							style="height: 100%; width: auto; margin-left: 0.5rem; margin-top: 3px;">
					</div>
				</q-btn>
				<!-- Copy Version Number -->
				<IconButton
					class="q-pa-none"
					@click="copy(globalStore.version)">
					<q-icon name="mdi-alpha-v-circle-outline" />
					<q-tooltip
						anchor="bottom middle"
						self="top middle"
						:offset="[10, 10]">
						{{ $t('components.app-bar.copy-version', { version: globalStore.version }) }}
					</q-tooltip>
				</IconButton>
				<!-- Update Button -->
				<IconButton
					icon="mdi-download-circle-outline"
					class="update-button q-mr-sm"
					:class="{ 'update-button--pulse': updateStore.hasUpdateAvailable }"
					@click="openUpdateDialog">
					<q-tooltip
						anchor="bottom middle"
						self="top middle"
						:offset="[10, 10]">
						{{ $t('components.app-bar.update-available') }}
					</q-tooltip>
				</IconButton>
			</q-toolbar-title>

			<ExternalLink href="https://github.com/Reaparr/Reaparr">
				<IconButton
					icon="mdi-github"
					style="padding: 0.5rem" />
			</ExternalLink>

			<IconButton
				style="padding: 0.5rem"
				@click="dialogStore.openDialog(DialogType.DiscordServerInviteDialog)">
				<DiscordIcon />
			</IconButton>

			<!-- Background Activity Toggle -->
			<BackgroundActivityToggleButton />

			<!-- Account Selector -->
			<AccountSelector />

			<!-- Notifications Selector -->
			<NotificationButton @toggle="showNotificationsDrawer" />
		</q-toolbar>

		<q-toolbar
			v-else
			class="app-bar__toolbar">
			<q-btn
				flat
				dense
				round
				data-cy="navigation-drawer-toggle"
				:aria-label="$t('components.app-bar.navigation-menu')"
				:aria-expanded="showNavigationDrawerState"
				aria-controls="navigation-drawer"
				:icon="showNavigationDrawerState ? 'mdi-arrow-collapse-left' : 'mdi-menu'"
				@click.stop="showNavigationDrawer" />

			<q-btn
				to="/"
				flat
				class="app-bar__brand q-pa-xs"
				aria-label="Reaparr">
				<img
					src="/img/logo/reaparr-full.svg"
					alt=""
					class="app-bar__logo">
				<img
					src="/img/logo/reaparr-title.svg"
					alt="Reaparr"
					class="app-bar__title-logo">
			</q-btn>

			<q-space />

			<q-btn-dropdown
				class="app-bar__overflow"
				flat
				round
				dense
				icon="mdi-dots-vertical"
				:aria-label="$t('components.app-bar.more-actions')">
				<q-list>
					<q-item
						v-close-popup
						clickable
						@click="copy(globalStore.version)">
						<q-item-section avatar>
							<q-icon name="mdi-alpha-v-circle-outline" />
						</q-item-section>
						<q-item-section>
							{{ $t('components.app-bar.copy-version', { version: globalStore.version }) }}
						</q-item-section>
					</q-item>
					<q-item
						v-close-popup
						clickable
						@click="openUpdateDialog">
						<q-item-section avatar>
							<q-icon name="mdi-download-circle-outline" />
						</q-item-section>
						<q-item-section>{{ $t('components.app-bar.update-available') }}</q-item-section>
					</q-item>
					<q-item
						clickable
						tag="a"
						href="https://github.com/Reaparr/Reaparr"
						target="_blank">
						<q-item-section avatar>
							<q-icon name="mdi-github" />
						</q-item-section>
						<q-item-section>{{ githubLabel }}</q-item-section>
					</q-item>
					<q-item
						v-close-popup
						clickable
						@click="dialogStore.openDialog(DialogType.DiscordServerInviteDialog)">
						<q-item-section avatar>
							<DiscordIcon />
						</q-item-section>
						<q-item-section>{{ discordLabel }}</q-item-section>
					</q-item>
				</q-list>
			</q-btn-dropdown>

			<BackgroundActivityToggleButton />
			<AccountSelector />
			<NotificationButton @toggle="showNotificationsDrawer" />
		</q-toolbar>
	</q-header>
</template>

<script setup lang="ts">
import { useGlobalStore, useDialogStore, useUpdateStore } from '@store';
import { DialogType } from '@enums';
import { useClipboard } from '@vueuse/core';

const globalStore = useGlobalStore();
const dialogStore = useDialogStore();
const updateStore = useUpdateStore();
const $q = useQuasar();

const { copy } = useClipboard({ legacy: true });
const githubLabel = 'GitHub';
const discordLabel = 'Discord';

defineProps<{
	showNavigationDrawerState?: boolean;
}>();

const emit = defineEmits<{
	(e: 'show-navigation' | 'show-notifications'): void;
}>();

function showNavigationDrawer(): void {
	emit('show-navigation');
}

function showNotificationsDrawer(): void {
	emit('show-notifications');
}

function openUpdateDialog(): void {
	dialogStore.openDialog(DialogType.UpdateAvailableDialog);
}
</script>

<style lang="scss">
@use '@/assets/scss/variables' as *;

@media (max-width: $breakpoint-sm-max) {
  .app-bar {
    z-index: 4000;
  }

  .app-bar__toolbar {
    min-width: 0;
    gap: 0.25rem;
  }

  .app-bar__toolbar .q-btn {
    min-width: 44px !important;
    min-height: 44px !important;
  }

  .app-bar__brand {
    min-width: 0;
  }

  .app-bar__logo {
    height: 2.5rem;
    width: auto;
  }

  .app-bar__title-logo {
    height: 2rem;
    width: auto;
    margin-left: 0.5rem;
  }

  .app-bar__overflow {
    display: inline-flex;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .app-bar__title-logo {
    display: none;
  }

  .app-bar__logo {
    height: 2rem;
  }

  .app-bar__toolbar {
    padding-inline: 0.25rem;
  }

}

body {
  &.body--dark {
    .app-bar {
      background: rgba(255, 0, 0, 0.2) !important;
    }
  }

  &.body--light {
    .app-bar {
      background: rgba(255, 0, 0, 1) !important;
    }
  }
}
</style>
