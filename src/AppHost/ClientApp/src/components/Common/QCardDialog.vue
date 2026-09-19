<template>
	<q-dialog
		v-model:model-value="showDialog"
		:persistent="persistent"
		:full-height="fullHeight"
		:transition-show="transitionShow"
		:transition-hide="transitionHide"
		:full-width="fullWidth"
		@before-show="$emit('opened', dataValue!)"
		@before-hide="$emit('closed')">
		<div
			:data-cy="cy"
			:class="{
				'dialog-container': true,
				'dialog-container-background': true,
			}"
			:style="styles">
			<!--	Dialog Title -->
			<div
				v-if="$slots['title']"
				class="dialog-container-title">
				<QCardTitle>
					<slot
						name="title"
						:value="parentValue" />
					<div
						v-if="closeButton"
						class="dialog-close-button">
						<CloseIconButton
							cy="dialog-close-button"
							@click="closeDialog" />
					</div>
				</QCardTitle>
			</div>
			<!--	Dialog Top Row -->
			<div
				v-if="$slots['top-row']"
				class="dialog-container-top-row">
				<div v-show="!loading">
					<slot name="top-row" />
				</div>
			</div>
			<!-- Dialog Content	-->
			<div
				v-if="$slots['default']"
				ref="el"
				:class="{ 'dialog-container-content': true, [`dialog-container-content-${props.contentHeight}`]: contentHeight !== '0' }">
				<slot
					v-if="!loading"
					:value="parentValue"
					:size="contentSize" />
			</div>
			<!-- Dialog Actions	-->
			<div
				v-if="$slots['actions']"
				:class="['dialog-container-actions', [`justify-${buttonAlign}`]]">
				<slot
					name="actions"
					:close="closeDialog"
					:open="openDialog"
					:value="parentValue" />
			</div>

			<!--	Loading overlay	-->
			<QLoadingOverlay :loading="loading" />
		</div>
	</q-dialog>
</template>

<script setup lang="ts" generic="T">
import { get, set, useElementSize } from '@vueuse/core';
import { useDialogStore } from '@store';
import { useSubscription } from '@vueuse/rxjs';

const dialogStore = useDialogStore();
const $q = useQuasar();

const showDialog = ref(false);
const dataValue = ref<T>();

const props = withDefaults(
	defineProps<{
		name: string;
		type?: T;
		width?: string;
		fullHeight?: boolean;
		fullWidth?: boolean;
		contentHeight?: '100' | '80' | '60' | '40' | '20' | '0';
		loading?: boolean;
		persistent?: boolean;
		closeButton?: boolean;
		transitionShow?: string;
		transitionHide?: string;
		buttonAlign?: 'start' | 'center' | 'end' | 'between' | 'around' | 'evenly';
		cy?: string;
		id?: number;
	}>(),
	{
		type: undefined,
		width: '1000px',
		contentHeight: '0',
		loading: false,
		fullHeight: false,
		fullWidth: false,
		persistent: false,
		closeButton: false,
		buttonAlign: 'between',
		transitionShow: 'fade',
		transitionHide: 'fade',
		cy: 'q-card-dialog-cy',
		id: 0,
	},
);

defineEmits<{
	(e: 'opened', value: T): void;
	(e: 'closed'): void;
}>();

const contentSize = useElementSize(useTemplateRef('el'));

const parentValue = computed(() => {
	return get(dataValue);
});

function openDialog(value: T, id?: number) {
	if (!(props.id === 0 || props.id === id)) {
		return;
	}
	set(dataValue, value);
	set(showDialog, true);
}

function closeDialog(id?: number) {
	if (!(props.id === 0 || props.id === id)) {
		return;
	}
	set(showDialog, false);
}
const styles = computed(() => {
	if (!$q.screen.lt.md) {
		return { width: `clamp(200px, 100%, ${props.width})` };
	}

	return {
		width: `min(${props.width}, calc(100vw - 32px))`,
		height: props.fullHeight ? 'calc(100dvh - 32px)' : undefined,
	};
});

onMounted(() => {
	useSubscription(
		dialogStore.getDialogState()
			.subscribe(({ name, state, data, id }) => {
				if (name !== props.name) {
					return;
				}

				if (state) {
					openDialog(data as T, id);
				} else {
					closeDialog(id);
				}
			}),
	);
});
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;
@use '@/assets/scss/_mixins.scss';

body {
  .dialog-container {
    display: grid;
    grid-template-columns: 1fr;
    grid-template-rows: min-content min-content 1fr min-content;
    grid-column-gap: 0;
    grid-row-gap: 0;
    grid-template-areas:
      "title"
      "top-row"
      "content"
      "actions";
    // Scrollbar is hidden because otherwise the header and footer are also scrolling
    overflow-y: hidden;

    &-background {
      @extend .default-border;
      @extend .default-border-radius;
      @extend .default-shadow;
      @extend .blur;
      background-color: $dark-sm-background-color;
      max-width: none;
      max-height: none;
    }

    &-title {
      grid-area: title;

      .dialog-close-button {
        position: absolute;
        right: 0.5rem;
        top: 0.5rem;
      }
    }

    &-top-row {
      grid-area: top-row;
      padding: 0 1rem;
    }

    &-content {
      grid-area: content;
      overflow-y: scroll;
      margin: 0 1rem;

      @each $size in 20, 40, 60, 80, 100 {
        &-#{$size} {
          min-height: calc(#{$size}vh - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
          height: calc(#{$size}vh - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
          max-height: calc(#{$size}vh - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
        }
      }
    }

    &-actions {
      grid-area: actions;
      max-height: $q-card-dialog-actions-height;
      margin: 1rem;
      display: flex;
    }
  }
}

@media (max-width: 1023px) {
  body .dialog-container {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: min-content min-content minmax(0, 1fr) min-content;
    max-width: calc(100vw - 32px);
    max-height: calc(100dvh - 32px);
    min-width: 0;
    min-height: 0;
    overflow: hidden;

    &-title {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    &-top-row {
      min-width: 0;
    }

    &-content {
      min-width: 0;
      min-height: 0;
      overflow: auto;
      overscroll-behavior: contain;

      @each $size in 20, 40, 60, 80, 100 {
        &-#{$size} {
          min-height: 0 !important;
          height: calc(#{$size}dvh - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
          max-height: calc(100dvh - 32px - $q-card-dialog-title-height - $q-card-dialog-actions-height) !important;
        }
      }
    }

    &-actions {
      min-width: 0;
      flex-wrap: wrap;
      gap: 0.5rem;
      max-height: none;

      > .row {
        min-width: 0;
        flex: 1 1 100%;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
    }
  }
}

@media (max-width: $breakpoint-xs-max) {
  body .dialog-container {
    width: calc(100vw - 16px) !important;
    max-width: calc(100vw - 16px);
    max-height: calc(100dvh - 16px);
    border-radius: 8px;

    &-content {
      margin-inline: 0.75rem;
    }

    &-actions {
      margin: 0.75rem;

      .q-btn {
        min-height: 44px;
      }
    }
  }
}
</style>
