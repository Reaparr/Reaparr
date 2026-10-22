<template>
	<QCardDialog
		:name="DialogType.DirectoryBrowserDialog"
		full-height
		:loading="isLoading"
		button-align="between"
		:type="{} as FolderPathDTO"
		@opened="open"
		@closed="onClosed">
		<template #title>
			{{ dialogTitle }}
		</template>
		<template #top-row>
			<QAlert
				v-if="errorMessage"
				cy="directory-browser-save-error"
				role="alert"
				type="error">
				{{ errorMessage }}
			</QAlert>
			<QRow>
				<QCol
					v-if="!isCurrentWritable"
					cols="auto"
					class="q-px-md">
					<q-icon
						color="red"
						size="sm"
						name="mdi-alert-circle">
						<q-tooltip
							anchor="top middle"
							self="center middle">
							<span>{{ t('components.directory-browser.has-no-write-permission') }}</span>
						</q-tooltip>
					</q-icon>
				</QCol>
				<QCol>
					<PTextField
						:model-value="currentPath"
						:readonly="saving"
						outlined
						color="red"
						:placeholder="t('components.directory-browser.no-path')"
						@update:model-value="updateCurrentPath" />
				</QCol>
			</QRow>
			<q-markup-table class="directory-browser-table q-pr-md">
				<thead>
					<tr>
						<th
							class="text-left"
							style="width: 100px">
							{{ t('components.directory-browser.type') }}
						</th>
						<th class="text-left">
							{{ t('components.directory-browser.path') }}
						</th>
					</tr>
				</thead>
				<!-- The return row -->
				<tbody v-if="currentPathModel != null">
					<tr
						data-cy="directory-browser-row-return"
						@click="directoryNavigate(returnRow)">
						<td
							class="text-left"
							style="width: 100px">
							<q-icon
								size="md"
								:name="getIcon(returnRow.type)" />
						</td>
						<td class="directory-browser-name-cell text-left">
							{{ returnRow.name }}
						</td>
					</tr>
				</tbody>
			</q-markup-table>
		</template>
		<template #default>
			<!--	Directory Browser	-->
			<q-markup-table
				class="directory-browser-table"
				data-cy="directory-browser-rows">
				<tbody class="scroll">
					<tr
						v-for="(row, index) in items"
						:key="index"
						:data-cy="`directory-browser-row-${index}`"
						:class="rowClass(!row.hasReadPermission)"
						@click="directoryNavigate(row)">
						<td
							class="text-left"
							style="width: 100px">
							<q-icon
								size="md"
								:name="getIcon(row.type)" />
						</td>
						<td class="directory-browser-name-cell text-left">
							<q-icon
								v-if="!row.hasReadPermission"
								color="red"
								size="sm"
								name="mdi-alert-circle">
								<q-tooltip
									anchor="top middle"
									self="center middle">
									<span>{{ t('components.directory-browser.has-no-read-permission') }}</span>
								</q-tooltip>
							</q-icon>

							{{ row.name }}
						</td>
					</tr>
				</tbody>
			</q-markup-table>
		</template>
		<template #actions>
			<CancelButton @click="cancel" />
			<ConfirmButton
				cy="directory-browser-confirm-button"
				:disabled="saving || !isCurrentWritable"
				:loading="saving"
				:tooltip-text="!saving && !isCurrentWritable ? $t('components.directory-browser.current-folder-has-no-write-permission') : ''"
				@click="confirm()" />
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useSubscription } from '@vueuse/rxjs';
import { get, set } from '@vueuse/core';
import type { FileSystemModelDTO, FolderPathDTO } from '@dto';
import { FileSystemEntityType, FolderType } from '@dto';
import { folderPathApi } from '@api';
import { DialogType } from '@enums';
import { useDialogStore } from '@store';
import { cloneDeep } from 'lodash-es';
import { finalize, Subscription } from 'rxjs';

const { t } = useI18n();
const props = defineProps<{
	saving: boolean;
	errorMessage: string;
}>();
const dialogStore = useDialogStore();
const path = ref<FolderPathDTO | null>(null);
const currentPath = ref('');
const currentPathModel = ref<FileSystemModelDTO | null>(null);
const parentPath = ref('');
const isLoading = ref(true);
const items = ref<FileSystemModelDTO[]>([]);
const directoryRequests = new Subscription();
let currentRequest: Subscription | undefined;
useSubscription(directoryRequests);
const returnRow = ref<FileSystemModelDTO>({
	name: '...',
	path: '..',
	type: FileSystemEntityType.Parent,
	extension: '',
	size: 0,
	lastModified: '',
	hasReadPermission: true,
	hasWritePermission: false,
});

const emit = defineEmits<{
	(e: 'confirm' | 'opened', path: FolderPathDTO): void;
	(e: 'cancel' | 'closed'): void;
}>();

const isCurrentWritable = computed(() => {
	return get(currentPathModel)?.hasWritePermission ?? false;
});

const dialogTitle = computed(() => {
	const folderPath = get(path);
	if (folderPath?.id !== 0) {
		return t('components.directory-browser.select-path', { pathName: folderPath?.displayName ?? '' });
	}

	switch (folderPath.folderType) {
		case FolderType.DownloadFolder:
			return t('components.directory-browser.add-download-path');
		case FolderType.MovieFolder:
			return t('components.directory-browser.add-movie-path');
		case FolderType.TvShowFolder:
			return t('components.directory-browser.add-tv-show-path');
		default:
			return t('components.directory-browser.select-path', { pathName: folderPath.displayName });
	}
});

function rowClass(hasReadPermission: boolean) {
	return {
		'cursor-pointer': !hasReadPermission,
		'q-tr--no-hover': hasReadPermission,
	};
}

const getIcon = (type: FileSystemEntityType): string => {
	switch (type) {
		case FileSystemEntityType.Parent:
			return 'mdi-arrow-left';
		case FileSystemEntityType.Drive:
			return 'mdi-harddisk';
		case FileSystemEntityType.Folder:
			return 'mdi-folder';
		case FileSystemEntityType.File:
			return 'mdi-file';
		default:
			return 'mdi-crosshairs-question';
	}
};

function open(data: FolderPathDTO): void {
	let selectedPath = data as FolderPathDTO;
	if (!selectedPath) {
		Log.error('parameter was null when opening DirectoryBrowser');
		return;
	}
	selectedPath = cloneDeep(selectedPath);
	currentRequest?.unsubscribe();
	set(isLoading, true);
	set(path, selectedPath);
	set(currentPath, selectedPath.directory);
	emit('opened', selectedPath);
	requestDirectories(selectedPath.directory);
}

function invalidateLookup(): void {
	currentRequest?.unsubscribe();
	currentRequest = undefined;
	set(currentPathModel, null);
	set(items, []);
	set(parentPath, '');
}

function onClosed(): void {
	invalidateLookup();
	set(isLoading, false);
	emit('closed');
}

function cancel(): void {
	invalidateLookup();
	emit('cancel');
	dialogStore.closeDialog(DialogType.DirectoryBrowserDialog);
}

function confirm(): void {
	if (props.saving || !get(isCurrentWritable)) {
		return;
	}

	const selectedPath = get(path);
	if (!selectedPath) {
		Log.error('path was null when confirming DirectoryBrowser');
		return;
	}

	emit('confirm', cloneDeep(selectedPath));
}

function updateCurrentPath(newPath: string | undefined): void {
	if (props.saving) {
		return;
	}

	const pathValue = newPath ?? '';
	set(currentPath, pathValue);
	requestDirectories(pathValue);
}

function requestDirectories(newPath?: string): void {
	invalidateLookup();

	if (!newPath) {
		Log.warn(`path was invalid "${newPath}" setting to default empty root file-system view`);
		newPath = '';
	}

	const currentPathValue = get(path);
	if (currentPathValue) {
		set(path, {
			...currentPathValue,
			directory: newPath,
		});
	}

	currentRequest = folderPathApi
		.getFolderPathDirectoryEndpoint({ path: newPath })
		.pipe(finalize(() => set(isLoading, false)))
		.subscribe({
			next: ({ isSuccess, value }) => {
				if (isSuccess && value) {
					set(items, value.directories);
					set(currentPathModel, value.current ?? null);
					set(parentPath, value.parent);
				}
			},
			error: (error) => Log.error('Directory lookup failed', error),
		});
	directoryRequests.add(currentRequest);
}

function directoryNavigate(dataRow: FileSystemModelDTO): void {
	if (props.saving || !dataRow.hasReadPermission) {
		return;
	}

	if (dataRow.path === '..') {
		if (!get(currentPathModel)) return;
		const parentPathValue = get(parentPath);
		requestDirectories(parentPathValue);
		set(currentPath, parentPathValue);
	} else {
		requestDirectories(dataRow.path);
		set(currentPath, dataRow.path);
	}
}
</script>

<style lang="scss">
@media (max-width: $breakpoint-sm-max) {
  .directory-browser-name-cell {
    min-width: 0;
    overflow-wrap: anywhere;
    word-break: break-word;
    white-space: normal;
  }
}
</style>
