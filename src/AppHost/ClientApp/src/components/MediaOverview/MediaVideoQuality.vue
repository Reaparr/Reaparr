<template>
	<QHover>
		<template #default="{ }">
			<QGlowChip
				class="hover-expand-chip"
				:class="{ 'hover-expand-chip--compact': count > minCount }"
				:clickable="clickable"
				:color="getQualityDisplay(quality).color"
				size="md"
				has-background
				:value="count > minCount ? '' : getQualityDisplay(quality).label" />
			<template v-if="count > minCount">
				<q-tooltip
					anchor="bottom middle"
					self="top middle"
					:offset="[0, 0]"
					class="no-background tooltip-no-effect">
					<QGlowChip
						class="hover-expand-chip"
						:color="getQualityDisplay(quality).color"
						size="md"
						has-background
						:value="getQualityDisplay(quality).label" />
				</q-tooltip>
			</template>
		</template>
	</QHover>
</template>

<script setup lang="ts">
import Log from 'consola';
import { VideoQuality } from '@dto';
import type { IMediaActionEmits } from '@interfaces';
import { translateVideoQuality } from '@composables';

const props = withDefaults(defineProps<{
	quality: VideoQuality;
	truncated?: boolean; // Whether to truncate the display of qualities
	// Whether the chips are clickable
	clickable?: boolean;
	count?: number;
}>(), {
	truncated: true,
	clickable: false,
	count: 1,
});

defineEmits<IMediaActionEmits>();
const minCount = computed(() => {
	return props.truncated ? 1 : 100;
});

const getQualityDisplay = (quality: VideoQuality): {
	color: string;
	label: string;
} => {
	switch (quality) {
		case VideoQuality.None:
			return {
				color: 'grey-7',
				label: translateVideoQuality(VideoQuality.None),
			};
		case VideoQuality.SubSD144P:
			return {
				color: 'brown-6',
				label: translateVideoQuality(VideoQuality.SubSD144P),
			};
		case VideoQuality.SubSDCIF:
			return {
				color: 'deep-orange-6',
				label: translateVideoQuality(VideoQuality.SubSDCIF),
			};
		case VideoQuality.NHD:
			return {
				color: 'orange-7',
				label: translateVideoQuality(VideoQuality.NHD),
			};
		case VideoQuality.SD:
			return {
				color: 'amber-7',
				label: translateVideoQuality(VideoQuality.SD),
			};
		case VideoQuality.DVD:
			return {
				color: 'yellow-7',
				label: translateVideoQuality(VideoQuality.DVD),
			};
		case VideoQuality.HD:
			return {
				color: 'light-green-13',
				label: translateVideoQuality(VideoQuality.HD),
			};
		case VideoQuality.FullHD:
			return {
				color: 'light-blue-6',
				label: translateVideoQuality(VideoQuality.FullHD),
			};
		case VideoQuality.QHD:
			return {
				color: 'cyan-6',
				label: translateVideoQuality(VideoQuality.QHD),
			};
		case VideoQuality.UHD_4K:
			return {
				color: 'red darken-4',
				label: translateVideoQuality(VideoQuality.UHD_4K),
			};
		case VideoQuality.UHD_8K:
			return {
				color: 'purple-8',
				label: translateVideoQuality(VideoQuality.UHD_8K),
			};
		case VideoQuality.Unknown:
			return {
				color: 'blue-grey-4',
				label: translateVideoQuality(VideoQuality.Unknown),
			};
		default:
			Log.error('Missing quality display mapping for', quality);
			return {
				color: 'blue-grey-4',
				label: translateVideoQuality(VideoQuality.None),
			};
	}
};
</script>

<style lang="scss">
@use 'quasar/src/css/variables.sass' as quasar;
@use '@/assets/scss/variables' as *;

@media (max-width: quasar.$breakpoint-sm-max) {
  .hover-expand-chip {
    margin: 2px;
    min-height: 32px;
    padding-inline: 0.3rem;
    font-size: clamp(0.65rem, calc(0.5rem + 0.75vw), 0.875rem) !important;
  }

  .hover-expand-chip--compact {
    width: 32px;
    min-width: 32px;
    max-width: 32px;
    height: 32px;
    padding: 0;
    border-radius: 50%;
  }

  .hover-expand-chip .q-text {
    font-size: inherit !important;
    line-height: 1.1;
  }
}
</style>
