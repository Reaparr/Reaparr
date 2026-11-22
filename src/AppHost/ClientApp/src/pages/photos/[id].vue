<template>
	<NuxtPage v-if="route.params.albumId" />
	<QPage v-else>
		<PhotoBrowser
			v-if="validLibraryId"
			:key="libraryId"
			:library-id="libraryId" />
		<p
			v-else
			role="alert"
			class="q-pa-md"
			data-cy="photo-invalid-route">
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
const validLibraryId = computed(() => typeof route.params.id === 'string'
	&& /^[1-9]\d*$/.test(route.params.id)
	&& Number.isSafeInteger(get(libraryId)));
</script>
