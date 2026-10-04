<template>
	<div
		ref="grid"
		role="grid"
		data-cy="schedule-grid"
		:aria-label="label"
		:aria-rowcount="8"
		:aria-colcount="49"
		aria-multiselectable="true">
		<QMarkupTable
			flat
			bordered
			separator="cell"
			class="schedule-grid-window background-md">
			<thead>
				<tr role="row">
					<th
						scope="col"
						class="schedule-day schedule-timezone text-left background-md">
						{{ timeZone }}
					</th>
					<th
						v-for="hour in 24"
						:key="hour"
						colspan="2"
						scope="col"
						class="text-center">
						{{ times[(hour - 1) * 2]!.label }}
					</th>
				</tr>
			</thead>
			<tbody>
				<tr
					v-for="(day, dayIndex) in days"
					:key="dayIndex"
					role="row"
					class="q-tr--no-hover">
					<th
						scope="row"
						class="schedule-day text-left background-md">
						{{ day }}
					</th>
					<td
						colspan="48"
						class="schedule-slots">
						<div class="schedule-day-slots">
							<QBtn
								v-for="halfHour in 48"
								:key="halfHour"
								flat
								dense
								role="gridcell"
								class="schedule-cell"
								:class="{
									'selected': selectedSlots.has(dayIndex * 48 + halfHour - 1),
									'limited': slotLimit(dayIndex * 48 + halfHour - 1) !== null,
									'selection-top': selectedSlots.has(dayIndex * 48 + halfHour - 1) && !selectedSlots.has((dayIndex - 1) * 48 + halfHour - 1),
									'selection-bottom': selectedSlots.has(dayIndex * 48 + halfHour - 1) && !selectedSlots.has((dayIndex + 1) * 48 + halfHour - 1),
									'selection-start': selectedSlots.has(dayIndex * 48 + halfHour - 1) && (halfHour === 1 || !selectedSlots.has(dayIndex * 48 + halfHour - 2)),
									'selection-end': selectedSlots.has(dayIndex * 48 + halfHour - 1) && (halfHour === 48 || !selectedSlots.has(dayIndex * 48 + halfHour)),
								}"
								:tabindex="focusedSlot === dayIndex * 48 + halfHour - 1 ? 0 : -1"
								:aria-selected="selectedSlots.has(dayIndex * 48 + halfHour - 1)"
								:aria-label="cellLabel(day, dayIndex * 48 + halfHour - 1)"
								:aria-rowindex="dayIndex + 2"
								:aria-colindex="halfHour + 1"
								:data-schedule-slot="dayIndex * 48 + halfHour - 1"
								:data-cy="`schedule-cell-${dayIndex}-${halfHour - 1}`"
								@pointerdown="onPointerDown($event, dayIndex * 48 + halfHour - 1)"
								@click="onClick($event, dayIndex * 48 + halfHour - 1)"
								@keydown="onKeyDown($event, dayIndex * 48 + halfHour - 1)" />
							<div
								v-for="band in bands[dayIndex]"
								:key="band.from"
								class="schedule-band-label"
								:class="{ 'text-weight-bold': band.selected }"
								aria-hidden="true"
								:style="{ insetInlineStart: `${band.from / 48 * 100}%`, width: `${(band.until - band.from) / 48 * 100}%` }">
								{{ band.limit === null ? unlimitedLabel : `${numberFormat.format(band.limit)} kB/s` }}
							</div>
						</div>
					</td>
				</tr>
			</tbody>
		</QMarkupTable>
	</div>
</template>

<script setup lang="ts">
import { get, set, useEventListener } from '@vueuse/core';
import type { DownloadScheduleRange } from '@composables/download-schedule';

const props = defineProps<{
	limits: (number | null)[];
	selection: number[];
	previewLimit?: number | null;
	days: string[];
	label: string;
	unlimitedLabel: string;
	locale: string;
	timeFormat: string;
	timeZone: string;
}>();
const emit = defineEmits<{ select: [range: DownloadScheduleRange] }>();
const grid = useTemplateRef<HTMLElement>('grid');
const focusedSlot = ref(0);
const anchor = ref(0);
const dragging = ref(false);
let touchSelection = false;
const selectedSlots = computed(() => new Set(props.selection));
const numberFormat = computed(() => new Intl.NumberFormat(props.locale));
const times = useDownloadScheduleTimes(() => props.timeFormat, () => props.locale);

const bands = computed(() => props.days.map((_, day) => {
	const result: { from: number; until: number; limit: number | null; selected: boolean }[] = [];
	for (let halfHour = 0; halfHour < 48; halfHour++) {
		const limit = slotLimit(day * 48 + halfHour);
		const selected = get(selectedSlots).has(day * 48 + halfHour);
		const previous = result.at(-1);
		if (previous?.limit === limit && previous.selected === selected) {
			previous.until = halfHour + 1;
		} else {
			result.push({ from: halfHour, until: halfHour + 1, limit, selected });
		}
	}
	return result;
}));

function slotLimit(slot: number): number | null {
	return get(selectedSlots).has(slot) && props.previewLimit !== undefined ? props.previewLimit : props.limits[slot]!;
}

function cellLabel(day: string, slot: number): string {
	const daySlot = slot % 48;
	const until = get(times)[daySlot + 1]!.label;
	const limit = slotLimit(slot);
	return `${day} ${get(times)[daySlot]!.label}–${until}: ${limit === null ? props.unlimitedLabel : `${get(numberFormat).format(limit!)} kB/s`}`;
}

function selectBetween(first: number, last: number) {
	const firstDay = Math.min(Math.floor(first / 48), Math.floor(last / 48));
	const lastDay = Math.max(Math.floor(first / 48), Math.floor(last / 48));
	emit('select', {
		days: Array.from({ length: lastDay - firstDay + 1 }, (_, day) => firstDay + day),
		from: Math.min(first % 48, last % 48),
		until: Math.max(first % 48, last % 48) + 1,
	});
}

function onPointerDown(event: PointerEvent, slot: number) {
	if (!event.isPrimary || event.button !== 0) return;
	touchSelection = event.pointerType === 'touch';
	if (touchSelection) return;
	set(anchor, slot);
	set(focusedSlot, slot);
	selectBetween(slot, slot);
	event.preventDefault();
	const button = event.currentTarget as HTMLButtonElement;
	button.focus();
	button.setPointerCapture(event.pointerId);
	set(dragging, true);
}

function onClick(event: Event, slot: number) {
	if (!touchSelection && event instanceof MouseEvent && event.detail !== 0) return;
	touchSelection = false;
	if (!(event instanceof MouseEvent) || !event.shiftKey) set(anchor, slot);
	set(focusedSlot, slot);
	selectBetween(get(anchor), slot);
}

useEventListener(grid, 'pointermove', (event: PointerEvent) => {
	if (!get(dragging)) return;
	const button = document.elementFromPoint(event.clientX, event.clientY)?.closest<HTMLButtonElement>('button[data-schedule-slot]');
	if (!button || !get(grid)?.contains(button)) return;
	const slot = Number(button.dataset.scheduleSlot);
	set(focusedSlot, slot);
	selectBetween(get(anchor), slot);
});
useEventListener(grid, 'pointerup', () => set(dragging, false));
useEventListener(grid, 'pointercancel', () => set(dragging, false));

function onKeyDown(event: KeyboardEvent, slot: number) {
	let target = slot;
	const day = Math.floor(slot / 48);
	const halfHour = slot % 48;
	switch (event.key) {
		case 'ArrowLeft':
			target = day * 48 + Math.max(0, halfHour - 1);
			break;
		case 'ArrowRight':
			target = day * 48 + Math.min(47, halfHour + 1);
			break;
		case 'ArrowUp':
			target = Math.max(0, day - 1) * 48 + halfHour;
			break;
		case 'ArrowDown':
			target = Math.min(6, day + 1) * 48 + halfHour;
			break;
		case 'Home':
			target = event.ctrlKey ? 0 : day * 48;
			break;
		case 'End':
			target = event.ctrlKey ? 335 : day * 48 + 47;
			break;
		case ' ':
		case 'Enter':
			event.preventDefault();
			if (!event.shiftKey) set(anchor, slot);
			selectBetween(get(anchor), slot);
			return;
		default:
			return;
	}
	event.preventDefault();
	if (event.shiftKey) selectBetween(get(anchor), target);
	else set(anchor, target);
	set(focusedSlot, target);
	get(grid)?.querySelector<HTMLButtonElement>(`button[data-schedule-slot="${target}"]`)?.focus();
}
</script>

<style scoped lang="scss">
.schedule-grid-window :deep(table) {
  min-width: 1000px;
  table-layout: fixed;
}

.schedule-grid-window :deep(th:not(.schedule-day)) {
  padding-inline: 0;
}

.schedule-day {
  position: sticky;
  inset-inline-start: 0;
  z-index: 3;
  width: 88px;
  padding-inline: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.schedule-timezone {
  white-space: normal;
  overflow-wrap: anywhere;
}

.schedule-slots {
  padding: 0;
}

.schedule-day-slots {
  position: relative;
  display: grid;
  grid-template-columns: repeat(48, minmax(18px, 1fr));
}

.schedule-cell {
  min-height: 48px;
  min-width: 0;
  border-radius: 0;
  border-inline-end: 1px solid color-mix(in srgb, currentColor 12%, transparent);
  cursor: crosshair;
}

.schedule-cell.limited {
  background: color-mix(in srgb, var(--q-primary) 15%, transparent);
}

.schedule-cell.selected::after {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  border: 0 solid var(--q-primary);
  z-index: 1;
}

.schedule-cell.selection-top::after {
  border-top-width: 2px;
}

.schedule-cell.selection-bottom::after {
  border-bottom-width: 2px;
}

.schedule-cell.selection-start::after {
  border-inline-start-width: 2px;
}

.schedule-cell.selection-end::after {
  border-inline-end-width: 2px;
}

.schedule-cell:focus-visible {
  outline: 2px solid var(--q-primary);
  outline-offset: -4px;
  z-index: 2;
}

.schedule-band-label {
  position: absolute;
  inset-block: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding-inline: 4px;
  overflow: hidden;
  pointer-events: none;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}
</style>
