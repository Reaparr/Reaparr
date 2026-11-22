<template>
	<q-list class="editable-text">
		<q-item
			clickable
			class="editable-text-item items-center">
			<template v-if="!editMode">
				<q-item-section>
					<QText
						class="editable-text-display"
						:type="type"
						:size="size"
						:bold="bold"
						:value="displayText !== '' ? displayText : model"
						:align="align" />
				</q-item-section>
				<q-icon
					class="q-ml-sm"
					name="mdi-square-edit-outline"
					size="sm" />
			</template>
			<QPopupEdit
				v-slot="scope"
				:model-value="model"
				square
				auto-save
				@before-show="editMode = true"
				@before-hide="editMode = false"
				@save="model = $event">
				<q-input
					v-model="scope.value"
					dense
					autofocus
					:input-class="inputClasses"
					@keyup.enter="scope.set" />
			</QPopupEdit>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import type { IQTextProps } from '@interfaces';

const model = defineModel<string>();

const props = withDefaults(
	defineProps<
		Omit<IQTextProps, 'value'> & {
			displayText?: string;
			disabled?: boolean;
		}
	>(),
	{
		disabled: false,
		displayText: '',
		cy: '',
	},
);

const editMode = ref(false);

const inputClasses = computed(() => ({
	[`text-${props.size}`]: true,
	[`text-weight-${props.bold}`]: true,
}));
</script>

<style lang="scss">
.q-popup-edit {
  box-shadow: none !important;
  background: transparent !important;
  backdrop-filter: none !important;
}
</style>
