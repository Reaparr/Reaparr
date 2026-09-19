<template>
	<QRow
		no-gutters
		class="q-section">
		<QCol :cols="12">
			<!-- Header	-->
			<QCol
				v-if="$slots['header'] || header"
				class="q-section__header q-mx-md">
				<QText
					size="h5"
					full-width
					class="q-my-sm q-ml-md"
					:align="align"
					bold="medium">
					<slot name="header">
						{{ header }}
					</slot>
				</QText>
				<q-separator />
			</QCol>
			<!--	Section Content	-->
			<QCol class="q-section__content q-pa-md">
				<slot />
			</QCol>
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	header?: string;
	align?: 'left' | 'center' | 'right';
}>(), {
	header: '',
	align: 'left',
});
</script>

<style lang="scss">
@media (max-width: $breakpoint-xs-max) {
  .q-section__header {
    margin-inline: 0.5rem;
  }

  .q-section__content {
    min-width: 0;
    padding: 0.5rem;
  }
}
</style>
