<template>
	<NuxtPage v-if="route.params.albumId" />
	<QPage v-else>
		<MediaOverview
			v-if="validLibraryId"
			:key="libraryId"
			:library-id="libraryId" />
		<p
			v-else
			role="alert"
			class="q-pa-md">
			{{ t('components.photo-details.invalid-route') }}
		</p>
	</QPage>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';

definePageMeta({ scrollToTop: false });
const route = useRoute();
const { t } = useI18n();
const libraryId = computed(() => Number(route.params.id));
const validLibraryId = computed(() => Number.isSafeInteger(get(libraryId)) && get(libraryId) > 0);
</script>
