<template>
	<article
		class="server-stats-library"
		data-cy="server-stats-library-card"
		:data-library-id="library.id">
		<header class="server-stats-library__header">
			<h3>{{ library.title }}</h3>
			<span
				v-if="!library.isEnabled"
				class="server-stats-library__state">
				{{ t('components.server-dialog.tabs.server-stats.disabled') }}
			</span>
		</header>
		<span
			v-if="library.syncedAt || (serverEnabled && library.isEnabled)"
			class="server-stats-library__size">
			{{ library.syncedAt ? formattedSize : t('components.server-dialog.tabs.server-stats.not-indexed') }}
		</span>
		<div class="server-stats-library__counts">
			<div
				v-for="metric in metrics"
				:key="metric.mediaType"
				class="server-stats-library__metric">
				<ServerStatsCount
					:media-type="metric.mediaType"
					:count="metric.count"
					:icon-size="28" />
			</div>
		</div>
	</article>
</template>

<script setup lang="ts">
import { type PlexLibraryDTO, PlexMediaType } from '@dto';
import prettyBytes from 'pretty-bytes';
import { get } from '@vueuse/core';

const props = defineProps<{ library: PlexLibraryDTO; serverEnabled: boolean }>();
const { t, locale } = useI18n();
const formattedSize = computed(() => prettyBytes(Math.max(0, props.library.mediaSize), { locale: get(locale) }));
const metrics = computed(() => {
	const indexed = !!props.library.syncedAt;
	const count = (value: number) => indexed ? Math.max(0, value) : null;
	if (props.library.type === PlexMediaType.TvShow) {
		return [
			{ mediaType: PlexMediaType.TvShow, count: count(props.library.count) },
			{ mediaType: PlexMediaType.Season, count: count(props.library.seasonCount) },
			{ mediaType: PlexMediaType.Episode, count: count(props.library.episodeCount) },
		] as const;
	}
	return [{ mediaType: PlexMediaType.Movie, count: count(props.library.count) }] as const;
});
</script>

<style scoped lang="scss">
.server-stats-library {
	display: grid;
	grid-template-columns: var(--server-stats-library-columns);
	grid-template-rows: auto auto;
	grid-auto-flow: column;
	align-items: center;
	gap: 12px;
	min-width: 0;
	padding: var(--server-stats-row-padding);
	border: 1px solid var(--server-stats-line);
	border-top-color: color-mix(in srgb, var(--server-stats-accent) 55%, transparent);
	border-radius: 10px 2px;
	background: linear-gradient(135deg, rgb(255 255 255 / 3%), transparent), var(--server-stats-raised);
	box-shadow: inset 0 1px rgb(255 255 255 / 5%);
	color: var(--server-stats-accent);
	font-size: 16px;

	&__header {
		display: flex;
		grid-column: 1;
		flex-direction: column;
		align-items: flex-start;
		min-width: 0;
		text-align: left;
		gap: 6px;

		h3 {
			margin: 0;
			font-size: 14px;
			font-weight: 700;
			line-height: 1.35;
			text-align: left;
			color: var(--server-stats-text);
			overflow-wrap: anywhere;
		}
	}

	&__size {
		grid-column: 1;
		min-width: 0;
		color: var(--server-stats-muted);
		font-weight: 600;
		font-variant-numeric: tabular-nums;
		overflow-wrap: anywhere;
	}

	&__state {
		font-size: 12px;
		color: var(--server-stats-muted);
	}

	&__counts {
		display: contents;
	}

	&__metric {
		grid-row: 1 / span 2;
		min-width: 0;
	}
}
</style>
