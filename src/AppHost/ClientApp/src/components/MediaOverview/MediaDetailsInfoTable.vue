<template>
	<q-card
		v-bind="$attrs"
		class="media-info-container">
		<q-card-section>
			<q-markup-table wrap-cells>
				<tbody>
					<tr class="q-tr--no-hover">
						<td
							colspan="2"
							class="media-info-column media-title">
							{{ props.title }}
						</td>
					</tr>
					<slot />
					<template v-if="props.showSummary && hasSummary">
						<slot name="summary">
							<tr class="media-summary-row">
								<td
									colspan="2"
									class="media-info-column media-summary-column">
									<a
										href="#"
										class="media-summary-trigger"
										data-cy="media-summary-read-more"
										@click.prevent="openSummaryDialog">
										<span class="media-summary-label">
											{{ props.summaryLabel }}
										</span>
										<span class="media-summary-preview">
											{{ summaryPreview }}
										</span>
										<span class="media-summary-read-more">
											{{ t('components.details-overview.read-more') }}
										</span>
									</a>
								</td>
							</tr>
						</slot>
					</template>
				</tbody>
			</q-markup-table>
		</q-card-section>
	</q-card>
	<QCardDialog
		v-if="hasSummary"
		:name="DialogType.MediaSummaryDialog"
		width="720px"
		content-height="60"
		close-button
		cy="media-summary-dialog">
		<template #title>
			{{ t('components.details-overview.full-summary') }}
		</template>
		<template #default>
			<div
				class="media-summary-dialog__content"
				data-cy="media-summary-dialog-content">
				<p
					v-for="(paragraph, index) in summaryParagraphs"
					:key="index"
					class="media-summary-dialog__paragraph">
					{{ paragraph }}
				</p>
			</div>
		</template>
		<template #actions="{ close }">
			<q-btn
				flat
				data-cy="media-summary-dialog-close"
				:label="t('general.commands.close')"
				@click="() => close()" />
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { DialogType } from '@enums';
import { useDialogStore } from '@store';

defineOptions({ inheritAttrs: false });

// shortcut: 600 characters, increase when detail tables gain more vertical space.
const SUMMARY_PREVIEW_LENGTH = 600;
const props = withDefaults(defineProps<{
	title: string;
	summaryLabel: string;
	summary?: string | null;
	showSummary?: boolean;
}>(), {
	summary: '',
	showSummary: true,
});

const { t } = useI18n();
const dialogStore = useDialogStore();

const summaryText = computed(() => props.summary?.trim() ?? '');
const hasSummary = computed(() => get(summaryText).length > 0);
const summaryPreview = computed(() => {
	const summary = get(summaryText);
	return summary.length > SUMMARY_PREVIEW_LENGTH
		? `${summary.slice(0, SUMMARY_PREVIEW_LENGTH).trimEnd()}...`
		: summary;
});
const summaryParagraphs = computed(() => get(summaryText)
	.split(/\r?\n+/)
	.map((paragraph) => paragraph.trim())
	.filter(Boolean));

function openSummaryDialog(): void {
	dialogStore.openDialog(DialogType.MediaSummaryDialog);
}
</script>

<style lang="scss">
@use '@/assets/scss/_mixins.scss';

.media-info-container {
	@extend .background-sm;
	box-shadow: 0px 3px 1px -2px rgba(0, 0, 0, 0.2), 0px 2px 2px 0px rgba(0, 0, 0, 0.14),
	0px 1px 5px 0px rgba(0, 0, 0, 0.12);
	min-height: 300px;
	margin-right: 1rem;

	.media-title {
		font-size: 30px;
		font-weight: bold;
	}

	.media-info-column {
		min-width: 150px;
		text-align: left;
		white-space: pre-wrap;
	}
}

.media-info-detail-grid {
	display: grid;
	grid-template-columns: repeat(2, minmax(0, 1fr));
	gap: 0.5rem 1rem;
}

.media-info-detail-column {
	display: flex;
	flex-direction: column;
	gap: 0.5rem;
	min-width: 0;
}

.media-info-detail-field {
	min-width: 0;
}

.media-info-detail-label,
.media-summary-label {
	font-weight: 600;
}

.media-info-detail-value {
	overflow-wrap: anywhere;
}

.media-summary-column {
	vertical-align: top;
}

.media-summary-trigger {
	display: block;
	color: inherit;
	cursor: pointer;
	text-decoration: none;

	&:focus-visible {
		outline: 2px solid var(--q-primary);
		outline-offset: 2px;
	}
}

.media-summary-preview {
	display: -webkit-box;
	line-clamp: 3;
	-webkit-box-orient: vertical;
	-webkit-line-clamp: 3;
	max-height: 4.2rem;
	overflow: hidden;
	overflow-wrap: anywhere;
	white-space: pre-wrap;
}

.media-summary-read-more {
	display: block;
	margin-top: 0.25rem;
	color: var(--q-primary);
	text-decoration: underline;
}

.media-summary-dialog__content {
	overflow-wrap: anywhere;
}

.media-summary-dialog__paragraph {
	margin: 0;
	white-space: pre-wrap;

	&:not(:last-child) {
		margin-bottom: 1rem;
	}
}

@media (max-width: $breakpoint-xs-max) {
	.media-info-container {
		width: calc(100% - 1rem) !important;
		max-width: calc(100% - 1rem) !important;
		height: auto !important;
		margin: 0 0.5rem 1rem;

		.q-markup-table,
		.q-table__container,
		.q-table {
			width: 100% !important;
			max-width: 100% !important;
			table-layout: fixed;
		}

		.media-title {
			font-size: clamp(1.35rem, 1.1rem + 1vw, 1.5rem);
		}

		.media-info-column {
			min-width: 0;
			padding: 0.5rem;
			overflow-wrap: anywhere;
		}

		.media-info-detail-grid {
			grid-template-columns: 1fr;
		}

		.media-summary-preview {
			line-clamp: 6;
			-webkit-line-clamp: 6;
			max-height: 8.4rem;
		}
	}
}
</style>
