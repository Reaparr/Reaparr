<template>
	<QSection
		:header="t('components.download-schedule.title')"
		:help="helpText"
		role="region"
		:aria-label="t('components.download-schedule.title')"
		data-cy="download-schedule-section">
		<div class="row items-center justify-between q-mb-md">
			<QToggle
				:model-value="policy.enabled"
				:label="t('components.download-schedule.enabled')"
				:disable="isSaving"
				:aria-busy="saveState === 'toggle'"
				data-cy="schedule-enable"
				@update:model-value="saveRequests.next({ policy: { ...cloneDeep(policy), enabled: $event }, action: 'toggle' })">
				<QSpinner
					v-if="saveState === 'toggle'"
					class="q-ml-sm"
					aria-hidden="true" />
			</QToggle>
			<QBtn
				flat
				no-caps
				color="primary"
				icon="mdi-restore"
				:label="t('components.download-schedule.reset')"
				:disable="isSaving"
				data-cy="schedule-reset"
				@click="dialogStore.openDialog(DialogType.ResetDownloadScheduleConfirmationDialog)" />
		</div>
		<DownloadScheduleGrid
			:limits="limits"
			:selection="selection"
			:preview-limit="previewLimit"
			:days="dayLabels"
			:label="t('components.download-schedule.grid-label')"
			:unlimited-label="t('components.download-schedule.unlimited')"
			:locale="locale"
			:time-zone="settingsStore.dateTimeSettings.timeZone"
			@select="range = $event" />
		<DownloadScheduleRangeEditor
			:key="editorKey"
			class="q-mt-md"
			:range="range"
			:days="dayLabels"
			:selection="selection"
			:selected-limits="selection.map((slot) => limits[slot]!)"
			:default-limit="highestLimit"
			:saving="saveState === 'apply'"
			:disabled="isSaving"
			@change="range = $event"
			@preview="previewLimit = $event"
			@apply="applySelection" />
		<div
			class="q-mt-md"
			aria-live="polite">
			<QBanner
				v-if="saveState === 'error'"
				class="bg-negative text-white"
				role="alert"
				data-cy="schedule-save-error">
				{{ t('components.download-schedule.save-error') }} {{ saveError }}
			</QBanner>
		</div>
		<ConfirmationDialog
			:name="DialogType.ResetDownloadScheduleConfirmationDialog"
			:title="t('components.download-schedule.reset-title')"
			:text="t('components.download-schedule.reset-text')"
			:confirm-label="t('components.download-schedule.reset')"
			@confirm="resetSchedule" />
	</QSection>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { Subject } from 'rxjs';
import { concatMap, tap } from 'rxjs/operators';
import { cloneDeep, max } from 'lodash-es';
import { useSettingsStore, useDialogStore } from '@store';
import { DialogType } from '@enums';
import type { DownloadScheduleDTO } from '@dto';
import { decodeDownloadScheduleDays, encodeDownloadScheduleDays, getDownloadScheduleSlots, isDownloadScheduleLimit, type DownloadScheduleRange } from './downloadScheduleSelection';

const settingsStore = useSettingsStore();
const dialogStore = useDialogStore();
const { t, locale } = useI18n();
const policy = computed(() => settingsStore.confirmedDownloadSchedule);
const isSaving = computed(() => settingsStore.settingsSaveState === 'saving');
const saveState = ref<'idle' | 'toggle' | 'apply' | 'reset' | 'error'>('idle');
const saveError = ref<string | null>(null);
const range = ref<DownloadScheduleRange>({ days: [], from: 18, until: 36 });
const previewLimit = ref<number | null>();
const editorKey = ref(0);
const selection = computed(() => getDownloadScheduleSlots(get(range)));
const limits = computed(() => decodeDownloadScheduleDays(get(policy).days));
const highestLimit = computed(() => max(get(limits)) ?? null);
const dayLabels = computed(() => [
	t('components.download-schedule.monday'), t('components.download-schedule.tuesday'), t('components.download-schedule.wednesday'),
	t('components.download-schedule.thursday'), t('components.download-schedule.friday'), t('components.download-schedule.saturday'), t('components.download-schedule.sunday'),
]);
const helpText = computed(() => [
	t('components.download-schedule.description'),
	t('components.download-schedule.grid-help'),
	t('components.download-schedule.selection-help'),
	t('components.download-schedule.fairness-help'),
	t('components.download-schedule.timezone-help', { timezone: settingsStore.dateTimeSettings.timeZone }),
].join('\n\n'));
const saveRequests = new Subject<{ policy: DownloadScheduleDTO; action: 'toggle' | 'apply' | 'reset' }>();

useSubscription(saveRequests.pipe(
	concatMap(({ policy: candidate, action }) => {
		set(saveState, action);
		set(saveError, null);
		return settingsStore.saveDownloadSchedule(candidate).pipe(tap((settings) => {
			set(saveState, settings ? 'idle' : 'error');
			set(saveError, settings ? null : settingsStore.settingsSaveError);
			if (settings && action === 'reset') {
				set(range, { ...get(range), days: [] });
				set(previewLimit, undefined);
				set(editorKey, get(editorKey) + 1);
			}
		}));
	}),
).subscribe());

function applySelection(limit: number | null) {
	if (!get(selection).length || !isDownloadScheduleLimit(limit)) return;
	const updatedLimits = [...get(limits)];
	for (const slot of get(selection)) updatedLimits[slot] = limit;
	saveRequests.next({ policy: { ...cloneDeep(get(policy)), days: encodeDownloadScheduleDays(updatedLimits) }, action: 'apply' });
}

function resetSchedule() {
	if (get(isSaving)) return;
	saveRequests.next({ policy: { ...cloneDeep(get(policy)), days: {} }, action: 'reset' });
	dialogStore.closeDialog(DialogType.ResetDownloadScheduleConfirmationDialog);
}
</script>
