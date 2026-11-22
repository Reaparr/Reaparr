<template>
	<div
		:class="classes"
		:style="styles">
		<slot />
	</div>
</template>

<script setup lang="ts">
import type { ColLevels } from '@props';

interface IBreakPoints {
	xs?: ColLevels;
	sm?: ColLevels;
	md?: ColLevels;
	lg?: ColLevels;
	xl?: ColLevels;
}
interface IOffset {
	offsetXs?: ColLevels;
	offsetSm?: ColLevels;
	offsetMd?: ColLevels;
	offsetLg?: ColLevels;
	offsetXl?: ColLevels;
}
interface QColProps extends IBreakPoints, IOffset {
	cols?: ColLevels | 'auto' | 'grow' | 'shrink';
	offset?: ColLevels;
	width?: number;
	textAlign?: 'left' | 'center' | 'right' | 'justify';
	alignSelf?: 'auto' | 'start' | 'end' | 'center' | 'baseline' | 'stretch' | 'none';
}

// Provide default values for props using `withDefaults`
const props = withDefaults(defineProps<QColProps>(), {
	offset: 0,
	alignSelf: 'none',
	textAlign: 'left',
	width: 0,
	cols: 0 as ColLevels,
	xs: 0,
	sm: 0,
	md: 0,
	lg: 0,
	xl: 0,
	offsetXs: 0,
	offsetSm: 0,
	offsetMd: 0,
	offsetLg: 0,
	offsetXl: 0,
});

const classes = computed(() => {
	const classList: string[] = [];

	if (props.cols) {
		classList.push(`col-${props.cols}`);
	} else {
		classList.push('col');
	}

	// Align-self classes
	if (props.alignSelf && props.alignSelf !== 'none') {
		classList.push(`self-${props.alignSelf}`);
	}

	// Breakpoints and offsets
	const breakPoints = ['xs', 'sm', 'md', 'lg', 'xl'] as const;
	breakPoints.forEach((breakPoint) => {
		const value = props[breakPoint];
		if (value) {
			classList.push(`col-${breakPoint}-${value}`);
		}

		const offsetValue = props[`offset${breakPoint.charAt(0).toUpperCase() + breakPoint.slice(1)}` as keyof IOffset];
		if (offsetValue) {
			classList.push(`offset-${breakPoint}-${offsetValue}`);
		}
	});

	// General offset class
	if (props.offset) {
		classList.push(`offset-${props.offset}`);
	}

	// Text alignment classes
	if (props.textAlign) {
		classList.push(`text-${props.textAlign}`);
	}

	return classList;
});
const styles = computed(() => {
	const styleList: Record<string, string> = {};

	if (props.width) {
		styleList.minWidth = `${props.width}px`;
		styleList.maxWidth = `${props.width}px`;
	}

	return styleList;
});
</script>
