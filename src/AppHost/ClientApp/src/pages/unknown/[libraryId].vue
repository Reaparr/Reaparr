<template>
	<QPage>
		<QRow>
			<QCol class="q-ma-md">
				<QAlert
					type="error">
					<QText
						align="center"
						size="h4">
						{{ $t('pages.unknown.index.header', {
							libraryName: libraryStore.getLibraryName(libraryId),
						}) }}
					</QText>
				</QAlert>
			</QCol>
		</QRow>
	</QPage>
</template>

<script lang="ts" setup>
import { useRoute } from 'vue-router';
import { useLibraryStore } from '@store';

const route = useRoute();
const libraryId = +(route.params.libraryId as string);

const libraryStore = useLibraryStore();
</script>
