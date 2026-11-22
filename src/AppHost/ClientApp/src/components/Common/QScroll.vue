<template>
	<q-scroll-area
		ref="scrollAreaRef"
		:style="{ 'height': props.fit ? 'none' : props.height, 'max-width': props.fit ? 'none' : props.width }"
		:class="[props.fit ? 'fit' : '']"
		:thumb-style="thumbStyle"
		:bar-style="barStyle">
		<slot />
	</q-scroll-area>
</template>

<script setup lang="ts">
import type { VueStyleObjectProp } from 'quasar';
import { get } from '@vueuse/core';

type QScrollAreaInstance = {
	getScrollTarget: () => Element;
};

const props = withDefaults(defineProps<{
	fit?: boolean;
	height?: string | number;
	scrollId?: string;
	width?: string | number;
}>(), {
	fit: true,
	height: '400px',
	scrollId: undefined,
	width: '300px',
});
const thumbStyle: VueStyleObjectProp = {
	right: '4px',
	borderRadius: '5px',
	backgroundColor: '#d20000',
	width: '5px',
	opacity: '0.75',
};
const barStyle: VueStyleObjectProp = {
	right: '2px',
	borderRadius: '9px',
	backgroundColor: 'rgba(0,0,0,0)',
	width: '9px',
	opacity: '0.2',
};

const scrollAreaRef = ref<QScrollAreaInstance | null>(null);

function getScrollTarget(): HTMLElement | null {
	const target = get(scrollAreaRef)?.getScrollTarget();
	return target instanceof HTMLElement ? target : null;
}

onMounted(() => {
	const target = getScrollTarget();
	if (target && props.scrollId) {
		target.id = props.scrollId;
	}
});

defineExpose({ getScrollTarget });
</script>
