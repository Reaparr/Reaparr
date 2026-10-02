<template>
	<section
		class="server-stats-artwork"
		:class="[
			`server-stats-artwork--${mode}`,
			{ 'server-stats-artwork--export': exportMode },
		]"
		data-cy="server-stats-artwork"
		:data-mode="mode"
		:aria-label="t('components.server-dialog.tabs.server-stats.header')">
		<header class="server-stats-artwork__header">
			<h2>{{ serverName }}</h2>
			<span v-if="mode === 'summary'">{{ t('components.server-dialog.tabs.server-stats.collection') }}</span>
		</header>
		<div
			v-if="mode === 'summary'"
			class="server-stats-artwork__hero">
			<strong
				data-cy="server-stats-total-size"
				:aria-label="t('components.server-dialog.tabs.server-stats.total-size')">{{ formattedSize }}</strong>
		</div>
		<div
			class="server-stats-artwork__metrics"
			:class="{ 'server-stats-artwork__metrics--summary': mode === 'summary' }">
			<span
				v-if="mode === 'detailed'"
				class="server-stats-artwork__compact-size"
				data-cy="server-stats-total-size"
				:aria-label="t('components.server-dialog.tabs.server-stats.total-size')">{{ formattedSize }}</span>
			<ServerStatsCount
				v-for="metric in metrics"
				:key="metric.mediaType"
				:media-type="metric.mediaType"
				:count="metric.count"
				:icon-size="mode === 'summary' ? 40 : 28" />
		</div>
		<div
			v-if="mode === 'detailed' && stats.libraries.length"
			class="server-stats-artwork__libraries"
			data-cy="server-stats-library-grid">
			<ServerStatsLibraryCard
				v-for="library in stats.libraries"
				:key="library.id"
				:server-enabled="serverEnabled"
				:library="library" />
		</div>
		<footer
			v-if="statusLabel"
			class="server-stats-artwork__status"
			:data-status="serverEnabled ? stats.status : 'disabled'"
			data-cy="server-stats-indexing-status">
			<span
				class="server-stats-artwork__status-dot"
				aria-hidden="true" />
			<span>{{ statusLabel }}</span>
		</footer>
	</section>
</template>

<script setup lang="ts">
import { PlexMediaType } from '@dto';
import type { IServerStats } from '@interfaces';
import prettyBytes from 'pretty-bytes';
import { get } from '@vueuse/core';

const props = withDefaults(defineProps<{
	serverName: string;
	serverEnabled: boolean;
	stats: IServerStats;
	mode: 'summary' | 'detailed';
	exportMode?: boolean;
}>(), { exportMode: false });
const { t, locale } = useI18n();
const statusLabel = computed(() => !props.serverEnabled
	? t('components.server-dialog.tabs.server-stats.disabled')
	: props.stats.status === 'complete'
		? null
		: {
				'not-indexed': t('components.server-dialog.tabs.server-stats.not-indexed'),
				partial: t('components.server-dialog.tabs.server-stats.partial'),
				'no-enabled-libraries': t('components.server-dialog.tabs.server-stats.no-enabled-libraries'),
			}[props.stats.status]);
const formattedSize = computed(() => props.stats.hasIndexedData
	? prettyBytes(props.stats.mediaSize, { locale: get(locale) })
	: props.serverEnabled ? t('components.server-dialog.tabs.server-stats.not-indexed') : '—');
const metrics = computed(() => [
	{ mediaType: PlexMediaType.Movie, count: props.stats.hasIndexedData ? props.stats.movieCount : null },
	{ mediaType: PlexMediaType.TvShow, count: props.stats.hasIndexedData ? props.stats.tvShowCount : null },
	{ mediaType: PlexMediaType.Season, count: props.stats.hasIndexedData ? props.stats.seasonCount : null },
	{ mediaType: PlexMediaType.Episode, count: props.stats.hasIndexedData ? props.stats.episodeCount : null },
] as const);
</script>

<style scoped lang="scss">
.server-stats-artwork {
	--server-stats-accent: var(--q-primary);
	--server-stats-surface: var(--q-dark-page);
	--server-stats-raised: var(--q-dark);
	--server-stats-text: #fff;
	--server-stats-muted: #bdbdbd;
	--server-stats-line: rgb(255 255 255 / 12%);
	--server-stats-columns: minmax(0, 1.4fr) repeat(4, minmax(0, 1fr));
	--server-stats-library-columns: minmax(0, 2.4fr) repeat(3, minmax(0, 1fr));
	--server-stats-row-padding: 16px;
	container: server-stats / inline-size;
	box-sizing: border-box;
	position: relative;
	width: 100%;
	padding: 24px;
	border: 1px solid color-mix(in srgb, var(--server-stats-accent) 40%, var(--server-stats-line));
	border-radius: 18px 4px;
	background:
		radial-gradient(ellipse at 50% 35%, color-mix(in srgb, var(--server-stats-accent) 12%, transparent), transparent 65%),
		linear-gradient(135deg, rgb(255 255 255 / 4%), transparent 40%),
		var(--server-stats-surface);
	box-shadow: inset 0 1px rgb(255 255 255 / 8%), 0 0 32px color-mix(in srgb, var(--server-stats-accent) 10%, transparent);
	color: var(--server-stats-text);

	&__header {
		min-width: 0;

		h2 {
			margin: 0;
			font-size: clamp(24px, 6cqi, 64px);
			font-weight: 800;
			letter-spacing: -0.035em;
			line-height: 1.15;
			overflow-wrap: anywhere;
		}

		span {
			display: block;
			margin-top: 12px;
			color: var(--server-stats-muted);
			font-size: clamp(12px, 2cqi, 22px);
			letter-spacing: 0.08em;
		}
	}

	&--detailed {
		text-align: left;

		.server-stats-artwork__header h2 {
			text-align: left;
		}
	}

	&--summary {
		display: grid;
		grid-template-rows: auto minmax(min-content, 1fr) auto;
		gap: 24px;
		aspect-ratio: 1;
		text-align: center;

		&::before {
			content: '';
			position: absolute;
			inset: 12px;
			border: 1px solid var(--server-stats-line);
			border-top-color: color-mix(in srgb, var(--server-stats-accent) 65%, transparent);
			border-radius: 10px 2px;
			pointer-events: none;
		}

		&::after {
			content: '';
			position: absolute;
			inset: 36px 5px;
			border-inline: 3px dashed color-mix(in srgb, var(--server-stats-accent) 45%, transparent);
			pointer-events: none;
		}
	}

	&__hero {
		display: flex;
		flex-direction: column;
		justify-content: center;
		align-items: center;
		gap: 16px;
		min-height: 0;

		&::after {
			content: '';
			width: 45%;
			height: 2px;
			margin-top: 12px;
			background: var(--server-stats-accent);
			box-shadow: 0 0 12px var(--server-stats-accent), 0 0 32px color-mix(in srgb, var(--server-stats-accent) 50%, transparent);
		}

		strong {
			font-size: clamp(32px, 16cqi, 160px);
			font-weight: 900;
			letter-spacing: -0.055em;
			line-height: 1.1;
			color: var(--server-stats-text);
			font-variant-numeric: tabular-nums;
			text-shadow: 0 2px 0 rgb(255 255 255 / 15%), 0 0 60px color-mix(in srgb, var(--server-stats-accent) 30%, transparent);
			overflow-wrap: anywhere;
		}
	}

	&__metrics {
		display: grid;
		grid-template-columns: var(--server-stats-columns);
		gap: 12px;
		margin: 20px 0;
		padding: var(--server-stats-row-padding);
		border: 1px solid var(--server-stats-line);
		border-top-color: color-mix(in srgb, var(--server-stats-accent) 55%, transparent);
		border-radius: 10px 2px;
		background: var(--server-stats-raised);
		box-shadow: inset 0 1px rgb(255 255 255 / 5%);
		font-size: 16px;

		&--summary {
			grid-template-columns: repeat(2, minmax(0, 1fr));
			gap: 16px;
			margin: 0;
			padding: 0;
			border: none;
			background: transparent;
			font-size: clamp(18px, 5.5cqi, 64px);

			:deep(.server-stats-count) {
				gap: 8px;
				padding: clamp(12px, 3cqi, 32px);
				border: 1px solid var(--server-stats-line);
				border-radius: 12px 2px;
				background: linear-gradient(135deg, rgb(255 255 255 / 4%), transparent), var(--server-stats-raised);
				box-shadow: inset 3px 0 var(--server-stats-accent), inset 0 1px rgb(255 255 255 / 6%);
			}

			:deep(.server-stats-count .q-media-type-icon) {
				filter: drop-shadow(0 0 8px color-mix(in srgb, var(--server-stats-accent) 45%, transparent));
			}
		}
	}

	&__compact-size {
		align-self: center;
		color: var(--server-stats-muted);
		font-weight: 600;
		font-variant-numeric: tabular-nums;
	}

	&__libraries {
		display: grid;
		grid-template-columns: minmax(0, 1fr);
		gap: 12px;
	}

	&__status {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 8px;
		margin-top: 20px;
		color: var(--server-stats-muted);
		font-size: 12px;
		line-height: 1.5;
	}

	&__status-dot {
		flex: 0 0 6px;
		height: 6px;
		border-radius: 50%;
		background: var(--server-stats-accent);
		box-shadow: 0 0 8px color-mix(in srgb, var(--server-stats-accent) 65%, transparent);
	}

	&--export {
		width: 1200px;
		padding: 48px;
		--server-stats-row-padding: 20px;

		.server-stats-artwork__status {
			font-size: 18px;
		}

		&.server-stats-artwork--summary {
			height: 1200px;
		}

		&.server-stats-artwork--detailed {
			.server-stats-artwork__header h2 {
				font-size: 40px;
			}

			.server-stats-artwork__metrics {
				font-size: 26px;
			}

			:deep(.server-stats-library__header h3) {
				font-size: 20px;
			}

			:deep(.server-stats-library__counts), :deep(.server-stats-library__size) {
				font-size: 26px;
			}

			:deep(.server-stats-library__state) {
				font-size: 16px;
			}
		}
	}
}

@container server-stats (max-width: 620px) {
	.server-stats-artwork__metrics:not(.server-stats-artwork__metrics--summary), :deep(.server-stats-library) {
		--server-stats-columns: repeat(2, minmax(0, 1fr));
		--server-stats-library-columns: repeat(2, minmax(0, 1fr));
	}

	.server-stats-artwork__metrics:not(.server-stats-artwork__metrics--summary) {
		.server-stats-artwork__compact-size {
			grid-column: 1;
			grid-row: 1 / span 4;
		}

		:deep(.server-stats-count) {
			grid-column: 2;
		}
	}

	:deep(.server-stats-library__counts) {
		display: flex;
		grid-column: 2;
		grid-row: 1 / span 2;
		flex-direction: column;
		gap: 12px;
	}
}

@container server-stats (max-width: 340px) {
	.server-stats-artwork__metrics--summary :deep(.q-media-type-icon) {
		font-size: 24px !important;
	}
}
</style>
