<script lang="ts">
import { defineComponent, h } from 'vue';
import { QBtn, QTooltip } from 'quasar';
import type { IBaseButtonProps } from '@props';
import { useRouter } from '#imports';
import { baseBtnPropsDefault } from '~/composables/baseBtnProps';

export default defineComponent({
	name: 'BaseButton',
	inheritAttrs: true,
	props: baseBtnPropsDefault(),
	emits: ['click'],
	render() {
		const props = this.$props as IBaseButtonProps;
		const emit = this.$emit;
		const slots = this.$slots;
		const style = {
			flat: props.flat,
			round: props.round,
			rounded: props.rounded,
			outline: props.outline,
		};

		const classes = Object.assign(
			{
				'base-btn': true,
				'base-btn-outline': style.outline,
				'base-btn-block': props.block,
				'base-btn-vertical': props.vertical,
				[`base-btn-${props.color}`]: true,
				'i18n-formatting': true,
			},
			this.$attrs.class,
		);

		return h(
			QBtn,
			{
				class: classes,
				label: props.label,
				icon: props.iconAlign === 'left' ? props.icon : undefined,
				iconRight: props.iconAlign === 'right' ? props.icon : undefined,
				glossy: props.glossy,
				size: props.size,
				loading: props.loading,
				disable: props.disabled,
				'data-cy': props.cy,
				'aria-label': props.ariaLabel || undefined,
				flat: style.flat,
				round: style.round,
				rounded: style.rounded,
				href: props.href ? props.href : undefined,
				target: props.href ? '_blank' : '_self',
				push: props.push,
				onClick(event: MouseEvent) {
					if (props.to) {
						void useRouter().push({
							path: props.to,
						});
					} else {
						emit('click', event);
					}
				},
			},
			{
				default: () => [
					...(slots?.default?.() ?? []),
					...[
						props.tooltipText
							? h(
									QTooltip,
									{
										anchor: 'top middle',
										self: 'bottom middle',
										offset: [10, 10],
									},
									{
										default: () => {
											if (!props.tooltipText) {
												return '';
											}
											return props.tooltipText;
										},
									},
								)
							: null,
					],
				],
			},
		);
	},
});
</script>
