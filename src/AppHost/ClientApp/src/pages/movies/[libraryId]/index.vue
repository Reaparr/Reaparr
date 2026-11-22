<template>
	<QPage>
		<MediaOverview :library-id="libraryId" />
	</QPage>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router';
import { definePageMeta } from '#imports';

definePageMeta({
	scrollToTop: false,
});

const libraryId = +(route.params.libraryId as string);

const route = useRoute();
</script>
