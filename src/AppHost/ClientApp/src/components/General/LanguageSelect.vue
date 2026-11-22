<template>
	<q-select
		v-model:model-value="language"
		:dense="dense"
		:options="localizationStore.getLanguageLocaleOptions"
		:option-value="'code' as keyof ILocaleConfig"
		data-cy="language-selector">
		<template #selected-item="scope">
			<q-item
				:dense="dense"
				class="q-pl-none">
				<q-item-section avatar>
					<q-img
						:ratio="16/9"
						:src="scope.opt.img"
						:alt="scope.opt.text" />
				</q-item-section>
				<q-item-section>
					<q-item-label> {{ scope.opt.text }}</q-item-label>
				</q-item-section>
			</q-item>
		</template>
		<template #option="scope">
			<q-item
				:dense="dense"
				v-bind="scope.itemProps"
				:data-cy="`option-${scope.opt.code}`">
				<q-item-section avatar>
					<q-img
						:ratio="16/9"
						:src="scope.opt.img"
						:alt="scope.opt.text" />
				</q-item-section>
				<q-item-section>
					<q-item-label> {{ scope.opt.text }}</q-item-label>
				</q-item-section>
			</q-item>
		</template>
	</q-select>
</template>

<script setup lang="ts">
import type { ILocaleConfig } from '@interfaces';
import { useLocalizationStore } from '@store';

const localizationStore = useLocalizationStore();

const language = computed({
	get: (): ILocaleConfig =>	localizationStore.getLanguageLocale,
	set: (value: ILocaleConfig) => localizationStore.changeLanguageLocale(value.code),
});

withDefaults(defineProps<{ dense?: boolean }>(), {
	dense: false,
});
</script>
