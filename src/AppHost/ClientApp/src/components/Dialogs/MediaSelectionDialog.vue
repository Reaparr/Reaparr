<template>
	<QCardDialog
		min-width="50vw"
		max-width="50vw"
		:name="DialogType.MediaSelectionDialog"
		:loading="false"
		@opened="onOpen()">
		<template #title>
			{{
				$t('components.media-selection-dialog.title', {
					min: selectedRange.min,
					max: selectedRange.max,
				})
			}}
		</template>
		<!--	Help text	-->
		<template #default>
			<div class="media-selection-content">
				<QRow
					justify="center"
					align="center"
					class="q-pt-lg">
					<QCol
						cols="11"
						class="q-my-md">
						<q-range
							v-model="selectedRange"
							:min="1"
							:max="mediaOverviewStore.itemsLength"
							:step="1"
							track-size="5px"
							thumb-size="35px"
							label-always
							drag-range
							color="red" />
					</QCol>
				</QRow>
				<QRow
					justify="between"
					class="media-selection-adjustments">
					<QCol
						v-for="column in ['min', 'max']"
						:key="column"
						cols="auto"
						class="media-selection-adjustment q-mx-xs">
						<table>
							<tbody>
								<tr>
									<td colspan="2">
										<q-input
											v-model.number="numberInput[column]"
											type="number"
											outlined
											style="max-width: 200px" />
									</td>
								</tr>
								<tr
									v-for="index in [1, 10, 100, 1000, 10000]"
									:key="index">
									<td>
										<BaseButton
											:label="`-${index}`"
											block
											@click="adjustValue(column, -1 * index)" />
									</td>
									<td>
										<BaseButton
											:label="`+${index}`"
											block
											@click="adjustValue(column, index)" />
									</td>
								</tr>
							</tbody>
						</table>
					</QCol>
				</QRow>
			</div>
		</template>
		<!--	Close action	-->
		<template #actions="{ close }">
			<QRow justify="between">
				<QCol cols="2">
					<BaseButton
						:label="$t('general.commands.close')"
						block
						@click="close" />
				</QCol>
				<QCol cols="2">
					<BaseButton
						:label="$t('general.commands.set-selection')"
						color="positive"
						block
						@click="setSelection(close)" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { DialogType } from '@enums';
import { useMediaOverviewStore } from '@store';
import { clamp } from 'lodash-es';

const mediaOverviewStore = useMediaOverviewStore();

const selectedRange = ref({
	min: 1,
	max: 1,
});

const numberInput = computed({
	get: () => selectedRange.value,
	set: (value) => {
		adjustValue('min', value.min);
		adjustValue('max', value.max);
	},
});

function adjustValue(type: string, value: number) {
	let minValue = 1;
	let maxValue = mediaOverviewStore.itemsLength;
	switch (type) {
		case 'min':
			minValue = 1;
			maxValue = get(selectedRange).max;
			break;
		case 'max':
			minValue = get(selectedRange).min;
			maxValue = mediaOverviewStore.itemsLength;
			break;
	}
	get(selectedRange)[type] = clamp(get(selectedRange)[type] + value, minValue, maxValue);
}

function setSelection(close: () => void) {
	mediaOverviewStore.setSelectionRange(selectedRange.value.min, selectedRange.value.max);
	close();
}

function onOpen(): void {
	set(selectedRange, {
		min: 1,
		max: mediaOverviewStore.itemsLength,
	});
}
</script>

<style lang="scss">
@media (max-width: $breakpoint-xs-max) {
  .media-selection-adjustments {
    gap: 0.5rem;
  }

  .media-selection-adjustment table {
    width: 100%;
  }
}

@media (max-width: $breakpoint-xs-max) {
  .media-selection-adjustments {
    flex-direction: column;
  }

  .media-selection-adjustment {
    width: 100%;
    max-width: 100%;
    margin-inline: 0;

    .q-field {
      max-width: none !important;
    }

    .q-btn {
      min-height: 44px;
    }
  }
}
</style>
