<template>
	<MediaOverviewBar
		:media-type="props.mediaItem.type"
		:library-id="props.libraryId"
		:media-detail-item="props.mediaItem"
		detail-mode
		@action="onAction" />
	<QScroll class="page-content-minus-media-overview-bar">
		<QRow
			align="start"
			class="media-detail-header">
			<QCol cols="auto">
				<MediaPosterImage
					class="q-ma-md"
					:thumb-width="200"
					:thumb-height="300"
					:actions="false"
					:media-item="props.mediaItem" />
			</QCol>
			<QCol>
				<MediaDetailsInfoTable
					:title="props.mediaItem.title"
					:summary="props.mediaItem.summary"
					:summary-label="props.summaryLabel">
					<slot name="details" />
				</MediaDetailsInfoTable>
			</QCol>
		</QRow>
		<QRow no-gutters>
			<QCol>
				<slot />
			</QCol>
		</QRow>
	</QScroll>
</template>

<script setup lang="ts">
import type { PlexMediaDTO } from '@dto';
import type { IMediaOverviewBarActions } from '@interfaces';

const props = defineProps<{
	libraryId: number;
	mediaItem: PlexMediaDTO;
	summaryLabel: string;
}>();

function onAction(event: IMediaOverviewBarActions): void {
	emit('action', event);
}

const emit = defineEmits<{
	(e: 'action', event: IMediaOverviewBarActions): void;
}>();
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

@media (max-width: $breakpoint-xs-max) {
	.media-detail-header {
		flex-direction: column;
		align-items: center;

		> .col,
		> .col-auto {
			width: 100%;
			min-width: 0;
			flex: 0 0 100% !important;
			max-width: 100% !important;
		}

		> .col-auto {
			display: flex;
			justify-content: center;
		}
	}

	.page-content-minus-media-overview-bar,
	.page-content-minus-media-overview-bar .q-scrollarea__content {
		width: 100% !important;
		min-width: 0 !important;
		max-width: 100% !important;
	}
}
</style>
