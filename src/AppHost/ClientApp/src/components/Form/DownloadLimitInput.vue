<template>
	<QRow
		class="no-wrap"
		justify="between">
		<QCol
			cols="auto"
			class="q-ml-lg">
			<InputNumber
				v-model="value"
				debounce="1000"
				:min="0"
				suffix=" kB/s"
				data-cy="download-speed-limit-input" />
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@store';

const settingsStore = useSettingsStore();
const telemetry = useTelemetry();

const props = defineProps<{
	machineIdentifier: string;
}>();

const value = computed({
	get: () => {
		return settingsStore.getServerSettings(props.machineIdentifier)?.downloadSpeedLimit ?? 0;
	},
	set: (value) => {
		if (value == null || value < 0) {
			value = 0;
		}
		settingsStore.updateDownloadLimit(props.machineIdentifier, value);
		telemetry.serverDownloadLimitChanged(props.machineIdentifier);
	},
});
</script>
