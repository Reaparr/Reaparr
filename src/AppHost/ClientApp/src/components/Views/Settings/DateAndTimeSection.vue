<template>
	<QSection :header="$t('pages.settings.ui.date-and-time.header')">
		<HelpGroup class="q-mt-md">
			<!--	Time Zone Setting	-->
			<HelpRow
				:label="$t('help.settings.ui.date-and-time.time-zone.label')"
				:title="$t('help.settings.ui.date-and-time.time-zone.title')"
				:text="$t('help.settings.ui.date-and-time.time-zone.text')">
				<q-select
					v-model:model-value="settingsStore.dateTimeSettings.timeZone"
					:aria-label="$t('help.settings.ui.date-and-time.time-zone.label')"
					:options="filteredTimeZoneOptions"
					emit-value
					map-options
					use-input
					fill-input
					hide-selected
					:input-debounce="0"
					data-cy="time-zone"
					@filter="filterTimeZones"
					@popup-show="timeZoneDate = new Date()" />
			</HelpRow>
			<!--	Short Date Format Setting	-->
			<HelpRow
				:label="$t('help.settings.ui.date-and-time.short-date-format.label')"
				:title="$t('help.settings.ui.date-and-time.short-date-format.title')"
				:text="$t('help.settings.ui.date-and-time.short-date-format.text')">
				<q-select
					v-model:model-value="shortDateFormat"
					:options="shortDateOptions"
					data-cy="short-date-format">
					<template #option="scope">
						<q-item
							v-bind="scope.itemProps"
							:data-cy="`option-${scope.opt.value}`">
							<q-item-section>
								<q-item-label> {{ scope.opt.label }}</q-item-label>
							</q-item-section>
						</q-item>
					</template>
				</q-select>
			</HelpRow>
			<!--	Long Date Format Setting	-->
			<HelpRow
				:label="$t('help.settings.ui.date-and-time.long-date-format.label')"
				:title="$t('help.settings.ui.date-and-time.long-date-format.title')"
				:text="$t('help.settings.ui.date-and-time.long-date-format.text')">
				<q-select
					v-model:model-value="longDateFormat"
					:options="longDateOptions"
					data-cy="long-date-format">
					<template #option="scope">
						<q-item
							v-bind="scope.itemProps"
							:data-cy="`option-${scope.opt.value}`">
							<q-item-section>
								<q-item-label> {{ scope.opt.label }}</q-item-label>
							</q-item-section>
						</q-item>
					</template>
				</q-select>
			</HelpRow>
			<!--	Time Format Setting	-->
			<HelpRow
				:label="$t('help.settings.ui.date-and-time.time-format.label')"
				:title="$t('help.settings.ui.date-and-time.time-format.title')"
				:text="$t('help.settings.ui.date-and-time.time-format.text')">
				<q-select
					v-model:model-value="timeFormat"
					:options="timeFormatOptions"
					data-cy="time-format">
					<template #option="scope">
						<q-item
							v-bind="scope.itemProps"
							:data-cy="`option-${scope.opt.value}`">
							<q-item-section>
								<q-item-label> {{ scope.opt.label }}</q-item-label>
							</q-item-section>
						</q-item>
					</template>
				</q-select>
			</HelpRow>
			<!--	Show Relative Dates Setting	-->
			<HelpRow
				:label="$t('help.settings.ui.date-and-time.show-relative-dates.label')"
				:title="$t('help.settings.ui.date-and-time.show-relative-dates.title')"
				:text="$t('help.settings.ui.date-and-time.show-relative-dates.text')">
				<q-toggle
					v-model:model-value="settingsStore.dateTimeSettings.showRelativeDates"
					size="lg"
					color="red"
					data-cy="relative-date" />
			</HelpRow>
		</HelpGroup>
	</QSection>
</template>

<script setup lang="ts">
import { format } from 'date-fns';
import { enUS, fr } from 'date-fns/locale';
import { TZDate } from '@date-fns/tz';
import { orderBy } from 'lodash-es';
import { get, set } from '@vueuse/core';
import { useSettingsStore } from '@store';

const i18n = useI18n();
const settingsStore = useSettingsStore();

interface ISelectOption {
	value: string;
	label: string;
}

// region Settings
const defaultSelectOption: ISelectOption = { value: '', label: '' };
const shortDateFormat = computed({
	get: (): ISelectOption =>
		get(shortDateOptions).find((x) => x.value === settingsStore.dateTimeSettings.shortDateFormat) ?? defaultSelectOption,
	set: (value: ISelectOption) => (settingsStore.dateTimeSettings.shortDateFormat = value.value),
});
const longDateFormat = computed({
	get: (): ISelectOption =>
		get(longDateOptions).find((x) => x.value === settingsStore.dateTimeSettings.longDateFormat) ?? defaultSelectOption,
	set: (value: ISelectOption) => (settingsStore.dateTimeSettings.longDateFormat = value.value),
});
const timeFormat = computed({
	get: (): ISelectOption =>
		get(timeFormatOptions).find((x) => x.value === settingsStore.dateTimeSettings.timeFormat) ?? defaultSelectOption,
	set: (value: ISelectOption) => (settingsStore.dateTimeSettings.timeFormat = value.value),
});

// endregion

const getLocale = computed(() => {
	switch (get(i18n.locale)) {
		case 'en-US':
			return { locale: enUS };
		case 'fr-FR':
			return { locale: fr };
		default:
			return { locale: enUS };
	}
});

const shortDateOptions = computed(() => {
	const values: string[] = ['MMM dd yyyy', 'dd MMM yyyy', 'MM/dd/yyyy', 'dd/MM/yyyy', 'yyyy-MM-dd'];
	const date = Date.now();

	return values.map((dateFormat) => {
		return {
			value: dateFormat,
			label: format(date, dateFormat, get(getLocale)),
		};
	});
});

const longDateOptions = computed(() => {
	const values: string[] = ['EEEE, MMMM dd, yyyy', 'EEEE, dd MMMM yyyy'];
	const date = Date.now();

	return values.map((x) => {
		return {
			value: x,
			label: format(date, x, get(getLocale)),
		};
	});
});

const timeFormatOptions = computed(() => {
	const values: string[] = ['HH:mm:ss', 'pp'];
	const date = new TZDate(Date.now(), settingsStore.dateTimeSettings.timeZone);
	return values.map((x) => {
		return {
			value: x,
			label: format(date, x, get(getLocale)),
		};
	});
});

const supportedTimeZones = Intl.supportedValuesOf('timeZone');
const timeZoneDate = ref(new Date());
const timeZoneOptions = computed<ISelectOption[]>(() => {
	const date = get(timeZoneDate);
	const options = [...new Set(['UTC', settingsStore.dateTimeSettings.timeZone, ...supportedTimeZones])].map((zone) => {
		const zonedDate = new TZDate(date, zone);
		return {
			value: zone,
			label: `(UTC${format(zonedDate, 'xxx')}) ${zone}`,
			offset: -zonedDate.getTimezoneOffset(),
		};
	});
	return orderBy(options, ['offset', 'value']);
});
const timeZoneFilter = ref('');
const filteredTimeZoneOptions = computed(() => {
	const query = get(timeZoneFilter).trim().toLowerCase();
	return query ? get(timeZoneOptions).filter((option) => option.label.toLowerCase().includes(query)) : get(timeZoneOptions);
});

function filterTimeZones(value: string, update: (callback: () => void) => void) {
	update(() => set(timeZoneFilter, value));
}
</script>
