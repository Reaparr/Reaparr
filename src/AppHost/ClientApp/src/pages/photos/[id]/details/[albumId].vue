<template>
	<QPage>
		<PhotoBrowser
			v-if="validRoute"
			:key="`${libraryId}:${albumId}`"
			:library-id="libraryId"
			:album-id="albumId" />
		<div
			v-else
			role="alert"
			class="q-pa-md"
			data-cy="photo-album-invalid-route">
			{{ t('components.photo-details.invalid-route') }}
		</div>
	</QPage>
</template>

<script setup lang="ts">
definePageMeta({ scrollToTop: false });

const route = useRoute();
const { t } = useI18n();

const libraryId = computed(() => Number(route.params.id));
const albumId = computed(() => Number(route.params.albumId));
const validRoute = computed(() => [route.params.id, route.params.albumId].every((id) =>
	typeof id === 'string' && /^[1-9]\d*$/.test(id) && Number.isSafeInteger(Number(id))));
</script>
