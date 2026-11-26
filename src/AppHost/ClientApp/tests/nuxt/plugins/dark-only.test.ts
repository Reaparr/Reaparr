import { afterAll, describe, expect, test } from 'vitest';
import { Dark } from 'quasar';
import quasarDefaults from '@/plugins/1.quasar-defaults.client';
import theme from '@/assets/scss/primevue/reaparr-theme';

const originalSet = Dark.set;

afterAll(() => {
	Dark.set = originalSet;
	Dark.set(true);
});

describe('Dark-only application theme', () => {
	test('Should keep framework state and body dark after light, automatic, and toggle requests', async () => {
		// Arrange
		await quasarDefaults(useNuxtApp());

		// Act / Assert
		for (const requested of [false, 'auto', true] as const) {
			Dark.set(requested);
			expect(Dark.isActive).toBe(true);
			expect(Dark.mode).toBe(true);
			expect(document.body.classList.contains('body--dark')).toBe(true);
			expect(document.body.classList.contains('body--light')).toBe(false);
		}
		Dark.toggle();
		expect(Dark.isActive).toBe(true);
		expect(document.body.classList.contains('body--light')).toBe(false);
		expect(theme.options.darkModeSelector).toBe(':root');
	});
});
