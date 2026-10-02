<template>
	<div
		class="server-stats-count"
		role="img"
		tabindex="0"
		:aria-label="accessibleName"
		:data-media-type="mediaType"
		@focus="tooltipVisible = true"
		@blur="tooltipVisible = false">
		<QMediaTypeIcon
			:media-type="mediaType"
			:size="iconSize"
			aria-hidden="true" />
		<span class="server-stats-count__value">{{ formattedCount }}</span>
		<q-tooltip v-model="tooltipVisible">
			{{ accessibleName }}
		</q-tooltip>
	</div>
</template>

<script setup lang="ts">
import { PlexMediaType } from '@dto';
import { get } from '@vueuse/core';

const props = withDefaults(defineProps<{
	mediaType: PlexMediaType.Movie | PlexMediaType.TvShow | PlexMediaType.Season | PlexMediaType.Episode;
	count: number | null;
	iconSize?: number;
}>(), { iconSize: 24 });

const { t, locale } = useI18n();
const tooltipVisible = ref(false);
const labels = computed(() => ({
	[PlexMediaType.Movie]: t('components.server-dialog.tabs.server-stats.movies'),
	[PlexMediaType.TvShow]: t('components.server-dialog.tabs.server-stats.tv-shows'),
	[PlexMediaType.Season]: t('components.server-dialog.tabs.server-stats.seasons'),
	[PlexMediaType.Episode]: t('components.server-dialog.tabs.server-stats.episodes'),
}));
const numberFormat = computed(() => new Intl.NumberFormat(get(locale)));
const formattedCount = computed(() => props.count === null ? '—' : get(numberFormat).format(props.count));
const accessibleName = computed(() => t('components.server-dialog.tabs.server-stats.media-count', {
	type: get(labels)[props.mediaType],
	count: props.count === null ? t('components.server-dialog.tabs.server-stats.not-indexed') : get(formattedCount),
}));
</script>

<style scoped lang="scss">
.server-stats-count {
	display: flex;
	align-items: center;
	justify-content: flex-start;
	gap: 6px;
	min-width: 0;
	color: var(--server-stats-accent);

	&:focus-visible {
		outline: 2px solid currentColor;
		outline-offset: 4px;
		border-radius: 4px;
	}

	&__value {
		min-width: 0;
		overflow-wrap: anywhere;
		color: var(--server-stats-text);
		font-weight: 800;
		font-variant-numeric: tabular-nums;
	}
}
</style>
