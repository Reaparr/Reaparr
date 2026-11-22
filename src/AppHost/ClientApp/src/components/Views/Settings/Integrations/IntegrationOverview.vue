<template>
	<QSection header="Integrations">
		<QRow justify="center">
			<QCol
				v-for="integration in items"
				:key="integration.id"
				class="q-pa-sm"
				cols="12"
				sm="6"
				md="3">
				<IntegrationCard
					:integration="integration"
					@edit="openEdit" />
			</QCol>
			<QCol
				class="q-pa-sm"
				cols="12"
				sm="6"
				md="3">
				<OverviewCard
					mode="add"
					cy="add-integration"
					:aria-label="$t('help.settings.integrations.choose-title')"
					@click="openAdd" />
			</QCol>
		</QRow>
		<IntegrationDialog />
	</QSection>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import type { IntegrationSummary } from '@dto';
import { useDialogStore, useIntegrationStore } from '@store';
import { storeToRefs } from 'pinia';

const { items } = storeToRefs(integrationStore);

const dialogStore = useDialogStore();
const integrationStore = useIntegrationStore();

function refresh(): void {
	useSubscription(integrationStore.refresh().subscribe());
}
function openAdd(): void {
	dialogStore.openIntegrationDialog(null);
}
function openEdit(integration: IntegrationSummary): void {
	dialogStore.openIntegrationDialog(integration);
}

onMounted(refresh);
</script>
