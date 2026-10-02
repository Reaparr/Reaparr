<template>
	<div
		class="server-stats-tab"
		data-cy="server-stats-tab">
		<q-tabs
			v-model="mode"
			class="q-mb-md"
			align="left"
			active-color="primary"
			:aria-label="t('components.server-dialog.tabs.server-stats.header')">
			<q-tab
				name="summary"
				:label="t('components.server-dialog.tabs.server-stats.summary')"
				data-cy="server-stats-summary-tab" />
			<q-tab
				name="detailed"
				:label="t('components.server-dialog.tabs.server-stats.detailed')"
				data-cy="server-stats-detailed-tab" />
		</q-tabs>
		<div
			class="server-stats-content"
			data-cy="server-stats-content">
			<ServerStatsArtwork
				:server-name="serverName"
				:server-enabled="serverEnabled"
				:stats="stats"
				:mode="mode" />
		</div>
		<Teleport
			v-if="actionsTarget"
			:to="actionsTarget">
			<BaseButton
				cy="server-stats-export-image"
				icon="mdi-download"
				:label="t('components.server-dialog.tabs.server-stats.export-image')"
				:loading="exporting"
				:disabled="exporting"
				@click="exportImage" />
		</Teleport>
		<div
			v-if="exportSnapshot"
			ref="exportContainer"
			class="server-stats-export-container"
			aria-hidden="true"
			inert>
			<ServerStatsArtwork
				:server-name="exportSnapshot.serverName"
				:server-enabled="exportSnapshot.serverEnabled"
				:stats="exportSnapshot.stats"
				:mode="exportSnapshot.mode"
				export-mode />
		</div>
	</div>
</template>

<script setup lang="ts">
import { cloneDeep } from 'lodash-es';
import { exportFile } from 'quasar';
import Log from 'consola';
import type { IServerStats } from '@interfaces';
import { useLibraryStore, useServerStore } from '@store';
import { renderServerStatsPng } from '../ServerStats/renderServerStatsPng';
import { get, set } from '@vueuse/core';

const props = defineProps<{
	plexServerId: number;
	actionsTarget: HTMLElement | null;
}>();
const { t } = useI18n();
const $q = useQuasar();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();
const mode = ref<'summary' | 'detailed'>('summary');
const exporting = ref(false);
const exportContainer = useTemplateRef<HTMLElement>('exportContainer');
const exportSnapshot = shallowRef<{
	serverName: string;
	serverEnabled: boolean;
	stats: IServerStats;
	mode: 'summary' | 'detailed';
} | null>(null);
const serverName = computed(() => serverStore.getServerName(props.plexServerId) || t('general.error.unknown'));
const serverEnabled = computed(() => serverStore.getServer(props.plexServerId)?.isEnabled ?? false);
const stats = computed(() => {
	const data = libraryStore.getServerStats(props.plexServerId);
	return {
		...data,
		libraries: data.libraries.map((library) => ({ ...library, title: libraryStore.getLibraryName(library.id) })),
	};
});

async function exportImage(): Promise<void> {
	if (get(exporting)) {
		return;
	}
	set(exporting, true);
	const snapshot = cloneDeep({ serverName: get(serverName), serverEnabled: get(serverEnabled), stats: get(stats), mode: get(mode) });
	set(exportSnapshot, snapshot);
	try {
		await nextTick();
		const artwork = get(exportContainer)?.firstElementChild;
		if (!(artwork instanceof HTMLElement)) {
			throw new Error('Server stats artwork is unavailable');
		}
		const image = await renderServerStatsPng(artwork);
		const name = snapshot.serverName.replace(/[^\p{L}\p{N}._-]+/gu, '-').replace(/^-+|-+$/g, '').slice(0, 100) || 'server';
		if (exportFile(`${name}-server-stats-${snapshot.mode}.png`, image, 'image/png') !== true) {
			throw new Error('Browser rejected the server stats download');
		}
	} catch (error) {
		Log.error('Failed to export server stats', error);
		$q.notify({ type: 'negative', message: t('components.server-dialog.tabs.server-stats.export-error') });
	} finally {
		set(exportSnapshot, null);
		set(exporting, false);
	}
}
</script>

<style scoped lang="scss">
.server-stats-tab {
	display: flex;
	flex-direction: column;
	height: 100%;
	min-height: 0;

	> .q-tabs {
		flex: 0 0 auto;
	}
}

.server-stats-content {
	flex: 1 1 auto;
	min-height: 0;
	overflow: auto;
	overscroll-behavior: contain;
}

.server-stats-export-container {
	position: fixed;
	left: -10000px;
	top: 0;
	width: 1200px;
	pointer-events: none;
}
</style>
