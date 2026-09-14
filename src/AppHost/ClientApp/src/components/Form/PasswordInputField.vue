<template>
	<q-input
		v-model="model"
		lazy-rules
		:rules="props.rules ?? (disableValidation ? [(v: string): boolean | string =>
			!!v || t('components.password-input-field.validation.password-is-required')] : getPasswordRules)"
		color="red"
		full-width
		outlined
		required
		hide-bottom-space
		name="password"
		autocomplete="current-password"
		:data-cy="cy"
		:append-icon="showPassword ? 'mdi-eye' : 'mdi-eye-off'"
		:type="showPassword ? 'text' : 'password'"
		@focus="hasFocus = true"
		@blur="onBlur"
		@click:append="showPassword = !showPassword">
		<template
			v-if="!hideMaskButton"
			#append>
			<q-btn
				flat
				:icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
				@click="showPassword = !showPassword" />
		</template>
	</q-input>
</template>

<script setup lang="ts">
import { set } from '@vueuse/core';

const { t } = useI18n();
const model = defineModel<string>({
	default: '',
});

const hasFocus = defineModel<boolean>('hasFocus');

const showPassword = ref(false);

type ValidationRule = (value: string) => boolean | string;

const props = withDefaults(defineProps<{
	hideMaskButton?: boolean;
	rules?: ValidationRule[];
	cy?: string;
	showStrength?: boolean;
	minPasswordLength?: number;
	disableValidation?: boolean;
}>(), {
	hideMaskButton: false,
	cy: 'password-input-field',
	showStrength: false,
	minPasswordLength: 8,
	disableValidation: false,
});

const emits = defineEmits<{
	(e: 'blur'): void;
}>();

const getPasswordRules = computed(() => [
	// Rule: Password is required
	(v: string): boolean | string =>
		!!v || t('components.password-input-field.validation.password-is-required'),

	// Rule: Minimum password length
	(v: string): boolean | string =>
		(v && v.length >= props.minPasswordLength)
		|| t('components.password-input-field.validation.password-length', { count: props.minPasswordLength }),

	// Rule: Contains an uppercase letter
	(v: string): boolean | string =>
		(v && /[A-Z]/.test(v))
		|| t('components.password-input-field.validation.password-is-missing-capital-letter'), // 'Has a capital letter'

	// Rule: Contains a lowercase letter
	(v: string): boolean | string =>
		(v && /[a-z]/.test(v))
		|| t('components.password-input-field.validation.password-is-missing-lowercase-letter'), // 'Has a lowercase letter'

	// Rule: Contains a number
	(v: string): boolean | string =>
		(v && /[0-9]/.test(v))
		|| t('components.password-input-field.validation.password-is-missing-number'),

	// Rule: Contains a special character
	(v: string): boolean | string =>
		(v && /[!@#$%^&*(),.?":{}|<>]/.test(v))
		|| t('components.password-input-field.validation.password-is-missing-special-symbol'),
]);

function onBlur() {
	set(hasFocus, false);
	emits('blur');
}
</script>
