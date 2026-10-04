import { defineConfig } from 'vitest/config';
import { defineVitestProject } from '@nuxt/test-utils/config';
import tsconfigPaths from 'vite-tsconfig-paths';
import { fileURLToPath } from 'node:url';

const globalSetupFile = fileURLToPath(new URL('./tests/_base/global-auth-setup.ts', import.meta.url));
const routerMockSetupFile = fileURLToPath(new URL('./tests/setup-router-mock.ts', import.meta.url));

export default defineConfig({
	plugins: [
		tsconfigPaths(),
	],
	test: {
		projects: [
			await defineVitestProject({
				test: {
					name: 'nuxt',
					include: ['tests/nuxt/**/**/*.test.ts'],
					environment: 'nuxt',
					setupFiles: [globalSetupFile, routerMockSetupFile],
				},
			}),
		],
	},
});
