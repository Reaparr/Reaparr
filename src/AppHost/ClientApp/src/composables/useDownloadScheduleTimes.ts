import { format } from 'date-fns';
import { enUS, fr } from 'date-fns/locale';
import { TZDate } from '@date-fns/tz';
import type { MaybeRefOrGetter } from 'vue';

// Schedule slots are local wall-clock times, not instants to convert between timezones.
const slotDates = Array.from({ length: 49 }, (_, slot) => new TZDate(2000, 0, 1, 0, slot * 30, 0, 'UTC'));

export function useDownloadScheduleTimes(timeFormat: MaybeRefOrGetter<string>, locale: MaybeRefOrGetter<string>) {
	return computed(() => slotDates.map((date, value) => ({
		value,
		label: format(date, toValue(timeFormat), { locale: toValue(locale) === 'fr-FR' ? fr : enUS }) + (value === 48 ? ' (+1)' : ''),
	})));
}
