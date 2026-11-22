<template>
	<QRow
		no-wrap
		align="center">
		<QCol>
			<QSubHeader>
				{{ help.label }}
			</QSubHeader>
		</QCol>
		<QCol
			v-if="hasHelpPage"
			cols="auto">
			<IconButton
				icon="mdi-help-circle-outline"
				class="q-ma-sm"
				@click="helpStore.openHelpDialog(help)" />
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { useHelpStore } from '@store';
import type { IHelp } from '@interfaces';

const props = withDefaults(defineProps<Partial<IHelp> & { value?: IHelp }>(), {
	label: '',
	title: '',
	text: '',
});

const { t } = useI18n();
const helpStore = useHelpStore();

const help = computed(() => props.value ?? {
	label: props.label !== '' ? props.label : t('help.default.label'),
	title: props.title,
	text: props.text,
});
const hasHelpPage = computed(() => {
	return get(help).title !== '' && get(help).text !== '';
});
</script>
