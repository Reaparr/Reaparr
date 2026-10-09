<template>
	<NuxtPage v-if="route.params.artistId" />
	<QPage v-else>
		<MediaOverview
			v-if="validLibraryId"
			:key="libraryId"
			:library-id="libraryId" />
		<p
			v-else
			role="alert"
			class="q-pa-md">
			{{ t('components.music-details.invalid-route') }}
		</p>
	</QPage>
</template>

<script setup lang="ts">
definePageMeta({
	scrollToTop: false,
});

const route = useRoute();
const { t } = useI18n();
const libraryId = computed(() => +(route.params.id as string));
const validLibraryId = computed(() => Number.isSafeInteger(libraryId.value) && libraryId.value > 0);
</script>
