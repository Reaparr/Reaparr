<template>
	<div
		v-if="controlled"
		ref="controlledScrollRef"
		class="media-q-table-scroll media-overview-table--controlled"
		data-cy="photo-overview-scroll"
		:aria-busy="props.selectionLoading || undefined"
		:aria-rowcount="props.itemCount">
		<QLinearProgress
			v-if="props.selectionLoading"
			class="media-overview-table__selection-progress"
			indeterminate />
		<QBanner
			v-if="props.selectionError"
			class="q-mb-sm"
			role="alert"
			data-cy="media-overview-table-selection-error">
			{{ $t('components.photo-browser.load-error') }}
		</QBanner>
		<table class="media-overview-table media-overview-table__virtual q-table">
			<thead>
				<tr>
					<th class="media-overview-table__selection-column">
						<q-checkbox
							dense
							data-cy="photo-select-all"
							:aria-label="$t('general.commands.selection')"
							:model-value="controlledIndeterminate ? null : props.allSelected"
							:disable="props.selectionLoading"
							@update:model-value="emit('select-all', Boolean($event))" />
					</th>
					<th
						v-for="column in props.columns"
						:key="column.name"
						:class="column.align ? `text-${column.align}` : undefined">
						{{ column.label }}
					</th>
				</tr>
			</thead>
			<tbody>
				<tr
					class="media-overview-table__virtual-spacer"
					aria-hidden="true"
					:style="{ height: `${safeTotalSize}px` }">
					<td :colspan="props.columns.length + 1" />
				</tr>
				<tr
					v-for="virtualRow in rowVirtualizer.getVirtualItems()"
					:key="virtualRow.index"
					class="media-overview-table__virtual-row"
					:data-photo-overview-index="virtualRow.index"
					:style="{
						transform: `translateY(${virtualRow.start}px)`,
					}">
					<template v-if="getControlledRow(virtualRow.index)">
						<td class="media-overview-table__selection-column">
							<q-checkbox
								dense
								data-cy="photo-select-row"
								:aria-label="`${$t('general.commands.selection')} ${getControlledRowTitle(getControlledRow(virtualRow.index)!)}`"
								:model-value="props.isRowSelected?.(getControlledRow(virtualRow.index)!) ?? false"
								@click.stop
								@update:model-value="emit('row-selection', { row: getControlledRow(virtualRow.index)!, selected: Boolean($event) })" />
						</td>
						<slot
							name="controlled-row"
							:row="getControlledRow(virtualRow.index)!"
							:index="virtualRow.index" />
					</template>
					<td
						v-else
						:colspan="props.columns.length + 1"
						class="media-overview-table__loading-cell">
						<q-skeleton type="text" />
					</td>
				</tr>
			</tbody>
		</table>
	</div>
	<div
		v-else
		class="media-q-table-scroll">
		<QTable
			class="media-overview-table"
			:selected="props.selected"
			selection="multiple"
			:row-key="props.rowKey"
			:columns="props.columns"
			:rows="props.rows ?? []"
			:rows-per-page-options="[0]"
			:grid="$q.screen.lt.md"
			:grid-header="$q.screen.lt.md && !$slots.item"
			:hide-header="$q.screen.lt.md && Boolean($slots.item)"
			:visible-columns="$q.screen.lt.md && !$slots.item ? ['title', 'actions'] : undefined"
			hide-pagination
			flat
			dense
			@update:selected="emitSelection"
			@row-click="emitRowClick">
			<template
				v-for="(_, name) in forwardedSlots"
				#[name]="slotProps">
				<slot
					:name="name"
					v-bind="slotProps" />
			</template>
			<template #body-selection="slotProps">
				<slot
					name="body-selection"
					v-bind="slotProps">
					<q-checkbox
						dense
						:aria-label="`${$t('general.commands.selection')} ${getRowTitle(slotProps.row)}`"
						:model-value="slotProps.selected"
						@click.stop
						@update:model-value="updateItemSelection(slotProps.row, Boolean($event))" />
				</slot>
			</template>
			<template #body-cell-title="slotProps">
				<slot
					name="body-cell-title"
					v-bind="slotProps">
					<q-td :props="slotProps">
						<q-btn
							flat
							dense
							no-caps
							no-wrap
							align="left"
							padding="0"
							class="full-width"
							:label="getRowTitle(slotProps.row)"
							@click.stop="emitItemRowClick(slotProps.row, $event)" />
					</q-td>
				</slot>
			</template>
			<template #body-cell-actions="slotProps">
				<slot
					name="body-cell-actions"
					v-bind="slotProps">
					<q-td class="text-center">
						<slot
							name="actions"
							v-bind="slotProps" />
					</q-td>
				</slot>
			</template>
			<template #item="slotProps">
				<slot
					name="item"
					v-bind="slotProps">
					<div
						class="media-q-table-card media-overview-table__item"
						data-cy="media-q-table-mobile-card"
						tabindex="0"
						role="group"
						:aria-label="getRowTitle(slotProps.row)"
						@keydown.enter.self.prevent="emitItemRowClick(slotProps.row, $event)"
						@keydown.space.self.prevent="emitItemRowClick(slotProps.row, $event)"
						@click="emitItemRowClick(slotProps.row, $event)">
						<div class="media-q-table-card__header">
							<q-checkbox
								dense
								:aria-label="`${$t('general.commands.selection')} ${getRowTitle(slotProps.row)}`"
								:model-value="slotProps.selected"
								@click.stop
								@update:model-value="updateItemSelection(slotProps.row, Boolean($event))" />
							<QText
								class="media-q-table-card__title"
								:value="getRowTitle(slotProps.row)" />
							<slot
								name="actions"
								v-bind="slotProps" />
						</div>
						<div
							v-if="mobileColumns.length"
							class="media-q-table-card__metadata">
							<QText
								v-for="column in mobileColumns"
								:key="column.name"
								:value="`${column.label}: ${getColumnValue(slotProps.row, column)}`" />
						</div>
					</div>
				</slot>
			</template>
		</QTable>
	</div>
</template>

<script setup lang="ts">
import Log from 'consola';
import type { Observable } from 'rxjs';
import { useSubscription } from '@vueuse/rxjs';
import { useVirtualizer } from '@tanstack/vue-virtual';
import type { QTableProps } from 'quasar';
import { get, set } from '@vueuse/core';
import { waitForElement } from '@composables';

type TableRows = NonNullable<QTableProps['rows']>;
type TableRow = TableRows[number];
type TableColumns = NonNullable<QTableProps['columns']>;
type TableColumn = TableColumns[number];
type TableSelected = NonNullable<QTableProps['selected']>;
type TableSelection = readonly TableRow[];
type ControlledRowProvider = (index: number) => TableRow | null;
type RangeRequest = (start: number, end: number) => Observable<void>;

const props = withDefaults(defineProps<{
	rows?: QTableProps['rows'];
	columns: TableColumns;
	selected?: TableSelected;
	rowKey?: QTableProps['rowKey'];
	rowTitle?: (row: TableRow) => string;
	itemCount?: number;
	itemAt?: ControlledRowProvider;
	requestRange?: RangeRequest;
	rangeContextKey?: string;
	isRowSelected?: (row: TableRow) => boolean;
	allSelected?: boolean;
	selectedCount?: number;
	selectionLoading?: boolean;
	selectionError?: boolean;
	initialScrollIndex?: number;
	onScrollIndex?: (index: number) => void;
}>(), {
	rows: undefined,
	selected: () => [],
	rowKey: 'id',
	rowTitle: undefined,
	itemCount: undefined,
	itemAt: undefined,
	requestRange: undefined,
	rangeContextKey: '',
	isRowSelected: undefined,
	allSelected: false,
	selectedCount: 0,
	selectionLoading: false,
	selectionError: false,
	initialScrollIndex: 0,
	onScrollIndex: undefined,
});
const ROW_HEIGHT = 42;
const MAX_CSS_HEIGHT = 33_000_000;

const controlledScrollRef = ref<HTMLElement | null>(null);
const requestedRange = ref('');

const $q = useQuasar();
const slots = useSlots();
const rowVirtualizer = useVirtualizer(
	computed(() => ({
		count: get(controlled) ? Math.max(0, props.itemCount ?? 0) : 0,
		getScrollElement: () => get(controlledScrollRef),
		estimateSize: () => ROW_HEIGHT,
		overscan: 8,
		getItemKey: (index: number) => index,
		onChange: (_instance: unknown) => {
			if (!get(controlled))
				return;
			const first = requestControlledRange();
			if (first === null)
				return;
			props.onScrollIndex?.(first);
		},
	})),
);

const controlled = computed(() => props.itemCount !== undefined && props.itemAt !== undefined);
const safeTotalSize = computed(() => Math.min(get(rowVirtualizer).getTotalSize(), MAX_CSS_HEIGHT));
const controlledIndeterminate = computed(() => (props.selectedCount ?? 0) > 0 && !props.allSelected);
const forwardedSlots = computed(() => {
	const forwarded = { ...slots };
	delete forwarded.item;
	delete forwarded['body-selection'];
	delete forwarded['body-cell-title'];
	delete forwarded['body-cell-actions'];
	delete forwarded.actions;
	delete forwarded['controlled-row'];
	return forwarded;
});
const mobileColumns = computed(() => props.columns.filter((column) =>
	column.name !== 'title' && column.name !== 'actions' && column.align !== 'left'));

watch(() => props.rangeContextKey, () => {
	set(requestedRange, '');
	nextTick(() => {
		if (get(controlled))
			requestControlledRange();
	}).catch((error) => Log.error('Failed to request the updated controlled table range', error));
});
watch(() => props.itemCount, () => {
	nextTick(() => {
		get(rowVirtualizer).measure();
		if (get(controlled))
			requestControlledRange();
	}).catch((error) => Log.error('Failed to update the controlled table after item count changed', error));
});
watch(() => props.initialScrollIndex, (index) => {
	if (get(controlled) && index > 0)
		nextTick(() => scrollToIndex(index)).catch((error) => Log.error('Failed to scroll to the initial controlled table index', error));
});

function requestControlledRange(): number | null {
	const visible = get(rowVirtualizer).getVirtualItems();
	const first = visible.at(0)?.index ?? 0;
	const last = visible.at(-1)?.index ?? Math.min(props.itemCount ?? 0, 20) - 1;
	if (last < first)
		return null;
	const rangeKey = `${props.rangeContextKey}:${props.itemCount}:${first}:${last + 1}`;
	if (props.requestRange && get(requestedRange) !== rangeKey) {
		set(requestedRange, rangeKey);
		useSubscription(props.requestRange(first, last + 1).subscribe());
	}
	return first;
}
function getControlledRowTitle(row: TableRow): string {
	return props.rowTitle?.(row) ?? getRowTitle(row);
}
function getControlledRow(index: number): TableRow | null {
	return get(controlled) ? props.itemAt?.(index) ?? null : null;
}
function getRowKey(row: TableRow): unknown {
	const rowKey = props.rowKey ?? 'id';
	return typeof rowKey === 'function' ? rowKey(row) : row?.[rowKey];
}
function getRowTitle(row: TableRow): string {
	const title = row?.title;
	return title === undefined || title === null ? String(getRowKey(row) ?? '') : String(title);
}
function getColumnValue(row: TableRow, column: TableColumn): string {
	const value = typeof column.field === 'function' ? column.field(row) : row?.[column.field];
	const formattedValue = column.format?.(value, row);
	return String(formattedValue ?? value ?? '');
}
function emitSelection(selected: TableSelection): void {
	emit('selection', selected);
}
function emitRowClick(event: Event, row: TableRow): void {
	emit('row-click', { row, event });
}
function emitItemRowClick(row: TableRow, event: Event): void {
	emit('row-click', { row, event });
}
function updateItemSelection(row: TableRow, selected: boolean): void {
	const rowKey = getRowKey(row);
	if (!selected) {
		emit('selection', props.selected.filter((item) => getRowKey(item) !== rowKey));
		return;
	}
	const nextSelected = props.selected.some((item) => getRowKey(item) === rowKey)
		? props.selected
		: [...props.selected, row];
	emit('selection', nextSelected);
}
function scrollToIndex(index: number): void {
	if (!get(controlled) || index < 0)
		return;
	// Fixed-height rows need no index-scroll retries that could override the user's next scroll.
	const boundedIndex = Math.min(index, Math.max(0, (props.itemCount ?? 1) - 1));
	get(rowVirtualizer).scrollToOffset(boundedIndex * ROW_HEIGHT, { align: 'start' });
}
async function focusItem(index: number): Promise<boolean> {
	if (!get(controlled) || index < 0 || index >= (props.itemCount ?? 0))
		return false;
	scrollToIndex(index);
	const target = await waitForElement(
		get(controlledScrollRef),
		`[data-photo-overview-index="${index}"] [data-photo-overview-focus="title"]`,
	);
	if (!target)
		return false;
	target.focus({ preventScroll: true });
	return true;
}

onMounted(() => {
	if (!get(controlled))
		return;
	nextTick(() => {
		if (props.initialScrollIndex > 0)
			scrollToIndex(props.initialScrollIndex);
		requestControlledRange();
	}).catch((error) => Log.error('Failed to initialize the controlled table after mount', error));
});

const emit = defineEmits<{
	(e: 'selection', selected: TableSelection): void;
	(e: 'row-click', payload: { row: TableRow; event: Event }): void;
	(e: 'select-all', selected: boolean): void;
	(e: 'row-selection', payload: { row: TableRow; selected: boolean }): void;
}>();

defineExpose({ scrollToIndex, focusItem });
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;
@use 'quasar/src/css/variables.sass' as quasar;

.media-q-table-scroll {
  width: 100%;
  min-width: 0;
  max-width: 100%;
  height: 100%;
}

.media-overview-table {
  width: 100%;
  height: 100%;
  background: transparent;
  border-radius: 0;
  box-shadow: none;
  color: inherit;

  .q-table__middle,
  .q-table {
    min-height: 0;
    background: transparent;
  }

  .q-table__grid-content {
    align-content: flex-start;
  }

  thead tr {
    height: $media-table-row-height;
    border-bottom: 1px solid;
  }

  thead th {
    height: $media-table-row-height;
    padding: 0 8px;
    background: transparent;
    font-weight: bold;
    white-space: nowrap;
  }

  tbody tr {
    height: $media-table-row-height;
    background: transparent;
    border-bottom: 1px solid;
  }

  tbody td {
    height: $media-table-row-height;
    padding: 0 8px;
  }
}

.media-overview-table--controlled {
  position: relative;
  overflow: auto;

  .media-overview-table__selection-progress {
    position: absolute;
    z-index: 2;
    inset: 0 0 auto;
  }
}

.media-overview-table__virtual {
  table-layout: fixed;

  thead,
  tbody {
    width: 100%;
  }

  tbody {
    position: relative;
    display: block;
  }

  thead,
  thead tr,
  .media-overview-table__virtual-row {
    display: table;
    width: 100%;
    table-layout: fixed;
  }

  .media-overview-table__virtual-row {
    position: absolute;
    top: 0;
    left: 0;
  }

  .media-overview-table__loading-cell {
    height: $media-table-row-height;
  }
}
.media-overview-table__virtual-spacer {
  display: table;
  width: 100%;
  table-layout: fixed;
  visibility: hidden;
  pointer-events: none;
}

.media-overview-table__virtual-spacer td {
  height: 100%;
  padding: 0;
}

.media-overview-table__selection-column {
  width: 52px;
  text-align: center;
}

.body--dark {
  .media-overview-table {
    thead tr,
    tbody tr,
    .media-q-table-card {
      border-bottom-color: $separator-dark-color;
    }
  }
}

.body--light {
  .media-overview-table {
    thead tr,
    tbody tr,
    .media-q-table-card {
      border-bottom-color: $separator-color;
    }
  }
}

@media (max-width: quasar.$breakpoint-sm-max) {
  .media-overview-table {
    thead tr,
    thead th {
      height: 48px;
    }
  }

  .media-q-table-scroll {
    overflow-x: hidden;
  }

  .media-q-table-card {
    width: 100%;
    min-width: 0;
    min-height: 96px;
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid $separator-color;

    &__header {
      display: grid;
      grid-template-columns: 44px minmax(0, 1fr) 44px;
      align-items: center;
      gap: 0.5rem;
    }

    &__title {
      min-width: 0;
      font-weight: 600;
      overflow-wrap: anywhere;
    }

    &__metadata {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      min-width: 0;
      padding: 0.25rem 0 0.25rem 3.25rem;
      flex-wrap: wrap;
    }

    &.media-overview-table__item .media-q-table-card__metadata > * {
      width: auto;
      flex: 0 1 auto;
    }

    .q-btn,
    .q-checkbox {
      min-width: 44px;
      min-height: 44px;
    }
  }
}
</style>
