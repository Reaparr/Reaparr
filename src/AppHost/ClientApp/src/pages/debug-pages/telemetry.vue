<template>
	<QPage
		class="telemetry-diagnostic-page q-pa-md"
		data-cy="telemetry-diagnostic-page">
		<QCard
			flat
			bordered>
			<QCardSection>
				<div class="text-h5">
					{{ t('pages.debug.telemetry.title') }}
				</div>
				<div class="text-body1 q-mt-sm">
					{{ t('pages.debug.telemetry.description') }}
				</div>
			</QCardSection>

			<QCardActions align="left">
				<QBtn
					data-cy="telemetry-send-test-event"
					color="primary"
					:label="t('pages.debug.telemetry.send-test-event')"
					:loading="logsStore.telemetryDiagnostic.pending"
					:disable="logsStore.telemetryDiagnostic.pending"
					@click="requestDiagnostics" />
			</QCardActions>

			<QCardSection>
				<QList separator>
					<QItem data-cy="telemetry-backend-status">
						<QItemSection>
							<QItemLabel>{{ t('pages.debug.telemetry.backend') }}</QItemLabel>
							<QItemLabel caption>
								{{ statusText(logsStore.telemetryDiagnostic.backend) }}
							</QItemLabel>
						</QItemSection>
					</QItem>
					<QItem data-cy="telemetry-frontend-status">
						<QItemSection>
							<QItemLabel>{{ t('pages.debug.telemetry.frontend') }}</QItemLabel>
							<QItemLabel caption>
								{{ statusText(logsStore.telemetryDiagnostic.frontend) }}
							</QItemLabel>
						</QItemSection>
					</QItem>
				</QList>
			</QCardSection>
		</QCard>
	</QPage>
</template>

<script setup lang="ts">
import { useLogsStore } from '@store';
import { useSubscription } from '@vueuse/rxjs';
import { exhaustMap, Subject } from 'rxjs';

const { t } = useI18n();
const logsStore = useLogsStore();
const telemetry = useTelemetry();
const sendTestEvent = new Subject<void>();

useSubscription(sendTestEvent.pipe(
	exhaustMap(() => logsStore.sendTelemetryDiagnostics()),
).subscribe());

function requestDiagnostics(): void {
	telemetry.diagnosticsRequested();
	sendTestEvent.next();
}

function statusText(status: boolean | null): string {
	if (status === true) return t('pages.debug.telemetry.accepted');
	if (status === false) return t('pages.debug.telemetry.failed');
	return t('pages.debug.telemetry.not-run');
}
</script>
