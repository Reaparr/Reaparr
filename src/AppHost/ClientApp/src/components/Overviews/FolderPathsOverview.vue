<template>
	<QSection :header="onlyDefaults ? '' : $t('components.folder-paths-overview.main.header')">
		<q-tabs
			v-if="!onlyDefaults"
			v-model="activeFolderType"
			active-color="primary"
			align="justify"
			indicator-color="primary"
			mobile-arrows
			outside-arrows
			class="folder-path-tabs">
			<q-tab
				v-for="tab in folderPathTabs"
				:key="tab.folderType"
				:name="tab.folderType"
				:no-caps="true"
				:aria-label="tab.header"
				:data-cy="`folder-path-tab-${kebabCase(tab.folderType)}`">
				<q-icon
					v-if="tab.folderType === FolderType.DownloadFolder"
					name="mdi-download"
					class="q-mr-sm" />
				<QMediaTypeIcon
					v-else
					:media-type="tab.mediaType"
					class="q-mr-sm" />
				<span class="folder-path-tab-label">{{ tab.header }}</span>
			</q-tab>
		</q-tabs>

		<div :class="{ 'q-mt-md': !onlyDefaults }">
			<HelpRow
				v-for="folderPath in visibleFolderPaths"
				:key="folderPath.id"
				class="folder-path-row"
				:col-label="3"
				:col-content="8"
				:edit-model="folderPath.displayName"
				:allow-label-edit="!onlyDefaults && activeFolderPathGroup?.isFolderNameEditable && !folderPath.isDefault"
				:title="toTranslation(folderPath.folderType).title"
				:label="toTranslation(folderPath.folderType).label"
				:text="toTranslation(folderPath.folderType).text"
				@update:edit-model="saveDisplayName(folderPath.id, $event!)">
				<QRow
					no-wrap
					align="center"
					:cy="`${getFolderPathCyPrefix(folderPath)}-row`">
					<QCol
						cols="grow"
						class="folder-path-directory">
						<q-input
							:model-value="folderPath.directory"
							class="folder-path-input"
							:data-cy="`${getFolderPathCyPrefix(folderPath)}-input`"
							readonly>
							<IconSquareButton
								class="folder-path-browse-button"
								:cy="`${getFolderPathCyPrefix(folderPath)}-edit-button`"
								icon="mdi-folder-open-outline"
								:aria-label="`Edit folder path: ${folderPath.displayName}`"
								@click="dialogStore.openDirectoryBrowserDialog(folderPath)" />
						</q-input>
					</QCol>
					<QCol
						:width="56"
						class="folder-path-action">
						<ValidIcon
							:cy="`${getFolderPathCyPrefix(folderPath)}-valid-icon`"
							:invalid-text="$t('general.alerts.invalid-directory')"
							:valid="folderPath.isValid ? ValidationLevel.Valid : ValidationLevel.Invalid"
							:valid-text="$t('general.alerts.valid-directory')" />
					</QCol>
					<QCol
						v-if="!onlyDefaults && activeFolderPathGroup?.isFolderDeletable && !folderPath.isDefault"
						:width="56"
						class="folder-path-action">
						<DeleteIconButton
							:cy="`${getFolderPathCyPrefix(folderPath)}-delete-button`"
							@click="deleteFolderPath(folderPath)" />
					</QCol>
				</QRow>
			</HelpRow>
		</div>

		<QRow
			v-if="!onlyDefaults && activeFolderPathGroup?.isFolderAddable"
			class="q-my-sm"
			justify="center">
			<QCol cols="auto">
				<AddIconButton
					:aria-label="`Add ${activeFolderPathGroup.header}`"
					:cy="`${kebabCase(activeFolderPathGroup.folderType)}-add-button`"
					@click="addFolderPath(activeFolderPathGroup)" />
			</QCol>
		</QRow>
	</QSection>

	<ConfirmationDialog
		:name="DialogType.FolderPathDeleteConfirmationDialog"
		:title="$t('confirmation.delete-folder-path.title')"
		:text="$t('confirmation.delete-folder-path.text', { displayName: pendingDeleteFolderPath?.displayName ?? '' })"
		:confirm-label="$t('general.commands.delete')"
		@cancel="cancelDeleteFolderPath"
		@confirm="confirmDeleteFolderPath" />

	<!--	Directory Browser	-->
	<DirectoryBrowser @confirm="confirmDirectoryBrowser" />
</template>

<script lang="ts" setup>
import { get, set } from '@vueuse/core';
import { type FolderPathDTO, FolderType } from '@dto';
import type { IFolderPathGroup, IHelp } from '@interfaces';
import { DialogType, ValidationLevel } from '@enums';
import { kebabCase, orderBy } from 'lodash-es';
import { showErrorNotification, useDialogStore, useFolderPathStore, useI18n, useSubscription } from '#imports';

const { t } = useI18n();

const dialogStore = useDialogStore();
const folderPathStore = useFolderPathStore();
const activeFolderType = ref(FolderType.DownloadFolder);
const pendingDeleteFolderPath = ref<FolderPathDTO | null>(null);

const props = withDefaults(defineProps<{ onlyDefaults?: boolean }>(), {
	onlyDefaults: false,
});

const folderPathTabs = computed<IFolderPathGroup[]>(() => folderPathStore.getFolderPathsGroups(false));

const activeFolderPathGroup = computed<IFolderPathGroup | undefined>(() =>
	get(folderPathTabs).find((group) => group.folderType === get(activeFolderType)),
);

const visibleFolderPaths = computed(() => {
	if (props.onlyDefaults) {
		return folderPathStore.getDefaultFolderPaths;
	}

	return orderBy(
		folderPathStore.folderPaths.filter((folderPath) => folderPath.folderType === get(activeFolderType)),
		['isDefault'],
		['desc'],
	);
});

const confirmDirectoryBrowser = (path: FolderPathDTO): void => {
	if (path.id === 0) {
		// New folder path — create with the confirmed directory
		useSubscription(
			folderPathStore.createFolderPath(path).subscribe({
				error(err) {
					showErrorNotification(err);
				},
			}),
		);
	} else {
		// Existing folder path — update the directory
		useSubscription(
			folderPathStore.setFolderPathDirectory(path.id, path.directory).subscribe({
				error(err) {
					showErrorNotification(err);
				},
			}),
		);
	}
};

function addFolderPath(folderGroup: IFolderPathGroup): void {
	let displayName = '';
	switch (folderGroup.folderType) {
		case FolderType.DownloadFolder:
			displayName = t('components.folder-paths-overview.download.default-name');
			break;
		case FolderType.MovieFolder:
			displayName = t('components.folder-paths-overview.movie.default-name');
			break;
		case FolderType.TvShowFolder:
			displayName = t('components.folder-paths-overview.tv-show.default-name');
			break;
		default:
			throw new Error(`Unknown folder type: ${folderGroup.folderType}`);
	}

	dialogStore.openDirectoryBrowserDialog({
		id: 0,
		displayName,
		directory: '',
		folderType: folderGroup.folderType,
		mediaType: folderGroup.mediaType,
		isValid: false,
		isDefault: false,
	});
}

function deleteFolderPath(folderPath: FolderPathDTO): void {
	if (folderPath.isDefault) {
		return;
	}

	set(pendingDeleteFolderPath, folderPath);
	dialogStore.openDialog(DialogType.FolderPathDeleteConfirmationDialog);
}

function cancelDeleteFolderPath(): void {
	set(pendingDeleteFolderPath, null);
}

function confirmDeleteFolderPath(): void {
	const folderPath = get(pendingDeleteFolderPath);
	if (!folderPath) {
		return;
	}

	useSubscription(
		folderPathStore.deleteFolderPath(folderPath.id).subscribe({
			next: (result) => {
				if (result.isSuccess) {
					dialogStore.closeDialog(DialogType.FolderPathDeleteConfirmationDialog);
					set(pendingDeleteFolderPath, null);
				}
			},
			error(err) {
				showErrorNotification(err);
			},
		}),
	);
}

function getFolderPathCyPrefix(folderPath: FolderPathDTO): string {
	return `${folderPath.isDefault ? 'default' : 'custom'}-${kebabCase(folderPath.folderType)}`;
}

function toTranslation(type: FolderType): IHelp {
	switch (type) {
		case FolderType.DownloadFolder:
			return {
				label: t('help.settings.paths.download-folder.label'),
				text: t('help.settings.paths.download-folder.text'),
				title: t('help.settings.paths.download-folder.title'),
			};
		case FolderType.MovieFolder:
			return {
				label: t('help.settings.paths.movie-folder.label'),
				text: t('help.settings.paths.movie-folder.text'),
				title: t('help.settings.paths.movie-folder.title'),
			};
		case FolderType.TvShowFolder:
			return {
				label: t('help.settings.paths.tv-show-folder.label'),
				text: t('help.settings.paths.tv-show-folder.text'),
				title: t('help.settings.paths.tv-show-folder.title'),
			};
		default:
			throw new Error('FolderType not supported');
	}
}

function saveDisplayName(id: number, value: string) {
	useSubscription(
		folderPathStore.setFolderPathDisplayName(id, value).subscribe({
			error(err) {
				showErrorNotification(err);
			},
		}),
	);
}
</script>

<style lang="scss">
.folder-path-tabs {
  min-height: 72px;

  .q-tab {
    min-height: 72px;
  }
}

.folder-path-directory {
  min-width: 0;
}

.folder-path-action {
  display: flex;
  align-items: center;
  justify-content: center;
}

.folder-path-input {
  .q-field__control {
    // Ensures the folder button is outlined to the right border
    padding: 0 0 0 12px;
  }
}

.folder-path-browse-button {
  min-width: 44px;
  min-height: 44px;
}

@media (max-width: $breakpoint-xs-max) {
  .folder-path-tabs {
    min-height: 48px;

    .q-tab {
      min-width: 0;
      min-height: 48px;
      padding-inline: 0.75rem;
    }

    .folder-path-tab-label {
      display: none;
    }

    .q-icon {
      margin-right: 0;
    }
  }

  .folder-path-row {
    margin: 0.75rem 0.5rem;
    padding: 0.5rem;
    border: 1px solid currentcolor;
    border-radius: 8px;

    .help-row-label,
    .help-row-default-slot {
      flex: 0 0 100%;
      max-width: 100%;
    }

    .help-row-label {
      padding-inline: 0;
    }

    .editable-text-item {
      padding-inline: 0;
    }

    .editable-text-display,
    .editable-text-display .text-right {
      text-align: left !important;
    }

    .help-row-default-slot {
      padding: 0.25rem 0 0;
    }

    .folder-path-action {
      flex: 0 0 44px !important;
      max-width: 44px !important;
    }
  }
}
</style>
