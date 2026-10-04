import { afterEach, describe, expect, test } from 'vitest';
import { createApp, h, nextTick, reactive, type App } from 'vue';
import { createI18n } from 'vue-i18n';
import { Quasar } from 'quasar';
import PasswordStrength from '@components/Form/PasswordStrength.vue';

describe('PasswordStrength', () => {
	let app: App;

	afterEach(() => {
		app?.unmount();
	});

	test('Should reactively validate all required character categories', async () => {
		// Arrange
		const validity: boolean[] = [];
		const props = reactive({ value: 'Valid1!x' });
		app = createApp(() => h(PasswordStrength, {
			...props,
			'onUpdate:isValid': (value: boolean) => validity.push(value),
		})).use(Quasar).use(createI18n({ legacy: false, missingWarn: false, fallbackWarn: false }));
		app.mount(document.createElement('div'));
		expect(validity.at(-1)).toBe(true);

		// Act / Assert
		for (const password of ['valid1!x', 'VALID1!X', 'Valid!!x', 'Valid11x', 'Valid1-x', 'Ab1!xyz']) {
			props.value = password;
			await nextTick();
			expect(validity.at(-1)).toBe(false);
		}

		props.value = 'Valid1!x';
		await nextTick();
		expect(validity.at(-1)).toBe(true);
	});

	test('Should use the configured minimum length and react to value changes', async () => {
		// Arrange
		const props = reactive({ value: 'Ab1!', minPasswordLength: 5, isValid: false });
		app = createApp(() => h(PasswordStrength, {
			...props,
			'onUpdate:isValid': (value: boolean) => props.isValid = value,
		})).use(Quasar).use(createI18n({ legacy: false, missingWarn: false, fallbackWarn: false }));
		app.mount(document.createElement('div'));
		expect(props.isValid).toBe(false);

		// Act
		props.value = 'Ab1!x';
		await nextTick();

		// Assert
		expect(props.isValid).toBe(true);
	});

	test.each([undefined, null])('Should treat an absent password (%s) as empty', (password) => {
		let isValid = true;
		app = createApp(PasswordStrength, {
			value: password,
			isValid,
			'onUpdate:isValid': (value: boolean) => isValid = value,
		}).use(Quasar).use(createI18n({ legacy: false, missingWarn: false, fallbackWarn: false }));
		app.mount(document.createElement('div'));

		expect(isValid).toBe(false);
	});
});
