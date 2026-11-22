<template>
	<QCardDialog
		:name="DialogType.RefreshMediaDialog"
		width="600px"
		button-align="between"
		cy="refresh-mode-dialog">
		<template #title>
			{{ title }}
		</template>
		<template #default>
			<p>{{ text }}</p>
			<QAlert type="warning">
				{{ warning }}
			</QAlert>
		</template>
		<template #actions>
			<CancelButton
				cy="refresh-mode-cancel-button"
				@click="close" />
			<div class="row q-gutter-sm">
				<BaseButton
					color="negative"
					cy="refresh-mode-full-reset-button"
					:label="t('components.refresh-mode-dialog.full-reset')"
					@click="select(true)" />
				<BaseButton
					color="positive"
					cy="refresh-mode-incremental-button"
					:label="t('components.refresh-mode-dialog.incremental')"
					@click="select(false)" />
			</div>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { DialogType } from '@enums';
import { useDialogStore, useI18n } from '#imports';

const props = withDefaults(defineProps<{
	scope?: 'library' | 'server';
	libraryName?: string;
	serverName?: string;
}>(), {
	scope: 'library',
	libraryName: '',
	serverName: '',
});

const { t } = useI18n();
const dialogStore = useDialogStore();

const title = computed(() => props.scope === 'server'
	? t('components.refresh-mode-dialog.server.title', { serverName: props.serverName })
	: t('components.refresh-mode-dialog.library.title', { libraryName: props.libraryName }));
const text = computed(() => props.scope === 'server'
	? t('components.refresh-mode-dialog.server.text')
	: t('components.refresh-mode-dialog.library.text'));
const warning = computed(() => props.scope === 'server'
	? t('components.refresh-mode-dialog.server.full-reset-warning')
	: t('components.refresh-mode-dialog.library.full-reset-warning'));

function close(): void {
	dialogStore.closeDialog(DialogType.RefreshMediaDialog);
}
function select(forceMediaRefresh: boolean): void {
	close();
	emit('select', forceMediaRefresh);
}

const emit = defineEmits<{
	(e: 'select', forceMediaRefresh: boolean): void;
}>();
</script>
