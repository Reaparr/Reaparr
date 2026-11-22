<template>
	<div
		class="photo-thumbnail"
		:data-cy="cy"
		:style="{ width: `${width}px`, height: `${height}px` }">
		<q-skeleton
			v-if="loading"
			square
			animation="fade"
			class="fit" />
		<q-img
			v-else-if="imageUrl"
			:src="imageUrl"
			:alt="title"
			fit="cover"
			loading="lazy"
			class="fit"
			crossorigin="anonymous">
			<template #error>
				<div
					class="photo-thumbnail__fallback"
					role="img"
					:aria-label="t('components.photo-browser.thumbnail-unavailable')">
					<q-icon
						name="mdi-image-off-outline"
						size="28px"
						aria-hidden="true" />
				</div>
			</template>
		</q-img>
		<div
			v-else
			class="photo-thumbnail__fallback"
			role="img"
			:aria-label="t('components.photo-browser.thumbnail-unavailable')">
			<q-icon
				name="mdi-image-off-outline"
				size="28px"
				aria-hidden="true" />
		</div>
	</div>
</template>

<script setup lang="ts">
import { Subject, of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { set } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { useMediaStore } from '@store';

const props = withDefaults(defineProps<{
	plexServerId: number;
	plexApiRatingKey?: number | string | null;
	plexApiMetaDataKey?: number | null;
	hasThumbnail?: boolean;
	title: string;
	width?: number;
	height?: number;
	cy?: string;
}>(), {
	plexApiRatingKey: null,
	plexApiMetaDataKey: null,
	hasThumbnail: false,
	width: 72,
	height: 72,
	cy: undefined,
});
const requests = new Subject<void>();

const imageUrl = ref('');
const loading = ref(false);

const { t } = useI18n();
const mediaStore = useMediaStore();
useSubscription(requests.pipe(
	tap(() => {
		set(imageUrl, '');
		set(loading, true);
	}),
	switchMap(() => {
		const metadataKey = props.plexApiMetaDataKey;
		const ratingKey = props.plexApiRatingKey;
		if (!props.hasThumbnail || metadataKey === null || metadataKey === 0 || ratingKey === null || ratingKey === '') {
			return of('');
		}
		return mediaStore.getMediaThumbnailUrl({
			plexServerId: props.plexServerId,
			plexKey: String(ratingKey),
			metaDataKey: metadataKey,
			width: props.width,
			height: props.height,
		}).pipe(catchError(() => of('')));
	}),
	tap((url) => {
		set(imageUrl, url);
		set(loading, false);
	}),
).subscribe());

watch(() => [props.plexServerId, props.plexApiRatingKey, props.plexApiMetaDataKey, props.hasThumbnail, props.title, props.width, props.height], () => requests.next(), { immediate: true });

onBeforeUnmount(() => requests.complete());
</script>

<style scoped lang="scss">
.photo-thumbnail {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  border-radius: 4px;
  background: var(--q-dark-page, #1d1d1d);
}

.photo-thumbnail__fallback {
  display: flex;
  width: 100%;
  height: 100%;
  align-items: center;
  justify-content: center;
  color: var(--q-grey-6);
}
</style>
