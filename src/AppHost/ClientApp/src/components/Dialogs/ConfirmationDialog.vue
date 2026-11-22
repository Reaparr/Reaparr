<template>
	<QCardDialog
		:id="id"
		persistent
		:name="name"
		width="500px"
		button-align="between"
		cy="confirmation-dialog">
		<template #title>
			{{ confirmationText.title }}
		</template>
		<template #default>
			<div>
				<p>{{ confirmationText.text }}</p>
				<p
					v-if="confirmationText.warning"
					class="text-center">
					<b>{{ confirmationText.warning }}</b>
				</p>
			</div>
		</template>
		<template #actions>
			<CancelButton
				cy="confirmation-dialog-cancel-button"
				:disabled="disableCancel"
				@click="cancel" />
			<ConfirmButton
				cy="confirmation-dialog-confirmation-button"
				:label="confirmLabel"
				:loading="confirmLoading"
				@click="confirm" />
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import type { DialogType } from '@enums';
import { useI18n } from 'vue-i18n';
import { useDialogStore } from '@store';

const props = withDefaults(defineProps<{
	name: DialogType;
	id?: number;
	title: string;
	text: string;
	warning?: string;
	confirmLabel?: string;
	confirmLoading?: boolean;
	disableCancel?: boolean;
}>(), {
	warning: '',
	id: 0,
	confirmLabel: '',
	confirmLoading: false,
	disableCancel: false,
});

const { t } = useI18n();
const dialogStore = useDialogStore();

const confirmationText = computed(() => ({
	title: props.title != '' ? props.title : t('confirmation.not-found.title'),
	text: props.text != '' ? props.text : t('confirmation.not-found.text'),
	warning: props.warning,
}));

function cancel() {
	emit('cancel');
	dialogStore.closeDialog(props.name, props.id);
}
function confirm() {
	emit('confirm');
}

const emit = defineEmits<{
	(e: 'confirm' | 'cancel'): void;
}>();
</script>
