<template>
	<!--	Download Destinations	-->
	<q-markup-table
		separator="horizontal"
		flat>
		<template v-if="plexLibraries.length">
			<HelpRow
				v-for="library in plexLibraries"
				:key="library.id"
				:col-label="5"
				:col-content="5"
				disable-responsive
				:label="library.title">
				<template #append>
					<QMediaTypeIcon
						:media-type="library.type"
						class="q-mx-sm" />
				</template>
				<q-select
					:model-value="getDefaultDestination(library.id)"
					option-label="displayName"
					option-value="id"
					:options="folderPathStore.getFolderPathOptions(library.type)"
					@update:model-value="libraryStore.updateDefaultDestination(library.id, $event.id)" />
			</HelpRow>
		</template>
		<template v-else>
			<q-tr>
				<q-td>
					<QAlert type="warning">
						{{ $t('components.server-library-destinations-tab-content.no-libraries') }}
					</QAlert>
				</q-td>
			</q-tr>
		</template>
	</q-markup-table>
</template>

<script setup lang="ts">
import type { PlexLibraryDTO, PlexServerDTO } from '@dto';
import { useFolderPathStore } from '@store';

const props = defineProps<{
	plexServer: PlexServerDTO | null;
	plexLibraries: PlexLibraryDTO[];
}>();

const folderPathStore = useFolderPathStore();
const libraryStore = useLibraryStore();

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
</script>
