<template>
	<QCardDialog
		:name="`${DialogType.AlertInfoDialog}-${alert.id}`"
		:type="{} as IAlert"
		width="800px"
		:content-height="alert.apiErrors ? '80' : '0'"
		cy="alert-dialog"
		@closed="alertStore.removeAlert(alert.id)">
		<template #title>
			{{ alert.apiErrors ? $t('components.alert-dialog.title') : alert.title }}
		</template>
		<template #default>
			<div v-if="alert.apiErrors">
				<QText>{{ $t('components.alert-dialog.description') }}</QText>
				<QText class="q-mt-sm">
					{{ $t('components.alert-dialog.error-explanation') }}
				</QText>
				<div
					v-for="error in alert.apiErrors"
					:key="`${error.method}-${error.url}-${error.statusCode}-${error.code}`"
					class="q-my-md"
					data-cy="api-error">
					<div class="text-weight-bold">
						{{ `${error.method} — HTTP ${error.statusCode}` }}
					</div>
					<div style="overflow-wrap: anywhere">
						{{ $t('components.alert-dialog.endpoint') }}: {{ error.url }}
					</div>
					<pre style="white-space: break-spaces">{{ error.message }}</pre>
				</div>
				<QText
					v-if="alert.hasOmittedApiErrors"
					data-cy="api-errors-omitted">
					{{ $t('components.alert-dialog.omitted-errors') }}
				</QText>
				<QText class="q-mt-md">
					{{ $t('components.alert-dialog.next-steps') }}
				</QText>
			</div>
			<div v-else>
				<pre style="white-space: break-spaces">{{ alert.text }}</pre>
				<QText>{{ $t('components.alert-dialog.request-data-sent') }}</QText>
				<pre
					v-if="alert.result"
					style="white-space: break-spaces">
				{{ alert.result }}
			</pre>
				<pre
					v-if="errors"
					style="white-space: break-spaces">
				{{ errors }}
			</pre>
			</div>
		</template>
		<template #actions="{ close }">
			<BaseButton
				:label="$t('general.commands.close')"
				cy="close-alert-dialog"
				@click="close" />
			<BaseButton
				v-if="issueUrl"
				:href="issueUrl"
				:label="$t('components.alert-dialog.report-issue')"
				icon="mdi-open-in-new"
				target="_blank"
				rel="noopener noreferrer"
				cy="report-api-error"
				style="margin-left: auto" />
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import type { IAlert } from '@interfaces';
import { DialogType } from '@enums';
import { useAlertStore, useDialogStore, useGlobalStore } from '@store';

const alertStore = useAlertStore();

const props = defineProps<{ alert: IAlert }>();
const dialogStore = useDialogStore();
const { t } = useI18n();
const globalStore = useGlobalStore();
const route = useRoute();
const $q = useQuasar();
const browserOptions: [boolean | undefined, string][] = [
	[$q.platform.is.edge, 'Microsoft Edge (LOL)'],
	[$q.platform.is.opera, 'Opera'],
	[$q.platform.is.vivaldi, 'Vivaldi'],
	[$q.platform.is.firefox, 'Firefox'],
	[$q.platform.is.safari, 'Safari (Yeah good luck with that...)'],
	[$q.platform.is.chrome, 'Google Chrome'],
];
const browser = browserOptions.find(([detected]) => detected)?.[1];

onMounted(() => dialogStore.openAlertInfoDialog(props.alert));

watch(() => props.alert, () => {
	dialogStore.openAlertInfoDialog(props.alert);
}, { flush: 'post' });

const issueUrl = computed(() => {
	const apiErrors = props.alert.apiErrors;
	if (!apiErrors?.length) {
		return undefined;
	}

	// Keep every retained failure in the prefill; full details remain in the dialog and logs.
	const logs = [
		'Captured frontend request failures (not backend logs):',
		...apiErrors.map((error) => [
			`${error.method} ${error.url.slice(0, 100)}`,
			`HTTP ${error.statusCode}`,
			error.message.slice(0, 40),
		].join('\n')),
		...(props.alert.hasOmittedApiErrors ? [t('components.alert-dialog.omitted-errors')] : []),
	].join('\n\n');
	const params = new URLSearchParams({
		template: 'BUG-REPORT.yml',
		title: `[BUG] - Reaparr: ${t('components.alert-dialog.title')}`,
		description: [
			t('components.alert-dialog.description'),
			`Deployment: ${globalStore.platform.slice(0, 40)}`,
			`Detected browser: ${$q.platform.is.name ?? 'unknown'} ${$q.platform.is.version ?? ''}`,
		].join('\n\n'),
		'reproduce-steps': `Page at report time: ${route.path.slice(0, 200)}\n\n${t('components.alert-dialog.report-description')}`,
		logs,
	});
	const version = globalStore.version.trim();
	if (version && !['?', 'unknown', 'latest'].includes(version.toLowerCase())) {
		params.set('version', version.slice(0, 100));
	}
	const os = ['Unraid', 'Linux', 'Windows', 'Mac', 'Synology'].find(
		(option) => option.toLowerCase() === globalStore.platform.toLowerCase(),
	);
	if (os) {
		params.set('os', os);
	}
	if (browser) {
		params.set('browsers', browser);
	} else if ($q.platform.is.name) {
		params.set('browsers', 'Other (Specify in description)');
	}
	return `https://github.com/Reaparr/Reaparr/issues/new?${params}`;
});

const errors = computed(() => {
	if (props.alert?.result?.errors) {
		return props.alert.result.errors;
	}
	return null;
});
</script>
