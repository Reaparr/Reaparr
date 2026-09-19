<template>
	<QRow
		:no-wrap="isDesktopLayout || disableResponsive"
		:class="{ 'help-row--fixed': disableResponsive }"
		:align="align">
		<QCol
			class="help-row-label"
			:cols="disableResponsive ? colLabel : 12"
			:md="!disableResponsive ? colLabel : 0"
			:lg="!disableResponsive ? 4 : 0"
			:xl="!disableResponsive ? 3 : 0"
			align-self="center"
			align-items="end">
			<!-- Help Label -->
			<QText
				v-if="!allowLabelEdit"
				full-width
				:align="isDesktopLayout || disableResponsive ? 'right' : 'left'"
				:value="help.label">
				<template #prepend>
					<slot name="prepend" />
				</template>
				<template #append>
					<QCol
						v-if="$slots['append']"
						:cols="'auto'">
						<slot name="append" />
					</QCol>
					<!-- Help Icon -->
					<HelpButton
						v-else-if="hasHelpPage"
						icon="mdi-help-circle-outline"
						class="q-ma-sm"
						@click="helpStore.openHelpDialog(help)" />
					<div
						v-else
						style="width: 42px; height: 42px" />
				</template>
			</QText>
			<EditableText
				v-else
				v-model="editModel"
				align="right"
				class="full-width" />
		</QCol>

		<!-- Default Form Slot -->
		<QCol
			:cols="disableResponsive ? colContent : 12"
			:md="!disableResponsive ? colContent : 0"
			:lg="!disableResponsive ? 5 : 0"
			:xl="!disableResponsive ? 4 : 0"
			align-self="center"
			class="help-row-default-slot q-pa-sm">
			<slot />
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { useHelpStore } from '@store';
import type { IHelp } from '@interfaces';
import type { ColLevels } from '@props';

const { t } = useI18n();
const helpStore = useHelpStore();
const breakpoints = useBreakpoints({
	sm: 600,
	md: 1024,
	lg: 1440,
	xl: 1920,
});
const isDesktopLayout = breakpoints.greaterOrEqual('md');

const editModel = defineModel<string>('editModel');

const props = withDefaults(defineProps<Partial<IHelp> & {
	value?: IHelp;
	hideLabel?: boolean;
	allowLabelEdit?: boolean;
	disableResponsive?: boolean;
	colContent?: ColLevels;
	colLabel?: ColLevels;
	align?: 'start' | 'center' | 'end';
}>(), {
	label: '',
	title: '',
	text: '',
	allowLabelEdit: false,
	hideLabel: false,
	disableResponsive: false,
	colLabel: 6,
	colContent: 6,
	align: 'center',
});

const help = computed(() => props.value ?? {
	label: !props.hideLabel ? props.label !== '' ? props.label : t('help.default.label') : '',
	title: props.title,
	text: props.text,
});

const hasHelpPage = computed(() => {
	return get(help).title !== '' && get(help).text !== '';
});
</script>

<style lang="scss">
@use '@/assets/scss/variables' as *;

.help-row {

  &-label {
    white-space: normal;
  }

  &-icon {
    color: $primary;
  }

  &-default-slot {
    white-space: break-spaces;
  }

  &-label:has(+ .help-row-default-slot .q-field__bottom) {
    --help-row-control-height: 56px;
    --help-row-content-padding: 8px;

    align-self: flex-start;
    position: relative;
    top: calc(var(--help-row-content-padding) + var(--help-row-control-height) / 2);
    transform: translateY(-50%);
  }

  &-label:has(+ .help-row-default-slot .q-field--dense .q-field__bottom) {
    --help-row-control-height: 40px;
  }

  &-default-slot:has(.q-field__bottom) {
    align-self: flex-start;
  }
}

@media (max-width: $breakpoint-sm-max) {
  .help-row:not(.help-row--fixed) .help-row-label {
    align-self: stretch;
    padding-inline: 0.5rem;

    &:has(+ .help-row-default-slot .q-field__bottom) {
      position: static;
      top: auto;
      transform: none;
    }

    :deep(.q-text) {
      text-align: left;
    }
  }

  .help-row:not(.help-row--fixed) .help-row-default-slot {
    min-width: 0;
  }
}

.help-row--fixed {
  flex-wrap: nowrap;
}
</style>
