<template>
	<q-tabs
		v-model="model"
		active-color="primary"
		indicator-color="primary"
		:vertical="$q.screen.gt.xs">
		<!-- Step headers	-->
		<template
			v-for="(header, index) in headers"
			:key="index">
			<q-tab
				:color="color"
				:complete="index + 1 === headers.length ? model > index : model > index + 1"
				:data-cy="`setup-header-tab-${index + 1}`"
				:label="header.name"
				:name="index + 1"
				:disable="!isTabEnabled(index + 1)"
				class="setup-tab"
				edit-icon="$complete" />
			<q-separator
				v-if="index < headers.length - 1"
				:key="index + 100" />
		</template>
	</q-tabs>
</template>

<script setup lang="ts">
import { get } from '@vueuse/core';
import { SetupPanelType } from '@enums';
import { useFolderPathStore } from '@store';

const $q = useQuasar();
const settingsStore = useSettingsStore();
const authStore = useAuthenticationStore();
const folderPathStore = useFolderPathStore();
const accountStore = useAccountStore();

const model = defineModel<number>({
	default: 1,
});

const props = defineProps<{ headers: { name: string }[] }>();

const color = computed(() => {
	return get(model) === props.headers.length ? 'green' : get(model) > props.headers.length ? 'green' : 'red';
});

function isTabEnabled(tab: SetupPanelType): boolean {
	const enabled: SetupPanelType[] = [SetupPanelType.DisclaimerPanel];

	if (settingsStore.generalSettings.hasAgreedToDisclaimer) {
		enabled.push(SetupPanelType.IntroductionPanel);
		enabled.push(SetupPanelType.AuthorizationPanel);
	}

	if (enabled.includes(SetupPanelType.AuthorizationPanel) && !authStore.isDefaultCredentials) {
		enabled.push(SetupPanelType.FolderOverviewPanel);
	}

	if (enabled.includes(SetupPanelType.FolderOverviewPanel) && folderPathStore.areDefaultFolderPathsValid) {
		enabled.push(SetupPanelType.PlexAccountsPanel);
	}

	if (enabled.includes(SetupPanelType.PlexAccountsPanel) && accountStore.getAccounts.length > 0) {
		enabled.push(SetupPanelType.FinishPanel);
	}

	return enabled.includes(tab);
}
</script>
