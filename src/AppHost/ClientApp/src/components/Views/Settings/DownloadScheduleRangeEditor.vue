<template>
	<div
		class="background-md rounded-borders q-pa-md"
		data-cy="schedule-range-editor">
		<div class="row items-center justify-between q-gutter-sm">
			<strong>{{ t('components.download-schedule.edit-selection') }}</strong>
			<QBtn
				flat
				dense
				no-caps
				color="primary"
				:label="t('components.download-schedule.weekday-work-hours')"
				data-cy="schedule-weekday-work-hours"
				@click="emit('change', { days: [0, 1, 2, 3, 4], from: 18, until: 36 })" />
		</div>
		<div class="row q-col-gutter-md q-mt-sm">
			<div class="col-12 col-sm-6">
				<QSelect
					:model-value="range.days"
					:options="days.map((label, value) => ({ label, value }))"
					:label="t('components.download-schedule.days')"
					multiple
					emit-value
					map-options
					use-chips
					data-cy="schedule-days"
					@update:model-value="emit('change', { ...range, days: $event })">
					<template #selected-item="scope">
						<QGlowChip
							:value="scope.opt.label"
							removable
							class="no-shadow"
							:tabindex="scope.tabindex"
							@remove="scope.removeAtIndex(scope.index)" />
					</template>
				</QSelect>
			</div>
			<div class="col-6 col-sm-3">
				<QSelect
					:model-value="range.from"
					:options="times.slice(0, 48)"
					:label="t('components.download-schedule.from')"
					emit-value
					map-options
					data-cy="schedule-from"
					@update:model-value="emit('change', { ...range, from: $event })" />
			</div>
			<div class="col-6 col-sm-3">
				<QSelect
					:model-value="range.until"
					:options="times"
					:label="t('components.download-schedule.until-exclusive')"
					emit-value
					map-options
					data-cy="schedule-until"
					@update:model-value="emit('change', { ...range, until: $event })" />
			</div>
		</div>
		<div class="row items-center q-gutter-md q-mt-sm">
			<QRadio
				v-model="mode"
				val="limited"
				:label="t('components.download-schedule.limit')"
				data-cy="schedule-limit-mode"
				@update:model-value="onEdit" />
			<InputNumber
				v-if="mode === 'limited'"
				v-model="limit"
				:min="1"
				:max="2147483647"
				:max-fraction-digits="0"
				suffix=" kB/s"
				:aria-label="`${t('components.download-schedule.limit')} (kB/s)`"
				:placeholder="t('components.download-schedule.positive-integer')"
				data-cy="schedule-limit-input"
				@input="onLimitInput($event.value)"
				@update:model-value="onEdit" />
			<QRadio
				v-model="mode"
				val="unlimited"
				:label="t('components.download-schedule.unlimited')"
				data-cy="schedule-unlimited-mode"
				@update:model-value="onEdit" />
			<QBadge
				v-if="mode === 'mixed'"
				color="grey-7">
				{{ t('components.download-schedule.mixed-values') }}
			</QBadge>
		</div>
		<p
			v-if="range.days.length && range.from === range.until"
			class="text-negative q-mt-sm"
			role="alert"
			data-cy="schedule-invalid-range">
			{{ t('components.download-schedule.equal-times-invalid') }}
		</p>
		<p
			v-else-if="mode === 'limited' && limit !== null && !isDownloadScheduleLimit(limit)"
			class="text-negative q-mt-sm"
			role="alert">
			{{ t('components.download-schedule.positive-integer-error') }}
		</p>
		<div class="row items-center justify-end q-gutter-sm q-mt-md">
			<BaseButton
				color="positive"
				outline
				no-caps
				:label="t('components.download-schedule.apply-save')"
				:loading="saving"
				:disabled="disabled || !selection.length || mode === 'mixed' || (mode === 'limited' && (limit === null || !isDownloadScheduleLimit(limit)))"
				cy="schedule-apply"
				@click="applyLimit" />
		</div>
	</div>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { isEqual, max } from 'lodash-es';
import { formatDownloadScheduleTime, isDownloadScheduleLimit, type DownloadScheduleRange } from './downloadScheduleSelection';

const props = defineProps<{
	range: DownloadScheduleRange;
	days: string[];
	selection: number[];
	selectedLimits: (number | null)[];
	defaultLimit?: number | null;
	saving: boolean;
	disabled?: boolean;
}>();
const emit = defineEmits<{ change: [range: DownloadScheduleRange]; apply: [limit: number | null]; preview: [limit: number | null | undefined] }>();
const { t } = useI18n();
const mode = ref<'limited' | 'unlimited' | 'mixed'>('limited');
const limit = ref<number | null>(null);
let hasLocalEdits = false;
let hasAppliedLimit = false;
let submittedLimit: number | null | undefined;
const previewLimit = computed(() => get(mode) === 'unlimited'
	? null
	: get(mode) === 'limited' && get(limit) !== null && isDownloadScheduleLimit(get(limit)) ? get(limit) : undefined);
watch(previewLimit, (value) => emit('preview', value), { immediate: true });
const times = Array.from({ length: 49 }, (_, value) => ({
	label: formatDownloadScheduleTime(value),
	value,
}));

function onEdit() {
	hasLocalEdits = true;
}

function onLimitInput(value: number | string | null | undefined) {
	set(limit, typeof value === 'number' ? value : null);
	onEdit();
}

function applyLimit() {
	submittedLimit = get(mode) === 'unlimited' ? null : get(limit)!;
	hasAppliedLimit = true;
	hasLocalEdits = false;
	emit('apply', submittedLimit);
}

watch([() => props.selection.join(','), () => props.selectedLimits, () => props.defaultLimit], ([selectionKey], [previousSelectionKey, previousLimits]) => {
	const values = [...new Set(props.selectedLimits)];
	const nextLimit = values.length === 1 ? values[0] ?? null : null;
	const acknowledged = submittedLimit !== undefined && values.length === 1 && nextLimit === submittedLimit;
	const selectionChanged = selectionKey !== previousSelectionKey;
	if (selectionChanged && hasAppliedLimit) hasLocalEdits = true;
	const shouldSynchronize = !hasLocalEdits && (selectionChanged || submittedLimit === undefined || acknowledged);
	if (selectionChanged || acknowledged) submittedLimit = undefined;
	if (shouldSynchronize) {
		if (!selectionChanged && !isEqual(props.selectedLimits, previousLimits)) {
			set(mode, values.length > 1 ? 'mixed' : values.length === 1 && nextLimit === null ? 'unlimited' : 'limited');
		}
		set(limit, max(props.selectedLimits) ?? props.defaultLimit ?? null);
	}
}, { immediate: true, deep: true });
</script>
