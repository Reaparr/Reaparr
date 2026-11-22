<template>
	<QRow justify="center">
		<QCol cols="11">
			<!-- Circular Mode progress -->
			<template v-if="circularMode">
				<QRow
					justify="center"
					no-gutters>
					<QCol cols="auto">
						<q-circular-progress
							show-value
							size="100px"
							:rotate="-90"
							:width="15"
							:value="getPercentage"
							:indeterminate="indeterminate"
							color="red">
							<template v-if="!indeterminate">
								<span
									v-if="getPercentage < 100"
									class="text-bold"> {{ n(getPercentage / 100, 'percent') }}</span>
								<q-icon
									v-else
									large
									name="mdi-check" />
							</template>
						</q-circular-progress>
					</QCol>
				</QRow>
				<!-- Progress text -->
				<QRow
					v-if="text"
					justify="center"
					class="q-mt-lg"
					no-gutters>
					<QCol>
						<QText
							size="h4"
							align="center">
							{{ text }}
						</QText>
					</QCol>
				</QRow>
			</template>
			<!-- Linear Mode Progress -->
			<template v-else>
				<!-- Progress text -->
				<QRow
					v-if="text"
					justify="center"
					no-gutters>
					<QCol cols="auto">
						<QText
							size="h4"
							align="center">
							{{ text }}
						</QText>
					</QCol>
				</QRow>
				<!-- Progress bar -->
				<QRow
					justify="center"
					no-gutters>
					<QCol>
						<q-linear-progress
							:value="Math.min(getPercentage, 100)"
							show-value
							height="20px"
							class="q-mx-sm"
							striped
							stream
							color="red">
							<strong>{{ n(getPercentage / 100, 'percent') }}</strong>
						</q-linear-progress>
					</QCol>
				</QRow>
			</template>
		</QCol>
	</QRow>
</template>

<script setup lang="ts">
const props = defineProps<{
	text?: string;
	percentage: number;
	circularMode?: boolean;
	completed?: boolean;
	indeterminate?: boolean;
}>();

const { n } = useI18n();

const getPercentage = computed((): number => {
	return Math.round(props.percentage * 100) / 100;
});
</script>
