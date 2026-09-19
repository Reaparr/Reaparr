<template>
	<QAlert
		type="info"
		class="q-mb-md">
		{{ $t('components.server-dialog.tabs.server-libraries.sync-alert') }}
	</QAlert>
	<HelpGroup v-if="plexServer">
		<HelpRow
			v-for="library in libraries"
			:key="library.id"
			class="server-library-row"
			col-label="3"
			:label="library.title">
			<template #append>
				<QMediaTypeIcon
					:media-type="library.type"
					class="q-mx-sm" />
			</template>
			<QRow
				align="center"
				gutter="sm">
				<QCol cols="auto">
					<QToggle
						size="lg"
						:model-value="library.isEnabled"
						:disable="isLibraryUpdating(library.id)"
						:data-cy="`library-toggle-${library.id}`"
						@update:model-value="(value: boolean) => onLibraryEnabledChanged(library.id, value)" />
				</QCol>
				<QCol>
					<q-select
						:model-value="getDefaultDestination(library.id)"
						option-label="displayName"
						option-value="id"
						:options="folderPathStore.getFolderPathOptions(library.type)"
						:disable="!library.isEnabled || isLibraryUpdating(library.id)"
						@update:model-value="libraryStore.updateDefaultDestination(library.id, $event.id)" />
				</QCol>
			</QRow>
		</HelpRow>
	</HelpGroup>

	<QAlert
		v-else
		type="error">
		{{ $t('components.server-dialog.tabs.server-config.plex-server-was-null') }}
	</QAlert>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import type { PlexLibraryDTO, PlexServerDTO } from '@dto';
import { useFolderPathStore, useLibraryStore } from '@store';

const props = defineProps<{
	plexServer: PlexServerDTO | null;
	plexLibraries: PlexLibraryDTO[];
}>();

const folderPathStore = useFolderPathStore();
const libraryStore = useLibraryStore();
const updatingLibraryIds = ref<number[]>([]);

const libraries = computed(() => get(props.plexLibraries));

function getDefaultDestination(libraryId: number): { id: number; displayName: string } {
	const library = props.plexLibraries.find((x) => x.id === libraryId);
	if (!library) {
		return {
			id: 0,
			displayName: 'Library not found',
		};
	}

	return {
		id: library.defaultDestinationId,
		displayName: folderPathStore.getFolderPath(library.defaultDestinationId)?.displayName ?? 'Not set',
	};
}

function isLibraryUpdating(libraryId: number): boolean {
	return get(updatingLibraryIds).includes(libraryId);
}

function onLibraryEnabledChanged(libraryId: number, isEnabled: boolean): void {
	set(updatingLibraryIds, [...get(updatingLibraryIds), libraryId]);
	useSubscription(libraryStore.setLibraryEnabled(libraryId, isEnabled).subscribe({
		complete: () => removeUpdatingLibraryId(libraryId),
		error: () => removeUpdatingLibraryId(libraryId),
	}));
}

function removeUpdatingLibraryId(libraryId: number): void {
	set(updatingLibraryIds, get(updatingLibraryIds).filter((id) => id !== libraryId));
}
</script>

<style lang="scss">
@media (min-width: $breakpoint-lg-min) {
  .server-library-row {
    > .help-row-label {
      flex: 0 0 25%;
      max-width: 25%;
    }

    > .help-row-default-slot {
      flex: 0 0 50%;
      max-width: 50%;
    }
  }
}
</style>
