<template>
	<QCardDialog
		:name="DialogType.ServerSettingsDialog"
		content-height="80"
		:loading="loading"
		cy="server-dialog-cy"
		@opened="open"
		@closed="close">
		<template #title>
			<EditableText
				size="h5"
				bold="medium"
				:display-text="
					$t('components.server-dialog.header', {
						serverName: serverStore.getServerName(plexServer?.id ?? 0) ?? $t('general.error.unknown'),
					})
				"
				:value="serverStore.getServerName(plexServer?.id ?? 0)"
				@save="onServerAliasSave" />
		</template>
		<template #default>
			<QRow
				align="start"
				class="server-dialog-layout"
				full-height>
				<QCol
					cols="auto"
					class="server-dialog-tabs"
					align-self="stretch">
					<!-- Tab Index -->
					<q-tabs
						v-model="tabIndex"
						:vertical="$q.screen.gt.xs"
						active-color="red">
						<!--	Server Data	Tab Header -->
						<q-tab
							name="server-data"
							icon="mdi-server"
							data-cy="server-dialog-tab-1"
							:aria-label="$t('components.server-dialog.tabs.server-data.header')"
							:label="$t('components.server-dialog.tabs.server-data.header')" />
						<!--	Server Connections Tab Header	-->
						<q-tab
							name="server-connection"
							icon="mdi-connection"
							data-cy="server-dialog-tab-2"
							:aria-label="$t('components.server-dialog.tabs.server-connections.header')"
							:label="$t('components.server-dialog.tabs.server-connections.header')" />
						<!--	Server Configuration Tab Header	-->
						<q-tab
							name="server-config"
							icon="mdi-cog-box"
							data-cy="server-dialog-tab-3"
							:aria-label="$t('components.server-dialog.tabs.server-config.header')"
							:label="$t('components.server-dialog.tabs.server-config.header')" />
						<!--	Server Libraries Tab Header	-->
						<q-tab
							name="server-libraries"
							icon="mdi-bookshelf"
							data-cy="server-dialog-tab-4"
							:aria-label="$t('components.server-dialog.tabs.server-libraries.header')"
							:label="$t('components.server-dialog.tabs.server-libraries.header')" />
						<!--	Server Commands Tab Header	-->
						<q-tab
							name="server-commands"
							icon="mdi-console"
							data-cy="server-dialog-tab-5"
							:aria-label="$t('components.server-dialog.tabs.server-commands.header')"
							:label="$t('components.server-dialog.tabs.server-commands.header')" />
					</q-tabs>
				</QCol>
				<QCol
					ref="tabContent"
					align-self="stretch"
					class="tab-content inherit-all-height scroll">
					<!-- Tab Content -->
					<q-tab-panels
						v-model="tabIndex"
						animated
						:vertical="$q.screen.gt.xs"
						:transition-prev="$q.screen.gt.xs ? 'slide-down' : 'slide-right'"
						:transition-next="$q.screen.gt.xs ? 'slide-up' : 'slide-left'">
						<!-- Server Data Tab Content -->
						<q-tab-panel
							name="server-data"
							data-cy="server-dialog-tab-content-1">
							<ServerDataTabContent
								:plex-server="plexServer"
								:is-visible="isVisible" />
						</q-tab-panel>

						<!-- Server Connections Tab Content	-->
						<q-tab-panel
							name="server-connection"
							data-cy="server-dialog-tab-content-2">
							<ServerConnectionsTabContent
								:plex-server-id="plexServerId"
								:is-visible="isVisible" />
						</q-tab-panel>

						<!--	Server Configuration Tab Content	-->
						<q-tab-panel
							name="server-config"
							data-cy="server-dialog-tab-content-3">
							<ServerConfigTabContent :plex-server="plexServer" />
						</q-tab-panel>

						<!--	Server Libraries Tab Content -->
						<q-tab-panel
							name="server-libraries"
							data-cy="server-dialog-tab-content-4">
							<ServerLibrariesTabContent
								:plex-server="plexServer"
								:plex-libraries="libraryStore.getAllLibrariesByServerId(plexServerId)" />
						</q-tab-panel>

						<!--	Server Commands -->
						<q-tab-panel
							name="server-commands"
							data-cy="server-dialog-tab-content-5">
							<ServerCommandsTabContent
								:plex-server-id="plexServerId"
								:is-visible="isVisible" />
						</q-tab-panel>
					</q-tab-panels>
				</QCol>
			</QRow>
		</template>
		<template #actions>
			<QRow justify="end">
				<QCol cols="auto">
					<BaseButton
						cy="server-dialog-close-btn"
						flat
						:label="$t('general.commands.close')"
						color="default"
						@click="close" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import type { ComponentPublicInstance } from 'vue';
import { get, set } from '@vueuse/core';
import type { PlexServerDTO } from '@dto';
import { DialogType } from '@enums';
import { useServerStore, useLibraryStore, useDialogStore } from '@store';

const serverStore = useServerStore();
const $q = useQuasar();
const libraryStore = useLibraryStore();
const dialogStore = useDialogStore();

const loading = ref(false);
const tabContent = ref<ComponentPublicInstance | null>(null);
const tabIndex = ref<string>('server-data');
const plexServer = ref<PlexServerDTO | null>(null);
const plexServerId = ref<number>(0);

const isVisible = computed((): boolean => plexServerId.value > 0);

watch(tabIndex, async () => {
	await nextTick();
	const element = tabContent.value?.$el;
	if (element instanceof HTMLElement) {
		element.scrollTop = 0;
	}
});

function open(event: unknown): void {
	const newPlexServerId = event as number;
	set(plexServerId, newPlexServerId);
	set(loading, true);

	set(plexServer, serverStore.getServer(newPlexServerId));
	set(loading, false);
}

function close(): void {
	dialogStore.closeDialog(DialogType.ServerSettingsDialog);
	set(plexServerId, 0);
	set(tabIndex, 'server-data');
}

function onServerAliasSave(serverAlias: string): void {
	useSubscription(serverStore.setServerAlias(get(plexServerId), serverAlias).subscribe());
}
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.tab-content {
  max-height: calc(80vh - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
}

.editable-text {
  &-item {
    padding-top: 0;
    padding-bottom: 0;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .server-dialog-layout {
    flex-direction: column;
    flex-wrap: nowrap;
    height: 100%;
    min-height: 0;
  }

  .server-dialog-tabs {
    width: 100%;
    min-width: 0;
    max-width: 100%;
    flex: 0 0 48px;
    overflow: hidden;

    .q-tabs {
      width: 100%;
      height: 48px;
    }

    .q-tabs__content {
      flex-wrap: nowrap;
    }

    .q-tab {
      width: auto;
      min-width: 44px;
      min-height: 48px;
      padding: 0;
      flex: 1 1 20%;
    }

    .q-tab__label {
      display: none;
    }

    .q-tab__icon {
      margin: 0;
    }
  }

  .tab-content {
    width: 100%;
    min-width: 0;
    min-height: 0;
    height: calc(100% - 48px);
    max-height: calc(100% - 48px) !important;
    flex: 1 1 auto;
    overflow: auto !important;

    .q-tab-panels,
    .q-panel,
    .q-tab-panel,
    .q-list {
      width: 100%;
      min-width: 0;
      max-width: 100%;
    }

    .q-tab-panel {
      width: auto;
      box-sizing: border-box;
      overflow-x: hidden;
      padding: 0.75rem;
    }
  }
}
</style>
