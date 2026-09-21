<script lang="ts">
import { defineComponent, h, mergeProps } from 'vue';
import { useI18n } from 'vue-i18n';
import IconButton from './IconButton.vue';
import { baseBtnPropsDefault } from '~/composables/baseBtnProps';

export default defineComponent({
	name: 'HelpButton',
	inheritAttrs: false,
	props: baseBtnPropsDefault(),
	setup() {
		const { t } = useI18n();
		return { t };
	},
	render() {
		return h(
			IconButton,
			{
				...mergeProps(this.$attrs, this.$props, {
					icon: 'mdi-help-circle-outline',
					size: 'md',
					'aria-label': String(this.$attrs['aria-label'] ?? this.t('components.help-button.label')),
				}),
			},
			this.$slots.default,
		);
	},
});
</script>
