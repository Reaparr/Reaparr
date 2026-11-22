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

const libraryId = +(route.params.libraryId as string);

const route = useRoute();
const libraryStore = useLibraryStore();
</script>
