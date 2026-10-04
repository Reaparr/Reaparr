<template>
	<QRow>
		<QCol
			v-for="(item, index) in requirements"
			:key="index"
			cols="6">
			<QText :value="item.text">
				<template #prepend>
					<ValidIcon :valid="item.valid ? ValidationLevel.Valid : ValidationLevel.Invalid" />
				</template>
			</QText>
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { ValidationLevel } from '@enums';

const { t } = useI18n();

const isValid = defineModel<boolean>('isValid', {
	default: false,
});

const props = withDefaults(defineProps<{
	value?: string;
	minPasswordLength?: number;
}>(), {
	value: '',
	minPasswordLength: 8,
});

const requirements = computed((): { text: string; valid: boolean }[] => {
	const value = props.value ?? '';
	return [
		{
			// 'Has a capital letter'
			text: t('components.password-strength.validation.uppercase'),
			valid: /[A-Z]/.test(value),
		},
		{
			// 'Has a lowercase letter'
			text: t('components.password-strength.validation.lowercase'),
			valid: /[a-z]/.test(value),
		},
		{
			// 'Has a number'
			text: t('components.password-strength.validation.number'),
			valid: /[0-9]/.test(value),
		},
		{
			// 'Has a special character'
			text: t('components.password-strength.validation.symbol'),
			valid: /[^a-zA-Z0-9]/.test(value),
		},
		{
			// 'Longer than 7 characters'
			text: t('components.password-strength.validation.length'),
			valid: value.length >= props.minPasswordLength,
		},
	];
});

watchImmediate(() => get(requirements), () => {
	set(isValid, get(requirements).every((x) => x.valid));
});
</script>
