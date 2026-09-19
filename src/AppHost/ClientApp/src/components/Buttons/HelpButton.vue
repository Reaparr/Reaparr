<script lang="ts">
import { defineComponent, h, mergeProps } from 'vue';
import IconButton from './IconButton.vue';
import { baseBtnPropsDefault } from '~/composables/baseBtnProps';

export default defineComponent({
	name: 'HelpButton',
	inheritAttrs: false,
	props: baseBtnPropsDefault(),
	render() {
		return h(
			IconButton,
			{
				...mergeProps(this.$attrs, this.$props, {
					icon: 'mdi-help-circle-outline',
					size: 'md',
					'aria-label': String(this.$attrs['aria-label'] ?? 'Help'),
				}),
			},
			this.$slots.default,
		);
	},
});
</script>
