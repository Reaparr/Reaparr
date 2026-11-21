import vueI18n from '@intlify/eslint-plugin-vue-i18n';
import withNuxt from './.nuxt/eslint.config.mjs';
import vueSetupRules from 'eslint-vue-setup-rules';

export default withNuxt([{
	ignores: ['.idea/'],
}]).append(...vueI18n.configs['flat/recommended'])
	.append({
		rules: {
			'vue/html-closing-bracket-newline': [
				'error',
				{
					singleline: 'never',
					multiline: 'never',
					selfClosingTag: {
						singleline: 'never',
						multiline: 'never',
					},
				},
			], // Reason: Opinionated not to have useless empty lines with just a closing tag
			'vue/component-name-in-template-casing': [
				'error',
				'PascalCase',
				{
					registeredComponentsOnly: false,
					ignores: quasarComponents(),
				},
			], // Reason: This ensures that the order of components is always the same
			'vue/block-order': ['error', { order: ['template', 'script', 'style'] }],
			'vue/html-closing-bracket-spacing': [
				'error',
				{
					startTag: 'never',
					endTag: 'never',
					selfClosingTag: 'always',
				},
			],
		},
	})
	.append({
		files: ['src/**/*.{ts,tsx,vue}'],
		languageOptions: {
			parserOptions: {
				projectService: true,
				tsconfigRootDir: import.meta.dirname,
			},
		},
		rules: {
			'@typescript-eslint/no-floating-promises': 'warn',
			'@typescript-eslint/no-misused-promises': ['error', {
				checksVoidReturn: { arguments: false, attributes: false },
			}],
			'@typescript-eslint/no-unnecessary-type-assertion': 'warn',
			'vue/no-setup-props-reactivity-loss': 'error',
			'vue/no-ref-object-reactivity-loss': 'error',
		},
	})
	.append({
		rules: {
			'@intlify/vue-i18n/no-dynamic-keys': 'error',
			'@intlify/vue-i18n/no-unused-keys': [
				'error',
				{
					extensions: ['.js', '.vue', '.ts'],
				},
			],
			'@intlify/vue-i18n/key-format-style': [
				'error',
				'kebab-case',
				{
					allowArray: false,
					splitByDots: true,
				},
			],
		},
		settings: {
			'vue-i18n': {
				localeDir: './src/lang/*.{json,json5,yaml,yml}', // Specify the version of `vue-i18n` you are using.
				// If not specified, the message will be parsed twice.
				messageSyntaxVersion: '^9.0.0',
			},
		},
	})
	.append({
		files: ['**/*.vue'],
		plugins: {
			'vue3-script-setup': vueSetupRules,
		},
		rules: {
			'vue3-script-setup/declaration-order': 'error',
		},
	});

function quasarComponents() {
	return [
		'q-alert',
		'q-ajax-bar',
		'q-avatar',
		'q-badge',
		'q-banner',
		'q-bar',
		'q-breadcrumbs',
		'q-btn',
		'q-btn-dropdown',
		'q-btn-toggle',
		'q-button',
		'q-button-dropdown',
		'q-button-group',
		'q-card',
		'q-card-section',
		'q-carousel',
		'q-chat-message',
		'q-checkbox',
		'q-chip',
		'q-circular-progress',
		'q-color-picker',
		'q-date',
		'q-dialog',
		'q-drawer',
		'q-editor',
		'q-expansion-item',
		'q-field',
		'q-file',
		'q-floating-action-button',
		'q-form',
		'q-header',
		'q-icon',
		'q-img',
		'q-infinite-scroll',
		'q-inner-loading',
		'q-input',
		'q-intersection',
		'q-item',
		'q-item-label',
		'q-item-section',
		'q-knob',
		'q-layout',
		'q-linear-progress',
		'q-list',
		'q-markup-table',
		'q-menu',
		'q-no-ssr',
		'q-option-group',
		'q-page',
		'q-page-container',
		'q-pagination',
		'q-parallax',
		'q-popup-edit',
		'q-popup-proxy',
		'q-pull-to-refresh',
		'q-radio',
		'q-range',
		'q-rating',
		'q-resize-observer',
		'q-responsive',
		'q-scroll',
		'q-scroll-area',
		'q-scroll-observer',
		'q-select',
		'q-separator',
		'q-skeleton',
		'q-slide-item',
		'q-slide-transition',
		'q-slider',
		'q-space',
		'q-spinner-gears',
		'q-spinners',
		'q-splitter',
		'q-stepper',
		'q-tab',
		'q-tab-panel',
		'q-tab-panels',
		'q-table',
		'q-tabs',
		'q-td',
		'q-time',
		'q-timeline',
		'q-toggle',
		'q-toolbar',
		'q-toolbar-title',
		'q-tooltip',
		'q-tr',
		'q-tree',
		'q-uploader',
		'q-video',
		'q-virtual-scroll',
	];
}
