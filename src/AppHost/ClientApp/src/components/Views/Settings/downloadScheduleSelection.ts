export const DOWNLOAD_SCHEDULE_SLOTS_PER_DAY = 48;
export const DOWNLOAD_SCHEDULE_SLOT_COUNT = 7 * DOWNLOAD_SCHEDULE_SLOTS_PER_DAY;
export const DOWNLOAD_SCHEDULE_DAY_KEYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const;
export const MAX_DOWNLOAD_SCHEDULE_LIMIT = 2_147_483_647;

export type DownloadScheduleLimit = number | null;
export type DownloadScheduleDays = Record<string, Record<string, DownloadScheduleLimit>>;

export interface DownloadScheduleRange {
	days: number[];
	from: number;
	until: number;
}

/** Monday-first half-hour slots; Until is exclusive and overnight ranges wrap into the following day. */
export function getDownloadScheduleSlots(range: DownloadScheduleRange): number[] {
	if (!Number.isInteger(range.from) || range.from < 0 || range.from >= DOWNLOAD_SCHEDULE_SLOTS_PER_DAY
		|| !Number.isInteger(range.until) || range.until < 0 || range.until > DOWNLOAD_SCHEDULE_SLOTS_PER_DAY
		|| range.from === range.until
		|| range.days.some((day) => !Number.isInteger(day) || day < 0 || day > 6)) {
		return [];
	}
	const slots = new Set<number>();
	for (const day of range.days) {
		const end = range.until > range.from ? range.until : range.until + DOWNLOAD_SCHEDULE_SLOTS_PER_DAY;
		for (let halfHour = range.from; halfHour < end; halfHour++) {
			slots.add((day * DOWNLOAD_SCHEDULE_SLOTS_PER_DAY + halfHour) % DOWNLOAD_SCHEDULE_SLOT_COUNT);
		}
	}
	return [...slots].sort((left, right) => left - right);
}

export function isDownloadScheduleLimit(value: number | null): boolean {
	return value === null || (Number.isInteger(value) && value > 0 && value <= MAX_DOWNLOAD_SCHEDULE_LIMIT);
}

export function formatDownloadScheduleTime(slot: number): string {
	const minutes = slot * 30;
	return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${minutes % 60 === 0 ? '00' : '30'}`;
}

export function decodeDownloadScheduleDays(days: DownloadScheduleDays | null | undefined): DownloadScheduleLimit[] {
	const limits: DownloadScheduleLimit[] = Array(DOWNLOAD_SCHEDULE_SLOT_COUNT).fill(null);
	for (let day = 0; day < DOWNLOAD_SCHEDULE_DAY_KEYS.length; day++) {
		const changes = days?.[DOWNLOAD_SCHEDULE_DAY_KEYS[day]!];
		if (!changes) continue;
		let current: DownloadScheduleLimit = null;
		for (let slot = 0; slot < DOWNLOAD_SCHEDULE_SLOTS_PER_DAY; slot++) {
			const time = formatDownloadScheduleTime(slot);
			if (Object.hasOwn(changes, time)) current = changes[time] ?? null;
			limits[day * DOWNLOAD_SCHEDULE_SLOTS_PER_DAY + slot] = current;
		}
	}
	return limits;
}

/** Encodes each day independently, so limits never implicitly carry across midnight. */
export function encodeDownloadScheduleDays(limits: readonly DownloadScheduleLimit[]): DownloadScheduleDays {
	if (limits.length !== DOWNLOAD_SCHEDULE_SLOT_COUNT) throw new RangeError(`Expected ${DOWNLOAD_SCHEDULE_SLOT_COUNT} half-hour limits.`);
	const days: DownloadScheduleDays = {};
	for (let day = 0; day < DOWNLOAD_SCHEDULE_DAY_KEYS.length; day++) {
		const changes: Record<string, DownloadScheduleLimit> = {};
		let previous: DownloadScheduleLimit = null;
		for (let slot = 0; slot < DOWNLOAD_SCHEDULE_SLOTS_PER_DAY; slot++) {
			const limit = limits[day * DOWNLOAD_SCHEDULE_SLOTS_PER_DAY + slot]!;
			if (!isDownloadScheduleLimit(limit)) throw new RangeError('Schedule limits must be Unlimited or a positive Int32 value.');
			if (limit !== previous) {
				changes[formatDownloadScheduleTime(slot)] = limit;
				previous = limit;
			}
		}
		if (Object.keys(changes).length) days[DOWNLOAD_SCHEDULE_DAY_KEYS[day]!] = changes;
	}
	return days;
}
