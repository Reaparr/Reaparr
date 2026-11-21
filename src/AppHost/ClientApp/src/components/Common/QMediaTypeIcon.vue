<template>
	<q-icon
		:class="{
			'q-media-type-icon': true,
			'q-media-type-icon--inactive': !props.active,
			'q-media-type-icon--loading': props.loading,
		}"
		:name="icon"
		:size="size + 'px'" />
</template>

<script setup lang="ts">
import Convert from '@class/Convert';
import { PlexMediaType } from '@dto';

const props = withDefaults(defineProps<{
	mediaType?: PlexMediaType;
	size?: number;
	active?: boolean;
	loading?: boolean;
}>(), {
	mediaType: PlexMediaType.None,
	size: 24,
	active: true,
	loading: false,
});

const icon = computed((): string => {
	return Convert.mediaTypeToIcon(props.mediaType);
});
</script>

<style lang="scss">
.q-media-type-icon {
  opacity: 1;

  &--inactive {
    opacity: 0.5;
  }

  @keyframes pulse {
    0% {
      opacity: 0.5;
    }
    50% {
      opacity: 1;
    }
    100% {
      opacity: 0.5;
    }
  }

  &--loading {
    animation: pulse 1.5s infinite;
  }
}
</style>
