<template>
	<TreeTable
		v-bind="$attrs"
		:page-link-size="pageLinkSize"
		:paginator="paginator"
		:row-hover="rowHover"
		:rows="rows"
		:rows-per-page-options="rowsPerPageOptions"
		:value="nodes"
		:paginator-position="paginatorPosition"
		:scroll-height="scrollHeight"
		:scrollable="scrollable"
		:size="size">
		<!-- @vue-ignore PrimeVue's TreeTableSlots type omits the documented nodetoggleicon slot. -->
		<template #nodetoggleicon="{ expanded }">
			<QIcon
				:name="expanded ? 'mdi-chevron-down' : 'mdi-chevron-right'"
				size="32px" />
		</template>
		<Column
			v-for="(column, columnIndex) in columns"
			:key="column.field"
			:expander="isTitleColumn(column, columnIndex) || column.expander"
			:field="column.field"
			:sortable="column.sortable"
			:header-style="getColumnHeaderStyle(column, columnIndex)"
			:body-style="getColumnBodyStyle(column, columnIndex)">
			<template #header>
				<QRow
					align="center"
					no-wrap>
					<QCheckbox
						v-if="isSelectionColumn(column, columnIndex)"
						:model-value="headerSelectionValue"
						@update:model-value="toggleAllSelection($event === true)"
						@click.stop />
					<slot
						:name="`header-${column.field}`"
						:column="column">
						<QText :value="column.header" />
					</slot>
				</QRow>
			</template>
			<template #body="{ node }: { node: QTreeNode<TData> }">
				<QRow
					align="center"
					no-wrap
					:class="{ 'q-tree-table-title-cell': isTitleColumn(column, columnIndex) }">
					<QCheckbox
						v-if="isSelectionColumn(column, columnIndex)"
						:model-value="getNodeSelectionValue(node)"
						@update:model-value="toggleNodeSelection(node, $event === true)"
						@click.stop />
					<slot
						:name="`cell-${column.field}`"
						:node="node"
						:data="node.data"
						:column="column"
						:value="getColumnValue(node.data, column)">
						<QText
							v-if="getColumnType(column, columnIndex) === QTreeTableColumnType.Text || getColumnType(column, columnIndex) === QTreeTableColumnType.Title || getColumnType(column, columnIndex) === QTreeTableColumnType.Custom"
							:cy="`column-${column.field}-${node.key}`"
							:value="getColumnTextValue(node.data, column)"
							:align="column.align" />
						<QFileSize
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.FileSize"
							:cy="`column-${column.field}-${node.key}`"
							:size="getColumnNumberValue(node.data, column)" />
						<QFileSize
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.FileSpeed"
							:cy="`column-${column.field}-${node.key}`"
							:size="getColumnNumberValue(node.data, column)"
							speed />
						<QDuration
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.Duration"
							:cy="`column-${column.field}-${node.key}`"
							:value="getColumnNumberValue(node.data, column)"
							:align="column.align"
							short />
						<QDateTime
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.Date"
							:text="getColumnDateTextValue(node.data, column)"
							:align="column.align"
							short-date />
						<QDateTime
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.DateTime"
							:text="getColumnDateTextValue(node.data, column)"
							:align="column.align"
							short-date
							time />
						<QProgressBar
							v-else-if="getColumnType(column, columnIndex) === QTreeTableColumnType.Percentage"
							:cy="`column-${column.field}-${node.key}`"
							:value="getColumnNumberValue(node.data, column)" />
						<QText
							v-else
							:cy="`column-${column.field}-${node.key}`"
							:value="getColumnTextValue(node.data, column)"
							:align="column.align" />
					</slot>
				</QRow>
			</template>
		</Column>
		<template #empty>
			<slot name="empty" />
		</template>
	</TreeTable>
</template>

<script setup lang="ts" generic="TData extends object">
import { get, set } from '@vueuse/core';
import type { TreeNode } from 'primevue/treenode';
import type { QTreeTableColumn } from '@props';
import { QTreeTableColumnType } from '@props';

defineOptions({
	inheritAttrs: false,
});

const props = withDefaults(defineProps<{
	nodes: QTreeNode<TData>[];
	columns: QTreeTableColumn[];
	selectionKeys?: TreeTableSelectionKeys;
	paginator?: boolean;
	rows?: number;
	rowsPerPageOptions?: number[];
	pageLinkSize?: number;
	rowHover?: boolean;
	paginatorPosition?: 'top' | 'bottom' | 'both';
	scrollHeight?: string;
	scrollable?: boolean;
	size?: 'small' | 'large' | undefined;
}>(), {
	selectionKeys: undefined,
	paginator: true,
	rows: 25,
	rowsPerPageOptions: () => [25, 50, 100],
	pageLinkSize: 25,
	rowHover: true,
	paginatorPosition: 'bottom',
	scrollHeight: 'flex',
	scrollable: true,
	size: 'small',
});
const defaultColumnWidth = 140;

const internalSelectionKeys = ref<TreeTableSelectionKeys>({});

const activeSelectionKeys = computed((): TreeTableSelectionKeys => {
	return props.selectionKeys ?? get(internalSelectionKeys);
});
const headerSelectionValue = computed((): boolean | null => {
	const nodeKeys = flattenNodeKeys(props.nodes);

	if (nodeKeys.length === 0) {
		return false;
	}

	const selectedKeys = get(activeSelectionKeys);
	const checkedCount = nodeKeys.filter((key) => selectedKeys[key]?.checked).length;
	const partialCount = nodeKeys.filter((key) => selectedKeys[key]?.partialChecked).length;

	if (checkedCount === 0 && partialCount === 0) {
		return false;
	}

	return checkedCount === nodeKeys.length ? true : null;
});

export interface TreeTableSelectionKeyState {
	checked: boolean;
	partialChecked: boolean;
}
export interface QTreeNode<TNodeData extends object> extends TreeNode {
	data: TNodeData;
	children?: QTreeNode<TNodeData>[];
}
export type TreeTableSelectionKeys = Record<string, TreeTableSelectionKeyState>;

function getColumnType(column: QTreeTableColumn, columnIndex: number): QTreeTableColumnType {
	return column.type ?? (columnIndex === 0 ? QTreeTableColumnType.Title : QTreeTableColumnType.Text);
}
function isTitleColumn(column: QTreeTableColumn, columnIndex: number): boolean {
	return getColumnType(column, columnIndex) === QTreeTableColumnType.Title;
}
function isSelectionColumn(column: QTreeTableColumn, columnIndex: number): boolean {
	return column.selection ?? isTitleColumn(column, columnIndex);
}
function getColumnHeaderStyle(column: QTreeTableColumn, columnIndex: number): string {
	return getColumnStyle(column, columnIndex, true);
}
function getColumnBodyStyle(column: QTreeTableColumn, columnIndex: number): string {
	return getColumnStyle(column, columnIndex, false);
}
function getColumnStyle(column: QTreeTableColumn, columnIndex: number, isHeader: boolean): string {
	const columnType = getColumnType(column, columnIndex);
	const align = column.align ?? (columnType === QTreeTableColumnType.Title ? 'left' : 'right');
	const style = column.headerStyle && isHeader ? `${column.headerStyle} ` : column.bodyStyle && !isHeader ? `${column.bodyStyle} ` : '';

	if (columnType === QTreeTableColumnType.Title) {
		return `${style}text-align: ${align};`;
	}

	const width = column.width ?? defaultColumnWidth;
	const widthStyles = `width: ${width}px; min-width: ${width}px; max-width: ${width}px;`;

	if (isHeader) {
		return `${style}${widthStyles} text-align: ${align};`;
	}

	return `${style}${widthStyles} text-align: ${align}; overflow: hidden; text-overflow: ellipsis;`;
}
function getColumnValue(data: TData, column: QTreeTableColumn): unknown {
	return (data as Record<string, unknown>)[column.field];
}
function getColumnTextValue(data: TData, column: QTreeTableColumn): string | number | null {
	const value = getColumnValue(data, column);

	if (typeof value === 'string' || typeof value === 'number') {
		return value;
	}

	return value == null ? null : String(value);
}
function getColumnDateTextValue(data: TData, column: QTreeTableColumn): string | undefined {
	const value = getColumnTextValue(data, column);

	return value == null ? undefined : String(value);
}
function getColumnNumberValue(data: TData, column: QTreeTableColumn): number {
	const value = getColumnValue(data, column);

	if (typeof value === 'number') {
		return value;
	}

	if (typeof value === 'string') {
		return Number(value);
	}

	return 0;
}
function getNodeSelectionValue(node: QTreeNode<TData>): boolean | null {
	const selection = get(activeSelectionKeys)[getNodeKey(node)];

	if (selection?.checked) {
		return true;
	}

	return selection?.partialChecked ? null : false;
}
function toggleAllSelection(checked: boolean): void {
	emitSelectionChange(buildSelectionKeysForAll(props.nodes, checked));
}
function toggleNodeSelection(node: QTreeNode<TData>, checked: boolean): void {
	const checkedKeys = getCheckedKeys(get(activeSelectionKeys), props.nodes);
	const targetKeys = getNodeAndDescendantKeys(node);

	for (const key of targetKeys) {
		if (checked) {
			checkedKeys.add(key);
		} else {
			checkedKeys.delete(key);
		}
	}

	emitSelectionChange(normalizeSelectionKeys(props.nodes, checkedKeys));
}
function emitSelectionChange(selectionKeys: TreeTableSelectionKeys): void {
	if (!props.selectionKeys) {
		set(internalSelectionKeys, selectionKeys);
	}

	emit('update:selectionKeys', selectionKeys);
	emit('selected', selectionKeys);
}
function getNodeKey(node: QTreeNode<TData>): string {
	return String(node.key);
}
function flattenNodeKeys(nodes: QTreeNode<TData>[]): string[] {
	return nodes.flatMap((node) => [getNodeKey(node), ...flattenNodeKeys(node.children ?? [])]);
}
function getNodeAndDescendantKeys(node: QTreeNode<TData>): string[] {
	return [getNodeKey(node), ...(node.children ?? []).flatMap(getNodeAndDescendantKeys)];
}
function buildSelectionKeysForAll(nodes: QTreeNode<TData>[], checked: boolean): TreeTableSelectionKeys {
	if (!checked) {
		return {};
	}

	return normalizeSelectionKeys(nodes, new Set(flattenNodeKeys(nodes)));
}
function getCheckedKeys(selectionKeys: TreeTableSelectionKeys, nodes: QTreeNode<TData>[]): Set<string> {
	const checkedKeys = new Set<string>();

	addCheckedKeysFromSelection(selectionKeys, nodes, checkedKeys);

	return checkedKeys;
}
function addCheckedKeysFromSelection(
	selectionKeys: TreeTableSelectionKeys,
	nodes: QTreeNode<TData>[],
	checkedKeys: Set<string>,
): void {
	for (const node of nodes) {
		const key = getNodeKey(node);

		if (selectionKeys[key]?.checked) {
			for (const descendantKey of getNodeAndDescendantKeys(node)) {
				checkedKeys.add(descendantKey);
			}
			continue;
		}

		addCheckedKeysFromSelection(selectionKeys, node.children ?? [], checkedKeys);
	}
}
function normalizeSelectionKeys(nodes: QTreeNode<TData>[], checkedKeys: Set<string>): TreeTableSelectionKeys {
	const selectionKeys: TreeTableSelectionKeys = {};

	for (const node of nodes) {
		normalizeNodeSelection(node, checkedKeys, selectionKeys);
	}

	return selectionKeys;
}
function normalizeNodeSelection(
	node: QTreeNode<TData>,
	checkedKeys: Set<string>,
	selectionKeys: TreeTableSelectionKeys,
): TreeTableSelectionKeyState {
	const children = node.children ?? [];
	const childStates = children.map((child) => normalizeNodeSelection(child, checkedKeys, selectionKeys));
	const nodeIsChecked = checkedKeys.has(getNodeKey(node));
	const childrenAreChecked = childStates.length > 0 && childStates.every((state) => state.checked);
	const hasSelectedChild = childStates.some((state) => state.checked || state.partialChecked);
	const state = {
		checked: nodeIsChecked || childrenAreChecked,
		partialChecked: !nodeIsChecked && !childrenAreChecked && hasSelectedChild,
	};

	if (state.checked || state.partialChecked) {
		selectionKeys[getNodeKey(node)] = state;
	}

	return state;
}

const emit = defineEmits<{
	(e: 'update:selectionKeys' | 'selected', payload: TreeTableSelectionKeys): void;
}>();
</script>

<style lang="scss">
@use '@/assets/scss/variables' as *;

.p-treetable {
  table {
    white-space: nowrap;
    table-layout: fixed;
    width: 100%;
  }

  .p-treetable-header {
    color: inherit;
    background: transparent;
    border: none;
  }

  .p-treetable-thead > tr > th {
    color: inherit;
    background: transparent;
    border-top: 0.13rem solid rgba(255, 255, 255, 0.28);
    border-bottom: 0.13rem solid rgba(255, 255, 255, 0.28);
  }

  .p-treetable-tbody > tr {
    color: inherit;
    background: transparent;
    border-bottom: 0.13rem solid rgba(255, 255, 255, 0.28);

    &:focus {
      outline: none;
    }
  }

  .p-paginator {
    color: inherit;
    background: transparent;
  }
}

//this creates a pseudochild of the button the size of the first anscestor with "relative" size
button.p-treetable-toggler.p-link::before {
  content: '';
  display: block;
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
}

//this element originally had "relative" size, overwriting it allow the pseudochild to be sized relative to a later anscestor
.p-treetable-toggler {
  position: static;
}

// this element contains the full row, by making it relative the pseudochild can size itself based on this
.p-treetable .p-treetable-tbody > tr {
  position: relative;
}

.q-tree-table-title-cell {
  min-width: 0;

  > :last-child {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;

    .q-text {
      display: block;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }
}
</style>
