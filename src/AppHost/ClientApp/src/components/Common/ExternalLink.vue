<template>
	<a
		:href="href"
		target="_blank"
		rel="noopener noreferrer"
		:data-cy="cy"
		@click="onClick">
		<slot />
	</a>
</template>

<script setup lang="ts">
import Log from 'consola';
import { sendDesktopMessage } from '@composables';
import { useGlobalStore } from '@store';
import { DesktopMessageType } from '@dto';

const props = defineProps<{
	href?: string;
	cy?: string;
}>();

const telemetry = useTelemetry();
const globalStore = useGlobalStore();

function onClick(): void {
	telemetry.externalLinkOpened(props.href);
	if (globalStore.isDesktopMode) {
		Log.debug('Desktop ExternalLink Click');
		sendDesktopMessage({
			type: DesktopMessageType.ExternalLink,
			value: props.href ?? 'unknown-link',
		});
	}
}
</script>
