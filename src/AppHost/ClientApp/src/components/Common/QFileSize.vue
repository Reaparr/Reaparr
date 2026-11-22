<template>
	<QText
		:cy="cy"
		:align="align"
		:value="formattedString" />
</template>

<script setup lang="ts">
import prettyBytes from 'pretty-bytes';
import { useLocalizationStore } from '@store';
import type { IQTextProps } from '@interfaces';

const props = defineProps<Pick<IQTextProps, 'align'> & {
	size: number;
	speed?: boolean;
	cy?: string;
}>();

const localizationStore = useLocalizationStore();
const { t } = useI18n();

const formattedString = computed(() => {
	if (props.size == null || props.size === 0) {
		return '-';
	}

	const bytes = prettyBytes(props.size, { locale: localizationStore.getLanguageLocale.bcp47Code || 'en-US' });
	return `${bytes}${props.speed ? t('general.units.per-second') : ''}`;
});
</script>
