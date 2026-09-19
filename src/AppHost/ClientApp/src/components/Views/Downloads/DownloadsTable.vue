<template>
	<q-expansion-item
		v-model="isExpanded"
		default-opened
		hide-expand-icon
		class="downloads-table background-sm q-ma-md">
		<template #header>
			<QRow
				align="center"
				class="download-server-header full-width">
				<!-- Download Server Title -->
				<QCol class="download-server-header__title row items-center">
					<QStatus :value="serverConnectionStore.isServerConnected(plexServer.id)" />
					<span
						class="title q-ml-md"
						:class="{ 'inaccessible-item-text': !accountStore.getHasAccountServerAccess(plexServer.id) }">
						{{ serverStore.getServerName(plexServer.id) }}
					</span>
					<QBadge
						v-if="plexServer.isDownloadsPausedByUser"
						class="q-ml-sm"
						color="warning"
						text-color="black"
						:label="t('components.server-download-status.pause')" />
				</QCol>
				<QCol
					cols="auto"
					class="download-server-header__actions q-py-none q-ml-auto">
					<QRow
						align="center"
						no-gutters
						class="q-gutter-sm">
						<!-- Clean Download Tasks -->
						<QCol cols="auto">
							<IconButton
								cy="clear-completed-by-server-button"
								icon="mdi-notification-clear-all"
								:tooltip-text="t('components.downloads-table.clear-completed.button')"
								:aria-label="t('components.downloads-table.clear-completed.button')"
								:disabled="!hasCompletedDownloads || clearCompletedLoading"
								@click.stop="openClearCompletedDialog" />
						</QCol>
						<!-- Toggle Expansion button -->
						<QCol cols="auto">
							<IconButton
								cy="toggle-download-table-button"
								:icon="isExpanded ? 'mdi-chevron-up' : 'mdi-chevron-down'"
								:aria-label="`${isExpanded ? 'Collapse' : 'Expand'} ${serverStore.getServerName(plexServer.id)}`"
								@click.stop="toggleExpanded" />
						</QCol>
					</QRow>
				</QCol>
			</QRow>
		</template>
		<template #default>
			<div class="download-table-desktop">
				<!-- Download Table Per Server -->
				<QTreeTable
					:nodes="nodes"
					:columns="getDownloadTableColumns"
					:selection-keys="downloadStore.getSelectedDownloadTasks(plexServer.id)"
					@selected="downloadStore.updateSelectedDownloadTasks(plexServer.id, $event)">
					<template #cell-title="{ data }: { data: IDownloadTableNode }">
						<QMediaTypeIcon
							v-if="data.mediaType"
							:media-type="data.mediaType"
							class="q-mr-sm"
							:size="26" />
						<QText
							:cy="`column-title-${data.id}`"
							:value="data.title" />
					</template>
					<template #cell-status="{ data }: { data: IDownloadTableNode }">
						<QText
							:cy="`column-status-${data.id}`"
							:value="translateDownloadStatus(data.status)" />
					</template>
					<template #cell-actions="{ data }: { data: IDownloadTableNode }">
						<QRow
							justify="start"
							no-wrap>
							<QCol cols="auto">
								<IconSquareButton
									v-for="action in data.actions"
									:key="`${data.id}-${kebabCase(action.type)}`"
									:cy="`column-actions-${kebabCase(action.type)}-${data.id}`"
									:disabled="action.disabled"
									:icon="toButtonIcon(action.type)"
									:aria-label="`${String(action.type)}: ${data.title}`"
									:loading="action.loading"
									dense
									@click.stop="onTableAction({ action: action.type, data })" />
							</QCol>
						</QRow>
					</template>
				</QTreeTable>
			</div>
			<div
				class="download-cards"
				data-cy="download-mobile-list">
				<article
					v-for="entry in mobileNodes"
					:key="String(entry.node.key)"
					class="download-card"
					:class="{ 'download-card--child': entry.depth > 0 }"
					:style="{ '--download-depth': entry.depth }">
					<div
						class="download-card__top"
						:class="{ 'download-card__top--expandable': entry.hasChildren }">
						<q-btn
							v-if="entry.hasChildren"
							class="download-card__expand"
							flat
							round
							dense
							:icon="isMobileNodeExpanded(entry.node) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
							:aria-label="entry.node.data.title"
							:aria-expanded="isMobileNodeExpanded(entry.node)"
							:data-cy="`download-node-toggle-${entry.node.data.id}`"
							@click.stop="toggleMobileNode(entry.node)" />
						<q-checkbox
							:model-value="getMobileSelectionValue(entry.node)"
							:aria-label="`${t('general.commands.selection')} ${entry.node.data.title}`"
							@update:model-value="toggleMobileSelection(entry.node, $event === true)" />
						<QMediaTypeIcon
							v-if="entry.node.data.mediaType"
							:media-type="entry.node.data.mediaType"
							:size="26" />
						<div class="download-card__identity">
							<QText
								:cy="`column-title-${entry.node.data.id}`"
								class="download-card__title"
								:value="entry.node.data.title" />
							<QText
								:cy="`column-status-${entry.node.data.id}`"
								class="download-card__status"
								:value="translateDownloadStatus(entry.node.data.status)" />
						</div>
					</div>
					<QProgressBar
						:cy="`column-percentage-${entry.node.data.id}`"
						:value="entry.node.data.percentage" />
					<dl class="download-card__metrics">
						<div class="download-card__metric">
							<dt>{{ t('components.downloads-table.columns.data-received') }}</dt>
							<dd>
								<QFileSize
									:cy="`column-dataReceived-${entry.node.data.id}`"
									:size="entry.node.data.dataReceived" />
							</dd>
						</div>
						<div class="download-card__metric">
							<dt>{{ t('components.downloads-table.columns.data-total') }}</dt>
							<dd>
								<QFileSize
									:cy="`column-dataTotal-${entry.node.data.id}`"
									:size="entry.node.data.dataTotal" />
							</dd>
						</div>
						<div class="download-card__metric">
							<dt>{{ t('components.downloads-table.columns.speed') }}</dt>
							<dd>
								<QFileSize
									:cy="`column-downloadSpeed-${entry.node.data.id}`"
									:size="entry.node.data.downloadSpeed"
									speed />
							</dd>
						</div>
						<div class="download-card__metric">
							<dt>{{ t('components.downloads-table.columns.time-remaining') }}</dt>
							<dd>
								<QDuration
									:cy="`column-timeRemaining-${entry.node.data.id}`"
									:value="entry.node.data.timeRemaining"
									short />
							</dd>
						</div>
					</dl>
					<div class="download-card__actions">
						<IconSquareButton
							v-for="action in entry.node.data.actions"
							:key="`${entry.node.data.id}-${kebabCase(action.type)}`"
							:cy="`column-actions-${kebabCase(action.type)}-${entry.node.data.id}`"
							:disabled="action.disabled"
							:icon="toButtonIcon(action.type)"
							:aria-label="`${String(action.type)}: ${entry.node.data.title}`"
							:loading="action.loading"
							@click.stop="onTableAction({ action: action.type, data: entry.node.data })" />
					</div>
				</article>
			</div>
		</template>
	</q-expansion-item>

	<!-- Clear Completed Confirmation Dialog  -->
	<ConfirmationDialog
		:id="plexServer.id"
		:confirm-loading="clearCompletedLoading"
		:name="DialogType.ClearCompletedDownloadsConfirmationDialog"
		:title="t('components.downloads-table.clear-completed.confirmation.title')"
		:text="t('components.downloads-table.clear-completed.confirmation.text')"
		@confirm="clearCompletedByServer" />
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import type { TreeNode } from 'primevue/treenode';
import type { DownloadProgressDTO, PlexServerDTO } from '@dto';
import { DownloadActions, DownloadStatus } from '@dto';
import { ButtonType, DialogType } from '@enums';
import type { IDownloadTableNode, ISelection } from '@interfaces';
import type { QTreeTableColumn } from '@props';
import { QTreeTableColumnType } from '@props';
import { flatMapDeep, kebabCase } from 'lodash-es';
import { useDownloadStore, useServerConnectionStore, useDialogStore, useServerStore, useAccountStore } from '@store';
import { toDownloadActions, translateDownloadStatus } from '@composables';
import Convert from '@class/Convert';
import { useI18n } from '#imports';

const serverStore = useServerStore();
const downloadStore = useDownloadStore();
const dialogStore = useDialogStore();
const serverConnectionStore = useServerConnectionStore();
const accountStore = useAccountStore();

const { t } = useI18n();

interface DownloadTreeNode extends TreeNode {
	data: IDownloadTableNode;
	children?: DownloadTreeNode[];
}

interface MobileDownloadNode {
	node: DownloadTreeNode;
	depth: number;
	hasChildren: boolean;
}

const loadingIds = ref<{
	id: string;
	action: DownloadActions;
}[]>([]);
const isExpanded = ref(true);
const clearCompletedLoading = ref(false);
const collapsedMobileNodeKeys = shallowRef<ReadonlySet<string>>(new Set());

const props = defineProps<{
	loading?: boolean;
	plexServer: PlexServerDTO;
	downloadRows: DownloadProgressDTO[];
}>();

defineEmits<{
	(e: 'selected', payload: ISelection): void;
}>();

const nodes = computed((): DownloadTreeNode[] => {
	// TODO: Move property mapping to back-end to increase performance
	return mapToTreeNodes(downloadStore.getDownloadsByServerId(props.plexServer.id));
});
const mobileNodes = computed(() => flattenNodes(get(nodes)));

function flattenNodes(value: DownloadTreeNode[], depth = 0): MobileDownloadNode[] {
	return value.flatMap((node) => {
		const hasChildren = (node.children?.length ?? 0) > 0;
		const entry = { node, depth, hasChildren };

		if (!hasChildren || !isMobileNodeExpanded(node)) {
			return [entry];
		}

		return [entry, ...flattenNodes(node.children ?? [], depth + 1)];
	});
}

function isMobileNodeExpanded(node: DownloadTreeNode): boolean {
	return !get(collapsedMobileNodeKeys).has(String(node.key));
}

function toggleMobileNode(node: DownloadTreeNode): void {
	const key = String(node.key);
	const collapsedKeys = new Set(get(collapsedMobileNodeKeys));

	if (collapsedKeys.has(key)) {
		collapsedKeys.delete(key);
	} else {
		collapsedKeys.add(key);
	}

	set(collapsedMobileNodeKeys, collapsedKeys);
}

const hasCompletedDownloads = computed((): boolean => {
	return containsCompletedTasks(props.downloadRows);
});

function mapToTreeNodes(value: DownloadProgressDTO[]): DownloadTreeNode[] {
	return value?.map((node) => {
		const children = mapToTreeNodes(node.children);
		const data = {
			...node,
			key: node.id,
			label: node.title,
			children: children.map((child) => child.data!),
			actions: toDownloadActions(node.status).map((action) => ({
				type: action,
				// show loading icon on action and disable the rest
				loading: get(loadingIds).some((y) => node.id === y.id && action === y.action),
				disabled: get(loadingIds).some((y) => node.id === y.id && action !== y.action),
			})),
		};

		return {
			key: node.id,
			label: node.title,
			data,
			children,
		};
	}) ?? [];
}

const getDownloadTableColumns: QTreeTableColumn[] = [
	{
		header: t('components.downloads-table.columns.title'),
		field: 'title',
	},
	{
		header: t('components.downloads-table.columns.status'),
		field: 'status',
		align: 'right',
		width: 150,
	},
	{
		header: t('components.downloads-table.columns.data-received'),
		field: 'dataReceived',
		type: QTreeTableColumnType.FileSize,
		align: 'right',
		width: 120,
	},
	{
		header: t('components.downloads-table.columns.data-total'),
		field: 'dataTotal',
		type: QTreeTableColumnType.FileSize,
		width: 120,
		align: 'right',
	},
	{
		header: t('components.downloads-table.columns.speed'),
		field: 'downloadSpeed',
		type: QTreeTableColumnType.FileSpeed,
		align: 'right',
		width: 120,
	},
	{
		header: t('components.downloads-table.columns.time-remaining'),
		field: 'timeRemaining',
		type: QTreeTableColumnType.Duration,
		align: 'left',
		width: 120,
	},
	{
		header: t('components.downloads-table.columns.percentage'),
		field: 'percentage',
		type: QTreeTableColumnType.Percentage,
		align: 'right',
		width: 120,
	},
	{
		header: t('components.downloads-table.columns.actions'),
		field: 'actions',
		type: QTreeTableColumnType.Actions,
		width: 150,
		align: 'right',
		sortable: false,
	},
];

function getMobileSelectionValue(node: DownloadTreeNode): boolean | null {
	const selection = downloadStore.getSelectedDownloadTasks(props.plexServer.id)[String(node.key)];
	return selection?.checked ? true : selection?.partialChecked ? null : false;
}

function toggleMobileSelection(node: DownloadTreeNode, selected: boolean): void {
	const current = downloadStore.getSelectedDownloadTasks(props.plexServer.id);
	const checkedKeys = getCheckedKeys(current, get(nodes));

	for (const key of getNodeAndDescendantKeys(node)) {
		if (selected) checkedKeys.add(key);
		else checkedKeys.delete(key);
	}

	if (!selected) {
		for (const key of getAncestorKeys(get(nodes), String(node.key)))
			checkedKeys.delete(key);
	}

	downloadStore.updateSelectedDownloadTasks(props.plexServer.id, normalizeSelectionKeys(get(nodes), checkedKeys));
}

function getNodeAndDescendantKeys(node: DownloadTreeNode): string[] {
	return [String(node.key), ...(node.children ?? []).flatMap(getNodeAndDescendantKeys)];
}

function getAncestorKeys(tree: DownloadTreeNode[], targetKey: string, ancestors: string[] = []): string[] {
	for (const node of tree) {
		if (String(node.key) === targetKey)
			return ancestors;

		const found = getAncestorKeys(node.children ?? [], targetKey, [...ancestors, String(node.key)]);
		if (found.length > 0)
			return found;
	}

	return [];
}

function getCheckedKeys(selection: Record<string, {
	checked: boolean;
	partialChecked: boolean;
}>, tree: DownloadTreeNode[]): Set<string> {
	const checkedKeys = new Set<string>();
	for (const node of tree) {
		if (selection[String(node.key)]?.checked) {
			getNodeAndDescendantKeys(node).forEach((key) => checkedKeys.add(key));
		} else {
			getCheckedKeys(selection, node.children ?? []).forEach((key) => checkedKeys.add(key));
		}
	}
	return checkedKeys;
}

function normalizeSelectionKeys(tree: DownloadTreeNode[], checkedKeys: Set<string>): Record<string, {
	checked: boolean;
	partialChecked: boolean;
}> {
	const selection: Record<string, { checked: boolean; partialChecked: boolean }> = {};
	const visit = (node: DownloadTreeNode): { checked: boolean; partialChecked: boolean } => {
		const childStates = (node.children ?? []).map(visit);
		const nodeChecked = checkedKeys.has(String(node.key));
		const hasChildren = childStates.length > 0;
		const childrenChecked = hasChildren && childStates.every((state) => state.checked);
		const hasSelectedChild = childStates.some((state) => state.checked || state.partialChecked);
		const checked = hasChildren ? childrenChecked : nodeChecked;
		const state = {
			checked,
			partialChecked: !checked && hasSelectedChild,
		};
		if (state.checked || state.partialChecked) selection[String(node.key)] = state;
		return state;
	};
	tree.forEach(visit);
	return selection;
}

function onTableAction({ action, data }: { action: DownloadActions; data: IDownloadTableNode }) {
	const ids: string[] = [data.id];

	if (action === DownloadActions.Details) {
		dialogStore.openDownloadTaskDetailsDialog(data.id);
		return;
	}

	const newIds = getAllIds([data]);
	get(loadingIds).push(...newIds.map((id) => ({ id, action })));

	useSubscription(downloadStore.executeDownloadCommand(action, ids, props.plexServer.id).subscribe({
		next: () => {
			set(loadingIds, get(loadingIds).filter((x) => !newIds.includes(x.id)));
		},
		error: () => {
			set(loadingIds, get(loadingIds).filter((x) => !newIds.includes(x.id)));
		},
	}));
}

function toButtonIcon(action: DownloadActions): string {
	switch (action) {
		case DownloadActions.Details:
			return Convert.buttonTypeToIcon(ButtonType.Details);

		case DownloadActions.Delete:
			return Convert.buttonTypeToIcon(ButtonType.Delete);

		case DownloadActions.Start:
			return Convert.buttonTypeToIcon(ButtonType.Start);

		case DownloadActions.Pause:
			return Convert.buttonTypeToIcon(ButtonType.Pause);

		case DownloadActions.Stop:
			return Convert.buttonTypeToIcon(ButtonType.Stop);

		case DownloadActions.Clear:
			return Convert.buttonTypeToIcon(ButtonType.Clear);

		case DownloadActions.Restart:
			return Convert.buttonTypeToIcon(ButtonType.Restart);

		default:
			return Convert.buttonTypeToIcon(ButtonType.None);
	}
}

function toggleExpanded() {
	set(isExpanded, !get(isExpanded));
}

function openClearCompletedDialog() {
	if (!get(hasCompletedDownloads)) {
		return;
	}

	dialogStore.openDialog(DialogType.ClearCompletedDownloadsConfirmationDialog, props.plexServer.id);
}

function closeClearCompletedDialog() {
	dialogStore.closeDialog(DialogType.ClearCompletedDownloadsConfirmationDialog, props.plexServer.id);
}

function clearCompletedByServer() {
	if (get(clearCompletedLoading)) {
		return;
	}

	set(clearCompletedLoading, true);
	useSubscription(
		downloadStore.executeDownloadCommand(DownloadActions.Clear, [], props.plexServer.id).subscribe({
			next: (result) => {
				if (result.isSuccess) {
					closeClearCompletedDialog();
				}
			},
			error: () => {
				set(clearCompletedLoading, false);
			},
			complete: () => {
				set(clearCompletedLoading, false);
			},
		}),
	);
}

function containsCompletedTasks(downloadRows: DownloadProgressDTO[]): boolean {
	for (const downloadRow of downloadRows) {
		if (downloadRow.status === DownloadStatus.Completed || containsCompletedTasks(downloadRow.children ?? [])) {
			return true;
		}
	}

	return false;
}

function getAllIds(nodes: IDownloadTableNode[]): string[] {
	return flatMapDeep(nodes, (node) => [
		node.id, // Assuming `key` holds the ID in TreeNode
		...getAllIds(node.children || []),
	]);
}
</script>

<style lang="scss">
.download-server-header {
  position: relative;
}

.download-server-header__title {
  position: absolute;
  top: 50%;
  left: 50%;
  display: flex;
  align-items: center;
  padding-inline: 16px;
  transform: translate(-50%, -50%);
  flex-wrap: nowrap;
}

.download-cards {
  display: none;
}

.inaccessible-item-text {
  text-decoration: line-through;
  opacity: 0.62;
}

@media (max-width: $breakpoint-sm-max) {
  .download-server-header {
    min-width: 0;
    gap: 0.5rem;
    flex-wrap: wrap;
  }

  .download-server-header__title {
    position: static;
    min-width: 0;
    padding-inline: 0;
    transform: none;
    flex-wrap: wrap;

    .title {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  .download-server-header__actions .q-btn {
    min-width: 44px;
    min-height: 44px;
  }
}

@media (max-width: $breakpoint-sm-max) {
  .downloads-table {
    margin: 0.5rem !important;
  }

  .download-table-desktop {
    display: none;
  }

  .download-cards {
    display: grid;
    gap: 0.375rem;
    padding: 0.375rem;
  }

  .download-card {
    display: grid;
    min-width: 0;
    gap: 0.5rem;
    padding: 0.625rem;
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 8px;
    background: rgba(0, 0, 0, 0.2);
  }

  .download-card--child {
    margin-left: 0;
    padding-inline-start: calc(0.625rem + min(var(--download-depth) * 0.5rem, 1.5rem));
    border-inline-start: 3px solid rgba(255, 255, 255, 0.32);
    border-start-start-radius: 3px;
    border-end-start-radius: 3px;
    background: rgba(255, 255, 255, 0.04);
  }

  .download-card__top {
    display: grid;
    min-width: 0;
    grid-template-columns: auto auto minmax(0, 1fr);
    align-items: start;
    gap: 0.5rem;
  }

  .download-card__top--expandable {
    grid-template-columns: auto auto auto minmax(0, 1fr);
  }

  .download-card__expand {
    width: 44px;
    height: 44px;
  }

  .download-card__identity {
    min-width: 0;
  }

  .download-card__title {
    display: -webkit-box;
    overflow: hidden;
    line-height: 1.25;
    overflow-wrap: anywhere;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
  }

  .download-card__status {
    margin-top: 0.125rem;
    font-size: 0.75rem;
    opacity: 0.72;
  }

  .download-card__metrics {
    display: grid;
    min-width: 0;
    margin: 0;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.375rem 0.75rem;
  }

  .download-card__metric {
    min-width: 0;

    dt {
      font-size: 0.7rem;
      opacity: 0.72;
    }

    dd {
      min-width: 0;
      margin: 0;
      overflow-wrap: anywhere;
    }
  }

  .download-card__actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 0.25rem;

    .q-btn {
      min-width: 44px;
      min-height: 44px;
    }
  }
}

@media (min-width: 600px) and (max-width: $breakpoint-sm-max) {
  .download-card {
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
  }

  .download-card__top,
  .download-card__metrics,
  .download-card > .q-linear-progress {
    grid-column: 1;
  }

  .download-card__actions {
    grid-column: 2;
    grid-row: 1 / span 3;
    flex-wrap: nowrap;
  }
}
</style>
