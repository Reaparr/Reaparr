<template>
	<QSection :header="$t('pages.debug.buttons.media-comparison-state-header')">
		<QRow
			v-for="comparisonState in comparisonStates"
			:key="`icon-${comparisonState}`"
			class="q-my-sm">
			<QCol
				cols="3"
				text-align="center">
				<MediaComparisonStateButton
					:comparison-state="comparisonState"
					:media-type="PlexMediaType.Movie"
					dense
					show-tooltip />
			</QCol>

			<QCol
				cols="3"
				text-align="center">
				<MediaComparisonStateButton
					:comparison-state="comparisonState"
					:media-type="PlexMediaType.Movie"
					show-tooltip />
			</QCol>

			<QCol
				cols="6"
				text-align="center">
				<MediaComparisonStateButton
					:comparison-state="comparisonState"
					:media-type="PlexMediaType.Movie"
					show-label />
			</QCol>
		</QRow>
	</QSection>
</template>

<script setup lang="ts">
import { PlexMediaComparisonState, PlexMediaType } from '@dto';

const comparisonStates: PlexMediaComparisonState[] = [
	PlexMediaComparisonState.NotCompared,
	PlexMediaComparisonState.Owned,
	PlexMediaComparisonState.Missing,
	PlexMediaComparisonState.HigherQuality,
	PlexMediaComparisonState.Pending,
	PlexMediaComparisonState.Partial,
	PlexMediaComparisonState.PartialAndHigherQuality,
	PlexMediaComparisonState.Unknown,
];
</script>
