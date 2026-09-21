<template>
	<q-menu
		:offset="[0, 12]"
		@hide="menuIndex = MediaMetaDataTypes.None">
		<q-list class="media-filter-menu-list">
			<!-- Categories -->
			<template v-if="menuIndex === MediaMetaDataTypes.None">
				<q-item
					v-close-popup
					clickable
					@click="clearMetadataFilter()">
					<q-item-section>{{ t('components.media-filter-menu.all') }}</q-item-section>
				</q-item>
				<q-separator />
				<q-item
					v-for="(item, index) in menuItems"
					:key="index"
					:data-cy="`media-filter-menu-category-${item.type}`"
					clickable
					@click="onMenuOpen(item.type)">
					<q-item-section>{{ item.text }}</q-item-section>
				</q-item>
			</template>
			<!-- Menu Level 2 -->
			<template v-else>
				<q-item
					clickable
					@click="goBackToMainMenu()">
					<q-item-section avatar>
						<q-icon
							size="2em"
							name="mdi-chevron-left" />
					</q-item-section>
					<q-item-section>
						{{ menuItems.find((item) => item.type === menuIndex)?.text }}
					</q-item-section>
					<q-item-section side>
						<IconButton
							icon="mdi-magnify"
							@click.stop="toggleMenuSearch" />
					</q-item-section>
				</q-item>
				<!-- Search Input -->
				<q-item
					v-if="showMenuSearch"
					clickable>
					<q-input
						v-model="menuFilterQuery"
						color="red"
						class="full-width"
						dense />
				</q-item>
				<q-separator />
				<div
					v-if="mediaOverviewStore.filterMetadataLoading"
					class="q-pa-md text-center">
					<QSpinnerDots
						size="2em"
						color="primary" />
				</div>
				<QScroll
					v-else
					:fit="false"
					width="100%"
					:height="'260px'">
					<!-- Show Genres Sub-Menu -->
					<template v-if="menuIndex === MediaMetaDataTypes.Genres">
						<q-item
							v-for="genre in mediaOverviewStore.getGenres.filter(x => x.name.toLowerCase().includes(menuFilterQuery.toLowerCase()))"
							:key="genre.id"
							clickable
							@click="useSubscription(mediaOverviewStore.setGenreFilter(genre.id).subscribe())">
							<q-item-section avatar>
								<q-icon
									v-if="genre.id === mediaOverviewStore.metadata.genreId"
									name="mdi-check" />
							</q-item-section>
							<q-item-section>
								{{ genre.name }}
							</q-item-section>
						</q-item>
					</template>

					<!-- Show Countries Sub-Menu -->
					<template v-if="menuIndex === MediaMetaDataTypes.Country">
						<q-item
							v-for="country in mediaOverviewStore.getCountries.filter(x => x.name.toLowerCase().includes(menuFilterQuery.toLowerCase()))"
							:key="country.id"
							clickable
							@click="useSubscription(mediaOverviewStore.setCountryFilter(country.id).subscribe())">
							<q-item-section avatar>
								<q-icon
									v-if="country.id === mediaOverviewStore.metadata.countryId"
									name="mdi-check" />
							</q-item-section>
							<q-item-section>{{ country.name }}</q-item-section>
						</q-item>
					</template>

					<!-- Show Roles Sub-Menu -->
					<template v-if="menuIndex === MediaMetaDataTypes.Roles">
						<q-item
							v-for="role in mediaOverviewStore.getRoles.filter(x => x.name.toLowerCase().includes(menuFilterQuery.toLowerCase()))"
							:key="role.id"
							clickable
							@click="useSubscription(mediaOverviewStore.setRoleFilter(role.id).subscribe())">
							<q-item-section avatar>
								<q-icon
									v-if="role.id === mediaOverviewStore.metadata.roleId"
									name="mdi-check" />
							</q-item-section>
							<q-item-section>{{ role.name }}</q-item-section>
						</q-item>
					</template>

					<!-- Show Quality Sub-Menu -->
					<template v-if="menuIndex === MediaMetaDataTypes.Quality">
						<q-item
							v-for="qualityDto in mediaOverviewStore.getQualities.filter(x => x.name.toLowerCase().includes(menuFilterQuery.toLowerCase()))"
							:key="qualityDto.name"
							clickable
							@click="useSubscription(mediaOverviewStore.setQualityFilter(qualityDto.id).subscribe())">
							<q-item-section avatar>
								<q-icon
									v-if="qualityDto.id === mediaOverviewStore.metadata.qualityId"
									name="mdi-check" />
							</q-item-section>
							<q-item-section>
								<MediaVideoQuality :quality="qualityDto.quality" />
							</q-item-section>
						</q-item>
					</template>

					<!-- Show Comparison State Sub-Menu -->
					<template v-if="menuIndex === MediaMetaDataTypes.ComparisonState">
						<q-item
							v-for="comparisonState in comparisonStateOptions"
							:key="comparisonState.value"
							clickable
							@click="useSubscription(mediaOverviewStore.setComparisonStateFilter(comparisonState.value).subscribe())">
							<q-item-section avatar>
								<q-icon
									v-if="comparisonState.value === mediaOverviewStore.metadata.comparisonState"
									name="mdi-check" />
							</q-item-section>
							<q-item-section>
								<MediaComparisonStateButton
									:comparison-state="comparisonState.value"
									show-label
									dense
									flat
									:cy="`comparison-filter-option-${comparisonState.value}`" />
							</q-item-section>
						</q-item>
					</template>
				</QScroll>
			</template>
		</q-list>
	</q-menu>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { PlexMediaComparisonState, PlexMediaType } from '@dto';
import { useLibraryStore, useMediaOverviewStore, useServerStore } from '@store';
import { MediaMetaDataTypes } from '@enums';
import IconButton from '@components/Buttons/IconButton.vue';

const menuIndex = ref<MediaMetaDataTypes>(MediaMetaDataTypes.None);
const mediaOverviewStore = useMediaOverviewStore();
const libraryStore = useLibraryStore();
const serverStore = useServerStore();
const { t } = useI18n();

const showMenuSearch = ref(false);
const menuFilterQuery = ref<string>('');

withDefaults(defineProps<{
	libraryId?: number;
}>(), {
	libraryId: 0,
});

const menuItems = computed((): { text: string; type: MediaMetaDataTypes }[] => [
	{
		text: t('components.media-filter-menu.meta-data.country'),
		type: MediaMetaDataTypes.Country,
	},
	{
		text: t('components.media-filter-menu.meta-data.genre'),
		type: MediaMetaDataTypes.Genres,
	},
	{
		text: t('components.media-filter-menu.meta-data.roles'),
		type: MediaMetaDataTypes.Roles,
	},
	{
		text: t('components.media-filter-menu.meta-data.quality'),
		type: MediaMetaDataTypes.Quality,
	},
	{
		text: t('components.media-filter-menu.meta-data.comparison-state'),
		type: MediaMetaDataTypes.ComparisonState,
	},
]);

const isOwnedLibrary = computed(() => {
	if (mediaOverviewStore.allMediaMode)
		return false;

	const serverId = libraryStore.getLibrary(mediaOverviewStore.libraryId)?.plexServerId ?? 0;
	return serverStore.getServer(serverId)?.owned ?? false;
});

const possibleComparisonStates = computed((): PlexMediaComparisonState[] => {
	const baseStates = [
		PlexMediaComparisonState.NotCompared,
		PlexMediaComparisonState.Owned,
		PlexMediaComparisonState.HigherQuality,
		PlexMediaComparisonState.Pending,
	];

	if (mediaOverviewStore.getMediaType !== PlexMediaType.TvShow)
		return get(isOwnedLibrary) ? baseStates : [...baseStates, PlexMediaComparisonState.Missing];

	return [
		...baseStates,
		...(get(isOwnedLibrary) ? [] : [PlexMediaComparisonState.Missing]),
		PlexMediaComparisonState.Partial,
		PlexMediaComparisonState.PartialAndHigherQuality,
	];
});

const comparisonStateOptions = computed(() => {
	return mediaOverviewStore.getComparisonStateOptions.filter((x) => {
		if (!get(possibleComparisonStates).includes(x.value))
			return false;

		return x.label.toLowerCase().includes(get(menuFilterQuery).toLowerCase());
	});
});

function onMenuOpen(category: MediaMetaDataTypes) {
	set(menuIndex, category);
}

function toggleMenuSearch() {
	set(showMenuSearch, !get(showMenuSearch));
	set(menuFilterQuery, '');
}

function goBackToMainMenu() {
	set(menuIndex, MediaMetaDataTypes.None);
	set(showMenuSearch, false);
	set(menuFilterQuery, '');
}

function clearMetadataFilter() {
	mediaOverviewStore.clearMetaDataFilter();

	useSubscription(mediaOverviewStore.refreshMediaData().subscribe());
}
</script>

<style lang="scss">
.media-filter-menu-list {
  min-width: 400px;
}

@media (max-width: $breakpoint-sm-max) {
  .media-filter-menu-list {
    width: min(400px, calc(100vw - 32px));
    min-width: min(400px, calc(100vw - 32px));
    max-height: min(70dvh, 600px);
    overflow-y: auto;
  }
}
</style>
