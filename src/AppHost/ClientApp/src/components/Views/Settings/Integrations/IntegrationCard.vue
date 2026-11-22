<template>
	<OverviewCard
		mode="edit"
		class="integration-card"
		cy="integration-card"
		:aria-label="integration.name"
		:icon="integrationIcon"
		:title="integration.name"
		:subtitle="integration.baseUrl"
		:chips="integrationChips"
		@click="open" />
</template>

<script setup lang="ts">
import type { NamedColor } from 'quasar';
import { IntegrationProvisioningState, IntegrationType, TestConnectionStatus, type IntegrationSummary } from '@dto';

const props = defineProps<{ integration: IntegrationSummary }>();

const { t } = useI18n();

const integrationChips = computed(() => {
	const provisioningColor: NamedColor = props.integration.provisioningState === IntegrationProvisioningState.Configured
		? 'positive'
		: props.integration.provisioningState === IntegrationProvisioningState.ChangesPending
			? 'warning'
			: 'negative';
	const isConnected = props.integration.lastConnectionTestStatus === TestConnectionStatus.Success;

	return [
		{
			color: isConnected ? 'positive' as const : 'negative' as const,
			value: isConnected
				? t('help.settings.integrations.connection-status.connected')
				: t('help.settings.integrations.connection-status.unconnected'),
		},
		{
			color: provisioningColor,
			value: props.integration.provisioningState,
		},
	];
});
const integrationIcon = computed(() => props.integration.type === IntegrationType.Radarr ? 'radarr' : 'sonarr');

function open(): void {
	emit('edit', props.integration);
}

const emit = defineEmits<{
	(e: 'edit', integration: IntegrationSummary): void;
}>();
</script>
