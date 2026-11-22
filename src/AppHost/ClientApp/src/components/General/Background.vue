<template>
	<div :class="backgroundOverlay">
		<slot />
	</div>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@store';
import { destroyBackgroundEffect, setupBackgroundEffect } from '@/public/background-effect.js';

const props = withDefaults(defineProps<{ hideBackground?: boolean }>(), {
	hideBackground: false,
});

const settingsStore = useSettingsStore();
const $q = useQuasar();

const isDark = computed(() => {
	return $q.dark.isActive;
});
const backgroundOverlay = computed(() => {
	if (props.hideBackground) {
		return {
			'background-overlay': true,
			'no-background': true,
		};
	}
	return {
		'background-overlay': true,
		'dark-background': isDark.value,
		'light-background': !isDark.value,
	};
});

watch(
	() => settingsStore.generalSettings.disableAnimatedBackground,
	(value) => toggleAnimatedBackground(!value),
);

function toggleAnimatedBackground(state: boolean) {
	return state ? setupBackgroundEffect() : destroyBackgroundEffect();
}

onMounted(() =>
	toggleAnimatedBackground(!settingsStore.generalSettings.disableAnimatedBackground),
);
</script>

<style lang="scss">
.background-overlay {
  position: fixed;
  width: 100%;
  height: 100%;
  top: 0;
  z-index: -1;
}
</style>
