import { fileURLToPath } from 'url';
import { defineNuxtConfig } from 'nuxt/config';

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
	modules: [
		// Doc: https://github.com/Maiquu/nuxt-quasar
		'nuxt-quasar-ui', // Doc: https://primevue.org/nuxt/
		'@vueuse/nuxt', // Doc: https://i18n.nuxtjs.org/
		'@primevue/nuxt-module',
		'@nuxtjs/i18n',
		'@nuxt/test-utils/module',
		'@pinia/nuxt',
		'@nuxt/eslint',
	],
	ssr: false,
	components: {
		dirs: [
			// Components directory
			{
				path: '~/components',
				pathPrefix: false,
				extensions: ['vue'],
			},
		],
	},

	imports: {
		dirs: ['store'],
	},
	devtools: { enabled: false },
	app: {
		head: {
			title: 'Reaparr',
			script: [
				{ src: 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r134/three.min.js' },
				{ src: 'https://cdn.jsdelivr.net/npm/vanta/dist/vanta.waves.min.js' },
			],
			// .dark added to ensure PrimeVue uses the dark mode styling
			htmlAttrs: { class: 'dark' },
			meta: [{ name: 'color-scheme', content: 'dark' }],
			link: [
				{ rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
			],
		},
	},

	// Global CSS: https://nuxt.com/docs/api/configuration/nuxt-config#css
	css: [
		'@/assets/scss/style.scss',
	],
	runtimeConfig: {
		// Config within public will be also exposed to the client
		public: {
			nodeEnv: process.env.NODE_ENV || 'development',
			apiPort: process.env.API_PORT || '5000',
			platform: process.env.PLATFORM || 'UNKNOWN',
			version: process.env.VERSION || '9.9.9',
		},
	},
	dir: {
		app: 'app',
		public: 'src/public',
	},
	srcDir: 'src',

	alias: {
		// Doc: https://nuxt.com/docs/api/configuration/nuxt-config#alias
		'@class': fileURLToPath(new URL('./src/types/class/', import.meta.url)),
		'@dto': fileURLToPath(new URL('./src/types/api/generated/data-contracts.ts', import.meta.url)),
		'@api': fileURLToPath(new URL('./src/types/api/', import.meta.url)),
		'@api-urls': fileURLToPath(new URL('./src/types/api/api-paths.ts', import.meta.url)),
		'@const': fileURLToPath(new URL('./src/types/const/', import.meta.url)),
		'@composables': fileURLToPath(new URL('./src/composables/', import.meta.url)),
		'@props': fileURLToPath(new URL('./src/types/props/', import.meta.url)),
		'@fixtures': fileURLToPath(new URL('./cypress/fixtures/', import.meta.url)),
		'@services-test-base': fileURLToPath(new URL('./tests/_base/base.ts', import.meta.url)),
		'@store': fileURLToPath(new URL('./src/store/', import.meta.url)),
		'@enums': fileURLToPath(new URL('./src/types/enums/', import.meta.url)),
		'@mock': fileURLToPath(new URL('./src/mock-data/', import.meta.url)),
		'@factories': fileURLToPath(new URL('./src/mock-data/factories/', import.meta.url)),
		'@interfaces': fileURLToPath(new URL('./src/types/interfaces/', import.meta.url)),
		'@components': fileURLToPath(new URL('./src/components/', import.meta.url)),
	},

	build: {
		transpile: ['primevue'],
	},
	compatibilityDate: '2024-07-25',

	nitro: {
		prerender: {
			crawlLinks: true,
		},
	},

	vite: {
		css: {
			preprocessorOptions: {
				scss: {
					// TODO remove import when https://github.com/Maiquu/nuxt-quasar/pull/106 is merged
					silenceDeprecations: ['import', 'global-builtin', 'legacy-js-api'],
				},
			},
		},
	},

	typescript: {
		// Doc: https://typescript.nuxtjs.org/guide/setup.html#configuration
		// Packages,  @types/node, vue-tsc and typescript are required
		strict: true,
	},
	// Doc: https://nuxtjs.org/docs/configuration-glossary/configuration-telemetry
	// loading: true, // TODO Maybe better to re-enable based on how it looks
	telemetry: false,
	eslint: {
		config: {
			stylistic: {
				indent: 'tab',
				semi: true,
				arrowParens: true,
				blockSpacing: true,
				braceStyle: '1tbs',
				commaDangle: 'always-multiline',
				quotes: 'single',
				quoteProps: 'as-needed',
			},
		},
	},

	i18n: {
		autoDeclare: true,
		restructureDir: 'src',
		langDir: 'lang',
		defaultLocale: 'en-US', // Ensure the SettingsStore is updated as well when changes are made here:
		locales: [
			{
				name: 'English',
				code: 'en-US',
				file: 'en-US.json',
			},
			{
				name: 'Français',
				code: 'fr-FR',
				file: 'fr-FR.json',
			},
			{
				name: 'Deutsch',
				code: 'de-DE',
				file: 'de-DE.json',
			},
			{
				name: 'Polski',
				code: 'pl-PL',
				file: 'pl-PL.json',
			},
		],
		bundle: {
			compositionOnly: true,
			runtimeOnly: true,
		},
		vueI18n: './config/vueI18n.config.ts',
		strategy: 'no_prefix',
	},
	pinia: {
		// https://pinia.vuejs.org/ssr/nuxt.html#Auto-imports
		storesDirs: ['./src/store/**'],
	},
	primevue: {
		importTheme: { from: '@/assets/scss/primevue/reaparr-theme.ts' },
	},
	quasar: {
		// Plugins: https://quasar.dev/quasar-plugins
		sassVariables: '@/assets/scss/_variables.scss',
		iconSet: 'mdi-v7',
		config: {
			dark: true,
			screen: {
				bodyClasses: true,
			},
		},
		plugins: ['Notify'],
		extras: {
			// string | null: Auto-import roboto font. https://quasar.dev/style/typography#default-font
			font: 'roboto-font', // string[]: Auto-import webfont icons. Usage: https://quasar.dev/vue-components/icon#webfont-usage
			fontIcons: ['mdi-v7'], // string[]: Auto-import svg icon collections. Usage: https://quasar.dev/vue-components/icon#svg-usage
			svgIcons: [], // string[]: Auto-import animations from 'animate.css'. Usage: https://quasar.dev/options/animations#usage
			// string[]: Auto-import animations from 'animate.css'. Usage: https://quasar.dev/options/animations#usage
			animations: ['fadeInLeft', 'fadeInRight', 'fadeInUp', 'fadeInDown', 'fadeOutLeft'],
		},
	},
},
);
