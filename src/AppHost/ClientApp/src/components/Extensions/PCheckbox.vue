<template>
	<q-checkbox
		:model-value="value"
		color="red"
		class="ma-3 pt-0"
		hide-details
		@click="handleInput()" />
</template>

<script setup lang="ts">
const props = withDefaults(
	defineProps<{
		value?: boolean;
	}>(),
	{
		value: false,
	},
);

function handleInput(): void {
	emit('input', !props.value);
}

const emit = defineEmits<{
	(e: 'input', save: boolean): void;
}>();
</script>
