<template>
	<BaseButton
		class="validation-button"
		:loading="loading"
		:cy="cy"
		:color="validationStyle.color"
		:icon="validationStyle.icon"
		:label="validationStyle.text" />
</template>

<script setup lang="ts">
import { get, set, watchOnce } from '@vueuse/core';
import { useI18n } from 'vue-i18n';

const props = withDefaults(defineProps<{
	loading?: boolean;
	isValidated?: boolean;
	triggerOnce?: boolean;
	defaultIcon?: string;
	cy?: string;
}>(), {
	watchOnce: false,
	loading: false,
	isValidated: false,
	defaultIcon: 'mdi-text-box-search-outline',
	cy: 'validation-button',
});

const isExecuted = ref(false);

const { t } = useI18n();

const validationStyle = computed((): {
	color: 'default' | 'positive' | 'warning' | 'negative';
	icon: string;
	text: string;
} => {
	if (!get(isExecuted) || props.loading) {
		return {
			color: 'default',
			icon: props.defaultIcon,
			text: t('general.commands.validate'),
		};
	}

	if (props.isValidated) {
		return {
			color: 'positive',
			icon: 'mdi-check-bold',
			text: '',
		};
	} else {
		return {
			color: 'negative',
			icon: 'mdi-alert-circle-outline',
			text: t('general.commands.validate'),
		};
	}
});

if (props.triggerOnce) {
	watchOnce(() => props.loading, () => set(isExecuted, true));
} else {
	whenever(() => props.loading, () => set(isExecuted, true));
}
whenever(() => !props.loading, () => setTimeout(() => set(isExecuted, false), 4000));
</script>

<style lang="scss">
.validation-button {
  transition: all 0.5s;
}
</style>
