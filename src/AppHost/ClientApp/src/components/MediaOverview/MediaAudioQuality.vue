<template>
	<QHover v-if="quality !== AudioQuality.None">
		<template #default="{ }">
			<QGlowChip
				class="hover-expand-chip"
				:class="{ 'hover-expand-chip--compact': count > minCount }"
				:color="quality === AudioQuality.Unknown ? 'blue-grey-4' : 'cyan-6'"
				:clickable="clickable"
				size="md"
				has-background
				:value="count > minCount ? '' : label" />
			<template v-if="count > minCount">
				<q-tooltip
					anchor="bottom middle"
					self="top middle"
					:offset="[0, 0]"
					class="no-background tooltip-no-effect">
					<QGlowChip
						class="hover-expand-chip"
						:color="quality === AudioQuality.Unknown ? 'blue-grey-4' : 'cyan-6'"
						size="md"
						has-background
						:value="label" />
				</q-tooltip>
			</template>
		</template>
	</QHover>
</template>

<script setup lang="ts">
import { AudioQuality } from '@dto';

const props = withDefaults(defineProps<{
	quality: AudioQuality;
	truncated?: boolean;
	clickable?: boolean;
	count?: number;
}>(), {
	truncated: true,
	clickable: false,
	count: 1,
});

const { t } = useI18n();

const minCount = computed(() => props.truncated ? 1 : 100);
const label = computed(() => props.quality === AudioQuality.Unknown
	? t('general.error.unknown')
	: props.quality.replace(/(\d+)_(\d+)kHz/, '$1.$2 kHz')
			.replace(/(\d+)kHz/, '$1 kHz')
			.replace(/_Below/, ' <').replace(/_/g, ' ')
			.replace(/Bit/g, '-bit').replace(/KbpsPlus/, ' kbps+').replace(/Kbps/, ' kbps'));
</script>

<style lang="scss">
@use 'quasar/src/css/variables.sass' as quasar;

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
